namespace RoBaStatus.Models;

public enum DataFreshness
{
    Unknown,
    Fresh,
    Stale
}

public sealed record BatteryReading(
    int? Percent,
    bool IsCharging = false,
    DataFreshness Freshness = DataFreshness.Unknown,
    DateTimeOffset? SampledAt = null)
{
    public static BatteryReading Unknown { get; } = new((int?)null);

    public string Display => Percent is int value ? $"{value}%" : "—";
}
