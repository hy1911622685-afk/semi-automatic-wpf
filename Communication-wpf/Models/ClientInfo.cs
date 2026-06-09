using System;

namespace Communication.Wpf.Models
{
    public class ClientInfo
    {
        public string ConnectionId { get; set; }
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public DateTime ConnectTime { get; set; }
        public DateTime? LoginTime { get; set; }

        public string DisplayName => $"{IPAddress}:{Port} [{Username}]";
    }
}
