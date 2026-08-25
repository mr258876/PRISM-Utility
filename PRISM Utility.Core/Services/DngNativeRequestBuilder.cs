using System.Runtime.InteropServices;

using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal static class DngNativeRequestBuilder
{
    internal static NativePrismDngWriteRequestV2 Build(DngWriteRequest request, IntPtr pixelData)
    {
        var activeArea = request.ActiveArea ?? DngRectangle.Empty;
        var defaultCrop = request.DefaultCrop ?? DngRectangle.Empty;

        return new NativePrismDngWriteRequestV2
        {
            StructSize = (uint)Marshal.SizeOf<NativePrismDngWriteRequestV2>(),
            AbiVersion = DngNativeConstants.AbiVersion2,
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
                    DngPixelLayout.LinearRgb => 2,
                    DngPixelLayout.MonochromeRaw => 3,
                    DngPixelLayout.LinearRawMultiChannel => 4,
                    _ => 1
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
                CaptureTime = ToNativeDateTime(request.CaptureTime),
                ActiveArea = ToNativeRectangle(activeArea),
                DefaultCrop = ToNativeRectangle(defaultCrop),
                MaskedAreas = ToNativeMaskedAreas(request.MaskedAreas),
                MaskedAreaCount = (uint)(request.MaskedAreas?.Length ?? 0),
                BlackLevelPlanes = ToNativeBlackLevelPlanes(request.BlackLevelPlanes),
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
        var nativeAreas = new NativePrismDngRectangle[DngNativeConstants.MaxMaskedAreas];
        if (areas is null)
            return nativeAreas;

        for (var index = 0; index < areas.Length && index < nativeAreas.Length; index++)
            nativeAreas[index] = ToNativeRectangle(areas[index]);

        return nativeAreas;
    }

    private static NativePrismDngBlackLevelPlane[] ToNativeBlackLevelPlanes(DngBlackLevelPlane[]? planes)
    {
        var nativePlanes = new NativePrismDngBlackLevelPlane[DngNativeConstants.MaxBlackPlanes];
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

    private static NativePrismDngDateTime ToNativeDateTime(DateTimeOffset? dateTime)
    {
        if (dateTime is null)
            return default;

        var value = dateTime.Value;
        return new NativePrismDngDateTime
        {
            Year = (uint)value.Year,
            Month = (uint)value.Month,
            Day = (uint)value.Day,
            Hour = (uint)value.Hour,
            Minute = (uint)value.Minute,
            Second = (uint)value.Second,
            OffsetMinutes = checked((int)value.Offset.TotalMinutes),
            HasDateTime = 1
        };
    }

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
}
