using System.Runtime.InteropServices;
using DareuAe6.Core;
using Microsoft.Win32.SafeHandles;

namespace DareuAe6.Windows;

public sealed class NativeHidTransport : IBatteryTransport
{
    private static readonly Guid HidGuid = GetHidGuid();
    private readonly byte _targetId;
    public NativeHidTransport(byte targetId = ProtocolLayout.DefaultTargetId) => _targetId = targetId;

    public ValueTask<BatteryReading?> ReadBatteryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var path in EnumerateCandidatePaths())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var device = NativeMethods.CreateFile(path, NativeMethods.GenericRead | NativeMethods.GenericWrite, NativeMethods.FileShareRead | NativeMethods.FileShareWrite, IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);
            if (device.IsInvalid) continue;
            if (!TryGetCapabilities(device, out var capabilities) || (ushort)capabilities.UsagePage != ProtocolLayout.UsagePage || (ushort)capabilities.Usage != ProtocolLayout.Usage || capabilities.FeatureReportByteLength != ProtocolLayout.FeatureReportLength) continue;
            var report = BatteryProtocol.CreateQuery(_targetId).CreateReport();
            if (!NativeMethods.HidD_SetFeature(device, report, report.Length)) continue;
            var response = new byte[capabilities.FeatureReportByteLength];
            response[0] = report[0];
            if (!NativeMethods.HidD_GetFeature(device, response, response.Length)) continue;
            if (BatteryProtocol.TryParse(response, _targetId, out var reading)) return ValueTask.FromResult<BatteryReading?>(reading);
        }
        return ValueTask.FromResult<BatteryReading?>(null);
    }

    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        var deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref HidGuid, IntPtr.Zero, IntPtr.Zero, NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);
        if (deviceInfoSet == new IntPtr(-1)) yield break;
        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new NativeMethods.SpDeviceInterfaceData { CbSize = Marshal.SizeOf<NativeMethods.SpDeviceInterfaceData>() };
                if (!NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref HidGuid, index, ref interfaceData)) yield break;
                NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);
                if (requiredSize <= 0) continue;
                var detail = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 5);
                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detail, requiredSize, out _, IntPtr.Zero)) continue;
                    var path = Marshal.PtrToStringUni(IntPtr.Add(detail, IntPtr.Size == 8 ? 8 : 4));
                    if (path is not null && IsMatchingCollection(path)) yield return path;
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet); }
    }

    private static bool IsMatchingCollection(string path) => path.Contains($"vid_{ProtocolLayout.VendorId:X4}", StringComparison.OrdinalIgnoreCase) && path.Contains($"pid_{ProtocolLayout.ProductId:X4}", StringComparison.OrdinalIgnoreCase);
    private static bool TryGetCapabilities(SafeFileHandle device, out NativeMethods.HidpCaps capabilities)
    {
        capabilities = default;
        var attributes = new NativeMethods.HidAttributes { Size = Marshal.SizeOf<NativeMethods.HidAttributes>() };
        if (!NativeMethods.HidD_GetAttributes(device, ref attributes) || attributes.VendorId != ProtocolLayout.VendorId || attributes.ProductId != ProtocolLayout.ProductId) return false;
        if (!NativeMethods.HidD_GetPreparsedData(device, out var preparsedData)) return false;
        using (preparsedData) return NativeMethods.HidP_GetCaps(preparsedData, out capabilities) == NativeMethods.HidpStatusSuccess;
    }
    private static Guid GetHidGuid() { NativeMethods.HidD_GetHidGuid(out var guid); return guid; }
}
