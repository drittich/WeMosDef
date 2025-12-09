using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WeMosDef
{
    public class Client
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }

        // Single static HttpClient for reuse
        private static readonly HttpClient http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        });

        public Client(string ip, int port)
        {
            IpAddress = ip;
            Port = port;
        }

        // Public API

        public string GetState()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetBinaryState xmlns:u=""urn:Belkin:service:basicevent:1"">
    </u:GetBinaryState>
  </s:Body>
</s:Envelope>
";
            var result = SendSoapAsync("GetBinaryState", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            var state = ExtractTagValue(result, "BinaryState");
            if (string.IsNullOrEmpty(state)) throw new Exception("BinaryState not found in SOAP response");
            return state.Trim();
        }

        public string On()
        {
            return SetBinaryState("1");
        }

        public string Off()
        {
            return SetBinaryState("0");
        }

        public string GetSignalStrength()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetSignalStrength xmlns:u=""urn:Belkin:service:basicevent:1"">
    </u:GetSignalStrength>
  </s:Body>
</s:Envelope>
";
            var result = SendSoapAsync("GetSignalStrength", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            var v = ExtractTagValue(result, "SignalStrength");
            if (string.IsNullOrEmpty(v)) throw new Exception("SignalStrength not found");
            return v.Trim();
        }

        public string GetLogFileURL()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetLogFileURL xmlns:u=""urn:Belkin:service:basicevent:1"">
    </u:GetLogFileURL>
  </s:Body>
</s:Envelope>
";
            var result = SendSoapAsync("GetLogFileURL", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            var v = ExtractTagValue(result, "LOGURL");
            if (string.IsNullOrEmpty(v)) throw new Exception("LOGURL not found");
            return v.Trim();
        }

        public string GetIconURL()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetIconURL xmlns:u=""urn:Belkin:service:basicevent:1"">
    </u:GetIconURL>
  </s:Body>
</s:Envelope>
";
            var result = SendSoapAsync("GetIconURL", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            var v = ExtractTagValue(result, "URL");
            if (string.IsNullOrEmpty(v)) throw new Exception("URL not found");
            return v.Trim();
        }

        public string GetHomeId()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetHomeId xmlns:u=""urn:Belkin:service:basicevent:1"">
    </u:GetHomeId>
  </s:Body>
</s:Envelope>
";
            var result = SendSoapAsync("GetHomeId", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            var v = ExtractTagValue(result, "HomeId");
            if (string.IsNullOrEmpty(v)) throw new Exception("HomeId not found");
            return v.Trim();
        }

        public string GetFriendlyName()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetFriendlyName xmlns:u=""urn:Belkin:service:basicevent:1"">
    </u:GetFriendlyName>
  </s:Body>
</s:Envelope>
";
            var result = SendSoapAsync("GetFriendlyName", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            var v = ExtractTagValue(result, "FriendlyName");
            if (string.IsNullOrEmpty(v)) throw new Exception("FriendlyName not found");
            return v.Trim();
        }

        public void ChangeFriendlyName(string newName = "0")
        {
            var safe = System.Security.SecurityElement.Escape(newName);
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:ChangeFriendlyName xmlns:u=""urn:Belkin:service:basicevent:1"">
      <FriendlyName>{0}</FriendlyName>
    </u:ChangeFriendlyName>
  </s:Body>
</s:Envelope>
";
            soapBody = string.Format(soapBody, safe);
            SendSoapAsync("ChangeFriendlyName", soapBody, CancellationToken.None).GetAwaiter().GetResult();
        }

        // Local scheduler API (unchanged)
        public Schedule ReadSchedule() { return ScheduleStore.Load(IpAddress); }
        public Schedule EnableSchedule()
        {
            var s = ScheduleStore.Load(IpAddress);
            s.Enabled = true;
            ScheduleStore.Save(s);
            return s;
        }
        public Schedule DisableSchedule()
        {
            var s = ScheduleStore.Load(IpAddress);
            s.Enabled = false;
            ScheduleStore.Save(s);
            return s;
        }
        public Schedule UpdateSchedule(Schedule schedule)
        {
            if (schedule == null) throw new ArgumentNullException("schedule");
            if (string.IsNullOrWhiteSpace(schedule.DeviceIp))
            {
                schedule.DeviceIp = IpAddress;
            }
            else if (!string.Equals(schedule.DeviceIp, IpAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Schedule.DeviceIp must match this client's IpAddress");
            }
            ScheduleStore.Save(schedule);
            return schedule;
        }

        // Helpers

        private string EndPointAddress
        {
            get { return "http://" + IpAddress + ":" + Port + "/upnp/control/basicevent1"; }
        }

        private string SetBinaryState(string value)
        {
            var safe = System.Security.SecurityElement.Escape(value);
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:SetBinaryState xmlns:u=""urn:Belkin:service:basicevent:1"">
      <BinaryState>{0}</BinaryState>
    </u:SetBinaryState>
  </s:Body>
</s:Envelope>
";
            soapBody = string.Format(soapBody, safe);
            var result = SendSoapAsync("SetBinaryState", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            var state = ExtractTagValue(result, "BinaryState");
            if (string.IsNullOrEmpty(state)) throw new Exception("BinaryState not found in response");
            return state.Trim();
        }

        private async Task<string> SendSoapAsync(string action, string soapXml, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, EndPointAddress))
            {
                req.Headers.Add("SOAPACTION", "\"urn:Belkin:service:basicevent:1#" + action + "\"");
                req.Content = new StringContent(soapXml, Encoding.UTF8, "text/xml");
                var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                // .NET Framework HttpContent.ReadAsStringAsync() has no ct overload
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return body;
            }
        }

        private static string ExtractTagValue(string xml, string tagName)
        {
            // Simple tag extraction to avoid extra XML dependencies
            var open = "<" + tagName + ">";
            var close = "</" + tagName + ">";
            var i = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return string.Empty;
            i += open.Length;
            var j = xml.IndexOf(close, i, StringComparison.OrdinalIgnoreCase);
            if (j < 0) return string.Empty;
            return xml.Substring(i, j - i);
        }
    }
}
