using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanDngOutputPlanTests
{
    [Theory]
    [InlineData(ScanDngExportMode.LinearRaw4, 2)]
    [InlineData(ScanDngExportMode.LinearRgbIrw, 3)]
    public void AdditionalWhiteCaptureIsExportedOnceAsRawMonochromeWithoutDroppingAnyChannel(ScanDngExportMode mode, int outputCount)
    {
        var assignment = new ScanChannelAssignment("IR", "Blue", "Red", "Green", false, false, false, false, "White");

        var plan = ScanDngOutputPlan.Build(assignment, 5, mode);

        Assert.Equal(outputCount, plan.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, plan.SelectMany(part => part.PassIndexes).Order().ToArray());
        var white = Assert.Single(plan, part => part.PassIndexes.Contains(4));
        Assert.Equal(DngPixelLayout.MonochromeRaw, white.PixelLayout);
        Assert.Equal(new[] { 4 }, white.PassIndexes);
        Assert.Equal("white_w", white.FileSuffix);
        Assert.DoesNotContain(plan, part => part.PassIndexes.Count > 4);
    }

    [Fact]
    public void RgbOutputUsesRoleMappingRatherThanLedPosition()
    {
        var assignment = new ScanChannelAssignment("IR", "Blue", "Red", "Green", false, false, false, false, "White");

        var plan = ScanDngOutputPlan.Build(assignment, 5, ScanDngExportMode.LinearRgbIrw);

        Assert.Equal(new[] { 2, 3, 1 }, plan[0].PassIndexes);
        Assert.Equal(DngPixelLayout.LinearRgb, plan[0].PixelLayout);
        Assert.Equal(new[] { 0 }, plan[1].PassIndexes);
        Assert.Equal(DngPixelLayout.MonochromeRaw, plan[1].PixelLayout);
    }

    [Theory]
    [InlineData(ScanDngExportMode.LinearRaw4, 1)]
    [InlineData(ScanDngExportMode.LinearRgbIrw, 2)]
    public void ExistingFourChannelExportsKeepTheirFileLayouts(ScanDngExportMode mode, int outputCount)
    {
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "White", false, false, false, false);

        var plan = ScanDngOutputPlan.Build(assignment, 4, mode);

        Assert.Equal(outputCount, plan.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, plan.SelectMany(part => part.PassIndexes).Order().ToArray());
        Assert.DoesNotContain(plan, part => part.FileSuffix == "white_w");
    }

    [Fact]
    public void UnsupportedOrMismatchedExtraChannelsAreRejectedBeforeOutputStarts()
    {
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "IR", false, false, false, false, "Red");
        Assert.Throws<InvalidOperationException>(() => ScanDngOutputPlan.Build(assignment, 5, ScanDngExportMode.LinearRaw4));
        Assert.Throws<InvalidOperationException>(() => ScanDngOutputPlan.Build(assignment with { Channel5Role = "Unused" }, 5, ScanDngExportMode.LinearRaw4));
    }

    [Fact]
    public void WhiteOnlyCaptureRemainsARealSingleChannelRawOutput()
    {
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "IR", false, false, false, false, "White");

        var part = Assert.Single(ScanDngOutputPlan.Build(assignment, new[] { 4 }, ScanDngExportMode.LinearRaw4));

        Assert.Equal(DngPixelLayout.MonochromeRaw, part.PixelLayout);
        Assert.Equal(new[] { 0 }, part.PassIndexes);
        Assert.Equal("white_w", part.FileSuffix);
    }

    [Fact]
    public void SparseCaptureOrderMapsEveryRawBufferToItsActualSlot()
    {
        var assignment = new ScanChannelAssignment("Red", "Green", "Blue", "IR", false, false, false, false, "White");

        var plan = ScanDngOutputPlan.Build(assignment, new[] { 4, 2 }, ScanDngExportMode.LinearRaw4);

        Assert.Equal(2, plan.Count);
        Assert.Equal(new[] { 1 }, plan.Single(part => part.ModelSuffix == "Blue").PassIndexes);
        Assert.Equal(new[] { 0 }, plan.Single(part => part.FileSuffix == "white_w").PassIndexes);
        Assert.Equal(new[] { 0, 1 }, plan.SelectMany(part => part.PassIndexes).Order().ToArray());
    }
}
