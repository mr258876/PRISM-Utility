using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanRoiOverlayLabelLayoutTests
{
    [Fact]
    public void Arrange_OverlappingPreviewLabelsUseSeparateVerticalLanesAndAvoidSampleLabel()
    {
        var labels = new[]
        {
            new ScanRoiOverlayLabelLayoutInput(0, 0, 78, 14, true),
            new ScanRoiOverlayLabelLayoutInput(1, 14, 72, 14, false),
            new ScanRoiOverlayLabelLayoutInput(2, 28, 94, 14, false),
            new ScanRoiOverlayLabelLayoutInput(3, 42, 68, 14, false),
            new ScanRoiOverlayLabelLayoutInput(4, 56, 76, 14, false),
            new ScanRoiOverlayLabelLayoutInput(5, 44, 116, 14, false, IsBottomAnchored: true)
        };

        var placements = ScanRoiOverlayLabelLayout.Arrange(320, 120, labels);

        Assert.Equal(labels.Length, placements.Count);
        foreach (var placement in placements)
        {
            var label = labels.Single(candidate => candidate.Index == placement.Index);
            Assert.InRange(placement.X, 0, 320 - label.Width);
            Assert.InRange(placement.Y, 0, 120 - label.Height);
        }

        for (var leftIndex = 0; leftIndex < placements.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < placements.Count; rightIndex++)
                Assert.False(Overlaps(placements[leftIndex], placements[rightIndex], labels), $"Labels {placements[leftIndex].Index} and {placements[rightIndex].Index} overlap.");
        }
    }

    [Fact]
    public void Arrange_NearRightEdgeLabelsClampInsideImageBounds()
    {
        var labels = new[]
        {
            new ScanRoiOverlayLabelLayoutInput(0, 310, 84, 14, true),
            new ScanRoiOverlayLabelLayoutInput(1, 312, 72, 14, false)
        };

        var placements = ScanRoiOverlayLabelLayout.Arrange(320, 120, labels);

        Assert.All(placements, placement =>
        {
            var label = labels.Single(candidate => candidate.Index == placement.Index);
            Assert.True(placement.X + label.Width <= 320);
            Assert.True(placement.X >= 0);
        });
    }

    private static bool Overlaps(
        ScanRoiOverlayLabelPlacement left,
        ScanRoiOverlayLabelPlacement right,
        IReadOnlyList<ScanRoiOverlayLabelLayoutInput> labels)
    {
        var leftLabel = labels.Single(candidate => candidate.Index == left.Index);
        var rightLabel = labels.Single(candidate => candidate.Index == right.Index);
        return left.X < right.X + rightLabel.Width
            && right.X < left.X + leftLabel.Width
            && left.Y < right.Y + rightLabel.Height
            && right.Y < left.Y + leftLabel.Height;
    }
}
