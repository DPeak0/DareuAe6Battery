using System.Runtime.InteropServices;
using DareuAe6.Core;
using Microsoft.Win32.SafeHandles;

namespace DareuAe6.Windows;

public sealed class NativeHidTransport : IBatteryTransport
{
    private static readonly Guid HidGuid = GetHidGuid();
    private readonly byte _targetId;

    public NativeHidTransport(byte targetId = ProtocolLayout.DefaultTargetId)
    {
        _targetId = targetId;
    }

    public ValueTask<BatteryReading?> ReadBatteryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidateFound = false;
        string? lastFailure = null;

        foreach (var path in EnumerateCandidatePaths())
        {
            candidateFound = true;
            cancellationToken.ThrowIfCancellationRequested();
            using var device = NativeMethods.CreateFile(
                path,
                NativeMethods.GenericRead | NativeMethods.GenericWrite,
                NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
                IntPtr.Zero,
                NativeMethods.OpenExisting,
                0,
                IntPtr.Zero);

            if (device.IsInvalid)
            {
                lastFailure = $"open failed (Win32 {Marshal.GetLastWin32Error()})";
                continue;
            }

            if (!TryGetCapabilities(device, out var capabilities, out var capabilityFailure))
            {
                lastFailure = capabilityFailure;
                continue;
            }

            if ((ushort)capabilities.UsagePage != ProtocolLayout.UsagePage ||
                (ushort)capabilities.Usage != ProtocolLayout.Usage)
            {
                lastFailure = $"unexpected HID usage page 0x{(ushort)capabilities.UsagePage:X4}/usage 0x{(ushort)capabilities.Usage:X4}";
                continue;
            }

            if (capabilities.FeatureReportByteLength != ProtocolLayout.FeatureReportLength)
            {
                lastFailure = $"unexpected feature report length {capabilities.FeatureReportByteLength}";
                continue;
            }

            var report = BatteryProtocol.CreateQuery(_targetId).CreateReport();
            if (!NativeMethods.HidD_SetFeature(device, report, report.Length))
            {
                lastFailure = $"SetFeature failed (Win32 {Marshal.GetLastWin32Error()})";
                continue;
            }

            var response = new byte[capabilities.FeatureReportByteLength];
            response[0] = report[0];
            if (!NativeMethods.HidD_GetFeature(device, response, response.Length))
            {
                lastFailure = $"GetFeature failed (Win32 {Marshal.GetLastWin32Error()})";
                continue;
            }

            if (BatteryProtocol.TryParse(response, _targetId, out var reading))
            {
                return ValueTask.FromResult<BatteryReading?>(reading);
            }

            lastFailure = "feature report validation failed";
        }

        throw new InvalidOperationException(
            candidateFound
                ? lastFailure ?? "no response"
                : "matching HID collection not found");
    }

    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        var hidGuid = HidGuid;
        var deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);

        if (deviceInfoSet == new IntPtr(-1))
        {
            yield break;
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new NativeMethods.SpDeviceInterfaceData
                {
                    CbSize = Marshal.SizeOf<NativeMethods.SpDeviceInterfaceData>()
                };
                if (!NativeMethods.SetupDiEnumDeviceInterfaces(
                        deviceInfoSet,
                        IntPtr.Zero,
                        ref hidGuid,
                        index,
                        ref interfaceData))
                {
                    yield break;
                }

                NativeMethods.SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    IntPtr.Zero,
                    0,
                    out var requiredSize,
                    IntPtr.Zero);
                if (requiredSize <= 0)
                {
                    continue;
                }

                var detail = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 5);
                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet,
                            ref interfaceData,
                            detail,
                            requiredSize,
                            out _,
                            IntPtr.Zero))
                    {
                        continue;
                    }

                    var pathPointer = IntPtr.Add(detail, 4);
                    var path = Marshal.PtrToStringUni(pathPointer);
                    if (path is not null)
                    {
                        yield return path;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static bool TryGetCapabilities(
        SafeFileHandle device,
        out NativeMethods.HidpCaps capabilities,
        out string failure)
    {
        capabilities = default;
        var attributes = new NativeMethods.HidAttributes
        {
            Size = Marshal.SizeOf<NativeMethods.HidAttributes>()
        };
        if (!NativeMethods.HidD_GetAttributes(device, ref attributes))
        {
            failure = $"GetAttributes failed (Win32 {Marshal.GetLastWin32Error()})";
            return false;
        }

        if (attributes.VendorId != ProtocolLayout.VendorId ||
            attributes.ProductId != ProtocolLayout.ProductId)
        {
            failure = $"unexpected VID/PID 0x{attributes.VendorId:X4}/0x{attributes.ProductId:X4}";
            return false;
        }

        if (!NativeMethods.HidD_GetPreparsedData(device, out var preparsedData))
        {
            failure = $"GetPreparsedData failed (Win32 {Marshal.GetLastWin32Error()})";
            return false;
        }

        using (preparsedData)
        {
            if (NativeMethods.HidP_GetCaps(preparsedData, out capabilities) != NativeMethods.HidpStatusSuccess)
            {
                failure = "GetCaps failed";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    private static Guid GetHidGuid()
    {
        NativeMethods.HidD_GetHidGuid(out var guid);
        return guid;
    }
}
