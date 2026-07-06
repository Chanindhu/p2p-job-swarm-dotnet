using System;
using System.Collections; // Hashtable
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;

namespace Swarm.Client.Remoting
{
    public class RemotingHost
    {
        private TcpChannel _channel;
        private ObjRef _objRef;

        public JobBoardImpl Instance { get; private set; }

        /// <summary>
        /// New overload: bind to a specific LAN IP + port so peers can reach you.
        /// Publishes the singleton at: tcp://{bindIp}:{listenPort}/JobBoard
        /// </summary>
        public void Start(string bindIp, int listenPort, JobBoardImpl impl)
        {
            if (impl == null) throw new ArgumentNullException(nameof(impl));

            // Clean up any previous registration
            Stop();
            Instance = impl;

            // Binary formatters + FULL type filter level for .NET Remoting
            var serverProv = new BinaryServerFormatterSinkProvider
            {
                TypeFilterLevel = TypeFilterLevel.Full
            };
            var clientProv = new BinaryClientFormatterSinkProvider();

            // Channel properties (explicit assignments to avoid initializer quirks)
            var props = new Hashtable();
            props["name"] = "JobSwarmTcp-" + listenPort; // unique per port
            props["port"] = listenPort;
            props["bindTo"] = string.IsNullOrWhiteSpace(bindIp) ? "0.0.0.0" : bindIp.Trim();

            // Register channel
            _channel = new TcpChannel(props, clientProv, serverProv);
            ChannelServices.RegisterChannel(_channel, false);

            // Publish the SAME instance at the SAME URI
            _objRef = RemotingServices.Marshal(Instance, "JobBoard");
        }

        /// <summary>
        /// Backward-compatible overload: defaults to binding on all interfaces.
        /// </summary>
        public void Start(int listenPort, JobBoardImpl impl)
        {
            Start("0.0.0.0", listenPort, impl);
        }

        /// <summary>
        /// Cleanly unpublish the object and unregister the channel. Safe to call multiple times.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_objRef != null && Instance != null)
                {
                    RemotingServices.Disconnect(Instance);
                }
            }
            catch { /* swallow */ }
            finally
            {
                _objRef = null;
            }

            try
            {
                if (_channel != null)
                {
                    ChannelServices.UnregisterChannel(_channel);
                }
            }
            catch { /* swallow */ }
            finally
            {
                _channel = null;
            }
        }
    }
}
