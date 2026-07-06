namespace Swarm.Server.Dtos
{
    public record CompleteDto(int ClientId, string PythonB64, string Sha256Hex, string? ResultB64, int? OwnerClientId);
}
