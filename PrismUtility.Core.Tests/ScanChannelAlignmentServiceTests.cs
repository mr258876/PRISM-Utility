using OpenCvSharp;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanChannelAlignmentServiceTests
{
    private const int Width = 96;
    private const int Rows = 48;

    [Theory]
    [InlineData(ScanChannelAlignmentMode.Ecc)]
    [InlineData(ScanChannelAlignmentMode.MutualInformation)]
    [InlineData(ScanChannelAlignmentMode.EccThenMutualInformation)]
    public void TryBuildAlignedNormalizedPassBuffers_ImprovesShiftedChannelAlignment(ScanChannelAlignmentMode alignmentMode)
    {
        var referenceSamples = BuildReferenceSamples();
        var shiftedSamples = ApplyTranslation(referenceSamples, Width, Rows, -4.0, 3.0);
        var service = CreateService();
        var result = BuildResult(EncodeSamples(shiftedSamples), EncodeSamples(referenceSamples), EncodeSamples(referenceSamples));
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "Unused", false, false, false, false);

        var success = service.TryBuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, out var alignedBuffers, out var error);

        Assert.True(success, error);
        var beforeDifference = ComputeMeanAbsoluteDifference(shiftedSamples, referenceSamples);
        var alignedSamples = DecodeSamples(alignedBuffers[0]);
        var afterDifference = ComputeMeanAbsoluteDifference(alignedSamples, referenceSamples);
        Assert.True(afterDifference < beforeDifference * 0.5, $"Expected {alignmentMode} to improve alignment. Before={beforeDifference}, After={afterDifference}");
    }

    [Fact]
    public void TryBuildAlignedNormalizedPassBuffers_FallsBackToOriginalBufferWhenModeFails()
    {
        var referenceSamples = BuildReferenceSamples();
        var shiftedSamples = ApplyTranslation(referenceSamples, Width, Rows, -4.0, 3.0);
        var originalBuffer = EncodeSamples(shiftedSamples);
        var service = CreateService();
        var result = BuildResult(originalBuffer, EncodeSamples(referenceSamples), EncodeSamples(referenceSamples));
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "Unused", false, false, false, false);

        var success = service.TryBuildAlignedNormalizedPassBuffers(result, assignment, (ScanChannelAlignmentMode)999, out var alignedBuffers, out var error);

        Assert.True(success, error);
        Assert.Equal(originalBuffer, alignedBuffers[0]);
    }

    private static ScanChannelAlignmentService CreateService()
        => new(new PassthroughCompositeImageProcessor(), new TestScanImageDecoder(Width, 0, Width - 1));

    private static ScanWorkflowResult BuildResult(params byte[][] passBuffers)
        => new(
            Rows,
            passBuffers.Select((buffer, index) => new ScanPassCapture(index, (byte)index, true, Rows, 0, buffer)).ToArray(),
            0,
            0,
            0,
            0);

    private static ushort[] BuildReferenceSamples()
    {
        var samples = new ushort[Width * Rows];
        var centerX = Width * 0.42;
        var centerY = Rows * 0.58;
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                var gaussian = 28000.0 * Math.Exp(-((dx * dx) / 220.0) - ((dy * dy) / 70.0));
                var wave = (9000.0 * Math.Sin((x * 0.17) + (y * 0.09))) + (7000.0 * Math.Cos((x * 0.05) - (y * 0.21)));
                var ramp = (x * 140.0) + (y * 210.0);
                samples[(y * Width) + x] = (ushort)Math.Clamp((int)Math.Round(6000.0 + gaussian + wave + ramp), 0, ushort.MaxValue);
            }
        }

        return samples;
    }

    private static ushort[] ApplyTranslation(ushort[] sourceSamples, int width, int rows, double shiftX, double shiftY)
    {
        var translated = new ushort[sourceSamples.Length];
        for (var y = 0; y < rows; y++)
        {
            var sourceY = y + shiftY;
            var y0 = (int)Math.Floor(sourceY);
            var y1 = y0 + 1;
            var wy = sourceY - y0;

            for (var x = 0; x < width; x++)
            {
                var sourceX = x + shiftX;
                var x0 = (int)Math.Floor(sourceX);
                var x1 = x0 + 1;
                var wx = sourceX - x0;

                var topLeft = GetSampleOrZero(sourceSamples, width, rows, x0, y0);
                var topRight = GetSampleOrZero(sourceSamples, width, rows, x1, y0);
                var bottomLeft = GetSampleOrZero(sourceSamples, width, rows, x0, y1);
                var bottomRight = GetSampleOrZero(sourceSamples, width, rows, x1, y1);
                var top = Lerp(topLeft, topRight, wx);
                var bottom = Lerp(bottomLeft, bottomRight, wx);
                translated[(y * width) + x] = (ushort)Math.Clamp((int)Math.Round(Lerp(top, bottom, wy)), 0, ushort.MaxValue);
            }
        }

        return translated;
    }

    private static byte[] EncodeSamples(ushort[] samples)
    {
        var buffer = new byte[Rows * ScanDebugConstants.BytesPerLine];
        for (var y = 0; y < Rows; y++)
        {
            var rowStart = y * ScanDebugConstants.BytesPerLine;
            var groupStart = rowStart + ScanDebugConstants.LineBufferMarginLeft;
            for (var x = 0; x < Width; x += ScanDebugConstants.PackedGroupPixels)
            {
                var evenSample = samples[(y * Width) + x];
                var oddSample = x + 1 < Width ? samples[(y * Width) + x + 1] : evenSample;
                var offset = groupStart + ((x / ScanDebugConstants.PackedGroupPixels) * ScanDebugConstants.PackedGroupBytes);
                buffer[offset] = (byte)(oddSample >> 8);
                buffer[offset + 1] = (byte)(evenSample >> 8);
                buffer[offset + 2] = (byte)(oddSample & 0xFF);
                buffer[offset + 3] = (byte)(evenSample & 0xFF);
            }
        }

        return buffer;
    }

    private static ushort[] DecodeSamples(byte[] buffer)
    {
        var decoder = new TestScanImageDecoder(Width, 0, Width - 1);
        var samples = new ushort[Width * Rows];
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                Assert.True(decoder.TryGetSample16(buffer, Rows, x, y, out var sample));
                samples[(y * Width) + x] = sample;
            }
        }

        return samples;
    }

    private static double ComputeMeanAbsoluteDifference(ushort[] left, ushort[] right)
    {
        var totalDifference = 0.0;
        for (var index = 0; index < left.Length; index++)
            totalDifference += Math.Abs(left[index] - right[index]);

        return totalDifference / left.Length;
    }

    private static double GetSampleOrZero(ushort[] samples, int width, int rows, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= rows)
            return 0.0;

        return samples[(y * width) + x];
    }

    private static double Lerp(double start, double end, double t)
        => start + ((end - start) * t);

    private sealed class PassthroughCompositeImageProcessor : IScanCompositeImageProcessor
    {
        public byte[] NormalizePassBuffer(ScanPassCapture capture, bool manuallyReverse)
            => (byte[])capture.ImageBytes.Clone();

        public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, out ScanCompositePixelBuffer? frame, out string error)
            => throw new NotSupportedException();
    }

    private sealed class TestScanImageDecoder(int width, int effectiveStart, int effectiveEndInclusive) : IScanImageDecoder
    {
        public int GetDecodedPixelsPerLine()
            => width;

        public (int Start, int EndInclusive) GetEffectivePixelRange()
            => (effectiveStart, effectiveEndInclusive);

        public void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma)
            => throw new NotSupportedException();

        public void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma)
            => throw new NotSupportedException();

        public bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample)
        {
            sample = 0;
            if (x < 0 || y < 0 || x >= width || y >= rows)
                return false;

            var rowStart = y * ScanDebugConstants.BytesPerLine;
            var groupStart = rowStart + ScanDebugConstants.LineBufferMarginLeft + ((x / ScanDebugConstants.PackedGroupPixels) * ScanDebugConstants.PackedGroupBytes);
            sample = (x & 1) == 0
                ? (ushort)((lineBuffer[groupStart + 1] << 8) | lineBuffer[groupStart + 3])
                : (ushort)((lineBuffer[groupStart] << 8) | lineBuffer[groupStart + 2]);
            return true;
        }
    }
}
