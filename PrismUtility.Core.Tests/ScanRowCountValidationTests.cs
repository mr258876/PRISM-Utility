using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanRowCountValidationTests
{
    [Fact]
    public void HostBufferBoundary_AcceptsMaximumSafeRowsWithoutAllocating()
    {
        var rows = ScanRowCountValidation.MaxHostRows;

        Assert.True(ScanRowCountValidation.IsValidForHostBuffer(rows));
        var frame = new ScanProtocolService().BuildSetScanLinesCommand(rows);

        Assert.Equal(8, frame.Length);
        Assert.Equal((uint)rows, BitConverter.ToUInt32(frame, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(282459)]
    [InlineData(int.MaxValue)]
    public void ProtocolRowsOutsideHostBufferBoundary_ThrowBeforeFrameProduction(int rows)
    {
        Assert.False(ScanRowCountValidation.IsValidForHostBuffer(rows));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScanProtocolService().BuildSetScanLinesCommand(rows));
    }

    [Fact]
    public void HostBufferBoundary_RejectsMaximumSafeRowsPlusOne()
    {
        var rows = checked(ScanRowCountValidation.MaxHostRows + 1);

        Assert.False(ScanRowCountValidation.IsValidForHostBuffer(rows));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScanRowCountValidation.EnsureValidForHostBuffer(rows, "rows"));
    }
}
