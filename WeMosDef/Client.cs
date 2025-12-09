using System.ServiceModel;

namespace WeMosDef
{
    public class Client
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public Client(string ip, int port) {
            IpAddress = ip;
            Port = port;
        }

        public string GetState()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var state = client.GetBinaryState(new ServiceReference1.GetBinaryState());
                return state.BinaryState;
            }
        }

        public string On()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var msg = new ServiceReference1.SetBinaryState { BinaryState = "1" };
                var state = client.SetBinaryState(msg);
                return state.BinaryState;
            }
        }

        public string Off()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var msg = new ServiceReference1.SetBinaryState { BinaryState = "0" };
                var state = client.SetBinaryState(msg);
                return state.BinaryState;
            }
        }

        public string GetSignalStrength()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var state = client.GetSignalStrength(new ServiceReference1.GetSignalStrength());
                return state.SignalStrength;
            }
        }

        public string GetLogFileURL()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var state = client.GetLogFileURL(new ServiceReference1.GetLogFileURL());
                return state.LOGURL;
            }
        }

        public string GetIconURL()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var state = client.GetIconURL(new ServiceReference1.GetIconURL());
                return state.URL;
            }
        }

        public string GetHomeId()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var state = client.GetHomeId(new ServiceReference1.GetHomeId());
                return state.HomeId;
            }
        }

        public string GetFriendlyName()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var state = client.GetFriendlyName(new ServiceReference1.GetFriendlyName());
                return state.FriendlyName;
            }
        }

        public void ChangeFriendlyName()
        {
            using (var client = new ServiceReference1.BasicServicePortTypeClient())
            {
                client.Endpoint.Address = new EndpointAddress(EndPointAddress);
                var msg = new ServiceReference1.ChangeFriendlyName { FriendlyName = "0" };
                var state = client.ChangeFriendlyName(msg);
            }
        }

        // Local scheduler API

        // Client.ReadSchedule()
        public Schedule ReadSchedule()
        {
            return ScheduleStore.Load(IpAddress);
        }

        // Client.EnableSchedule()
        public Schedule EnableSchedule()
        {
            var s = ScheduleStore.Load(IpAddress);
            s.Enabled = true;
            ScheduleStore.Save(s);
            return s;
        }

        // Client.DisableSchedule()
        public Schedule DisableSchedule()
        {
            var s = ScheduleStore.Load(IpAddress);
            s.Enabled = false;
            ScheduleStore.Save(s);
            return s;
        }

        // Client.UpdateSchedule(schedule)
        public Schedule UpdateSchedule(Schedule schedule)
        {
            if (schedule == null) throw new System.ArgumentNullException(nameof(schedule));
            if (string.IsNullOrWhiteSpace(schedule.DeviceIp))
            {
                schedule.DeviceIp = IpAddress;
            }
            else if (!string.Equals(schedule.DeviceIp, IpAddress, System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.ArgumentException("Schedule.DeviceIp must match this client's IpAddress");
            }

            ScheduleStore.Save(schedule);
            return schedule;
        }

        string EndPointAddress
        {
            get
            {
                return $"http://{IpAddress}:{Port}/upnp/control/basicevent1";
            }
        }
    }
}
