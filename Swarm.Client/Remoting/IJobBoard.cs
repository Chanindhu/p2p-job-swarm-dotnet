using System;

namespace Swarm.Client.Remoting
{
    public interface IJobBoard
    {
        bool HasJob();
        JobDto PullJob();               // may return null if none
        bool SubmitResult(ResultDto r); // worker -> owner
    }

    [Serializable]
    public class JobDto : MarshalByRefObject
    {
        public string PythonB64 { get; set; } = "";
        public string Sha256Hex { get; set; } = "";
        public string OwnerHost { get; set; } = "localhost";
        public int OwnerPort { get; set; } = 0;

        public override object InitializeLifetimeService() { return null; }
    }

    [Serializable]
    public class ResultDto : MarshalByRefObject
    {
        public string Sha256Hex { get; set; } = "";
        public string ResultB64 { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }

        public override object InitializeLifetimeService() { return null; }
    }
}
