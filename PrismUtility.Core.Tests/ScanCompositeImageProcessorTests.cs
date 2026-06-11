using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanCompositeImageProcessorTests
{
    private const int Width = 1;
    private const int Rows = 1;

    [Fact]
    public void TryBuildRgbComposite_AppliesD65WhitePointGainForEqualChannels()
    {
        var processor = new ScanCompositeImageProcessor(new TestScanImageDecoder());
        var result = new ScanWorkflowResult(
            Rows,
            new[]
            {
                BuildPass(0, ushort.MaxValue),
                BuildPass(1, ushort.MaxValue),
                BuildPass(2, ushort.MaxValue)
            },
            0,
            0,
            0,
            0);
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "Unused", false, false, false, false);
        var colorManagement = ScanColorManagementOptions.CreateDefault();

        var success = processor.TryBuildRgbComposite(result, assignment, colorManagement, out var frame, out var error);

        Assert.True(success, error);
        Assert.NotNull(frame);
        Assert.Equal(255, frame.Pixels[0]);
        Assert.Equal(255, frame.Pixels[1]);
        Assert.Equal(255, frame.Pixels[2]);
    }

    [Fact]
    public void TryBuildRgbComposite_RejectsInvalidManualWhitePointTemperature()
    {
        var processor = new ScanCompositeImageProcessor(new TestScanImageDecoder());
        var result = new ScanWorkflowResult(
            Rows,
            new[]
            {
                BuildPass(0, ushort.MaxValue),
                BuildPass(1, ushort.MaxValue),
                BuildPass(2, ushort.MaxValue)
            },
            0,
            0,
            0,
            0);
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "Unused", false, false, false, false);
        var colorManagement = ScanColorManagementOptions.CreateDefault() with
        {
            TargetWhitePointMode = ScanTargetWhitePointMode.ManualColorTemperature,
            ManualWhitePointColorTemperatureK = 1000.0
        };

        var success = processor.TryBuildRgbComposite(result, assignment, colorManagement, out var frame, out var error);

        Assert.False(success);
        Assert.Null(frame);
        Assert.Contains("Manual white point color temperature", error);
    }

    private static ScanPassCapture BuildPass(int passIndex, ushort sample)
        => new(passIndex, (byte)passIndex, true, Rows, 0, EncodeSample(sample));

    private static byte[] EncodeSample(ushort sample)
    {
        var buffer = new byte[ScanDebugConstants.BytesPerLine * Rows];
        var offset = ScanDebugConstants.LineBufferMarginLeft;
        buffer[offset + 1] = (byte)(sample >> 8);
        buffer[offset + 3] = (byte)(sample & 0xFF);
        return buffer;
    }

    private sealed class TestScanImageDecoder : IScanImageDecoder
    {
        public int GetDecodedPixelsPerLine()
            => Width;

        public (int Start, int EndInclusive) GetEffectivePixelRange()
            => (0, Width - 1);

        public void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeToBgra(byte[] lineBuffer, int rows, Span<byte> destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample)
        {
            sample = 0;
            if (x != 0 || y != 0 || rows != Rows)
                return false;

            var offset = ScanDebugConstants.LineBufferMarginLeft;
            sample = (ushort)((lineBuffer[offset + 1] << 8) | lineBuffer[offset + 3]);
            return true;
        }
    }
}
