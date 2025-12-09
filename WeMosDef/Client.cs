using System;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Globalization;

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
        
        // Device Rules (urn:Belkin:service:rules:1) — native scheduling
        
        // Raw XML accessors to let the web/UI layer parse/serialize rules precisely.
        // Get the full rules XML payload from the device.
        public string GetDeviceScheduleXml()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetRules xmlns:u=""urn:Belkin:service:rules:1"">
    </u:GetRules>
  </s:Body>
</s:Envelope>
";
            string result;
            try
            {
                result = SendRulesSoapAsync("GetRules", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (HttpRequestException ex)
            {
                // If initial port fails, probe alternate ports and retry once
                var found = TryFindRulesPort();
                if (found > 0)
                {
                    Port = found;
                    result = SendRulesSoapAsync("GetRules", soapBody, CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    throw;
                }
            }

            // GetRules usually returns rules as an XML string inside <Rules>/<rules> or <ruleDb> depending on firmware.
            // Try common tag names:
            var payload = ExtractTagValue(result, "Rules");
            if (string.IsNullOrEmpty(payload))
                payload = ExtractTagValue(result, "rules");
            if (string.IsNullOrEmpty(payload))
                payload = ExtractTagValue(result, "ruleDb");

            if (string.IsNullOrEmpty(payload))
                throw new Exception("Rules payload not found in GetRules response");

            // Some firmwares embed the rules XML as escaped text (e.g., CDATA with < >). Decode if needed.
            if (payload.IndexOf("<", StringComparison.OrdinalIgnoreCase) >= 0 ||
                payload.IndexOf(">", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                payload = WebUtility.HtmlDecode(payload);
            }

            return payload.Trim();
        }
        
        // High-level DTO mapping based on existing Schedule/Rule types (no local persistence).
        public Schedule GetDeviceSchedule()
        {
            var xml = GetDeviceScheduleXml();
            var schedule = new Schedule { DeviceIp = IpAddress, Enabled = true };
            
            // Best-effort parsing for common Wemo rule schemas.
            // Try to parse as XML document.
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch { throw new Exception("Failed to parse device rules XML"); }
            
            // Rules typically under <rules> or <Rules> with child <rule> entries.
            var ruleElements = doc.Descendants("rule");
            foreach (var re in ruleElements)
            {
                var r = new Rule();
                
                // Enabled
                var enabledVal = (string)re.Element("Enabled") ?? (string)re.Element("enabled");
                r.Enabled = ParseBool(enabledVal, defaultValue: true);
                
                // Action: devices often use <action> with values 1 (on) / 0 (off), or strings "on"/"off"
                var actionVal = (string)re.Element("action") ?? (string)re.Element("Action");
                if (string.IsNullOrEmpty(actionVal))
                {
                    // Some schemas store on/off under "State" or "on" flag
                    var stateVal = (string)re.Element("State") ?? (string)re.Element("state");
                    actionVal = stateVal;
                }
                r.Action = NormalizeAction(actionVal);
                
                // Time: may be <StartTime>HH:MM</StartTime> or hour/minute split
                var startTime = (string)re.Element("StartTime") ?? (string)re.Element("startTime");
                int hour = 0, minute = 0;
                if (!string.IsNullOrEmpty(startTime))
                {
                    var parts = startTime.Split(':');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0], out hour);
                        int.TryParse(parts[1], out minute);
                    }
                }
                else
                {
                    int.TryParse((string)re.Element("StartHour") ?? (string)re.Element("Hour"), out hour);
                    int.TryParse((string)re.Element("StartMinute") ?? (string)re.Element("Minute"), out minute);
                }
                r.Time = new TimeEvent { Hour = Clamp(hour, 0, 23), Minute = Clamp(minute, 0, 59) };
                
                // Weekdays: common tags are <Day>Mon</Day> repeated, or a bitmask or CSV under <Weekdays>
                var weekdaysCsv = (string)re.Element("Weekdays") ?? (string)re.Element("weekdays");
                var daysElems = re.Elements("Day");
                var days = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(weekdaysCsv))
                {
                    foreach (var d in weekdaysCsv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        days.Add(MapDay(d));
                }
                else if (daysElems != null)
                {
                    foreach (var de in daysElems)
                        days.Add(MapDay(((string)de) ?? ""));
                }
                if (days.Count == 0)
                {
                    // Some devices encode bitmask under <Repeat> or <DayMask> (Mon=1<<0 ... Sun=1<<6).
                    var maskStr = (string)re.Element("Repeat") ?? (string)re.Element("DayMask");
                    if (int.TryParse(maskStr, out var mask))
                    {
                        days = DecodeDayMask(mask);
                    }
                }
                if (days.Count == 0)
                {
                    // Default to every day
                    days = new System.Collections.Generic.List<string> { "Mon","Tue","Wed","Thu","Fri","Sat","Sun" };
                }
                r.Weekdays = days;
                
                // Id: keep device rule id if present
                var id = (string)re.Element("RuleID") ?? (string)re.Element("id");
                if (!string.IsNullOrWhiteSpace(id)) r.Id = id;
                
                // Validate and add
                try { r.Validate(); schedule.Rules.Add(r); }
                catch { /* Skip invalid rule entry */ }
            }
            
            // Determine global enabled heuristically since Wemo may not expose a flag
            schedule.Enabled = GetScheduleEnabled();
            return schedule;
        }
        
        public void SetDeviceSchedule(Schedule schedule)
        {
            if (schedule == null) throw new ArgumentNullException("schedule");
            schedule.DeviceIp = IpAddress;
            schedule.Validate();
            
            // Serialize to a common Wemo rules format:
            // <Rules><rule>...</rule></Rules>
            var rulesEl = new XElement("Rules");
            foreach (var r in schedule.Rules)
            {
                var actionVal = r.Action.Equals("on", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
                var startTime = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", r.Time.Hour, r.Time.Minute);
                var dayMask = EncodeDayMask(r.Weekdays);
                
                var ruleEl = new XElement("rule",
                    new XElement("RuleID", r.Id),
                    new XElement("Enabled", r.Enabled ? "1" : "0"),
                    new XElement("action", actionVal),
                    new XElement("StartTime", startTime),
                    new XElement("DayMask", dayMask)
                );
                
                rulesEl.Add(ruleEl);
            }
            var xml = new XDocument(new XElement("ruleDb", rulesEl)).ToString(SaveOptions.DisableFormatting);
            SetDeviceScheduleXml(xml);
            
            // Apply global enabled if possible or fallback to per-rule toggling performed by SetScheduleEnabled
            try
            {
                SetScheduleEnabled(schedule.Enabled);
            }
            catch
            {
                // Ignore inability to set global flag; rules themselves carry Enabled state
            }
        }
        
        private static bool ParseBool(string s, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(s)) return defaultValue;
            s = s.Trim();
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return defaultValue;
        }
        
        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        
        private static string NormalizeAction(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return "on";
            val = val.Trim();
            if (val == "1" || val.Equals("on", StringComparison.OrdinalIgnoreCase)) return "on";
            if (val == "0" || val.Equals("off", StringComparison.OrdinalIgnoreCase)) return "off";
            return "on";
        }
        
        private static System.Collections.Generic.List<string> DecodeDayMask(int mask)
        {
            var days = new System.Collections.Generic.List<string>();
            string[] all = { "Mon","Tue","Wed","Thu","Fri","Sat","Sun" };
            for (int i = 0; i < 7; i++)
            {
                if ((mask & (1 << i)) != 0) days.Add(all[i]);
            }
            return days;
        }
        
        private static int EncodeDayMask(System.Collections.Generic.List<string> days)
        {
            int mask = 0;
            string[] all = { "Mon","Tue","Wed","Thu","Fri","Sat","Sun" };
            for (int i = 0; i < 7; i++)
            {
                if (days.Exists(d => string.Equals(d, all[i], StringComparison.OrdinalIgnoreCase)))
                    mask |= (1 << i);
            }
            return mask;
        }
        
        private static string MapDay(string d)
        {
            if (string.IsNullOrWhiteSpace(d)) return "";
            d = d.Trim().ToLowerInvariant();
            switch (d)
            {
                case "mon": case "monday": case "1": return "Mon";
                case "tue": case "tuesday": case "2": return "Tue";
                case "wed": case "wednesday": case "3": return "Wed";
                case "thu": case "thursday": case "4": return "Thu";
                case "fri": case "friday": case "5": return "Fri";
                case "sat": case "saturday": case "6": return "Sat";
                case "sun": case "sunday": case "7": case "0": return "Sun";
                default: return "Mon";
            }
        }
        
        // Set the full rules XML payload to the device.
        public void SetDeviceScheduleXml(string rulesXml)
        {
            if (string.IsNullOrWhiteSpace(rulesXml)) throw new ArgumentNullException("rulesXml");
            var safe = System.Security.SecurityElement.Escape(rulesXml);
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:SetRules xmlns:u=""urn:Belkin:service:rules:1"">
      <Rules>{0}</Rules>
    </u:SetRules>
  </s:Body>
</s:Envelope>
";
            soapBody = string.Format(soapBody, safe);
            var result = SendRulesSoapAsync("SetRules", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            // Firmware may echo back success or new version; no strict parsing required here.
        }
        
        // Get rules DB version for caching/ETag-like behavior.
        public string GetRulesDBVersion()
        {
            var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetRulesDBVersion xmlns:u=""urn:Belkin:service:rules:1"">
    </u:GetRulesDBVersion>
  </s:Body>
</s:Envelope>
";
            string result;
            try
            {
                result = SendRulesSoapAsync("GetRulesDBVersion", soapBody, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (HttpRequestException)
            {
                var found = TryFindRulesPort();
                if (found > 0)
                {
                    Port = found;
                    result = SendRulesSoapAsync("GetRulesDBVersion", soapBody, CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    throw;
                }
            }

            var v = ExtractTagValue(result, "RulesDBVersion");
            if (string.IsNullOrEmpty(v))
                v = ExtractTagValue(result, "RuleDBVersion");
            if (string.IsNullOrEmpty(v)) throw new Exception("RulesDBVersion not found");
            return v.Trim();
        }
        
        // Enable/Disable scheduling: prefer a global rules engine flag if available; otherwise fallback
        // by toggling all rules' enabled states.
        public bool GetScheduleEnabled()
        {
            // Try global flag
            try
            {
                var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetRulesEngineEnabled xmlns:u=""urn:Belkin:service:rules:1"">
    </u:GetRulesEngineEnabled>
  </s:Body>
</s:Envelope>
";
                string result;
                try
                {
                    result = SendRulesSoapAsync("GetRulesEngineEnabled", soapBody, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (HttpRequestException)
                {
                    var found = TryFindRulesPort();
                    if (found > 0)
                    {
                        Port = found;
                        result = SendRulesSoapAsync("GetRulesEngineEnabled", soapBody, CancellationToken.None).GetAwaiter().GetResult();
                    }
                    else
                    {
                        throw;
                    }
                }
                var flag = ExtractTagValue(result, "Enabled");
                if (!string.IsNullOrEmpty(flag))
                {
                    return flag.Trim() == "1" || flag.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Fall through to heuristic
            }
            
            // Heuristic: consider enabled if at least one rule is enabled in the rules XML
            var rulesXml = GetDeviceScheduleXml();
            // crude check for <Enabled>true/1</Enabled> occurrences
            if (rulesXml.IndexOf(">true<", StringComparison.OrdinalIgnoreCase) >= 0 ||
                rulesXml.IndexOf(">1<", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return false;
        }
        
        public void SetScheduleEnabled(bool enabled)
        {
            // Prefer global flag if present
            try
            {
                var val = enabled ? "1" : "0";
                var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:SetRulesEngineEnabled xmlns:u=""urn:Belkin:service:rules:1"">
      <Enabled>{0}</Enabled>
    </u:SetRulesEngineEnabled>
  </s:Body>
</s:Envelope>
";
                soapBody = string.Format(soapBody, val);
                try
                {
                    SendRulesSoapAsync("SetRulesEngineEnabled", soapBody, CancellationToken.None).GetAwaiter().GetResult();
                    return;
                }
                catch (HttpRequestException)
                {
                    var found = TryFindRulesPort();
                    if (found > 0)
                    {
                        Port = found;
                        SendRulesSoapAsync("SetRulesEngineEnabled", soapBody, CancellationToken.None).GetAwaiter().GetResult();
                        return;
                    }
                    // else fall through to per-rule fallback
                }
            }
            catch
            {
                // Fallback toggle all rules
            }
            
            var rulesXml = GetDeviceScheduleXml();
            // Replace known enabled tags with the desired value. We aim to toggle:
            // <Enabled>true</Enabled> or <Enabled>1</Enabled> to <Enabled>false</Enabled>/<Enabled>0</Enabled>
            string newXml;
            if (enabled)
            {
                newXml = rulesXml
                    .Replace("<Enabled>false</Enabled>", "<Enabled>true</Enabled>")
                    .Replace("<Enabled>0</Enabled>", "<Enabled>1</Enabled>");
            }
            else
            {
                newXml = rulesXml
                    .Replace("<Enabled>true</Enabled>", "<Enabled>false</Enabled>")
                    .Replace("<Enabled>1</Enabled>", "<Enabled>0</Enabled>");
            }
            SetDeviceScheduleXml(newXml);
        }
        
        // Helpers

        private string EndPointAddress
        {
            get { return "http://" + IpAddress + ":" + Port + "/upnp/control/basicevent1"; }
        }
        
        private string RulesEndPointAddress
        {
            get { return "http://" + IpAddress + ":" + Port + "/upnp/control/rules1"; }
        }
        
        // Probe for a working rules:1 endpoint across candidate ports.
        // Returns the first port that responds to GetRulesDBVersion, or -1 if none found.
        public int TryFindRulesPort(params int[] candidatePorts)
        {
            if (candidatePorts == null || candidatePorts.Length == 0)
                candidatePorts = new[] { 49152, 49153, 49154, 49155, 49156, 49157, 49158, 49159, 49160 };
            
            var original = Port;
            foreach (var p in candidatePorts)
            {
                try
                {
                    Port = p;
                    var soapBody = @"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <u:GetRulesDBVersion xmlns:u=""urn:Belkin:service:rules:1"">
    </u:GetRulesDBVersion>
  </s:Body>
</s:Envelope>
";
                    var result = SendRulesSoapAsync("GetRulesDBVersion", soapBody, CancellationToken.None).GetAwaiter().GetResult();
                    // Basic success heuristic: contains RulesDBVersion or RuleDBVersion
                    var v = ExtractTagValue(result, "RulesDBVersion");
                    if (string.IsNullOrEmpty(v))
                        v = ExtractTagValue(result, "RuleDBVersion");
                    if (!string.IsNullOrEmpty(v))
                    {
                        // restore selected port and report found
                        Port = p;
                        return p; // found a working rules endpoint
                    }
                }
                catch
                {
                    // ignore and continue probing
                }
            }
            Port = original;
            return -1;
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
        
                // Verbose header logging disabled after diagnosis to reduce noise.
                // If needed for future diagnostics, re-enable by dumping resp.Content.Headers here.
        
                // Safely decode body regardless of invalid/quoted charset headers
                // Read raw bytes and force UTF-8 decoding (Wemo devices respond UTF-8)
                var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var body = Encoding.UTF8.GetString(bytes);
                return body;
            }
        }
        
        private async Task<string> SendRulesSoapAsync(string action, string soapXml, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, RulesEndPointAddress))
            {
                req.Headers.Add("SOAPACTION", "\"urn:Belkin:service:rules:1#" + action + "\"");
                req.Content = new StringContent(soapXml, Encoding.UTF8, "text/xml");
                var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        
                var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var body = Encoding.UTF8.GetString(bytes);
        
                if (!resp.IsSuccessStatusCode)
                {
                    // Bubble up device 500/4xx with payload for diagnostics
                    throw new HttpRequestException("Rules SOAP " + (int)resp.StatusCode + " " + resp.ReasonPhrase + " — " + body);
                }
        
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
