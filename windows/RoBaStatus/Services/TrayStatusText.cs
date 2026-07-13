using RoBaStatus.Models;

namespace RoBaStatus.Services;

public static class TrayStatusText
{
    public const int MaximumLength = 63;

    public static string Build(DeviceStatus status)
    {
        var text = $"roBa · {status.TransportLabel} · {status.LayerName} · " +
                   $"R {status.RightBattery.Display} · L {status.LeftBattery.Display}";
        return text.Length <= MaximumLength ? text : text[..MaximumLength];
    }
}
