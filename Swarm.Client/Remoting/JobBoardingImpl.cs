using System;
using System.Collections.Concurrent;

namespace Swarm.Client.Remoting
{
    public class JobBoardImpl : MarshalByRefObject, IJobBoard
    {
        private readonly ConcurrentQueue<JobDto> _queue = new ConcurrentQueue<JobDto>();
        private readonly Action<ResultDto> _onResult; // UI callback

        public JobBoardImpl(Action<ResultDto> onResult) => _onResult = onResult;

        public void Enqueue(JobDto job) => _queue.Enqueue(job);

        public bool HasJob() => !_queue.IsEmpty;

        public JobDto PullJob() => _queue.TryDequeue(out var j) ? j : null; // inline out var

        public bool SubmitResult(ResultDto r)
        {
            try
            {
                _onResult?.Invoke(r);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override object InitializeLifetimeService() => null; // keep singleton alive
    }
}
