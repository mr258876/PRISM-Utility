using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

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
                var pixel0 = (ushort)((lineBuffer[i] << 8) | lineBuffer[i + 2]);
                var pixel1 = (ushort)((lineBuffer[i + 1] << 8) | lineBuffer[i + 3]);

                WriteGrayPixel(rowPixels, pixelIndex++, pixel0, applyGammaCorrection, gamma);
                WriteGrayPixel(rowPixels, pixelIndex++, pixel1, applyGammaCorrection, gamma);
            }

            destination.Write(rowPixels, 0, rowPixels.Length);
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

        sample = (x & 1) == 0
            ? (ushort)((lineBuffer[groupStart] << 8) | lineBuffer[groupStart + 2])
            : (ushort)((lineBuffer[groupStart + 1] << 8) | lineBuffer[groupStart + 3]);
        return true;
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
