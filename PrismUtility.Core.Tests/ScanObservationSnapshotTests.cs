using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanObservationSnapshotTests
{
    [Fact]
    public void CopySurvivesPresenterAndAnalyzerBufferReuse()
    {
        byte[] pixels = [1, 2, 3, 255, 4, 5, 6, 255];
        long[] histogram = [1, 1];
        ScanRawProfileSample[] profile = [new(10, 123), new(11, 456)];
        var raw = new ScanRawSignalResult(2, 2, 2, new(10, 11), 2, 123, 456, 289.5, 0, histogram, profile);
        var frame = new ScanPreviewFrame(pixels, 2, 1, 8, ScanPreviewPixelFormat.Bgra8, 9);

        Assert.True(ScanObservationSnapshot.TryCreate(frame, raw, out var copy));
        pixels[0] = 99;
        histogram[0] = 99;
        profile[0] = new(10, 999);

        Assert.Equal(1, copy!.Frame.Pixels[0]);
        Assert.Equal(1, copy.Signal!.Histogram[0]);
        Assert.Equal((ushort)123, copy.Signal.Profile[0].Value);
        Assert.Equal(raw.Columns, copy.Signal.Columns);
        Assert.Equal(2, copy.Signal.ProfileRow);
        Assert.Equal(9, copy.Frame.Version);
        Assert.NotSame(frame.Pixels, copy.Frame.Pixels);
    }

    [Theory]
    [InlineData(0, 1, 4, 4)]
    [InlineData(1, 0, 4, 4)]
    [InlineData(2, 1, 4, 4)]
    [InlineData(1, 2, 4, 4)]
    [InlineData(1, 1, -1, 4)]
    [InlineData(1, int.MaxValue, 4, 4)]
    public void InvalidOrOversizedFramesAreRejectedBeforeAllocation(int width, int height, int stride, int bytes)
    {
        var frame = new ScanPreviewFrame(new byte[bytes], width, height, stride, ScanPreviewPixelFormat.Bgra8, 1);
        Assert.False(ScanObservationSnapshot.TryCreate(frame, null, out var copy));
        Assert.Null(copy);
    }

    [Fact]
    public void CopyKeepsStrideAndDoesNotInventRawMeasurements()
    {
        var frame = new ScanPreviewFrame([1, 2, 3, 255, 90, 91, 92, 93], 1, 1, 8, ScanPreviewPixelFormat.Bgra8, 4);
        Assert.True(ScanObservationSnapshot.TryCreate(frame, null, out var copy));
        Assert.Equal(frame.Pixels, copy!.Frame.Pixels);
        Assert.Equal(8, copy.Frame.StrideBytes);
        Assert.Null(copy.Signal);
    }

    [Fact]
    public void BaselineCopiedFromFrozenFrameDoesNotShareItsPixelStorage()
    {
        var frame = new ScanPreviewFrame([1, 2, 3, 255], 1, 1, 4, ScanPreviewPixelFormat.Bgra8, 4);
        Assert.True(ScanObservationSnapshot.TryCreate(frame, null, out var frozen));
        Assert.True(ScanObservationSnapshot.TryCreate(frozen!.Frame, frozen.Signal, out var baseline));
        frozen.Frame.Pixels[0] = 77;
        Assert.Equal(1, baseline!.Frame.Pixels[0]);
    }
}
