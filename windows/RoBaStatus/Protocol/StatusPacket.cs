namespace RoBaStatus.Protocol;

public sealed record StatusPacket(
    byte Version,
    byte Flags,
    byte HighestLayer,
    uint ActiveLayerMask,
    byte? RightBattery,
    byte? LeftBattery,
    byte StatusFlags,
    ushort Sequence)
{
    public const int Size = 12;
    public const byte SupportedVersion = 1;
}

public static class StatusPacketParser
{
    public const byte UsbReportId = 1;

    public static bool TryParse(ReadOnlySpan<byte> bytes, out StatusPacket? packet)
    {
        packet = null;
        if (bytes.Length < StatusPacket.Size || bytes[0] != StatusPacket.SupportedVersion)
        {
            return false;
        }

        var mask = (uint)(bytes[3] | bytes[4] << 8 | bytes[5] << 16 | bytes[6] << 24);
        var sequence = (ushort)(bytes[10] | bytes[11] << 8);
        packet = new StatusPacket(
            bytes[0],
            bytes[1],
            bytes[2],
            mask,
            bytes[7] == byte.MaxValue ? null : bytes[7],
            bytes[8] == byte.MaxValue ? null : bytes[8],
            bytes[9],
            sequence);
        return packet.HighestLayer < 32 && packet.ActiveLayerMask != 0;
    }

    public static bool TryParseUsbPayload(ReadOnlySpan<byte> bytes, out StatusPacket? packet)
    {
        if (bytes.Length >= StatusPacket.Size + 1 && bytes[0] == UsbReportId)
        {
            bytes = bytes[1..];
        }

        return TryParse(bytes, out packet);
    }
}
