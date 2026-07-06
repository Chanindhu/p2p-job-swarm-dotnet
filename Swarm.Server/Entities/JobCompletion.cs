namespace Swarm.Server.Entities
{
    public class JobCompletion
    {
        public int Id { get; set; }

        // who executed it
        public int ClientId { get; set; }

        public string PythonB64 { get; set; } = "";
        public string Sha256Hex { get; set; } = "";
        public string? ResultB64 { get; set; }

        // optional attribution to the submitter
        public int? OwnerClientId { get; set; }

        public DateTime FinishedUtc { get; set; }
    }
}
