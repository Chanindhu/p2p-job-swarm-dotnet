namespace Swarm.Server.Dtos
{
    // Server sends IsOnline (computed on server)
    public record ClientDto(
        int Id,
        string IpOrHost,
        int Port,
        string? DisplayName,
        DateTime LastSeenUtc,
        int TotalJobsDone,
        bool IsOnline
    );

    public record RegisterDto(string IpOrHost, int Port, string? DisplayName);
    public record HeartbeatDto(string IpOrHost, int Port);
    public record OfflineDto(string IpOrHost, int Port); 
}
