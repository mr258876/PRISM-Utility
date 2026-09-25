using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanRawSignalAnalyzer(IScanImageDecoder decoder)
{
    /// <summary>The caller must supply a stable copy containing exactly the completed packed rows.</summary>
    public ScanRawSignalResult Analyze(
        byte[] packedRows, int completedRows, int startRow, int endRowInclusive,
        ScanColumnRange columns, int profileRow)
    {
        ArgumentNullException.ThrowIfNull(packedRows);
        ArgumentNullException.ThrowIfNull(columns);
        if (!ScanRowCountValidation.IsValidForHostBuffer(completedRows))
            throw new ArgumentOutOfRangeException(nameof(completedRows));
        if (packedRows.Length != checked(completedRows * ScanDebugConstants.BytesPerLine))
            throw new ArgumentException("The buffer must contain exactly the completed packed rows.", nameof(packedRows));
        if (startRow < 0 || endRowInclusive < startRow || endRowInclusive >= completedRows)
            throw new ArgumentOutOfRangeException(nameof(startRow), "The row interval must contain completed rows.");
        if (profileRow < startRow || profileRow > endRowInclusive)
            throw new ArgumentOutOfRangeException(nameof(profileRow));

        var width = decoder.GetDecodedPixelsPerLine();
        if (columns.Start < 0 || columns.EndInclusive < columns.Start || columns.EndInclusive >= width)
            throw new ArgumentOutOfRangeException(nameof(columns), "The ROI must contain decoded columns.");

        var count = checked(((long)endRowInclusive - startRow + 1) * ((long)columns.EndInclusive - columns.Start + 1));
        var histogram = new long[256];
        var profile = new ScanRawProfileSample[width];
        for (var column = 0; column < width; column++)
        {
            if (!decoder.TryGetSample16(packedRows, completedRows, column, profileRow, out var value))
                throw new InvalidDataException("The packed scan buffer contains an undecodable sample.");
            profile[column] = new ScanRawProfileSample(column, value);
        }

        var minimum = ushort.MaxValue;
        ushort maximum = 0;
        ulong sum = 0;
        long fullScaleCount = 0;

        for (var row = startRow; row <= endRowInclusive; row++)
        {
            for (var column = columns.Start; column <= columns.EndInclusive; column++)
            {
                ushort value;
                if (row == profileRow)
                    value = profile[column].Value;
                else if (!decoder.TryGetSample16(packedRows, completedRows, column, row, out value))
                    throw new InvalidDataException("The packed scan buffer contains an undecodable sample.");

                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
                sum = checked(sum + value);
                histogram[value >> 8]++;
                if (value == ushort.MaxValue)
                    fullScaleCount++;
            }
        }

        return new ScanRawSignalResult(startRow, endRowInclusive, profileRow, columns, count,
            minimum, maximum, (double)sum / count, fullScaleCount,
            Array.AsReadOnly(histogram), Array.AsReadOnly(profile));
    }

    public static IReadOnlyList<ScanRawDisplayBucket> ReduceProfile(
        IReadOnlyList<ScanRawProfileSample> profile, int bucketCount)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (bucketCount <= 0 || bucketCount > profile.Count)
            throw new ArgumentOutOfRangeException(nameof(bucketCount));

        var buckets = new ScanRawDisplayBucket[bucketCount];
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var start = (int)((long)bucket * profile.Count / bucketCount);
            var end = (int)((long)(bucket + 1) * profile.Count / bucketCount);
            var minimum = profile[start];
            var maximum = minimum;
            for (var index = start + 1; index < end; index++)
            {
                var sample = profile[index];
                if (sample.Value < minimum.Value)
                    minimum = sample;
                if (sample.Value > maximum.Value)
                    maximum = sample;
            }

            buckets[bucket] = new ScanRawDisplayBucket(minimum, maximum);
        }

        return Array.AsReadOnly(buckets);
    }
}
