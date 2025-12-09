using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using FindingWemoNS;

public partial class WeMosDefGUI : System.Web.UI.Page
{
	public static string ip = "192.168.15.22";
	// Default common UPnP control port to avoid null/0 causing HttpClient errors
	public int port = 49153;
	public string PowerState = null;

	protected void Page_Load(object sender, EventArgs e)
	{
		// Allow runtime override of ip/port via query for diagnostics: ?ip=1.2.3.4&port=49154
		var qIp = Request.QueryString["ip"];
		var qPort = Request.QueryString["port"];
		if (!string.IsNullOrWhiteSpace(qIp)) { ip = qIp; }
		if (!string.IsNullOrWhiteSpace(qPort)) { int p; if (int.TryParse(qPort, out p) && p > 0) port = p; }
	
		// API routing: respond with JSON for /api/* paths, otherwise render page
		// When URL Rewrite forwards /api/* to /default.aspx, the original path is preserved in ?__path=...
		var rawPath = (Request.QueryString["__path"] ?? Request.Path ?? string.Empty).ToLowerInvariant();
		// Normalize: ensure leading slash for consistent matching
		var originalPath = rawPath.StartsWith("/") ? rawPath : "/" + rawPath;

		if (originalPath.Contains("/api/"))
		{
			HandleApiRequest(originalPath);
			return;
		}

		// Normal page render path - do device discovery only when needed
		DiscoverDevice();
		PowerState = SafeGetPowerState(ip, port);
		if (Request.ServerVariables["REQUEST_METHOD"] == "POST")
			HandleAction(Request.Form["action"]);
	}

	void DiscoverDevice()
	{
		try
		{
			var wemo = FindingWemo.Search(System.Net.IPAddress.Parse(ip), System.Net.IPAddress.Parse(ip), "Rocket").SingleOrDefault();
			if (wemo == null)
			{
				// If discovery fails, continue with configured ip/port (ensure default)
				if (port == 0) port = 49153;
			}
			else
			{
				port = wemo.Port != 0 ? wemo.Port : 49153;
				ip = wemo.IPAddress.ToString();
			}
		}
		catch
		{
			// If discovery throws, use defaults
			if (port == 0) port = 49153;
		}
	}

	void HandleApiRequest(string originalPath)
	{
		// For API endpoints that need device info, discover first
		bool needsDevice = originalPath.Contains("/api/state") ||
						   originalPath.Contains("/api/toggle") ||
						   originalPath.Contains("/api/events") ||
						   originalPath.Contains("/api/info") ||
						   originalPath.Contains("/api/schedule");

		// Validate WCF endpoint configuration early for API calls
		var endpoint = ConfigurationManager.AppSettings["BasicServiceEndpoint"];
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			Response.ContentType = "application/json";
			Response.StatusCode = 500;
			WriteJson("{\"error\":\"endpoint_not_configured\"}");
			return;
		}

		if (needsDevice)
		{
			DiscoverDevice();
		}

		// Special case: SSE stream
		if (originalPath.Contains("/api/events") && Request.HttpMethod == "GET")
		{
			StreamEvents(ip, port);
			return;
		}

