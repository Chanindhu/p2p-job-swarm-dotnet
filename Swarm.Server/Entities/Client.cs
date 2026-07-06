namespace Swarm.Server.Entities
{
    public class Client
    {
        public int Id { get; set; }
        public string IpOrHost { get; set; } = "";
        public int Port { get; set; }
        public string? DisplayName { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public int TotalJobsDone { get; set; }
    }
}
