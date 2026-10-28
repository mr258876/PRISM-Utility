using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanRawPreviewRowMappingTests
{
    [Theory]
    [InlineData(0, 100, 100, false, false, 0)]
    [InlineData(99, 100, 100, false, false, 99)]
    [InlineData(20, 512, 40, true, false, 20)]
    [InlineData(511, 512, 600, true, false, 511)]
    public void CurrentCapturePixelsMapToTheSameRawRow(int row, int height, int completed,
        bool waterfall, bool compressed, int expected)
    {
        Assert.True(ScanRawPreviewRowMapping.TryMapPreviewToRaw(row, height, completed, waterfall, compressed, out var raw));
        Assert.Equal(expected, raw);
        Assert.True(ScanRawPreviewRowMapping.TryMapRawToPreview(raw, height, completed, waterfall, compressed, out var preview));
        Assert.Equal(row, preview);
    }

    [Theory]
    [InlineData(0, 512, 40, true, true)]
    [InlineData(40, 512, 40, true, false)]
    [InlineData(511, 512, 40, true, false)]
    [InlineData(100, 100, 100, false, false)]
    [InlineData(-1, 100, 100, false, false)]
    [InlineData(0, 100, 0, false, false)]
    public void AveragedHistoryAndUncompletedPixelsNeverPretendToBeRawRows(int row, int height,
        int completed, bool waterfall, bool compressed)
    {
        Assert.False(ScanRawPreviewRowMapping.TryMapPreviewToRaw(row, height, completed, waterfall, compressed, out _));
        Assert.False(ScanRawPreviewRowMapping.TryMapRawToPreview(row, height, completed, waterfall, compressed, out _));
    }
}
