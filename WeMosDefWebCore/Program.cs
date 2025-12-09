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

// Read BasicServiceEndpoint for future WCF replacement if needed
string basicServiceEndpoint = app.Configuration["BasicServiceEndpoint"] ?? "";

// Device discovery placeholders (ported semantics)
string ip = "192.168.15.22";
int port = 49153;

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
    return Results.Json(new { rules = Array.Empty<object>(), enabled = false });
});

app.MapPost("/api/schedule", () =>
{
    return Results.Json(new { ok = true });
});

app.MapPost("/api/schedule/enable", (Dictionary<string, bool> body) =>
{
    var enabled = body.TryGetValue("enabled", out var v) && v;
    return Results.Json(new { ok = true, enabled });
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