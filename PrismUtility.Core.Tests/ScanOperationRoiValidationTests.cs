using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanOperationRoiValidationTests
{
    [Fact]
    public void AdcCalibration_RejectsInvalidEffectiveAndShieldWithExactRelativePaths()
    {
        var settings = new ScanCalibrationRoiSettings(
            new ScanColumnRange(4, 3),
            new ScanColumnRange(2, 2),
            new ScanColumnRange(0, 4),
            new ScanColumnRange(8, 12),
            new ScanColumnRange(0, 12));

        var result = ScanAdcCalibrationRoi.TryCreate(settings, 16);

        Assert.False(result.IsValid);
        Assert.Equal(
            [
                new ScanRoiValidationIssue(ScanRoiOperationOwner.AdcCalibration, ScanRoiValidationCode.Inverted, "EffectiveRange"),
                new ScanRoiValidationIssue(ScanRoiOperationOwner.AdcCalibration, ScanRoiValidationCode.TooNarrow, "ShieldRange")
            ],
            result.Issues);
    }

    [Fact]
    public void FocusAndAdcAdapters_ValidateOnlyTheirOwnedFields()
    {
        var adcOnlyMalformed = new ScanCalibrationRoiSettings(
            new ScanColumnRange(4, 3),
            new ScanColumnRange(2, 2),
            new ScanColumnRange(0, 4),
            new ScanColumnRange(8, 12),
            new ScanColumnRange(0, 12));
        var focusOnlyMalformed = new ScanCalibrationRoiSettings(
            new ScanColumnRange(0, 7),
            new ScanColumnRange(8, 11),
            new ScanColumnRange(0, 4),
            new ScanColumnRange(4, 8),
            new ScanColumnRange(0, 8));

        Assert.True(ScanFocusRoi.TryCreate(adcOnlyMalformed, 16).IsValid);
        Assert.True(ScanAdcCalibrationRoi.TryCreate(focusOnlyMalformed, 16).IsValid);
        Assert.Equal(
            new ScanRoiValidationIssue(ScanRoiOperationOwner.AutoFocus, ScanRoiValidationCode.Overlap, "FocusRightRange"),
            Assert.Single(ScanFocusRoi.TryCreate(focusOnlyMalformed, 16).Issues));
    }

    [Theory]
    [InlineData(0, -1, ScanRoiValidationCode.Empty)]
    [InlineData(5, 4, ScanRoiValidationCode.Inverted)]
    [InlineData(-1, 2, ScanRoiValidationCode.OutOfBounds)]
    [InlineData(0, 16, ScanRoiValidationCode.OutOfBounds)]
    public void ImageReferenceRange_RejectsInvalidBoundsWithoutAdcOrFocusSemantics(int start, int endInclusive, ScanRoiValidationCode code)
    {
        var result = ScanImageReferenceColumnRange.TryCreate(new ScanColumnRange(start, endInclusive), 16);

        Assert.Equal(code, Assert.Single(result.Issues).Code);
        Assert.Equal(ScanRoiOperationOwner.ImageReferenceSampling, result.Issues[0].Owner);
        Assert.Equal("ColumnRange", result.Issues[0].FieldPath);
    }

    [Theory]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(0, int.MaxValue)]
    public void RoiRanges_WithWidthsBeyondInt32_AreRejectedWithoutOverflow(int start, int endInclusive)
    {
        var range = new ScanColumnRange(start, endInclusive);

        Assert.Equal(int.MaxValue, range.Width);
        var result = ScanImageReferenceColumnRange.TryCreate(range, 16);

        Assert.Equal(ScanRoiValidationCode.OutOfBounds, Assert.Single(result.Issues).Code);
    }
}
