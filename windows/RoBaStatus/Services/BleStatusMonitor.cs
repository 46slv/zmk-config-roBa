using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using RoBaStatus.Models;
using RoBaStatus.Protocol;

namespace RoBaStatus.Services;

public sealed class BleStatusMonitor : IAsyncDisposable
{
    public static readonly Guid StatusServiceUuid = Guid.Parse("5a0e1000-7c7f-4b52-a8a8-3f5c726f4261");
    public static readonly Guid StatusCharacteristicUuid = Guid.Parse("5a0e1001-7c7f-4b52-a8a8-3f5c726f4261");

    private readonly DeviceStatus _status;
    private BluetoothLEDevice? _device;
    private readonly List<GattCharacteristic> _subscribed = [];

    public BleStatusMonitor(DeviceStatus status) => _status = status;

    public async Task<bool> RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        var device = await FindRoBaAsync(cancellationToken);
        if (device is null)
        {
            UpdateDisconnected("Bluetooth接続中のroBaが見つかりません");
            return false;
        }

        ReplaceDevice(device);
        await ReadBatteryServicesAsync(device, cancellationToken);
        var hasLayerService = await ReadLayerServiceAsync(device, cancellationToken);
        _status.IsConnected = true;
        _status.LastUpdated = DateTimeOffset.Now;
        _status.Message = hasLayerService
            ? "監視中"
            : "バッテリー監視中・レイヤー対応Firmware待ち";
        return true;
    }

    private static async Task<BluetoothLEDevice?> FindRoBaAsync(CancellationToken cancellationToken)
    {
        var selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
        var devices = await DeviceInformation.FindAllAsync(selector).AsTask(cancellationToken);
        var candidate = devices.FirstOrDefault(device =>
            device.Name.Contains("roBa", StringComparison.OrdinalIgnoreCase));
        return candidate is null
            ? null
            : await BluetoothLEDevice.FromIdAsync(candidate.Id).AsTask(cancellationToken);
    }

    private async Task ReadBatteryServicesAsync(BluetoothLEDevice device, CancellationToken cancellationToken)
    {
        var result = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (result.Status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException($"バッテリーサービス取得失敗 ({result.Status})");
        }

        BatteryReading? central = null;
        BatteryReading? peripheral = null;
        foreach (var service in result.Services)
        {
            var characteristics = await service.GetCharacteristicsForUuidAsync(
                GattCharacteristicUuids.BatteryLevel,
                BluetoothCacheMode.Uncached).AsTask(cancellationToken);
            if (characteristics.Status != GattCommunicationStatus.Success)
            {
                continue;
            }

            foreach (var characteristic in characteristics.Characteristics)
            {
                var label = await ReadUserDescriptionAsync(characteristic, cancellationToken);
                var value = await ReadByteAsync(characteristic, cancellationToken);
                var reading = new BatteryReading(value, false, DataFreshness.Fresh, DateTimeOffset.Now);
                if (label.Contains("peripheral", StringComparison.OrdinalIgnoreCase))
                {
                    peripheral = reading;
                }
                else
                {
                    central ??= reading;
                }

                await SubscribeAsync(characteristic);
            }
        }

        // roBa_R is central and roBa_L is peripheral.
        _status.RightBattery = central ?? BatteryReading.Unknown;
        _status.LeftBattery = peripheral ?? BatteryReading.Unknown;
    }

    private async Task<bool> ReadLayerServiceAsync(BluetoothLEDevice device, CancellationToken cancellationToken)
    {
        var result = await device.GetGattServicesForUuidAsync(StatusServiceUuid, BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (result.Status != GattCommunicationStatus.Success || result.Services.Count == 0)
        {
            return false;
        }

        var chars = await result.Services[0].GetCharacteristicsForUuidAsync(
            StatusCharacteristicUuid,
            BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (chars.Status != GattCommunicationStatus.Success || chars.Characteristics.Count == 0)
        {
            return false;
        }

        var characteristic = chars.Characteristics[0];
        var read = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (read.Status == GattCommunicationStatus.Success)
        {
            ApplyPacket(ToBytes(read.Value));
        }

        await SubscribeAsync(characteristic);
        return true;
    }

    private async Task SubscribeAsync(GattCharacteristic characteristic)
    {
        if (_subscribed.Any(item => item.AttributeHandle == characteristic.AttributeHandle))
        {
            return;
        }

        characteristic.ValueChanged += OnValueChanged;
        var mode = characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
            : characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate)
                ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
                : GattClientCharacteristicConfigurationDescriptorValue.None;
        if (mode != GattClientCharacteristicConfigurationDescriptorValue.None)
        {
            var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(mode);
            if (status == GattCommunicationStatus.Success)
            {
                _subscribed.Add(characteristic);
            }
        }
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var bytes = ToBytes(args.CharacteristicValue);
        if (sender.Uuid == StatusCharacteristicUuid)
        {
            ApplyPacket(bytes);
            return;
        }

        if (sender.Uuid == GattCharacteristicUuids.BatteryLevel && bytes.Length > 0)
        {
            _ = Task.Run(async () =>
            {
                var label = await ReadUserDescriptionAsync(sender, CancellationToken.None);
                var reading = new BatteryReading(bytes[0], false, DataFreshness.Fresh, DateTimeOffset.Now);
                if (label.Contains("peripheral", StringComparison.OrdinalIgnoreCase))
                {
                    _status.LeftBattery = reading;
                }
                else
                {
                    _status.RightBattery = reading;
                }
                _status.LastUpdated = DateTimeOffset.Now;
            });
        }
    }

    private void ApplyPacket(byte[] bytes)
    {
        if (!StatusPacketParser.TryParse(bytes, out var packet) || packet is null)
        {
            _status.Message = "未対応のレイヤー状態データ";
            return;
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
    }

    private static async Task<int?> ReadByteAsync(GattCharacteristic characteristic, CancellationToken cancellationToken)
    {
        var result = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        var bytes = result.Status == GattCommunicationStatus.Success ? ToBytes(result.Value) : [];
        return bytes.Length > 0 ? bytes[0] : null;
    }

    private static async Task<string> ReadUserDescriptionAsync(
        GattCharacteristic characteristic,
        CancellationToken cancellationToken)
    {
        var result = await characteristic.GetDescriptorsForUuidAsync(
            GattDescriptorUuids.CharacteristicUserDescription,
            BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (result.Status != GattCommunicationStatus.Success || result.Descriptors.Count == 0)
        {
            return "Central";
        }

        var read = await result.Descriptors[0].ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        return read.Status == GattCommunicationStatus.Success
            ? System.Text.Encoding.UTF8.GetString(ToBytes(read.Value)).TrimEnd('\0')
            : "Central";
    }

    private static byte[] ToBytes(IBuffer buffer)
    {
        using var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[buffer.Length];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private void ReplaceDevice(BluetoothLEDevice next)
    {
        ClearSubscriptions();
        _device?.Dispose();
        _device = next;
    }

    public void Disconnect()
    {
        ClearSubscriptions();
        _device?.Dispose();
        _device = null;
    }

    private void ClearSubscriptions()
    {
        foreach (var characteristic in _subscribed)
        {
            characteristic.ValueChanged -= OnValueChanged;
        }
        _subscribed.Clear();
    }

    private void UpdateDisconnected(string message)
    {
        _status.IsConnected = false;
        _status.Message = message;
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}
