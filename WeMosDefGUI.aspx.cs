using System;
using System.Linq;
using System.Net;
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
		var wemo = FindingWemo.Search(System.Net.IPAddress.Parse(ip), System.Net.IPAddress.Parse(ip), "Rocket").SingleOrDefault();

		if (wemo == null)
			throw new Exception("Wemo not found");

		port = wemo.Port;
		PowerState = GetPowerState(wemo.IPAddress.ToString(), port);
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
			case "clean":
				DoClean(ip, port);
				break;
			case "powerstate":
				PowerState = GetPowerState(ip, port);
				break;
			default:
				break;
		}
	}

	// a cleaning cycle for my espresso machine
	void DoClean(string ip, int port)
	{
		TimeSpan onInterval = TimeSpan.FromSeconds(10);
		TimeSpan offInterval = TimeSpan.FromSeconds(10);
		int repetitions = 6;

		var client = new WeMosDef.Client(ip, port);
		for (int i = 0; i < repetitions; i++)
		{
			client.On();
			System.Threading.Thread.Sleep(onInterval);
			client.Off();
			// don't sleep the last time
			if (i < repetitions - 1)
				System.Threading.Thread.Sleep(offInterval);
		}
	}
	
	public string GetPowerState(string ip, int port)
	{
		var client = new WeMosDef.Client(ip, port);

		
		var task = Task.Run(() => client.GetState());
		if (task.Wait(TimeSpan.FromSeconds(15)))
			return task.Result;
		else
			throw new Exception("Timed out getting Wemo status. Is WiFi connected?");
	}
}
