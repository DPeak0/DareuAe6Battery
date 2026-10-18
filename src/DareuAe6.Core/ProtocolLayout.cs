namespace DareuAe6.Core;

public static class ProtocolLayout
{
    public const ushort VendorId = 0x260D;
    public const ushort ProductId = 0x1261;
    public const ushort UsagePage = 0xFF02;
    public const ushort Usage = 0x0002;
    public const int FeatureReportLength = 64;
    public const byte PowerClass = 0x07;
    public const byte BatteryStatusCommand = 0x00;
    public const byte GetFlag = 0x80;
    public const int PayloadStartIndex = 7;
    public const int BatteryLevelPayloadOffset = 1;
    public const int BatteryLevelIndex = PayloadStartIndex + BatteryLevelPayloadOffset;
    public const byte DefaultTargetId = 0x02;
}
