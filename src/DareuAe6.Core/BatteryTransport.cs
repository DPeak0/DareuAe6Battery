namespace DareuAe6.Core;

public interface IBatteryTransport
{
    ValueTask<BatteryReading?> ReadBatteryAsync(CancellationToken cancellationToken);
}

public enum BatteryAvailability
{
    Unknown,
    Current,
    Unavailable,
    InvalidReport
}

public sealed record BatteryState(
    BatteryAvailability Availability,
    BatteryReading? Reading,
    DateTimeOffset? LastSuccessUtc,
    string? Detail)
{
    public static BatteryState Unknown() => new(BatteryAvailability.Unknown, null, null, null);
}
