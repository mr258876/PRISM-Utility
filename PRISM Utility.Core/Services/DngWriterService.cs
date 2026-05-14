using System.ComponentModel;
using System.Runtime.InteropServices;

using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class DngWriterService : IDngWriterService
{
    private const string NativeLibraryName = "DngSdkWarpper.dll";
    private const uint PrismDngAbiVersion2 = 2;
    private const uint PrismDngPixelLayoutRawMosaic = 1;
    private const uint PrismDngPixelLayoutLinearRgb = 2;
    private const uint PrismDngPixelLayoutMonochromeRaw = 3;
    private const uint PrismDngPixelLayoutLinearRawMultiChannel = 4;
    private const int PrismDngStatusOk = 0;
    private const int PrismDngMaxMaskedAreas = 4;
    private const int PrismDngMaxBlackPlanes = 4;

    public void WriteRawDng(DngWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
        ArgumentNullException.ThrowIfNull(request.PixelData);

        if (request.Width == 0 || request.Height == 0 || request.RowStrideBytes == 0)
            throw new ArgumentException("Width, height, and row stride must be non-zero.", nameof(request));

        if (request.BitsPerSample is not (8 or 16))
            throw new ArgumentOutOfRangeException(nameof(request), "Only 8-bit and 16-bit raw input are currently supported.");

        if (request.PixelLayout is not (DngPixelLayout.RawMosaic or DngPixelLayout.LinearRgb or DngPixelLayout.MonochromeRaw or DngPixelLayout.LinearRawMultiChannel))
            throw new ArgumentOutOfRangeException(nameof(request), "Only raw mosaic, linear RGB, monochrome raw, and multi-channel linear raw DNG export are currently supported.");

        if (request.PixelLayout == DngPixelLayout.RawMosaic && request.CfaPattern == DngCfaPattern.Unknown)
            throw new ArgumentOutOfRangeException(nameof(request), "A concrete CFA pattern is required for raw DNG export.");

        if (request.SamplesPerPixel == 0)
            throw new ArgumentOutOfRangeException(nameof(request), "SamplesPerPixel must be non-zero.");

        if (request.PixelLayout is DngPixelLayout.RawMosaic or DngPixelLayout.MonochromeRaw)
        {
            if (request.SamplesPerPixel != 1)
                throw new ArgumentOutOfRangeException(nameof(request), "Raw mosaic and monochrome raw export require SamplesPerPixel = 1.");
        }

        if (request.PixelLayout == DngPixelLayout.LinearRgb && request.SamplesPerPixel != 3)
            throw new ArgumentOutOfRangeException(nameof(request), "LinearRgb export requires SamplesPerPixel = 3.");

        if (request.PixelLayout == DngPixelLayout.LinearRawMultiChannel && request.SamplesPerPixel != 4)
            throw new ArgumentOutOfRangeException(nameof(request), "Current multi-channel linear raw export requires SamplesPerPixel = 4.");

        if (request.PixelLayout == DngPixelLayout.LinearRawMultiChannel)
        {
            if (request.ChannelColors is null || request.ChannelColors.Length != 4)
                throw new ArgumentException("LinearRawMultiChannel export requires exactly 4 channel colors.", nameof(request));
        }

        if (request.MaskedAreas is { Length: > PrismDngMaxMaskedAreas })
            throw new ArgumentException($"A maximum of {PrismDngMaxMaskedAreas} masked areas can be written per DNG.", nameof(request));

        if (request.BlackLevelPlanes is { Length: > PrismDngMaxBlackPlanes })
            throw new ArgumentException($"A maximum of {PrismDngMaxBlackPlanes} black-level planes are supported by the current native ABI.", nameof(request));

        if (request.BlackLevelPlanes is { Length: > 0 } && request.BlackLevelPlanes.Length != request.SamplesPerPixel)
            throw new ArgumentException("BlackLevelPlanes length must match SamplesPerPixel when provided.", nameof(request));

        var bytesPerSample = request.BitsPerSample / 8u;
        var minimumRowStride = checked(request.Width * request.SamplesPerPixel * bytesPerSample);
        if (request.RowStrideBytes < minimumRowStride)
            throw new ArgumentException("Row stride is smaller than the packed row width implied by width and bit depth.", nameof(request));

        var requiredBytes = checked((ulong)request.RowStrideBytes * request.Height);
        if ((ulong)request.PixelData.LongLength < requiredBytes)
            throw new ArgumentException("Pixel buffer is smaller than rowStrideBytes * height requires.", nameof(request));

        if (request.PixelData.LongLength > uint.MaxValue)
            throw new ArgumentException("Pixel buffer is too large for the current native ABI.", nameof(request));

        var pixelHandle = GCHandle.Alloc(request.PixelData, GCHandleType.Pinned);

        try
        {
            var nativeRequest = BuildNativeRequest(request, pixelHandle.AddrOfPinnedObject());
            var status = NativeMethods.PrismDngWriteFromBuffer(ref nativeRequest);

            if (status != PrismDngStatusOk)
            {
                throw new InvalidOperationException(BuildNativeFailureMessage(status));
            }
        }
        catch (DllNotFoundException exception)
        {
            throw new InvalidOperationException(
                $"Native DNG writer library '{NativeLibraryName}' was not found next to the application output.",
                exception);
        }
        catch (BadImageFormatException exception)
        {
            throw new InvalidOperationException(
                $"Native DNG writer library '{NativeLibraryName}' has an architecture mismatch with the current process.",
                exception);
        }
        finally
        {
            if (pixelHandle.IsAllocated)
                pixelHandle.Free();
        }
    }

    private static NativePrismDngWriteRequestV2 BuildNativeRequest(DngWriteRequest request, IntPtr pixelData)
    {
        var activeArea = request.ActiveArea ?? DngRectangle.Empty;
        var defaultCrop = request.DefaultCrop ?? DngRectangle.Empty;
        var maskedAreas = ToNativeMaskedAreas(request.MaskedAreas);
        var blackLevelPlanes = ToNativeBlackLevelPlanes(request.BlackLevelPlanes);

        return new NativePrismDngWriteRequestV2
        {
            StructSize = (uint)Marshal.SizeOf<NativePrismDngWriteRequestV2>(),
            AbiVersion = PrismDngAbiVersion2,
            OutputPath = request.OutputPath,
            Image = new NativePrismDngImageBuffer
            {
                Data = pixelData,
                DataBytes = (ulong)request.PixelData.LongLength,
                Width = request.Width,
                Height = request.Height,
                RowStrideBytes = request.RowStrideBytes,
                BitsPerSample = request.BitsPerSample,
                SamplesPerPixel = request.SamplesPerPixel,
                PixelLayout = request.PixelLayout switch
                {
                    DngPixelLayout.LinearRgb => PrismDngPixelLayoutLinearRgb,
                    DngPixelLayout.MonochromeRaw => PrismDngPixelLayoutMonochromeRaw,
                    DngPixelLayout.LinearRawMultiChannel => PrismDngPixelLayoutLinearRawMultiChannel,
                    _ => PrismDngPixelLayoutRawMosaic
                },
                CfaPattern = (uint)request.CfaPattern
            },
            Metadata = new NativePrismDngMetadata
            {
                Make = request.Make,
                Model = request.Model,
                Software = request.Software,
                UniqueCameraModel = request.Model,
                IsoSpeed = request.IsoSpeed,
                ExposureTime = ToNativeRational(request.ExposureTime),
                FrameRate = default,
                BlackLevel = request.BlackLevel,
                WhiteLevel = request.WhiteLevel,
                ActiveArea = ToNativeRectangle(activeArea),
                DefaultCrop = ToNativeRectangle(defaultCrop),
                MaskedAreas = maskedAreas,
                MaskedAreaCount = (uint)(request.MaskedAreas?.Length ?? 0),
                BlackLevelPlanes = blackLevelPlanes,
                BlackLevelPlaneCount = (uint)(request.BlackLevelPlanes?.Length ?? 0),
                Color = ToNativeColorMetadata(request.Color),
                ChannelColors = ToNativeChannelColors(request.ChannelColors),
                HasChannelColors = request.ChannelColors is { Length: 4 } ? (byte)1 : (byte)0
            },
            Reserved = new ulong[6]
        };
    }

    private static uint[] ToNativeChannelColors(DngChannelColor[]? channelColors)
    {
        if (channelColors is null)
            return new uint[4];

        if (channelColors.Length != 4)
            throw new ArgumentException($"Expected 4 channel colors but received {channelColors.Length}.");

        return channelColors.Select(static color => (uint)color).ToArray();
    }

    private static NativePrismDngRectangle ToNativeRectangle(DngRectangle rectangle)
        => new()
        {
            Top = rectangle.Top,
            Left = rectangle.Left,
            Bottom = rectangle.Bottom,
            Right = rectangle.Right
        };

    private static NativePrismDngRectangle[] ToNativeMaskedAreas(DngRectangle[]? areas)
    {
        var nativeAreas = new NativePrismDngRectangle[PrismDngMaxMaskedAreas];
        if (areas is null)
            return nativeAreas;

        for (var index = 0; index < areas.Length && index < nativeAreas.Length; index++)
        {
            nativeAreas[index] = ToNativeRectangle(areas[index]);
        }

        return nativeAreas;
    }

    private static NativePrismDngBlackLevelPlane[] ToNativeBlackLevelPlanes(DngBlackLevelPlane[]? planes)
    {
        var nativePlanes = new NativePrismDngBlackLevelPlane[PrismDngMaxBlackPlanes];
        if (planes is null)
            return nativePlanes;

        for (var index = 0; index < planes.Length && index < nativePlanes.Length; index++)
        {
            nativePlanes[index] = new NativePrismDngBlackLevelPlane
            {
                TopLeft = planes[index].TopLeft,
                TopRight = planes[index].TopRight,
                BottomLeft = planes[index].BottomLeft,
                BottomRight = planes[index].BottomRight
            };
        }

        return nativePlanes;
    }

    private static NativePrismDngRational64 ToNativeRational(DngRational? rational)
        => rational is null
            ? default
            : new NativePrismDngRational64
            {
                Numerator = rational.Numerator,
                Denominator = rational.Denominator
            };

    private static NativePrismDngColorMetadata ToNativeColorMetadata(DngColorMetadata? color)
        => new()
        {
            AnalogBalance = CopyExactArray(color?.AnalogBalance, 3),
            CameraNeutral = CopyExactArray(color?.CameraNeutral, 3),
            ColorMatrix1 = CopyExactArray(color?.ColorMatrix1, 9),
            ColorMatrix2 = CopyExactArray(color?.ColorMatrix2, 9),
            HasAnalogBalance = color?.AnalogBalance is { Length: 3 } ? (byte)1 : (byte)0,
            HasCameraNeutral = color?.CameraNeutral is { Length: 3 } ? (byte)1 : (byte)0,
            HasColorMatrix1 = color?.ColorMatrix1 is { Length: 9 } ? (byte)1 : (byte)0,
            HasColorMatrix2 = color?.ColorMatrix2 is { Length: 9 } ? (byte)1 : (byte)0
        };

    private static double[] CopyExactArray(double[]? source, int expectedLength)
    {
        if (source is null)
            return new double[expectedLength];

        if (source.Length != expectedLength)
            throw new ArgumentException($"Expected {expectedLength} values but received {source.Length}.");

        return (double[])source.Clone();
    }

    private static string BuildNativeFailureMessage(int status)
    {
        var nativeMessage = GetLastErrorMessage();
        return string.IsNullOrWhiteSpace(nativeMessage)
            ? $"Native DNG writer failed with status code {status}."
            : $"Native DNG writer failed with status code {status}: {nativeMessage}";
    }

    private static string GetLastErrorMessage()
    {
        var buffer = new char[1024];
        var status = NativeMethods.PrismDngGetLastErrorMessage(buffer, (uint)buffer.Length);
        if (status != PrismDngStatusOk)
            return string.Empty;

        var terminatorIndex = Array.IndexOf(buffer, '\0');
        if (terminatorIndex < 0)
            terminatorIndex = buffer.Length;

        return new string(buffer, 0, terminatorIndex);
    }

    private static class NativeMethods
    {
        [DllImport(NativeLibraryName, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PrismDngWriteFromBuffer(ref NativePrismDngWriteRequestV2 request);

        [DllImport(NativeLibraryName, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PrismDngGetLastErrorMessage([Out] char[] buffer, uint bufferChars);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePrismDngRectangle
    {
        public uint Top;
        public uint Left;
        public uint Bottom;
        public uint Right;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePrismDngRational64
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePrismDngImageBuffer
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
    private struct NativePrismDngColorMetadata
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
    private struct NativePrismDngBlackLevelPlane
    {
        public double TopLeft;
        public double TopRight;
        public double BottomLeft;
        public double BottomRight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativePrismDngMetadata
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
        public NativePrismDngRectangle ActiveArea;
        public NativePrismDngRectangle DefaultCrop;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PrismDngMaxMaskedAreas)]
        public NativePrismDngRectangle[] MaskedAreas;
        public uint MaskedAreaCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PrismDngMaxBlackPlanes)]
        public NativePrismDngBlackLevelPlane[] BlackLevelPlanes;
        public uint BlackLevelPlaneCount;
        public NativePrismDngColorMetadata Color;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] ChannelColors;
        public byte HasChannelColors;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativePrismDngWriteRequestV2
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
}
