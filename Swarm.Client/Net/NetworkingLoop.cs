using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Runtime.Remoting;
using Swarm.Client.Remoting;
using Swarm.Client.Core;

namespace Swarm.Client.Net
{
    public class NetworkingLoop
    {
        private readonly ServerApi _api;
        private readonly string _selfHost;
        private readonly int _selfPort;
        private readonly string _displayName;
        private int _myClientId;
        private readonly Action<string> _log;

        // GUI callbacks
        private readonly Action<bool> _onWorkingChanged;   // true while executing a job
        private readonly Action<int> _onPeersCountChanged;
        private readonly Action _onJobCompleted;           // when THIS client finishes a job

        private const int PeerTtlSeconds = 120; // skip peers older than this

        // Heartbeat tracking
        private DateTime _lastHeartbeatUtc = DateTime.MinValue;
        private static readonly TimeSpan HeartbeatPeriod = TimeSpan.FromSeconds(30);

        public NetworkingLoop(
            ServerApi api,
            string selfHost,
            int selfPort,
            string displayName,
            Action<string> log,
            Action<bool> onWorkingChanged = null,
            Action<int> onPeersCountChanged = null,
            Action onJobCompleted = null)
        {
            _api = api;
            _selfHost = selfHost;
            _selfPort = selfPort;
            _displayName = displayName;
            _log = log;

            _onWorkingChanged = onWorkingChanged;
            _onPeersCountChanged = onPeersCountChanged;
            _onJobCompleted = onJobCompleted;
        }

        public async Task InitAsync()
        {
            await _api.RegisterAsync(_selfHost, _selfPort, _displayName);

            var list = await _api.ListClientsAsync();
            var me = list.FirstOrDefault(c => c.IpOrHost == _selfHost && c.Port == _selfPort);
            _myClientId = me != null ? me.Id : 0;
            _log("Registered. My ClientId = " + _myClientId);

            // Immediate heartbeat so LastSeen is fresh
            try
            {
                await _api.HeartbeatAsync(_selfHost, _selfPort);
                _lastHeartbeatUtc = DateTime.UtcNow;
            }
            catch
            {
                // best-effort
            }
        }

        public async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Periodic heartbeat before peer work
                    var nowUtc = DateTime.UtcNow;
                    if ((nowUtc - _lastHeartbeatUtc) >= HeartbeatPeriod)
                    {
                        try
                        {
                            var okHb = await _api.HeartbeatAsync(_selfHost, _selfPort);
                            if (okHb)
                            {
                                _lastHeartbeatUtc = nowUtc;
                                _log($"Heartbeat OK ({_selfHost}:{_selfPort})");
                            }
                            else
                            {
                                _log($"Heartbeat FAILED (no matching client for {_selfHost}:{_selfPort})");
                            }
                        }
                        catch (Exception ex)
                        {
                            _log("Heartbeat error: " + ex.Message);
                        }
                    }

                    var peers = await _api.ListClientsAsync();
                    _log("Peers: " + peers.Count);

                    // Update peers count (excluding self) for the GUI
                    var peersCount = peers.Count(p => !(p.IpOrHost == _selfHost && p.Port == _selfPort));
                    _onPeersCountChanged?.Invoke(peersCount);

