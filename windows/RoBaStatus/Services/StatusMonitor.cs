using RoBaStatus.Models;

namespace RoBaStatus.Services;

public sealed class StatusMonitor : IAsyncDisposable
{
    private readonly DeviceStatus _status;
    private readonly UsbStatusMonitor _usb;
    private readonly BleStatusMonitor _ble;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private Task? _monitorTask;

    public StatusMonitor(DeviceStatus status)
    {
        _status = status;
        _usb = new UsbStatusMonitor(status);
        _ble = new BleStatusMonitor(status);
    }

    public void Start()
    {
        _monitorTask ??= Task.Run(() => MonitorLoopAsync(_shutdown.Token));
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            await RefreshCoreAsync(cancellationToken);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await _usb.TryRefreshAsync(cancellationToken))
            {
                _ble.Disconnect();
                _status.Transport = ConnectionTransport.Usb;
                _status.IsConnected = true;
                _status.Message = "USBで監視中";
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _usb.Disconnect();
        }

        try
        {
            if (await _ble.RefreshNowAsync(cancellationToken))
            {
                _status.Transport = ConnectionTransport.Bluetooth;
                _status.IsConnected = true;
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _ble.Disconnect();
            _status.Message = $"再接続待ち: {ex.Message}";
        }

        _status.Transport = ConnectionTransport.None;
        _status.IsConnected = false;
        if (!_status.Message.StartsWith("再接続待ち", StringComparison.Ordinal))
        {
            _status.Message = "USB/BluetoothのroBaが見つかりません";
        }
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshNowAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var delay = _status.Transport == ConnectionTransport.Usb
                    ? TimeSpan.FromSeconds(2)
                    : TimeSpan.FromSeconds(5);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _usb.Dispose();
        await _ble.DisposeAsync();
        _refreshGate.Dispose();
        _shutdown.Dispose();
    }
}
