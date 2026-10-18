namespace DareuAe6.Core;

public readonly record struct BatteryReading(byte Level, byte Status, byte MaximumLevel, byte CriticalLevel);

public readonly record struct BatteryQuery(byte TargetId)
{
    public byte[] CreateReport()
    {
        var report = new byte[ProtocolLayout.FeatureReportLength];
        report[0] = 0;
        report[1] = TargetId;
        report[2] = 4;
        report[3] = ProtocolLayout.PowerClass;
        report[4] = ProtocolLayout.BatteryStatusCommand | ProtocolLayout.GetFlag;
        report[5] = 0;
        report[6] = 0;
        return report;
    }
}

public static class BatteryProtocol
{
    public static BatteryQuery CreateQuery(byte targetId = ProtocolLayout.DefaultTargetId) => new(targetId);

    public static bool TryParse(ReadOnlySpan<byte> report, byte targetId, out BatteryReading reading)
    {
        reading = default;
        if (report.Length < ProtocolLayout.BatteryLevelIndex + 1)
        {
            return false;
        }

        if (report[1] != targetId ||
            report[3] != ProtocolLayout.PowerClass ||
            report[4] != ProtocolLayout.BatteryStatusCommand)
        {
            return false;
        }

        var level = report[ProtocolLayout.BatteryLevelIndex];
        if (level > 100)
        {
            return false;
        }

        reading = new BatteryReading(
            level,
            report[ProtocolLayout.PayloadStartIndex],
            report[ProtocolLayout.PayloadStartIndex + 2],
            report[ProtocolLayout.PayloadStartIndex + 3]);
        return true;
    }
}
