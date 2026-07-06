using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Swarm.Client.Remoting;
using Swarm.Client.Net;
using Swarm.Client.Core;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace Swarm.Client
{
    public partial class MainWindow : Window
    {
        private RemotingHost _host;
        private JobBoardImpl _jobBoard;        // Reused across Start/Stop
        private NetworkingLoop _loop;
        private ServerApi _api;
        private CancellationTokenSource _cts;

        private string _selfHost = "localhost";
        private int _selfPort = 0;
        private int _doneCount = 0;

        private volatile bool _isWorkingSnapshot = false;
        private bool _autoScroll = true;

        public MainWindow()
        {
            InitializeComponent();

            TxtServer.Text = "http://localhost:5265";
            TxtHost.Text = GetPreferredLocalIPv4();
            TxtPython.Text = "x = 6\ny = 7\nresult = x * y";
            SetWorking(false);
            LblPeers.Text = "0";
            LblSelf.Text = "-";
        }

        // ---------- Logging ----------

        private void Log(string msg)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var line = DateTime.Now.ToString("[HH:mm:ss] ") + msg;
                LogList.Items.Add(line);
                if (LogList.Items.Count > 2000) LogList.Items.RemoveAt(0);

                if (_autoScroll && LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);

                this.Title = "Job Swarm Client - " + msg;
            }));
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e) => LogList.Items.Clear();

        private void ChkAutoScroll_Checked(object sender, RoutedEventArgs e)
        {
            _autoScroll = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var lb = LogList;
                if (lb?.Items?.Count > 0)
                    lb.ScrollIntoView(lb.Items[lb.Items.Count - 1]);
            }));
        }

        private void ChkAutoScroll_Unchecked(object sender, RoutedEventArgs e) => _autoScroll = false;

        // ---------- Buttons ----------

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var baseUrl = TxtServer.Text.Trim();
                var display = TxtName.Text.Trim();
                _selfHost = string.IsNullOrWhiteSpace(TxtHost.Text) ? "localhost" : TxtHost.Text.Trim();

                if (!int.TryParse(TxtPort.Text.Trim(), out _selfPort) || _selfPort <= 0 || _selfPort >= 65536)
                    throw new InvalidOperationException("Please enter a valid listen port (1-65535).");

                Log("Starting... server=" + baseUrl + " name=" + display + " endpoint=" + _selfHost + ":" + _selfPort);

                if (_jobBoard == null)
                {
                    _jobBoard = new JobBoardImpl(r =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var output = DecodeB64(r.ResultB64);
                            var err = (r.Error ?? string.Empty).Trim();

                            bool isOk = false;
                            try { isOk = r.Success; } catch { /* DTO mismatch safe */ }
                            if (!isOk && string.IsNullOrWhiteSpace(err)) isOk = true;

                            if (isOk)
                            {
                                TxtLast.Text = string.IsNullOrWhiteSpace(output) ? "(no output)" : output;
                                Log("Result received (sha " + SafeSha(r.Sha256Hex) + ")");
                            }
                            else
                            {
                                TxtLast.Text = string.IsNullOrWhiteSpace(output) ? (string.IsNullOrWhiteSpace(err) ? "(no output)" : err)
                                                                               : (string.IsNullOrWhiteSpace(err) ? output : (output + "\n\n" + err));
                                Log("Result error: " + (string.IsNullOrWhiteSpace(err) ? "(no error message)" : err));
                            }
                        }));
                    });
                }

                if (_host == null) _host = new RemotingHost();
                _host.Start(_selfHost, _selfPort, _jobBoard);

                _api = new ServerApi(baseUrl);

                _loop = new NetworkingLoop(
                    api: _api,
                    selfHost: _selfHost,
                    selfPort: _selfPort,
                    displayName: display,
                    log: Log,
                    onWorkingChanged: SetWorking,
                    onPeersCountChanged: SetPeersCount,
                    onJobCompleted: IncrementCompletedCount
                );

                await _loop.InitAsync();

                _cts = new CancellationTokenSource();
                _ = Task.Run(() => _loop.RunAsync(_cts.Token)); // fire-and-forget, silence analyzer

                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = true;
                BtnSubmit.IsEnabled = true;
                TxtPort.IsEnabled = false;
                TxtName.IsEnabled = false;
                TxtServer.IsEnabled = false;
                TxtHost.IsEnabled = false;
                LblSelf.Text = _selfHost + ":" + _selfPort;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Start failed");
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("Stopping...");

                try { _cts?.Cancel(); } catch { }

                try
                {
                    if (_api != null)
                    {
                        var ok = await _api.OfflineAsync(_selfHost, _selfPort);
                        Log(ok ? "Offline posted" : "Offline post failed");
                    }
                }
                catch (Exception ex) { Log("Offline error: " + ex.Message); }

                try { _host?.Stop(); } catch { }

                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                BtnSubmit.IsEnabled = false;
                TxtPort.IsEnabled = true;
                TxtName.IsEnabled = true;
                TxtServer.IsEnabled = true;
                TxtHost.IsEnabled = true;

                SetWorking(false);
                LblSelf.Text = "-";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Stop failed");
            }
        }

        private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Python files (*.py)|*.py|All files (*.*)|*.*",
                Title = "Select a Python script"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    TxtPython.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                    Log("Loaded file " + System.IO.Path.GetFileName(dlg.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Load failed");
                }
            }
        }

        private void BtnClearCode_Click(object sender, RoutedEventArgs e)
        {
            TxtPython.Clear();
            TxtPython.Focus();
        }

        private async void BtnPeersRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_api == null) return;
            try
            {
                var list = await _api.ListClientsAsync();
                var peers = list.Count(p => !(p.IpOrHost == _selfHost && p.Port == _selfPort));
                SetPeersCount(peers);
                Log("Peers refresh -> " + peers);
            }
            catch (Exception ex)
            {
                Log("Peers refresh failed: " + ex.Message);
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            var py = TxtPython.Text;
            if (string.IsNullOrWhiteSpace(py))
            {
                MessageBox.Show("Please enter or load a Python script first.", "No script");
                return;
            }

            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(py));
            var sha = Crypto.Sha256HexOfBase64(b64);

            _jobBoard.Enqueue(new Swarm.Client.Remoting.JobDto
            {
                PythonB64 = b64,
                Sha256Hex = sha,
                OwnerHost = _selfHost,
                OwnerPort = _selfPort
            });

            Log("Queued job sha " + SafeSha(sha) + " for owner " + _selfHost + ":" + _selfPort);
        }

        private void BtnCheckStatus_Click(object sender, RoutedEventArgs e)
        {
            var peersText = LblPeers?.Text ?? "0";
            var msg =
                "Working: " + (_isWorkingSnapshot ? "Yes" : "No") + "\n" +
                "Jobs completed: " + _doneCount + "\n" +
                "Peers seen: " + peersText;
            MessageBox.Show(msg, "Current Status");
        }

        protected override void OnClosed(EventArgs e)
        {
            try { _cts?.Cancel(); } catch { }
            try { _host?.Stop(); } catch { }

            // fire-and-forget (and silence analyzer)
            try { _ = _api?.OfflineAsync(_selfHost, _selfPort); } catch { }

            base.OnClosed(e);
        }

        // ---------- UI update helpers ----------

        private void SetWorking(bool isWorking)
        {
            _isWorkingSnapshot = isWorking;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LblWorking.Text = isWorking ? "Working..." : "Idle";
                BarWorking.Visibility = isWorking ? Visibility.Visible : Visibility.Collapsed;
            }));
        }

        private void SetPeersCount(int n) => Dispatcher.BeginInvoke(new Action(() => LblPeers.Text = n.ToString()));

        private void IncrementCompletedCount()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _doneCount++;
                LblCount.Text = _doneCount.ToString();
            }));
        }

        // ---------- Utility ----------

        private static string DecodeB64(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return "";
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(b64)); }
            catch { return ""; }
        }

        private static string SafeSha(string s) => string.IsNullOrEmpty(s) ? "" : s.Substring(0, Math.Min(8, s.Length));

        private static string GetPreferredLocalIPv4()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                        n.Description.IndexOf("VirtualBox", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.Description.IndexOf("Hyper-V", StringComparison.OrdinalIgnoreCase) < 0);

                foreach (var nic in nics)
                {
                    var ip = nic.GetIPProperties().UnicastAddresses
                        .Select(a => a.Address)
                        .FirstOrDefault(a =>
                            a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(a) &&
                            !a.ToString().StartsWith("169.254."));
                    if (ip != null) return ip.ToString();
                }

                var entry = Dns.GetHostEntry(Dns.GetHostName());
                var fallback = entry.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (fallback != null && !IPAddress.IsLoopback(fallback)) return fallback.ToString();
            }
            catch { }
            return "localhost";
        }
    }
}
