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

        var success = processor.TryBuildRgbComposite(result, assignment, colorManagement, CancellationToken.None, out var frame, out var error);

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

        var success = processor.TryBuildRgbComposite(result, assignment, colorManagement, CancellationToken.None, out var frame, out var error);

        Assert.False(success);
        Assert.Null(frame);
        Assert.Contains("Manual white point color temperature", error);
    }

    [Fact]
    public void TryBuildPartialRgbComposite_UsesZeroForMissingChannels()
    {
        var processor = new ScanCompositeImageProcessor(new TestScanImageDecoder());
        var result = new ScanWorkflowResult(
            Rows,
            new[]
            {
                BuildPass(0, ushort.MaxValue),
                BuildPass(1, 0),
                BuildPass(2, 0)
            },
            0,
            0,
            0,
            0);
        var assignment = new ScanChannelAssignment("Red", "Unused", "Unused", "Unused", false, false, false, false);
        var colorManagement = new ScanColorManagementOptions(false, 680.0, 525.0, 450.0, 1.0);
        var availableRows = new Dictionary<string, ScanRowAvailability>(StringComparer.OrdinalIgnoreCase)
        {
            ["Red"] = new(0, Rows)
        };

        var success = processor.TryBuildPartialRgbComposite(result, assignment, colorManagement, availableRows, CancellationToken.None, out var frame, out var error);

        Assert.True(success, error);
        Assert.NotNull(frame);
        Assert.Equal(0, frame.Pixels[0]);
        Assert.Equal(0, frame.Pixels[1]);
        Assert.Equal(255, frame.Pixels[2]);
        Assert.Equal(255, frame.Pixels[3]);
    }

    [Fact]
    public void TryBuildPartialRgbComposite_SetsAlphaOnlyWhenAnyChannelRowIsAvailable()
    {
        const int partialRows = 2;
        var processor = new ScanCompositeImageProcessor(new TestScanImageDecoder(partialRows));
        var result = new ScanWorkflowResult(
            partialRows,
            new[]
            {
                BuildPass(0, partialRows, 0),
                BuildPass(1, partialRows, 0),
                BuildPass(2, partialRows, 0)
            },
            0,
            0,
            0,
            0);
        var assignment = new ScanChannelAssignment("Red", "Unused", "Unused", "Unused", false, false, false, false);
        var colorManagement = new ScanColorManagementOptions(false, 680.0, 525.0, 450.0, 1.0);
        var availableRows = new Dictionary<string, ScanRowAvailability>(StringComparer.OrdinalIgnoreCase)
        {
            ["Red"] = new(0, 1)
        };

        var success = processor.TryBuildPartialRgbComposite(result, assignment, colorManagement, availableRows, CancellationToken.None, out var frame, out var error);

        Assert.True(success, error);
        Assert.NotNull(frame);
        Assert.Equal(255, frame.Pixels[3]);
        Assert.Equal(0, frame.Pixels[7]);
    }

    private static ScanPassCapture BuildPass(int passIndex, ushort sample)
        => BuildPass(passIndex, Rows, sample);

    private static ScanPassCapture BuildPass(int passIndex, int rows, ushort sample)
        => new(passIndex, (byte)passIndex, true, rows, 0, EncodeSample(rows, sample));

    private static byte[] EncodeSample(int rows, ushort sample)
    {
        var buffer = new byte[ScanDebugConstants.BytesPerLine * rows];
        for (var row = 0; row < rows; row++)
        {
            var offset = (row * ScanDebugConstants.BytesPerLine) + ScanDebugConstants.LineBufferMarginLeft;
            buffer[offset + 1] = (byte)(sample >> 8);
            buffer[offset + 3] = (byte)(sample & 0xFF);
        }

        return buffer;
    }

    private sealed class TestScanImageDecoder : IScanImageDecoder
    {
        private readonly int _rows;

        public TestScanImageDecoder(int rows = Rows)
        {
            _rows = rows;
        }

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
            if (x != 0 || y < 0 || y >= _rows || rows != _rows)
                return false;

            var offset = (y * ScanDebugConstants.BytesPerLine) + ScanDebugConstants.LineBufferMarginLeft;
            sample = (ushort)((lineBuffer[offset + 1] << 8) | lineBuffer[offset + 3]);
            return true;
        }
    }
}
