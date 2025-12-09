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

    // Device-native scheduling only: local persistence removed.
    // Kept for backward compatibility to avoid compile errors; all methods throw NotSupportedException.
    public static class ScheduleStore
    {
        public static string RootDirectory => throw new NotSupportedException("Local schedule persistence has been removed. Use device rules via Client.");
    
        public static string GetPath(string deviceIp) => throw new NotSupportedException("Local schedule persistence has been removed. Use device rules via Client.");
    
        public static Schedule Load(string deviceIp) => throw new NotSupportedException("Local schedule persistence has been removed. Use device rules via Client.");
    
        public static void Save(Schedule schedule) => throw new NotSupportedException("Local schedule persistence has been removed. Use device rules via Client.");
    
        private static string Sanitize(string ip) => throw new NotSupportedException("Local schedule persistence has been removed. Use device rules via Client.");
    }

    // Device-native scheduling handles execution; local runner removed.
    // Kept as a stub to avoid breaking references.
    public class SchedulerRunner
    {
        public SchedulerRunner(Client client, Func<DateTime> nowLocal = null)
        {
            throw new NotSupportedException("Local scheduler has been removed. Device executes rules natively.");
        }
    
        public void Tick()
        {
            throw new NotSupportedException("Local scheduler has been removed. Device executes rules natively.");
        }
    }
}