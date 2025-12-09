using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using WeMosDef;
using FindingWemoNS;

public partial class WeMosDefGUI : System.Web.UI.Page
{
	public static string ip = "192.168.15.22"; 
	public int port;
	public string PowerState = null;

	protected void Page_Load(object sender, EventArgs e)
	{
		// Discover single device once per request (fallback to fixed ip/port if needed)
		var wemo = FindingWemo.Search(System.Net.IPAddress.Parse(ip), System.Net.IPAddress.Parse(ip), "Rocket").SingleOrDefault();
		if (wemo == null)
		{
			// If discovery fails, continue with configured ip/port (port unknown -> 0)
			port = port == 0 ? 49153 : port; // default typical UPnP port fallback
		}
		else
		{
			port = wemo.Port;
			ip = wemo.IPAddress.ToString();
		}

		// API routing: respond with JSON for /api/* paths, otherwise render page
		var path = (Request.Path ?? string.Empty).ToLowerInvariant();
		if (path.Contains("/api/"))
		{
			// Special case: SSE stream
			if (path.Contains("/api/events") && Request.HttpMethod == "GET")
			{
				StreamEvents(ip, port);
				return;
			}

			Response.ContentType = "application/json";
			try
			{
				if (path.Contains("/api/state") && Request.HttpMethod == "GET")
				{
					var state = SafeGetPowerState(ip, port);
					WriteJson($"{{\"state\":\"{(state == "0" ? "off" : "on")}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}");
					return;
				}
				if (path.Contains("/api/toggle") && Request.HttpMethod == "POST")
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
					WriteJson($"{{\"state\":\"{(newState == "0" ? "off" : "on")}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}");
					return;
				}
				if (path.Contains("/api/info") && Request.HttpMethod == "GET")
				{
					// Basic info; populate with what the libraries expose if available
					var make = "Belkin";
					var model = "WeMo Smart Plug";
					var firmware = "unknown";
					var name = "Rocket";

					WriteJson($"{{\"make\":\"{make}\",\"model\":\"{model}\",\"firmware\":\"{firmware}\",\"friendlyName\":\"{name}\",\"ip\":\"{ip}\",\"port\":{port}}}");
					return;
				}
				// Schedule endpoints (stubs to be implemented)
				if (path.Contains("/api/schedule") && Request.HttpMethod == "GET")
				{
					// TODO: read rules from device when supported by WeMosDef/FindingWemo
					WriteJson("{\"rules\":[],\"enabled\":false}");
					return;
				}
				if (path.Contains("/api/schedule") && Request.HttpMethod == "POST")
				{
					// TODO: validate payload and write rules to device
					WriteJson("{\"ok\":true}");
					return;
				}
				if (path.Contains("/api/schedule/enable") && Request.HttpMethod == "POST")
				{
					// TODO: enable/disable schedule on device
					WriteJson("{\"ok\":true}");
					return;
				}

				Response.StatusCode = 404;
				WriteJson("{\"error\":\"not_found\"}");
				return;
			}
			catch (Exception ex)
			{
				Response.StatusCode = 500;
				WriteJson($"{{\"error\":\"{HttpUtility.JavaScriptStringEncode(ex.Message)}\"}}");
				return;
			}
		}

		// Normal page render path
		PowerState = SafeGetPowerState(ip, port);
		if (Request.ServerVariables["REQUEST_METHOD"] == "POST")
			HandleAction(Request.Form["action"]);
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
			Response.Write($"data: {{\"type\":\"state\",\"state\":\"{(initial == "0" ? "off" : "on")}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}\n\n");
			Response.Flush();
		}
		catch (Exception ex)
		{
			Response.Write($"data: {{\"type\":\"error\",\"message\":\"{HttpUtility.JavaScriptStringEncode(ex.Message)}\"}}\n\n");
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
					Response.Write($"data: {{\"type\":\"state\",\"state\":\"{(s == "0" ? "off" : "on")}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}\n\n");
					Response.Flush();
				}
			}
			catch (Exception ex)
			{
				Response.Write($"data: {{\"type\":\"error\",\"message\":\"{HttpUtility.JavaScriptStringEncode(ex.Message)}\"}}\n\n");
				Response.Flush();
			}
		}
		// End of stream
	}
	
	// Safe wrapper: returns "0"/"1" or throws friendly error
	string SafeGetPowerState(string ipAddr, int p)
	{
		var client = new WeMosDef.Client(ipAddr, p);
		var task = Task.Run(() => client.GetState());
		if (task.Wait(TimeSpan.FromSeconds(15)))
		{
			return task.Result;
		}
		throw new Exception("Timed out getting Wemo status. Is WiFi connected?");
	}
}
