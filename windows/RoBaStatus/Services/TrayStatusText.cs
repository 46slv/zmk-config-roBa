using RoBaStatus.Models;

namespace RoBaStatus.Services;

public static class TrayStatusText
{
    public const int MaximumLength = 63;

    public static string BuildLayer(DeviceStatus status) => Limit(
        $"roBa · {status.TransportLabel} · Layer: {status.LayerName}");

    public static string BuildLeftBattery(DeviceStatus status) => BuildBattery("Left", status.LeftBattery);

    public static string BuildRightBattery(DeviceStatus status) => BuildBattery("Right", status.RightBattery);

    private static string BuildBattery(string side, BatteryReading battery)
    {
        var charging = battery.IsCharging ? " · charging" : string.Empty;
        return Limit($"roBa · {side} battery: {battery.Display}{charging}");
    }

    private static string Limit(string text)
    {
        return text.Length <= MaximumLength ? text : text[..MaximumLength];
    }
}
