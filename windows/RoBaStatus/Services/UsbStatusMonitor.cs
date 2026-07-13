using System.IO;
using RoBaStatus.Models;
using RoBaStatus.Protocol;

namespace RoBaStatus.Services;

public sealed class UsbStatusMonitor : IDisposable
{
    public const ushort UsagePage = 0xFF60;
    public const ushort UsageId = 0x0001;
    public const ushort VendorId = 0x1D50;
    public const ushort ProductId = 0x615E;
    public const byte ReportId = StatusPacketParser.UsbReportId;

    private readonly DeviceStatus _status;
    private Win32HidDevice? _device;

    public UsbStatusMonitor(DeviceStatus status) => _status = status;

    public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_device is not null)
        {
            if (_device.IsConnected)
            {
                return Task.FromResult(true);
            }

            Disconnect();
        }

        _device = Win32HidDevice.TryOpen(VendorId, ProductId, UsagePage, UsageId);
        if (_device is null)
        {
            return Task.FromResult(false);
        }

        if (!_device.TryGetInputReport(ReportId, out var initialReport) || !Apply(initialReport))
        {
            Disconnect();
            throw new InvalidDataException("USB状態パケットを読み取れません");
        }

        _device.ReportReceived += OnReportReceived;
        _device.StartReading();
        return Task.FromResult(true);
    }

    private void OnReportReceived(byte[] bytes) => Apply(bytes);

    private bool Apply(byte[] bytes)
    {
        if (!StatusPacketParser.TryParseUsbPayload(bytes, out var packet) || packet is null)
        {
            return false;
        }

        _status.HighestLayer = packet.HighestLayer;
        _status.ActiveLayerMask = packet.ActiveLayerMask;
        if (packet.RightBattery is { } right)
        {
            _status.RightBattery = new BatteryReading(right, false, DataFreshness.Fresh, DateTimeOffset.Now);
        }
        if (packet.LeftBattery is { } left)
        {
            _status.LeftBattery = new BatteryReading(left, false, DataFreshness.Fresh, DateTimeOffset.Now);
        }
        _status.LastUpdated = DateTimeOffset.Now;
        return true;
    }

    public void Disconnect()
    {
        if (_device is null)
        {
            return;
        }

        _device.ReportReceived -= OnReportReceived;
        _device.Dispose();
        _device = null;
    }

    public void Dispose() => Disconnect();
}