		Response.ContentType = "application/json";
		try
		{
			if (originalPath.Contains("/api/state") && Request.HttpMethod == "GET")
			{
				var state = SafeGetPowerState(ip, port);
				WriteJson(string.Format("{{\"state\":\"{0}\",\"timestamp\":\"{1:o}\"}}", state == "0" ? "off" : "on", DateTime.UtcNow));
				return;
			}
			if (originalPath.Contains("/api/toggle") && Request.HttpMethod == "POST")
			{
				// Toggle by reading current state then flipping
				var state = SafeGetPowerState(ip, port);
				var client = new WeMosDef.Client(ip, port);
				if (state == "0")
					client.On();
				else
					client.Off();

				// Re-read to confirm
				var newState = SafeGetPowerState(ip, port);
				WriteJson(string.Format("{{\"state\":\"{0}\",\"timestamp\":\"{1:o}\"}}", newState == "0" ? "off" : "on", DateTime.UtcNow));
				return;
			}
			if (originalPath.Contains("/api/info") && Request.HttpMethod == "GET")
			{
				// Basic info; populate with what the libraries expose if available
				var make = "Belkin";
				var model = "WeMo Smart Plug";
				var firmware = "unknown";
				var name = "Rocket";

				WriteJson(string.Format("{{\"make\":\"{0}\",\"model\":\"{1}\",\"firmware\":\"{2}\",\"friendlyName\":\"{3}\",\"ip\":\"{4}\",\"port\":{5}}}", make, model, firmware, name, ip, port));
				return;
			}
			// Device-native schedule endpoints via rules:1 (GetRules/SetRules)
			if (originalPath.Contains("/api/schedule") && Request.HttpMethod == "GET")
			{
				var client = new WeMosDef.Client(ip, port);
				try
				{
					// Probe rules DB version early to reveal transport/endpoint issues
					string version = null;
					string rawXml = null;
					bool enabledFlag = false;
					try
					{
						version = client.GetRulesDBVersion();
					}
					catch (Exception vex)
					{
						// capture version probe error
						version = "error:" + (vex.InnerException != null ? vex.InnerException.Message : vex.Message);
					}
					try
					{
						rawXml = client.GetDeviceScheduleXml();
					}
					catch (Exception rex)
					{
						rawXml = "error:" + (rex.InnerException != null ? rex.InnerException.Message : rex.Message);
					}
					try
					{
						enabledFlag = client.GetScheduleEnabled();
					}
					catch { /* ignore */ }
	
					// Use high-level DTO mapping for readability in UI
					var s = client.GetDeviceSchedule();
					var rulesJson = string.Join(",", s.Rules.Select(r =>
						string.Format("{{\"id\":\"{0}\",\"action\":\"{1}\",\"time\":{{\"hour\":{2},\"minute\":{3}}},\"weekdays\":[{4}],\"enabled\":{5}}}",
							HttpUtility.JavaScriptStringEncode(r.Id),
							HttpUtility.JavaScriptStringEncode(r.Action),
							r.Time.Hour,
							r.Time.Minute,
							string.Join(",", r.Weekdays.Select(d => "\"" + HttpUtility.JavaScriptStringEncode(d) + "\"")),
							r.Enabled ? "true" : "false"
						)
					));
	
					// Return diagnostics alongside parsed DTO to help troubleshoot 500s
					WriteJson(string.Format("{{\"enabled\":{0},\"rules\":[{1}],\"diagnostics\":{{\"ip\":\"{2}\",\"port\":{3},\"rulesDBVersion\":\"{4}\",\"rawXml\":\"{5}\",\"enabledProbe\":{6}}}}}",
						s.Enabled ? "true" : "false",
						rulesJson,
						HttpUtility.JavaScriptStringEncode(ip),
						port,
						HttpUtility.JavaScriptStringEncode(version ?? ""),
						HttpUtility.JavaScriptStringEncode(rawXml ?? ""),
						enabledFlag ? "true" : "false"));
					return;
				}
				catch (Exception ex)
				{
					// Detect UPnPError 501 Action Failed from device response and surface as 501
					var msg = ex.Message ?? "";
					var inner = ex.InnerException != null ? ex.InnerException.Message ?? "" : "";
					var isTimeout = ex is TaskCanceledException || (ex.InnerException is TaskCanceledException);
					var upnp501 = (msg.IndexOf("<errorCode>501", StringComparison.OrdinalIgnoreCase) >= 0)
						|| (msg.IndexOf("Action Failed", StringComparison.OrdinalIgnoreCase) >= 0)
						|| (inner.IndexOf("<errorCode>501", StringComparison.OrdinalIgnoreCase) >= 0)
						|| (inner.IndexOf("Action Failed", StringComparison.OrdinalIgnoreCase) >= 0);
					
					Response.StatusCode = upnp501 ? 501 : (isTimeout ? 504 : 500);
					
					if (upnp501)
					{
						// Hint that rules:1 may not be supported on this firmware/port
						WriteJson(string.Format("{{\"error\":\"rules_service_action_failed\",\"hint\":\"Device may not support rules:1 or requires different port\",\"ip\":\"{0}\",\"port\":{1}}}",
							HttpUtility.JavaScriptStringEncode(ip),
							port));
					}
					else
					{
						WriteJson(string.Format("{{\"error\":\"{0}\",\"inner\":\"{1}\",\"ip\":\"{2}\",\"port\":{3}}}",
							HttpUtility.JavaScriptStringEncode(msg),
							HttpUtility.JavaScriptStringEncode(inner),
							HttpUtility.JavaScriptStringEncode(ip),
							port));
					}
					return;
				}
			}
			if (originalPath.Contains("/api/schedule") && Request.HttpMethod == "POST")
			{
				try
				{
					// Read JSON body
					string body;
					using (var reader = new System.IO.StreamReader(Request.InputStream))
					{
						body = reader.ReadToEnd();
					}
					// Input supports:
					// - enabled: bool
					// - startHHMM: "HH:MM"
					// - stopHHMM: "HH:MM" (optional)
					bool enabled = ExtractBool(body, "enabled", defaultValue: true);
					string startHHMM = ExtractString(body, "startHHMM");
					string stopHHMM = ExtractString(body, "stopHHMM");
	
					if (string.IsNullOrWhiteSpace(startHHMM))
					{
						Response.StatusCode = 400;
						WriteJson("{\"ok\":false,\"error\":\"startHHMM required (local time)\"}");
						return;
					}
	
					(int sh, int sm) = ParseHHMM(startHHMM);
					int? stopH = null, stopM = null;
					if (!string.IsNullOrWhiteSpace(stopHHMM))
					{
						(int eh, int em) = ParseHHMM(stopHHMM);
						stopH = eh; stopM = em;
					}
	
					var allDays = new System.Collections.Generic.List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
					var schedule = new WeMosDef.Schedule
					{
						DeviceIp = ip,
						Enabled = enabled,
						Rules = new System.Collections.Generic.List<WeMosDef.Rule>()
					};
					schedule.Rules.Add(new WeMosDef.Rule
					{
						Action = "on",
						Time = new WeMosDef.TimeEvent { Hour = sh, Minute = sm },
						Weekdays = allDays,
						Enabled = true
					});
					if (stopH.HasValue && stopM.HasValue)
					{
						schedule.Rules.Add(new WeMosDef.Rule
						{
							Action = "off",
							Time = new WeMosDef.TimeEvent { Hour = stopH.Value, Minute = stopM.Value },
							Weekdays = allDays,
							Enabled = true
						});
					}
	
					var client = new WeMosDef.Client(ip, port);
					// Write device-native rules then set global enabled if supported
					client.SetDeviceSchedule(schedule);
	
					WriteJson("{\"ok\":true}");
					return;
				}
				catch (Exception ex)
				{
					Response.StatusCode = 400;
					WriteJson(string.Format("{{\"ok\":false,\"error\":\"{0}\"}}", HttpUtility.JavaScriptStringEncode(ex.Message)));
					return;
				}
			}
			if (originalPath.Contains("/api/schedule/enable") && Request.HttpMethod == "POST")
			{
				try
				{
					string body;
					using (var reader = new System.IO.StreamReader(Request.InputStream))
					{
						body = reader.ReadToEnd();
					}
					bool enabled = ExtractBool(body, "enabled", defaultValue: true);
					var client = new WeMosDef.Client(ip, port);
					client.SetScheduleEnabled(enabled);
					var isEnabled = client.GetScheduleEnabled();
					WriteJson(string.Format("{{\"ok\":true,\"enabled\":{0}}}", isEnabled ? "true" : "false"));
					return;
				}
				catch (Exception ex)
				{
					Response.StatusCode = 400;
					WriteJson(string.Format("{{\"ok\":false,\"error\":\"{0}\"}}", HttpUtility.JavaScriptStringEncode(ex.Message)));
					return;
				}
			}

			Response.StatusCode = 404;
			WriteJson("{\"error\":\"not_found\"}");
		}
		catch (ThreadAbortException)
		{
			// Expected from Response.End() - don't handle
			throw;
		}
		catch (Exception ex)
		{
			Response.StatusCode = 500;
			WriteJson(string.Format("{{\"error\":\"{0}\"}}", HttpUtility.JavaScriptStringEncode(ex.Message)));
		}
	}

	void HandleAction(string action)
	{
		WeMosDef.Client client;
		switch (action)
		{
			case "on":
				client = new WeMosDef.Client(ip, port);
				client.On();
				break;
			case "off":
				client = new WeMosDef.Client(ip, port);
				client.Off();
				break;
			case "powerstate":
				PowerState = SafeGetPowerState(ip, port);
				break;
			default:
				break;
		}
	}

	// JSON writer helper
	void WriteJson(string json)
	{
		Response.Write(json);
		Response.End();
	}

	// Server-Sent Events stream of state changes (poll-based)
	void StreamEvents(string ipAddr, int p)
	{
		Response.ContentType = "text/event-stream";
		Response.Charset = "utf-8";
		Response.BufferOutput = false;

		string lastState = null;
		var cancellation = Response.ClientDisconnectedToken;

		// Initial send
		try
		{
			var initial = SafeGetPowerState(ipAddr, p);
			lastState = initial;
			Response.Write(string.Format("data: {{\"type\":\"state\",\"state\":\"{0}\",\"timestamp\":\"{1:o}\"}}\n\n", initial == "0" ? "off" : "on", DateTime.UtcNow));
			Response.Flush();
		}
		catch (Exception ex)
		{
			Response.Write(string.Format("data: {{\"type\":\"error\",\"message\":\"{0}\"}}\n\n", HttpUtility.JavaScriptStringEncode(ex.Message)));
			Response.Flush();
		}

		// Poll loop
		while (!cancellation.IsCancellationRequested)
		{
			Thread.Sleep(3000);
			try
			{
				var s = SafeGetPowerState(ipAddr, p);
				if (s != lastState)
				{
					lastState = s;
					Response.Write(string.Format("data: {{\"type\":\"state\",\"state\":\"{0}\",\"timestamp\":\"{1:o}\"}}\n\n", s == "0" ? "off" : "on", DateTime.UtcNow));
					Response.Flush();
				}
			}
			catch (Exception ex)
			{
				Response.Write(string.Format("data: {{\"type\":\"error\",\"message\":\"{0}\"}}\n\n", HttpUtility.JavaScriptStringEncode(ex.Message)));
				Response.Flush();
			}
		}
		// End of stream
	}

	// Centralized WCF client construction from appSettings
	WeMosDef.ServiceReference1.BasicServicePortTypeClient GetClient()
	{
		var endpoint = ConfigurationManager.AppSettings["BasicServiceEndpoint"];
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			return null;
		}
		var binding = new System.ServiceModel.BasicHttpBinding();
		var address = new System.ServiceModel.EndpointAddress(endpoint);
		return new WeMosDef.ServiceReference1.BasicServicePortTypeClient(binding, address);
	}

	// Safe wrapper: returns "0"/"1" or throws friendly error
	string SafeGetPowerState(string ipAddr, int p)
	{
		// If endpoint missing, fail fast with friendly message for API paths
		var svcClient = GetClient();
		if (svcClient == null && (Request.Path?.IndexOf("/api/", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
		{
			throw new Exception("Endpoint not configured");
		}

		// Existing local device logic retained
		var client = new WeMosDef.Client(ipAddr, p);
		var task = Task.Run(() => client.GetState());
		if (task.Wait(TimeSpan.FromSeconds(15)))
		{
			return task.Result;
		}
		throw new Exception("Timed out getting Wemo status. Is WiFi connected?");
	}

	// Helpers for light-weight JSON extraction and time parsing (HH:MM)
	static string ExtractString(string json, string key)
	{
		if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return string.Empty;
		// naive search: "key":"value"
		var pattern = "\"" + key + "\"";
		var i = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
		if (i < 0) return string.Empty;
		i = json.IndexOf(':', i);
		if (i < 0) return string.Empty;
		// skip whitespace
		while (i + 1 < json.Length && char.IsWhiteSpace(json[i + 1])) i++;
		if (i + 1 >= json.Length) return string.Empty;
		if (json[i + 1] == '\"')
		{
			var start = i + 2;
			var end = json.IndexOf('\"', start);
			if (end < 0) return string.Empty;
			return json.Substring(start, end - start);
		}
		return string.Empty;
	}

	static bool ExtractBool(string json, string key, bool defaultValue)
	{
		if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return defaultValue;
		var pattern = "\"" + key + "\"";
		var i = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
		if (i < 0) return defaultValue;
		i = json.IndexOf(':', i);
		if (i < 0) return defaultValue;
		while (i + 1 < json.Length && char.IsWhiteSpace(json[i + 1])) i++;
		var start = i + 1;
		if (start >= json.Length) return defaultValue;
		if (json.IndexOf("true", start, StringComparison.OrdinalIgnoreCase) == start) return true;
		if (json.IndexOf("false", start, StringComparison.OrdinalIgnoreCase) == start) return false;
		return defaultValue;
	}

	static (int h, int m) ParseHHMM(string s)
	{
		var parts = (s ?? "").Split(':');
		if (parts.Length != 2) throw new Exception("Time must be HH:MM");
		int h, m;
		if (!int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m)) throw new Exception("Time must be HH:MM");
		if (h < 0 || h > 23) throw new Exception("Hour must be 0-23");
		if (m < 0 || m > 59) throw new Exception("Minute must be 0-59");
		return (h, m);
	}
}
