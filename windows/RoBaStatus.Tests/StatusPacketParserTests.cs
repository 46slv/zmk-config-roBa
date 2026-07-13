using RoBaStatus.Protocol;

namespace RoBaStatus.Tests;

public sealed class StatusPacketParserTests
{
    [Fact]
    public void ParsesVersionOnePacket()
    {
        byte[] bytes = [1, 0, 11, 0x81, 0x08, 0, 0, 82, 76, 0, 0x34, 0x12];

        var success = StatusPacketParser.TryParse(bytes, out var packet);

        Assert.True(success);
        Assert.NotNull(packet);
        Assert.Equal((byte)11, packet.HighestLayer);
        Assert.Equal(0x00000881u, packet.ActiveLayerMask);
        Assert.Equal((byte)82, packet.RightBattery);
        Assert.Equal((byte)76, packet.LeftBattery);
        Assert.Equal((ushort)0x1234, packet.Sequence);
    }

    [Fact]
    public void MapsUnknownBatteryToNull()
    {
        byte[] bytes = [1, 0, 0, 1, 0, 0, 0, 255, 255, 0, 0, 0];

        Assert.True(StatusPacketParser.TryParse(bytes, out var packet));
        Assert.Null(packet!.RightBattery);
        Assert.Null(packet.LeftBattery);
    }

    [Fact]
    public void ParsesUsbReportWithLeadingReportId()
    {
        byte[] bytes = [1, 1, 0, 11, 0x01, 0x08, 0, 0, 82, 76, 0, 0x34, 0x12];

        var success = StatusPacketParser.TryParseUsbPayload(bytes, out var packet);

        Assert.True(success);
        Assert.NotNull(packet);
        Assert.Equal((byte)11, packet.HighestLayer);
        Assert.Equal(0x00000801u, packet.ActiveLayerMask);
        Assert.Equal((byte)82, packet.RightBattery);
        Assert.Equal((byte)76, packet.LeftBattery);
    }

    [Fact]
    public void ParsesUsbPayloadWhenWindowsOmitsReportId()
    {
        byte[] bytes = [1, 0, 7, 0x81, 0, 0, 0, 60, 55, 0, 1, 0];

        Assert.True(StatusPacketParser.TryParseUsbPayload(bytes, out var packet));
        Assert.Equal((byte)7, packet!.HighestLayer);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 2, 0, 0, 1, 0, 0, 0, 50, 50, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 32, 1, 0, 0, 0, 50, 50, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 0, 0, 0, 0, 0, 50, 50, 0, 0, 0 })]
    public void RejectsInvalidPackets(byte[] bytes)
    {
        Assert.False(StatusPacketParser.TryParse(bytes, out _));
    }
}
