using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace WeMosDef
{
    // EnabledFlag(value)
    [DataContract]
    public class EnabledFlag
    {
        [DataMember(Order = 1)]
        public bool Value { get; set; }
    }

    // TimeEvent(hour, minute)
    [DataContract]
    public class TimeEvent
    {
        [DataMember(Order = 1)]
        public int Hour { get; set; }
        [DataMember(Order = 2)]
        public int Minute { get; set; }

        public DateTime TodayAtLocal()
        {
            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, now.Day, Hour, Minute, 0, DateTimeKind.Local);
        }
    }

    // Rule(id, enabled, action, time, weekdays[])
    [DataContract]
    public class Rule
    {
        // Action is "on" or "off"
        [DataMember(Order = 1)]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [DataMember(Order = 2)]
        public bool Enabled { get; set; } = true;

        [DataMember(Order = 3)]
        public string Action { get; set; } = "on";

        [DataMember(Order = 4)]
        public TimeEvent Time { get; set; } = new TimeEvent { Hour = 0, Minute = 0 };

        // Weekdays as three-letter names: Mon,Tue,Wed,Thu,Fri,Sat,Sun
        [DataMember(Order = 5)]
        public List<string> Weekdays { get; set; } = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public void Validate()
        {
            if (Action != "on" && Action != "off")
                throw new ArgumentException("Rule.Action must be 'on' or 'off'");
            if (Time == null)
                throw new ArgumentException("Rule.Time is required");
            if (Time.Hour < 0 || Time.Hour > 23)
                throw new ArgumentException("Rule.Time.Hour must be between 0 and 23");
            if (Time.Minute < 0 || Time.Minute > 59)
                throw new ArgumentException("Rule.Time.Minute must be between 0 and 59");
            if (Weekdays == null || Weekdays.Count == 0)
                throw new ArgumentException("Rule.Weekdays must include at least one weekday");
            var valid = new HashSet<string>(new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, StringComparer.OrdinalIgnoreCase);
            foreach (var d in Weekdays)
            {
                if (!valid.Contains(d))
                    throw new ArgumentException("Rule.Weekdays must be three-letter names: Mon,Tue,Wed,Thu,Fri,Sat,Sun");
            }
        }

        public bool IsDueNow(DateTime nowLocal)
        {
            if (!Enabled) return false;

            var weekday = nowLocal.ToString("ddd"); // Mon, Tue, ...
            if (!Weekdays.Any(w => string.Equals(w, weekday, StringComparison.OrdinalIgnoreCase)))
                return false;

            return Time.Hour == nowLocal.Hour && Time.Minute == nowLocal.Minute;
        }
    }

    // Schedule(deviceIp, enabled, rules[])
    [DataContract]
    public class Schedule
    {
        [DataMember(Order = 1)]
        public string DeviceIp { get; set; } = "";

        [DataMember(Order = 2)]
        public bool Enabled { get; set; } = true;

        [DataMember(Order = 3)]
        public List<Rule> Rules { get; set; } = new List<Rule>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DeviceIp))
                throw new ArgumentException("Schedule.DeviceIp is required");
            // Unique rule IDs
            var dup = Rules.GroupBy(r => r.Id).FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
                throw new ArgumentException("Schedule.Rules must have unique Ids");
            foreach (var r in Rules)
            {
                r.Validate();
            }
        }
    }

    // ScheduleStore: JSON persistence under %USERPROFILE%/.wemodef/schedules/{deviceIp}.json
    public static class ScheduleStore
    {
        public static string RootDirectory
        {
            get
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var dir = Path.Combine(home, ".wemodef", "schedules");
                return dir;
            }
        }

        public static string GetPath(string deviceIp)
        {
            var file = $"{Sanitize(deviceIp)}.json";
            return Path.Combine(RootDirectory, file);
        }

        public static Schedule Load(string deviceIp)
        {
            var path = GetPath(deviceIp);
            if (!File.Exists(path))
            {
                return new Schedule
                {
                    DeviceIp = deviceIp,
                    Enabled = true,
                    Rules = new List<Rule>()
                };
            }

            using (var fs = File.OpenRead(path))
            {
                var ser = new DataContractJsonSerializer(typeof(Schedule));
                var schedule = (Schedule)ser.ReadObject(fs);
                schedule.DeviceIp = deviceIp; // ensure consistency
                return schedule;
            }
        }

        public static void Save(Schedule schedule)
        {
            schedule.Validate();

            Directory.CreateDirectory(RootDirectory);
            var path = GetPath(schedule.DeviceIp);
            using (var fs = File.Create(path))
            {
                var ser = new DataContractJsonSerializer(typeof(Schedule));
                ser.WriteObject(fs, schedule);
            }
        }

        private static string Sanitize(string ip)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(ip.Length);
            foreach (var ch in ip)
            {
                sb.Append(invalid.Contains(ch) ? '_' : ch);
            }
            return sb.ToString();
        }
    }

    // SchedulerRunner: checks each minute and applies On/Off using Client
    public class SchedulerRunner
    {
        private readonly Client _client;
        private readonly Func<DateTime> _nowLocal;
        private DateTime _lastRunMinute = DateTime.MinValue;

        // Conflict policy: if multiple rules due same minute, prefer the last rule order ("last wins")
        public SchedulerRunner(Client client, Func<DateTime> nowLocal = null)
        {
            _client = client;
            _nowLocal = nowLocal ?? (() => DateTime.Now);
        }

        // Call Tick() periodically (e.g., via a timer every 10-15 seconds); it will execute once per minute
        public void Tick()
        {
            var now = _nowLocal().AddSeconds(0);
            var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Local);
            if (currentMinute == _lastRunMinute) return;

            _lastRunMinute = currentMinute;

            var schedule = ScheduleStore.Load(_client.IpAddress);
            if (!schedule.Enabled) return;

            var dueActions = new List<string>();
            foreach (var rule in schedule.Rules)
            {
                if (rule.IsDueNow(now))
                {
                    dueActions.Add(rule.Action);
                }
            }

            if (dueActions.Count == 0) return;

            // Last wins policy; allow OFF to override ON or vice versa by order
            var finalAction = dueActions.Last();
            if (string.Equals(finalAction, "on", StringComparison.OrdinalIgnoreCase))
            {
                _client.On();
            }
            else if (string.Equals(finalAction, "off", StringComparison.OrdinalIgnoreCase))
            {
                _client.Off();
            }
        }
    }
}