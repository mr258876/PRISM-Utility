using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public class ScanImageDecoder : IScanImageDecoder
{
    private const double MaxSampleValue = ushort.MaxValue;

    public int GetDecodedPixelsPerLine()
    {
        var usableBytes = ScanDebugConstants.BytesPerLine - ScanDebugConstants.LineBufferMarginLeft - ScanDebugConstants.LineBufferMarginRight;
        if (usableBytes < ScanDebugConstants.PackedGroupBytes)
            return 0;

        var packedGroupCount = usableBytes / ScanDebugConstants.PackedGroupBytes;
        return packedGroupCount * ScanDebugConstants.PackedGroupPixels;
    }

    public (int Start, int EndInclusive) GetEffectivePixelRange()
    {
        var width = GetDecodedPixelsPerLine();
        if (width <= 0)
            return (0, -1);

        var effectiveStart = Math.Clamp(ScanDebugConstants.EffectivePixelStart, 0, width - 1);
        var effectiveEnd = Math.Clamp(ScanDebugConstants.EffectivePixelEnd, effectiveStart, width - 1);
        return (effectiveStart, effectiveEnd);
    }

    public void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma)
    {
        var width = GetDecodedPixelsPerLine();
        if (width <= 0)
            throw new InvalidOperationException("Decoded preview width is invalid.");

        if (applyGammaCorrection && gamma <= 0)
            throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma must be greater than zero.");

        ValidateBufferSize(lineBuffer, rows);

        var rowPixels = new byte[width * 4];
        destination.Position = 0;

        for (var y = 0; y < rows; y++)
        {
            Array.Clear(rowPixels);

            var rowStart = y * ScanDebugConstants.BytesPerLine;
            var decodeStart = rowStart + ScanDebugConstants.LineBufferMarginLeft;
            var decodeEndExclusive = rowStart + ScanDebugConstants.BytesPerLine - ScanDebugConstants.LineBufferMarginRight;
            var pixelIndex = 0;

            for (var i = decodeStart; i + (ScanDebugConstants.PackedGroupBytes - 1) < decodeEndExclusive; i += ScanDebugConstants.PackedGroupBytes)
            {
                ReadPackedGroupSamples(lineBuffer, i, out var pixel0, out var pixel1);

                WriteGrayPixel(rowPixels, pixelIndex++, pixel0, applyGammaCorrection, gamma);
                WriteGrayPixel(rowPixels, pixelIndex++, pixel1, applyGammaCorrection, gamma);
            }

            destination.Write(rowPixels, 0, rowPixels.Length);
        }
    }

    public void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma)
    {
        var width = GetDecodedPixelsPerLine();
        if (width <= 0)
            throw new InvalidOperationException("Decoded preview width is invalid.");

        if (applyGammaCorrection && gamma <= 0)
            throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma must be greater than zero.");

        ValidateBufferSize(lineBuffer, rows);

        var expectedLength = width * 4;
        if (destination.Length != expectedLength)
            throw new ArgumentException($"Waterfall strip buffer size mismatch: expected {expectedLength}, actual {destination.Length}", nameof(destination));

        var columnSums = new ulong[width];

        for (var y = 0; y < rows; y++)
        {
            var rowStart = y * ScanDebugConstants.BytesPerLine;
            var decodeStart = rowStart + ScanDebugConstants.LineBufferMarginLeft;
            var decodeEndExclusive = rowStart + ScanDebugConstants.BytesPerLine - ScanDebugConstants.LineBufferMarginRight;
            var pixelIndex = 0;

            for (var i = decodeStart; i + (ScanDebugConstants.PackedGroupBytes - 1) < decodeEndExclusive; i += ScanDebugConstants.PackedGroupBytes)
            {
                ReadPackedGroupSamples(lineBuffer, i, out var pixel0, out var pixel1);

                columnSums[pixelIndex++] += pixel0;
                columnSums[pixelIndex++] += pixel1;
            }
        }

        for (var x = 0; x < width; x++)
        {
            var average = (ushort)(columnSums[x] / (ulong)rows);
            WriteGrayPixel(destination, x, average, applyGammaCorrection, gamma);
        }
    }

    public bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample)
    {
        sample = 0;

        var width = GetDecodedPixelsPerLine();
        if (width <= 0)
            return false;

        if (x < 0 || y < 0 || x >= width || y >= rows)
            return false;

        var expectedBytes = rows * ScanDebugConstants.BytesPerLine;
        if (lineBuffer.Length != expectedBytes)
            return false;

        var rowStart = y * ScanDebugConstants.BytesPerLine;
        var groupStart = rowStart + ScanDebugConstants.LineBufferMarginLeft + ((x / ScanDebugConstants.PackedGroupPixels) * ScanDebugConstants.PackedGroupBytes);
        var decodeEndExclusive = rowStart + ScanDebugConstants.BytesPerLine - ScanDebugConstants.LineBufferMarginRight;
        if (groupStart + (ScanDebugConstants.PackedGroupBytes - 1) >= decodeEndExclusive)
            return false;

        ReadPackedGroupSamples(lineBuffer, groupStart, out var evenSample, out var oddSample);
        sample = (x & 1) == 0 ? evenSample : oddSample;
        return true;
    }

    private static void ReadPackedGroupSamples(byte[] lineBuffer, int startIndex, out ushort sample0, out ushort sample1)
    {
        sample1 = (ushort)((lineBuffer[startIndex] << 8) | lineBuffer[startIndex + 2]);
        sample0 = (ushort)((lineBuffer[startIndex + 1] << 8) | lineBuffer[startIndex + 3]);
    }

    private static void WriteGrayPixel(byte[] rowPixels, int pixelIndex, ushort sample16, bool applyGammaCorrection, double gamma)
    {
        var gray = ConvertAdcSampleToGray(sample16, applyGammaCorrection, gamma);
        var byteIndex = pixelIndex * 4;
        rowPixels[byteIndex] = gray;
        rowPixels[byteIndex + 1] = gray;
        rowPixels[byteIndex + 2] = gray;
        rowPixels[byteIndex + 3] = 255;
    }

    private static void ValidateBufferSize(byte[] lineBuffer, int rows)
    {
        var expectedBytes = rows * ScanDebugConstants.BytesPerLine;
        if (lineBuffer.Length != expectedBytes)
            throw new IOException($"Scan buffer size mismatch: expected {expectedBytes}, actual {lineBuffer.Length}");
    }

    private static byte ConvertAdcSampleToGray(ushort sample, bool applyGammaCorrection, double gamma)
    {
        if (!applyGammaCorrection)
            return (byte)(sample / 256);

        var normalized = sample / MaxSampleValue;
        var corrected = Math.Pow(normalized, 1.0 / gamma);
        return (byte)Math.Clamp((int)Math.Round(corrected * byte.MaxValue), 0, byte.MaxValue);
    }
}
