using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanRawSignalAnalyzerTests
{
    private readonly ScanImageDecoder _decoder = new();

    [Fact]
    public void Analyze_WhenTwoRowsIncludeBinEdgesAndFullScale_ReportsExactDomainAndProfile()
    {
        var buffer = CreateRows(2);
        SetSample(buffer, 0, 0, 0);
        SetSample(buffer, 0, 1, 255);
        SetSample(buffer, 0, 2, 256);
        SetSample(buffer, 0, 3, 65535);
        SetSample(buffer, 1, 0, 65535);
        SetSample(buffer, 1, 1, 511);
        SetSample(buffer, 1, 2, 512);
        SetSample(buffer, 1, 3, 0);

        var result = Analyzer().Analyze(buffer, 2, 0, 1, new ScanColumnRange(0, 3), 1);

        Assert.Equal(8, result.SampleCount);
        Assert.Equal(0, result.Minimum);
        Assert.Equal(65535, result.Maximum);
        Assert.Equal(132604d / 8, result.Mean);
        Assert.Equal(2, result.FullScaleCount);
        Assert.Equal(0.25, result.SaturationRatio);
        Assert.Equal(256, result.Histogram.Count);
        Assert.Equal(3, result.Histogram[0]);
        Assert.Equal(2, result.Histogram[1]);
        Assert.Equal(1, result.Histogram[2]);
        Assert.Equal(2, result.Histogram[255]);
        Assert.Equal(8, result.Histogram.Sum());
        Assert.Equal(_decoder.GetDecodedPixelsPerLine(), result.Profile.Count);
        Assert.Equal([new(0, 65535), new(1, 511), new(2, 512), new(3, 0)], result.Profile.Take(4));
        Assert.Equal(0, result.StartRow);
        Assert.Equal(1, result.EndRowInclusive);
        Assert.Equal(1, result.ProfileRow);
        Assert.Equal(new ScanColumnRange(0, 3), result.Columns);
    }

    [Fact]
    public void Analyze_WhenSelectedRowIsInsideSubset_ExcludesOtherRowsAndRetainsActualColumns()
    {
        var buffer = CreateRows(3);
        SetSample(buffer, 0, 5, 65535);
        SetSample(buffer, 1, 5, 255);
        SetSample(buffer, 1, 6, 256);
        SetSample(buffer, 2, 5, 512);
        SetSample(buffer, 2, 6, 1024);

        var result = Analyzer().Analyze(buffer, 3, 1, 2, new ScanColumnRange(5, 6), 2);

        Assert.Equal(4, result.SampleCount);
        Assert.Equal(255, result.Minimum);
        Assert.Equal(1024, result.Maximum);
        Assert.Equal(2047d / 4, result.Mean);
        Assert.Equal(0, result.FullScaleCount);
        Assert.Equal(_decoder.GetDecodedPixelsPerLine(), result.Profile.Count);
        Assert.Equal([new(5, 512), new(6, 1024)], result.Profile.Skip(5).Take(2));
    }

    [Fact]
    public void Analyze_WhenRoiHasOddWidth_ProcessesBothParityGroupsAndInclusiveEnd()
    {
        var buffer = CreateRows(1);
        SetSample(buffer, 0, 4, 254);
        SetSample(buffer, 0, 5, 256);
        SetSample(buffer, 0, 6, 65535);

        var result = Analyzer().Analyze(buffer, 1, 0, 0, new ScanColumnRange(4, 6), 0);
        var buckets = ScanRawSignalAnalyzer.ReduceProfile(result.Profile.Skip(4).Take(3).ToArray(), 2);

        Assert.Equal(3, result.SampleCount);
        Assert.Equal(1, result.FullScaleCount);
        Assert.Equal(_decoder.GetDecodedPixelsPerLine(), result.Profile.Count);
        Assert.Equal([new(4, 254), new(5, 256), new(6, 65535)], result.Profile.Skip(4).Take(3));
        Assert.Equal(new ScanRawProfileSample(4, 254), buckets[0].Minimum);
        Assert.Equal(new ScanRawProfileSample(5, 256), buckets[1].Minimum);
        Assert.Equal(new ScanRawProfileSample(6, 65535), buckets[1].Maximum);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    public void Analyze_WhenSingleRawSampleIsAtExtreme_ReportsRealValue(ushort value)
    {
        var buffer = CreateRows(1);
        SetSample(buffer, 0, _decoder.GetDecodedPixelsPerLine() - 1, value);
        var column = _decoder.GetDecodedPixelsPerLine() - 1;

        var result = Analyzer().Analyze(buffer, 1, 0, 0, new ScanColumnRange(column, column), 0);

        Assert.Equal(1, result.SampleCount);
        Assert.Equal(value, result.Minimum);
        Assert.Equal(value, result.Maximum);
        Assert.Equal(value, result.Mean);
        Assert.Equal(value == 65535 ? 1 : 0, result.FullScaleCount);
        Assert.Equal(1, result.Histogram[value >> 8]);
        Assert.Equal(_decoder.GetDecodedPixelsPerLine(), result.Profile.Count);
        Assert.Equal(new ScanRawProfileSample(column, value), result.Profile[column]);
    }

    [Fact]
    public void Analyze_WhenCallerLaterReusesItsBuffer_ResultDoesNotChange()
    {
        var buffer = CreateRows(1);
        SetSample(buffer, 0, 0, 65535);
        var result = Analyzer().Analyze(buffer, 1, 0, 0, new ScanColumnRange(0, 0), 0);

        SetSample(buffer, 0, 0, 0);

        Assert.Equal(65535, result.Mean);
        Assert.Equal(1, result.Histogram[255]);
        Assert.Equal(65535, result.Profile[0].Value);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(2, 1, 0)]
    public void Analyze_WhenRowsAreEmptyOrOutsideCompletedRegion_Rejects(int completedRows, int startRow, int endRow)
    {
        var buffer = CreateRows(Math.Max(1, completedRows));

        Assert.ThrowsAny<ArgumentException>(() => Analyzer().Analyze(
            buffer, completedRows, startRow, endRow, new ScanColumnRange(0, 0), 0));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(3, 2)]
    [InlineData(-1, 0)]
    [InlineData(0, 7593)]
    public void Analyze_WhenRoiIsEmptyOrOutsideDecodedWidth_Rejects(int start, int end)
    {
        var buffer = CreateRows(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => Analyzer().Analyze(
            buffer, 1, 0, 0, new ScanColumnRange(start, end), 0));
    }

    [Fact]
    public void Analyze_WhenSelectedRowIsOutsideStatisticsDomain_Rejects()
    {
        var buffer = CreateRows(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => Analyzer().Analyze(
            buffer, 2, 0, 0, new ScanColumnRange(0, 0), 1));
    }

    [Fact]
    public void Analyze_WhenPackedBufferIsTruncatedOrContainsIncompleteExtraRow_Rejects()
    {
        var analyzer = Analyzer();

        Assert.Throws<ArgumentException>(() => analyzer.Analyze(new byte[ScanDebugConstants.BytesPerLine - 1],
            1, 0, 0, new ScanColumnRange(0, 0), 0));
        Assert.Throws<ArgumentException>(() => analyzer.Analyze(new byte[ScanDebugConstants.BytesPerLine + 1],
            1, 0, 0, new ScanColumnRange(0, 0), 0));
    }

    [Fact]
    public void Analyze_WhenRowCountWouldOverflowPackedSize_RejectsBeforeSampling()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Analyzer().Analyze(
            Array.Empty<byte>(), int.MaxValue, 0, 0, new ScanColumnRange(0, 0), 0));
    }

    [Fact]
    public void ReduceProfile_WhenNarrowSpikeAndDipOccurInsideBuckets_PreservesBothOriginalColumns()
    {
        var buffer = CreateRows(1);
        for (var column = 0; column < 10; column++)
            SetSample(buffer, 0, column, 1000);
        SetSample(buffer, 0, 2, 65535);
        SetSample(buffer, 0, 3, 0);
        SetSample(buffer, 0, 8, 60000);
        var full = Analyzer().Analyze(buffer, 1, 0, 0, new ScanColumnRange(0, 9), 0);

        var buckets = ScanRawSignalAnalyzer.ReduceProfile(full.Profile.Take(10).ToArray(), 2);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(new ScanRawProfileSample(3, 0), buckets[0].Minimum);
        Assert.Equal(new ScanRawProfileSample(2, 65535), buckets[0].Maximum);
        Assert.Equal(new ScanRawProfileSample(5, 1000), buckets[1].Minimum);
        Assert.Equal(new ScanRawProfileSample(8, 60000), buckets[1].Maximum);
        Assert.Equal(10, full.SampleCount);
        Assert.Equal(1, full.FullScaleCount);
    }

    [Fact]
    public void Analyze_WhenPeakIsOutsideRoi_ProfileAndBucketsKeepItWithoutChangingStatistics()
    {
        var buffer = CreateRows(2);
        var width = _decoder.GetDecodedPixelsPerLine();
        SetSample(buffer, 0, 4, 256);
        SetSample(buffer, 0, 5, 512);
        SetSample(buffer, 1, 4, 768);
        SetSample(buffer, 1, 5, 1024);
        SetSample(buffer, 1, width - 2, 65535);

        var result = Analyzer().Analyze(buffer, 2, 0, 1, new ScanColumnRange(4, 5), 1);
        var buckets = ScanRawSignalAnalyzer.ReduceProfile(result.Profile, 2);

        Assert.Equal(width, result.Profile.Count);
        Assert.Equal(new ScanRawProfileSample(4, 768), result.Profile[4]);
        Assert.Equal(new ScanRawProfileSample(5, 1024), result.Profile[5]);
        Assert.Equal(new ScanRawProfileSample(width - 2, 65535), result.Profile[width - 2]);
        Assert.Equal(new ScanRawProfileSample(width - 2, 65535), buckets[1].Maximum);
        Assert.Equal(4, result.SampleCount);
        Assert.Equal(256, result.Minimum);
        Assert.Equal(1024, result.Maximum);
        Assert.Equal(640, result.Mean);
        Assert.Equal(0, result.FullScaleCount);
        Assert.Equal(0, result.Histogram[255]);
        Assert.Equal(4, result.Histogram.Sum());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ReduceProfile_WhenBucketCountIsInvalid_Rejects(int bucketCount)
    {
        ScanRawProfileSample[] profile = [new(4, 10), new(5, 20)];

        Assert.Throws<ArgumentOutOfRangeException>(() => ScanRawSignalAnalyzer.ReduceProfile(profile, bucketCount));
    }

    private ScanRawSignalAnalyzer Analyzer() => new(_decoder);

    private static byte[] CreateRows(int rows) => new byte[checked(rows * ScanDebugConstants.BytesPerLine)];

    private static void SetSample(byte[] buffer, int row, int column, ushort value)
    {
        var index = row * ScanDebugConstants.BytesPerLine + ScanDebugConstants.LineBufferMarginLeft
            + column / 2 * ScanDebugConstants.PackedGroupBytes;
        var offset = (column & 1) == 0 ? 1 : 0;
        buffer[index + offset] = (byte)(value >> 8);
        buffer[index + offset + 2] = (byte)value;
    }
}
