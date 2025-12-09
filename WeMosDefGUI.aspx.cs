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
				// If discovery fails, continue with configured ip/port (port unknown -> 0)
				port = port == 0 ? 49153 : port; // default typical UPnP port fallback
			}
			else
			{
				port = wemo.Port;
				ip = wemo.IPAddress.ToString();
			}
		}
		catch
		{
			// If discovery throws, use defaults
			port = port == 0 ? 49153 : port;
		}
	}

	void HandleApiRequest(string originalPath)
	{
		// For API endpoints that need device info, discover first
		bool needsDevice = originalPath.Contains("/api/state") ||
		                   originalPath.Contains("/api/toggle") ||
		                   originalPath.Contains("/api/events") ||
		                   originalPath.Contains("/api/info");
		
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
			// Schedule endpoints (stubs to be implemented)
			if (originalPath.Contains("/api/schedule") && Request.HttpMethod == "GET")
			{
				// TODO: read rules from device when supported by WeMosDef/FindingWemo
				WriteJson("{\"rules\":[],\"enabled\":false}");
				return;
			}
			if (originalPath.Contains("/api/schedule") && Request.HttpMethod == "POST")
			{
				// TODO: validate payload and write rules to device
				WriteJson("{\"ok\":true}");
				return;
			}
			if (originalPath.Contains("/api/schedule/enable") && Request.HttpMethod == "POST")
			{
				// TODO: enable/disable schedule on device
				WriteJson("{\"ok\":true}");
				return;
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
