using DareuAe6.Core;
using Xunit;
namespace DareuAe6.Core.Tests;
public sealed class BatteryProtocolTests
{
    [Fact] public void Query_has_fixed_size_and_read_only_power_fields()
    {
        var report = BatteryProtocol.CreateQuery().CreateReport();
        Assert.Equal(ProtocolLayout.FeatureReportLength, report.Length);
        Assert.Equal(4, report[2]); Assert.Equal(ProtocolLayout.PowerClass, report[3]); Assert.Equal(ProtocolLayout.GetFlag, report[4]); Assert.All(report[7..], value => Assert.Equal(0, value));
    }
    [Theory] [InlineData(0)] [InlineData(45)] [InlineData(100)]
    public void Parse_accepts_valid_levels(byte level)
    {
        var report = new byte[64]; report[1] = ProtocolLayout.DefaultTargetId; report[3] = ProtocolLayout.PowerClass; report[4] = ProtocolLayout.BatteryStatusCommand; report[7] = 0; report[8] = level; report[9] = 100; report[10] = 5;
        Assert.True(BatteryProtocol.TryParse(report, ProtocolLayout.DefaultTargetId, out var reading)); Assert.Equal(level, reading.Level); Assert.Equal(100, reading.MaximumLevel); Assert.Equal(5, reading.CriticalLevel);
    }
    [Fact] public void Parse_rejects_short_and_invalid_reports()
    {
        Assert.False(BatteryProtocol.TryParse(new byte[8], 0, out _)); var report = new byte[64]; report[1] = 0; report[3] = ProtocolLayout.PowerClass; report[4] = ProtocolLayout.BatteryStatusCommand; report[8] = 101; Assert.False(BatteryProtocol.TryParse(report, 0, out _)); report[8] = 50; report[3] = 0; Assert.False(BatteryProtocol.TryParse(report, 0, out _));
    }
}
