using System.Net.NetworkInformation;

namespace SnnuAutoLogin.Core;

/// <summary>
/// Windows 网络事件监听：网线插拔、WiFi 连断、IP 变化都会触发
/// NetworkAvailabilityChanged / NetworkAddressChanged 两个事件。
/// 网卡切换瞬间事件会连发，统一做 2 秒去抖后回调一次。
/// </summary>
public sealed class NetworkMonitor : IDisposable
{
    private readonly object _lock = new();
    private readonly Action<string> _onNetworkChanged;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    public NetworkMonitor(Action<string> onNetworkChanged)
    {
        _onNetworkChanged = onNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnAddressChanged;
    }

    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        => Debounce(e.IsAvailable ? "网络已恢复" : "网络已断开");

    private void OnAddressChanged(object? sender, EventArgs e)
        => Debounce("网络地址变化");

    /// <summary>去抖：2 秒内多次事件合并为一次回调。</summary>
    private void Debounce(string reason)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            var capturedReason = reason;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                    if (!token.IsCancellationRequested)
                    {
                        _onNetworkChanged(capturedReason);
                    }
                }
                catch (TaskCanceledException)
                {
                    // 被后续事件取代，正常
                }
            });
        }
    }

    /// <summary>当前是否存在至少一个已联网（up 且非虚拟回环）的网卡。</summary>
    public static bool HasAnyUpInterface()
    {
        return NetworkInterface.GetAllNetworkInterfaces().Any(nic =>
            nic.OperationalStatus == OperationalStatus.Up &&
            nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
        }
        NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnAddressChanged;
    }
}
