using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal static class DngWriteRequestValidator
{
    internal static void Validate(DngWriteRequest request)
    {
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

        if (request.PixelLayout == DngPixelLayout.LinearRawMultiChannel
            && (request.ChannelColors is null || request.ChannelColors.Length != 4))
        {
            throw new ArgumentException("LinearRawMultiChannel export requires exactly 4 channel colors.", nameof(request));
        }

        if (request.MaskedAreas is { Length: > DngNativeConstants.MaxMaskedAreas })
            throw new ArgumentException($"A maximum of {DngNativeConstants.MaxMaskedAreas} masked areas can be written per DNG.", nameof(request));

        if (request.BlackLevelPlanes is { Length: > DngNativeConstants.MaxBlackPlanes })
            throw new ArgumentException($"A maximum of {DngNativeConstants.MaxBlackPlanes} black-level planes are supported by the current native ABI.", nameof(request));

        if (request.BlackLevelPlanes is { Length: > 0 } && request.BlackLevelPlanes.Length != request.SamplesPerPixel)
            throw new ArgumentException("BlackLevelPlanes length must match SamplesPerPixel when provided.", nameof(request));

        var bytesPerSample = request.BitsPerSample / 8u;
        var minimumRowStride = checked((ulong)request.Width * request.SamplesPerPixel * bytesPerSample);
        if (minimumRowStride > uint.MaxValue || request.RowStrideBytes < minimumRowStride)
            throw new ArgumentException("Row stride is smaller than the packed row width implied by width and bit depth.", nameof(request));

        var requiredBytes = checked((ulong)request.RowStrideBytes * request.Height);
        if ((ulong)request.PixelData.LongLength < requiredBytes)
            throw new ArgumentException("Pixel buffer is smaller than rowStrideBytes * height requires.", nameof(request));

        if (request.PixelData.LongLength > uint.MaxValue)
            throw new ArgumentException("Pixel buffer is too large for the current native ABI.", nameof(request));

        ValidateOptionalRectangle(request.ActiveArea, request.Width, request.Height, "ActiveArea");
        ValidateOptionalRectangle(request.DefaultCrop, request.Width, request.Height, "DefaultCrop");

        if (request.MaskedAreas is not null)
        {
            foreach (var area in request.MaskedAreas)
            {
                if (!IsRectangleWithinImage(area, request.Width, request.Height))
                    throw new ArgumentException("MaskedAreas must be non-empty rectangles within the source image bounds.", nameof(request));
            }
        }
    }

    private static void ValidateOptionalRectangle(DngRectangle? rectangle, uint width, uint height, string name)
    {
        if (rectangle is null || IsRectangleEmpty(rectangle))
            return;

        if (!IsRectangleWithinImage(rectangle, width, height))
            throw new ArgumentException($"{name} must be empty or a non-empty rectangle within the source image bounds.", nameof(DngWriteRequest));
    }

    private static bool IsRectangleEmpty(DngRectangle rectangle)
        => rectangle.Top == 0 && rectangle.Left == 0 && rectangle.Bottom == 0 && rectangle.Right == 0;

    private static bool IsRectangleWithinImage(DngRectangle rectangle, uint width, uint height)
        => rectangle.Top < rectangle.Bottom
            && rectangle.Left < rectangle.Right
            && rectangle.Bottom <= height
            && rectangle.Right <= width;
}
