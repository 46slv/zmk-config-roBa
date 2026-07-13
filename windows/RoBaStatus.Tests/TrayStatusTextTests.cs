using RoBaStatus.Models;
using RoBaStatus.Services;

namespace RoBaStatus.Tests;

public sealed class TrayStatusTextTests
{
    [Fact]
    public void BuildsThreeDistinctTooltips()
    {
        var status = new DeviceStatus
        {
            IsConnected = true,
            Transport = ConnectionTransport.Usb,
            HighestLayer = 11,
            RightBattery = new BatteryReading(82),
            LeftBattery = new BatteryReading(76)
        };

        var layer = TrayStatusText.BuildLayer(status);
        var left = TrayStatusText.BuildLeftBattery(status);
        var right = TrayStatusText.BuildRightBattery(status);

        Assert.Contains("USB", layer);
        Assert.Contains("SCROLL", layer);
        Assert.Contains("Left battery: 76%", left);
        Assert.Contains("Right battery: 82%", right);
        Assert.All(new[] { layer, left, right }, text => Assert.True(text.Length <= TrayStatusText.MaximumLength));
    }

    [Fact]
    public void DisconnectedTooltipStaysWithinNotifyIconLimit()
    {
        var status = new DeviceStatus();
        var text = TrayStatusText.BuildLeftBattery(status);

        Assert.NotEmpty(text);
        Assert.True(text.Length <= TrayStatusText.MaximumLength);
    }

    [Fact]
    public void RendersIndependentLayerAndBatteryIcons()
    {
        var status = new DeviceStatus
        {
            IsConnected = true,
            HighestLayer = 11,
            LeftBattery = new BatteryReading(76, true),
            RightBattery = new BatteryReading(82)
        };

        using var layer = TaskbarIconRenderer.RenderLayerSystemIcon(status);
        using var left = TaskbarIconRenderer.RenderBatterySystemIcon(status.LeftBattery);
        using var right = TaskbarIconRenderer.RenderBatterySystemIcon(status.RightBattery);

        Assert.NotEqual(IntPtr.Zero, layer.Handle);
        Assert.NotEqual(IntPtr.Zero, left.Handle);
        Assert.NotEqual(IntPtr.Zero, right.Handle);
    }

    [Fact]
    public void UsesTwoCharacterLayerAndNumericBatteryLabels()
    {
        var status = new DeviceStatus { IsConnected = true, HighestLayer = 11 };

        Assert.Equal("SC", TrayStatusText.LayerIconLabel(status));
        Assert.Equal("82", TrayStatusText.BatteryIconLabel(new BatteryReading(82)));
        Assert.Equal("100", TrayStatusText.BatteryIconLabel(new BatteryReading(100)));
        Assert.Equal("--", TrayStatusText.BatteryIconLabel(BatteryReading.Unknown));
    }
}
