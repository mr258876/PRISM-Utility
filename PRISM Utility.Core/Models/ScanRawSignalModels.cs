namespace PRISM_Utility.Core.Models;

public sealed record ScanRawProfileSample(int Column, ushort Value);

public sealed record ScanRawDisplayBucket(ScanRawProfileSample Minimum, ScanRawProfileSample Maximum);

public sealed record ScanRawSignalResult(
    int StartRow,
    int EndRowInclusive,
    int ProfileRow,
    ScanColumnRange Columns,
    long SampleCount,
    ushort Minimum,
    ushort Maximum,
    double Mean,
    long FullScaleCount,
    IReadOnlyList<long> Histogram,
    IReadOnlyList<ScanRawProfileSample> Profile)
{
    public double SaturationRatio => (double)FullScaleCount / SampleCount;
}