                    foreach (var p in peers
                        .Where(p => !(p.IpOrHost == _selfHost && p.Port == _selfPort))
                        .Where(p => (nowUtc - p.LastSeenUtc).TotalSeconds <= PeerTtlSeconds))
                    {
                        try
                        {
                            var url = "tcp://" + p.IpOrHost + ":" + p.Port + "/JobBoard";
                            _log("Contact " + p.IpOrHost + ":" + p.Port);

                            var proxy = (IJobBoard)Activator.GetObject(typeof(IJobBoard), url);
                            if (proxy == null) { _log("No proxy"); continue; }

                            bool hasJob;
                            try
                            {
                                hasJob = proxy.HasJob();
                            }
                            catch (SocketException se)
                            {
                                _log("Peer offline (" + p.IpOrHost + ":" + p.Port + "): " + se.SocketErrorCode);
                                continue;
                            }
                            catch (RemotingException re)
                            {
                                _log("Remoting error (" + p.IpOrHost + ":" + p.Port + "): " + re.Message);
                                continue;
                            }

                            if (!hasJob) { _log("No job at peer"); continue; }

                            JobDto job;
                            try
                            {
                                job = proxy.PullJob();
                            }
                            catch (Exception exPull)
                            {
                                _log("PullJob failed: " + exPull.Message);
                                continue;
                            }

                            if (job == null) { _log("Peer reported job but returned null"); continue; }

                            _log("Pulled job sha " + SafeSha(job.Sha256Hex));

                            if (!Crypto.VerifySha256OfBase64(job.PythonB64, job.Sha256Hex))
                            {
                                _log("Hash mismatch. Skipping job from " + p.IpOrHost + ":" + p.Port);
                                TryReturnToOwner(job, success: false, resultB64: "", error: "SHA-256 verification failed");
                                await TryPostCompletion(job, resultB64: "");
                                continue;
                            }

                            // Mark working ON while executing the job
                            _onWorkingChanged?.Invoke(true);
                            try
                            {
                                _log("Hash OK, executing…");

                                var python = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(job.PythonB64));

                                // ✅ Deconstruct tuple to satisfy analyzer suggestion
                                var (ok, resultB64, error) = PythonRunner.RunPy2(python);

                                var runError = ok ? "" : (error ?? "");

                                // Return result to owner
                                TryReturnToOwner(job, ok, resultB64 ?? "", runError);

                                // Record completion in Web Service (we don’t store success/error there today)
                                await TryPostCompletion(job, resultB64 ?? "");

                                // Notify GUI that THIS client finished a job
                                _onJobCompleted?.Invoke();

                                _log("Completed job from " + p.IpOrHost + ":" + p.Port + " (ok=" + ok + ")");
                            }
                            finally
                            {
                                _onWorkingChanged?.Invoke(false);
                            }
                        }
                        catch (Exception exPeer)
                        {
                            _log("Peer error: " + exPeer.Message);
                            _onWorkingChanged?.Invoke(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log("Loop error: " + ex.Message);
                    _onWorkingChanged?.Invoke(false);
                }

                try { await Task.Delay(1500, ct); } catch { /* shutting down */ }
            }

            _onWorkingChanged?.Invoke(false);
        }

        private void TryReturnToOwner(JobDto job, bool success, string resultB64, string error)
        {
            try
            {
                var ownerUrl = "tcp://" + job.OwnerHost + ":" + job.OwnerPort + "/JobBoard";
                var owner = (IJobBoard)Activator.GetObject(typeof(IJobBoard), ownerUrl);
                if (owner != null)
                {
                    owner.SubmitResult(new ResultDto
                    {
                        Sha256Hex = job.Sha256Hex,
                        ResultB64 = resultB64,
                        Success = success,
                        Error = error ?? ""
                    });
                    _log("Returned result to " + job.OwnerHost + ":" + job.OwnerPort);
                }
            }
            catch (Exception exBack)
            {
                _log("SubmitResult failed: " + exBack.Message);
            }
        }

        // Kept minimal: server currently stores basic completion data only
        private async Task TryPostCompletion(JobDto job, string resultB64)
        {
            if (_myClientId == 0) return;

            try
            {
                await _api.CompleteAsync(_myClientId, job.PythonB64, job.Sha256Hex, resultB64, ownerClientId: null);
                _log("Posted completion for clientId " + _myClientId);
            }
            catch (Exception exPost)
            {
                _log("POST /jobs/complete failed: " + exPost.Message);
            }
        }

        private static string SafeSha(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Substring(0, Math.Min(8, s.Length));
        }
    }
}
