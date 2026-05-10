using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace VPNDriveMapper.Services
{
    public class VpnDetector
    {
        public event EventHandler<bool> VpnStatusChanged;

        private readonly Models.VpnSettings _settings;
        private CancellationTokenSource _cts;
        private Task _monitorTask;
        private bool _lastStatus = false;
        private int _isChecking;
        private bool _networkEventsRegistered;

        public bool IsConnected { get; private set; }

        public VpnDetector(Models.VpnSettings settings)
        {
            _settings = settings;
            IsConnected = false;
        }

        public void UpdateSettings(Models.VpnSettings settings)
        {
            _settings.TargetIp = settings.TargetIp;
            _settings.CheckInterval = settings.CheckInterval;
        }

        public void Start()
        {
            if (_monitorTask != null && !_monitorTask.IsCompleted)
                return;

            _cts = new CancellationTokenSource();
            RegisterNetworkEvents();
            _monitorTask = Task.Run(() => MonitorVpn(_cts.Token));
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            UnregisterNetworkEvents();
        }

        private async Task MonitorVpn(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    CheckAndPublishStatus();

                    await Task.Delay(_settings.CheckInterval * 1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("VPN检测异常: {0}", ex.Message));
                    try
                    {
                        Task.Delay(1000).Wait(token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public bool CheckVpnConnection()
        {
            try
            {
                return CheckByIp();
            }
            catch
            {
                return false;
            }
        }

        private void CheckAndPublishStatus()
        {
            if (Interlocked.Exchange(ref _isChecking, 1) == 1)
            {
                return;
            }

            try
            {
                bool currentStatus = CheckVpnConnection();
                if (currentStatus != _lastStatus)
                {
                    _lastStatus = currentStatus;
                    IsConnected = currentStatus;
                    EventHandler<bool> handler = VpnStatusChanged;
                    if (handler != null)
                    {
                        handler(this, currentStatus);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isChecking, 0);
            }
        }

        private void RegisterNetworkEvents()
        {
            if (_networkEventsRegistered)
            {
                return;
            }

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            _networkEventsRegistered = true;
        }

        private void UnregisterNetworkEvents()
        {
            if (!_networkEventsRegistered)
            {
                return;
            }

            NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
            _networkEventsRegistered = false;
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            TriggerImmediateCheck();
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            TriggerImmediateCheck();
        }

        private void TriggerImmediateCheck()
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                return;
            }

            Task.Run(() => CheckAndPublishStatus());
        }

        private bool CheckByIp()
        {
            if (string.IsNullOrEmpty(_settings.TargetIp))
                return false;

            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(_settings.TargetIp, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
