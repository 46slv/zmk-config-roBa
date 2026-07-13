using RoBaStatus.Models;
using RoBaStatus.Services;

namespace RoBaStatus.Tests;

public sealed class TrayStatusTextTests
{
    [Fact]
    public void IncludesTransportLayerAndBothBatteries()
    {
        var status = new DeviceStatus
        {
            IsConnected = true,
            Transport = ConnectionTransport.Usb,
            HighestLayer = 11,
            RightBattery = new BatteryReading(82),
            LeftBattery = new BatteryReading(76)
        };

        var text = TrayStatusText.Build(status);

        Assert.Contains("USB", text);
        Assert.Contains("SCROLL", text);
        Assert.Contains("R 82%", text);
        Assert.Contains("L 76%", text);
        Assert.True(text.Length <= TrayStatusText.MaximumLength);
    }

    [Fact]
    public void DisconnectedTooltipStaysWithinNotifyIconLimit()
    {
        var text = TrayStatusText.Build(new DeviceStatus());

        Assert.NotEmpty(text);
        Assert.True(text.Length <= TrayStatusText.MaximumLength);
    }
}
