using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RoBaStatus.Services;

internal sealed class Win32HidDevice : IDisposable
{
    private readonly SafeFileHandle _handle;
    private readonly FileStream _stream;
    private readonly int _inputReportLength;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _readTask;
    private volatile bool _faulted;

    private Win32HidDevice(SafeFileHandle handle, int inputReportLength)
    {
        _handle = handle;
        _inputReportLength = inputReportLength;
        _stream = new FileStream(handle, FileAccess.Read, inputReportLength, isAsync: true);
    }

    public event Action<byte[]>? ReportReceived;

    public bool IsConnected => !_faulted && !_handle.IsInvalid && !_handle.IsClosed;

    public static Win32HidDevice? TryOpen(
        ushort vendorId,
        ushort productId,
        ushort usagePage,
        ushort usageId)
    {
        NativeMethods.HidD_GetHidGuid(out var hidGuid);
        var deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);
        if (deviceInfoSet == NativeMethods.InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
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
                    var error = Marshal.GetLastWin32Error();
                    if (error == NativeMethods.ErrorNoMoreItems)
                    {
                        return null;
                    }

                    throw new Win32Exception(error);
                }

                var path = GetDevicePath(deviceInfoSet, ref interfaceData);
                if (path is null)
                {
                    continue;
                }

                var handle = NativeMethods.CreateFile(
                    path,
                    NativeMethods.GenericRead,
                    NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
                    IntPtr.Zero,
                    NativeMethods.OpenExisting,
                    NativeMethods.FileFlagOverlapped,
                    IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    continue;
                }

                if (Matches(handle, vendorId, productId, usagePage, usageId, out var inputLength))
                {
                    try
                    {
                        return new Win32HidDevice(handle, inputLength);
                    }
                    catch
                    {
                        handle.Dispose();
                        throw;
                    }
                }

                handle.Dispose();
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static string? GetDevicePath(
        IntPtr deviceInfoSet,
        ref NativeMethods.SpDeviceInterfaceData interfaceData)
    {
        NativeMethods.SetupDiGetDeviceInterfaceDetail(
            deviceInfoSet,
            ref interfaceData,
            IntPtr.Zero,
            0,
            out var requiredSize,
            IntPtr.Zero);
        if (requiredSize <= 0)
        {
            return null;
        }

        var detail = Marshal.AllocHGlobal(requiredSize);
        try
        {
            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
            if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    detail,
                    requiredSize,
                    out _,
                    IntPtr.Zero))
            {
                return null;
            }

            return Marshal.PtrToStringUni(IntPtr.Add(detail, sizeof(int)));
        }
        finally
        {
            Marshal.FreeHGlobal(detail);
        }
    }

    private static bool Matches(
        SafeFileHandle handle,
        ushort vendorId,
        ushort productId,
        ushort usagePage,
        ushort usageId,
        out int inputReportLength)
    {
        inputReportLength = 0;
        var attributes = new NativeMethods.HiddAttributes
        {
            Size = Marshal.SizeOf<NativeMethods.HiddAttributes>()
        };
        if (!NativeMethods.HidD_GetAttributes(handle, ref attributes) ||
            attributes.VendorId != vendorId ||
            attributes.ProductId != productId ||
            !NativeMethods.HidD_GetPreparsedData(handle, out var preparsedData))
        {
            return false;
        }

        try
        {
            if (NativeMethods.HidP_GetCaps(preparsedData, out var caps) != NativeMethods.HidpStatusSuccess ||
                caps.UsagePage != usagePage ||
                caps.Usage != usageId ||
                caps.InputReportByteLength < 13)
            {
                return false;
            }

            inputReportLength = caps.InputReportByteLength;
            return true;
        }
        finally
        {
            NativeMethods.HidD_FreePreparsedData(preparsedData);
        }
    }

    public bool TryGetInputReport(byte reportId, out byte[] report)
    {
        report = new byte[_inputReportLength];
        report[0] = reportId;
        if (!NativeMethods.HidD_GetInputReport(_handle, report, report.Length))
        {
            _faulted = true;
            return false;
        }

        return true;
    }

    public void StartReading()
    {
        _readTask ??= Task.Run(() => ReadLoopAsync(_shutdown.Token));
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var buffer = new byte[_inputReportLength];
                var count = await _stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0)
                {
                    _faulted = true;
                    return;
                }

                if (count != buffer.Length)
                {
                    Array.Resize(ref buffer, count);
                }
                ReportReceived?.Invoke(buffer);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
            _faulted = true;
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _stream.Dispose();
        _handle.Dispose();
        _shutdown.Dispose();
    }

    private static class NativeMethods
    {
        internal static readonly IntPtr InvalidHandleValue = new(-1);
        internal const uint DigcfPresent = 0x00000002;
        internal const uint DigcfDeviceInterface = 0x00000010;
        internal const int ErrorNoMoreItems = 259;
        internal const uint GenericRead = 0x80000000;
        internal const uint FileShareRead = 0x00000001;
        internal const uint FileShareWrite = 0x00000002;
        internal const uint OpenExisting = 3;
        internal const uint FileFlagOverlapped = 0x40000000;
        internal const int HidpStatusSuccess = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        internal struct SpDeviceInterfaceData
        {
            internal int CbSize;
            internal Guid InterfaceClassGuid;
            internal int Flags;
            internal IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HiddAttributes
        {
            internal int Size;
            internal ushort VendorId;
            internal ushort ProductId;
            internal ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HidpCaps
        {
            internal ushort Usage;
            internal ushort UsagePage;
            internal ushort InputReportByteLength;
            internal ushort OutputReportByteLength;
            internal ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            internal ushort[] Reserved;
            internal ushort NumberLinkCollectionNodes;
            internal ushort NumberInputButtonCaps;
            internal ushort NumberInputValueCaps;
            internal ushort NumberInputDataIndices;
            internal ushort NumberOutputButtonCaps;
            internal ushort NumberOutputValueCaps;
            internal ushort NumberOutputDataIndices;
            internal ushort NumberFeatureButtonCaps;
            internal ushort NumberFeatureValueCaps;
            internal ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        internal static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool HidD_GetAttributes(
            SafeFileHandle hidDeviceObject,
            ref HiddAttributes attributes);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool HidD_GetPreparsedData(
            SafeFileHandle hidDeviceObject,
            out IntPtr preparsedData);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool HidD_GetInputReport(
            SafeFileHandle hidDeviceObject,
            [Out] byte[] reportBuffer,
            int reportBufferLength);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SpDeviceInterfaceData deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize,
            out int requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);
    }
}
