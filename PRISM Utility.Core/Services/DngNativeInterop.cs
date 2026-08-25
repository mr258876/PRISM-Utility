using System.Runtime.InteropServices;

using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal interface IDngNativeApi
{
    int WriteFromBuffer(ref NativePrismDngWriteRequestV2 request);

    int GetLastErrorMessage(char[] buffer, uint bufferChars);
}

internal sealed class PrismDngNativeApi : IDngNativeApi
{
    public int WriteFromBuffer(ref NativePrismDngWriteRequestV2 request)
        => NativeMethods.PrismDngWriteFromBuffer(ref request);

    public int GetLastErrorMessage(char[] buffer, uint bufferChars)
        => NativeMethods.PrismDngGetLastErrorMessage(buffer, bufferChars);

    private static class NativeMethods
    {
        [DllImport(DngNativeConstants.NativeLibraryName, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PrismDngWriteFromBuffer(ref NativePrismDngWriteRequestV2 request);

        [DllImport(DngNativeConstants.NativeLibraryName, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PrismDngGetLastErrorMessage([Out] char[] buffer, uint bufferChars);
    }
}

internal static class DngNativeConstants
{
    internal const string NativeLibraryName = "DngSdkWarpper.dll";
    internal const uint AbiVersion2 = 2;
    internal const int StatusOk = 0;
    internal const int MaxMaskedAreas = 4;
    internal const int MaxBlackPlanes = 4;
    internal const int ErrorMessageBufferChars = 1024;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePrismDngRectangle
{
    public uint Top;
    public uint Left;
    public uint Bottom;
    public uint Right;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePrismDngRational64
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePrismDngImageBuffer
{
    public IntPtr Data;
    public ulong DataBytes;
    public uint Width;
    public uint Height;
    public uint RowStrideBytes;
    public ushort BitsPerSample;
    public ushort SamplesPerPixel;
    public uint PixelLayout;
    public uint CfaPattern;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePrismDngColorMetadata
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] AnalogBalance;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] CameraNeutral;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public double[] ColorMatrix1;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public double[] ColorMatrix2;

    public byte HasAnalogBalance;
    public byte HasCameraNeutral;
    public byte HasColorMatrix1;
    public byte HasColorMatrix2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePrismDngBlackLevelPlane
{
    public double TopLeft;
    public double TopRight;
    public double BottomLeft;
    public double BottomRight;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePrismDngDateTime
{
    public uint Year;
    public uint Month;
    public uint Day;
    public uint Hour;
    public uint Minute;
    public uint Second;
    public int OffsetMinutes;
    public uint HasDateTime;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativePrismDngMetadata
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? Make;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? Model;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? Software;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? UniqueCameraModel;

    public uint IsoSpeed;
    public NativePrismDngRational64 ExposureTime;
    public NativePrismDngRational64 FrameRate;
    public uint BlackLevel;
    public uint WhiteLevel;
    public NativePrismDngDateTime CaptureTime;
    public NativePrismDngRectangle ActiveArea;
    public NativePrismDngRectangle DefaultCrop;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DngNativeConstants.MaxMaskedAreas)]
    public NativePrismDngRectangle[] MaskedAreas;

    public uint MaskedAreaCount;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DngNativeConstants.MaxBlackPlanes)]
    public NativePrismDngBlackLevelPlane[] BlackLevelPlanes;

    public uint BlackLevelPlaneCount;
    public NativePrismDngColorMetadata Color;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] ChannelColors;

    public byte HasChannelColors;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativePrismDngWriteRequestV2
{
    public uint StructSize;
    public uint AbiVersion;
    public uint Flags;
    public uint Reserved0;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? OutputPath;

    public NativePrismDngImageBuffer Image;
    public NativePrismDngMetadata Metadata;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public ulong[] Reserved;
}
