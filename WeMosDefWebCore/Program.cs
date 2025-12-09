using WeMosDef;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// App settings
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Strongly-typed options
var app = builder.Build();
app.UseResponseCompression();
app.UseStaticFiles();
app.MapRazorPages();

// Fixed device selection per requirements
string ip = "192.168.15.22";
int port = 49153;

// EST timezone handling for scheduler ticks
TimeZoneInfo? estTzi = null;
try
{
    estTzi = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
}
catch
{
    // Fallback: if not found, use local time
    estTzi = null;
}
Func<DateTime> nowEst = () =>
{
    var nowLocal = DateTime.Now;
    return estTzi != null ? TimeZoneInfo.ConvertTime(nowLocal, estTzi) : nowLocal;
};

// Background scheduler: tick every 15s
var schedulerClient = new Client(ip, port);
var runner = new SchedulerRunner(schedulerClient, nowEst);
var schedulerTimer = new System.Threading.Timer(_ => {
    try { runner.Tick(); } catch { /* swallow to keep timer alive */ }
}, null, dueTime: TimeSpan.FromSeconds(2), period: TimeSpan.FromSeconds(15));
app.Lifetime.ApplicationStopping.Register(() => {
    try { schedulerTimer.Dispose(); } catch { }
});
// Read BasicServiceEndpoint for future WCF replacement if needed
string basicServiceEndpoint = app.Configuration["BasicServiceEndpoint"] ?? "";
static async Task<string> SafeGetPowerStateAsync(string ipAddr, int p)
{
    var client = new Client(ipAddr, p);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    return await Task.Run(() => client.GetState(), cts.Token);
}

app.MapGet("/api/state", async () =>
{
    try
    {
        var s = await SafeGetPowerStateAsync(ip, port);
        return Results.Json(new { state = s == "0" ? "off" : "on", timestamp = DateTime.UtcNow }, contentType: "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/toggle", async () =>
{
    try
    {
        var s = await SafeGetPowerStateAsync(ip, port);
        var client = new Client(ip, port);
        if (s == "0") client.On(); else client.Off();
        var newState = await SafeGetPowerStateAsync(ip, port);
        return Results.Json(new { state = newState == "0" ? "off" : "on", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/info", () =>
{
    var make = "Belkin";
    var model = "WeMo Smart Plug";
    var firmware = "unknown";
    var name = "Rocket";
    return Results.Json(new { make, model, firmware, friendlyName = name, ip, port });
});

app.MapGet("/api/schedule", () =>
{
    var client = new Client(ip, port);
    var s = client.ReadSchedule();
    // Ensure one or two rules only are reported as-is
    return Results.Json(new
    {
        enabled = s.Enabled,
        rules = s.Rules.Select(r => new {
            id = r.Id,
            action = r.Action,
            time = new { hour = r.Time.Hour, minute = r.Time.Minute },
            weekdays = r.Weekdays
        }).ToArray()
    });
});
app.MapPost("/api/schedule", async (HttpRequest req) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(req.Body);
        var root = doc.RootElement;

        bool enabled = root.TryGetProperty("enabled", out var enabledProp) && enabledProp.ValueKind == JsonValueKind.True || (enabledProp.ValueKind == JsonValueKind.False && enabledProp.GetBoolean());
        string startHHMM = root.TryGetProperty("startHHMM", out var startProp) && startProp.ValueKind == JsonValueKind.String ? startProp.GetString() ?? "" : "";
        string stopHHMM = root.TryGetProperty("stopHHMM", out var stopProp) && stopProp.ValueKind == JsonValueKind.String ? stopProp.GetString() ?? "" : "";

        // Validate HH:MM format (24h)
        static (int h, int m) parseHHMM(string s)
        {
            var parts = s.Split(':');
            if (parts.Length != 2) throw new ArgumentException("Time must be HH:MM");
            if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m))
                throw new ArgumentException("Time must be HH:MM");
            if (h < 0 || h > 23) throw new ArgumentException("Hour must be 0-23");
            if (m < 0 || m > 59) throw new ArgumentException("Minute must be 0-59");
            return (h, m);
        }

        if (string.IsNullOrWhiteSpace(startHHMM))
            return Results.Json(new { ok = false, error = "startHHMM required (EST)" }, statusCode: 400);

        var (sh, sm) = parseHHMM(startHHMM);
        int? stopH = null, stopM = null;
        if (!string.IsNullOrWhiteSpace(stopHHMM))
        {
            var (eh, em) = parseHHMM(stopHHMM);
            stopH = eh; stopM = em;
        }

        // Build schedule with one or two rules, Mon–Sun
        var allDays = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var schedule = new Schedule
        {
            DeviceIp = ip,
            Enabled = enabled,
            Rules = new List<Rule>()
        };
        schedule.Rules.Add(new Rule
        {
            Action = "on",
            Time = new TimeEvent { Hour = sh, Minute = sm },
            Weekdays = allDays
        });
        if (stopH.HasValue && stopM.HasValue)
        {
            schedule.Rules.Add(new Rule
            {
                Action = "off",
                Time = new TimeEvent { Hour = stopH.Value, Minute = stopM.Value },
                Weekdays = allDays
            });
        }

        var client = new Client(ip, port);
        client.UpdateSchedule(schedule);

        return Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: 400);
    }
});
app.MapPost("/api/schedule/enable", (Dictionary<string, bool> body) =>
{
    var enabled = body.TryGetValue("enabled", out var v) && v;
    var client = new Client(ip, port);
    Schedule s = enabled ? client.EnableSchedule() : client.DisableSchedule();
    return Results.Json(new { ok = true, enabled = s.Enabled });
});
// SSE events stream (poll-based)
app.MapGet("/api/events", async (HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";
    ctx.Response.Headers.ContentType = "text/event-stream; charset=utf-8";

    string? lastState = null;

    try
    {
        var initial = await SafeGetPowerStateAsync(ip, port);
        lastState = initial;
        await ctx.Response.WriteAsync($"data: {{\"type\":\"state\",\"state\":\"{(initial == "0" ? "off" : "on")}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}\n\n");
        await ctx.Response.Body.FlushAsync();
    }
    catch (Exception ex)
    {
        await ctx.Response.WriteAsync($"data: {{\"type\":\"error\",\"message\":\"{JsonEncodedText.Encode(ex.Message)}\"}}\n\n");
        await ctx.Response.Body.FlushAsync();
    }

    while (!ctx.RequestAborted.IsCancellationRequested)
    {
        await Task.Delay(3000, ctx.RequestAborted);
        try
        {
            var s = await SafeGetPowerStateAsync(ip, port);
            if (s != lastState)
            {
                lastState = s;
                await ctx.Response.WriteAsync($"data: {{\"type\":\"state\",\"state\":\"{(s == "0" ? "off" : "on")}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}\n\n");
                await ctx.Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            await ctx.Response.WriteAsync($"data: {{\"type\":\"error\",\"message\":\"{JsonEncodedText.Encode(ex.Message)}\"}}\n\n");
            await ctx.Response.Body.FlushAsync();
        }
    }
});

// Fallback to index page
app.MapFallbackToFile("/index.html");

app.Run();