namespace PRISM_Utility.Core.Models;

public readonly record struct ScanRoiOverlayLabelLayoutInput(
    int Index,
    double AnchorX,
    double Width,
    double Height,
    bool IsSelected,
    bool IsBottomAnchored = false);

public readonly record struct ScanRoiOverlayLabelPlacement(int Index, double X, double Y);

public static class ScanRoiOverlayLabelLayout
{
    public static IReadOnlyList<ScanRoiOverlayLabelPlacement> Arrange(
        double imageWidth,
        double imageHeight,
        IReadOnlyList<ScanRoiOverlayLabelLayoutInput> labels,
        double inset = 4,
        double gap = 2)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || labels.Count == 0)
            return Array.Empty<ScanRoiOverlayLabelPlacement>();

        var placements = new List<ScanRoiOverlayLabelPlacement>(labels.Count);
        var labelsByIndex = labels.ToDictionary(label => label.Index);
        var orderedLabels = labels
            .Select((label, order) => new { Label = label, Order = order })
            .OrderBy(item => item.Label.IsBottomAnchored ? 1 : 0)
            .ThenByDescending(item => item.Label.IsSelected)
            .ThenBy(item => item.Order);

        foreach (var item in orderedLabels)
        {
            var label = item.Label;
            var maxX = Math.Max(0, imageWidth - label.Width);
            var x = Math.Clamp(label.AnchorX + inset, 0, maxX);
            var y = FindY(imageHeight, inset, gap, label, x, placements, labelsByIndex);
            placements.Add(new ScanRoiOverlayLabelPlacement(label.Index, x, y));
        }

        return placements.OrderBy(placement => placement.Index).ToArray();
    }

    private static double FindY(
        double imageHeight,
        double inset,
        double gap,
        ScanRoiOverlayLabelLayoutInput label,
        double x,
        IReadOnlyList<ScanRoiOverlayLabelPlacement> placements,
        IReadOnlyDictionary<int, ScanRoiOverlayLabelLayoutInput> labelsByIndex)
    {
        var maxY = Math.Max(0, imageHeight - label.Height);
        var step = Math.Max(1, label.Height + gap);
        var preferredY = label.IsBottomAnchored ? imageHeight - label.Height - inset : inset;
        preferredY = Math.Clamp(preferredY, 0, maxY);

        for (var lane = 0; lane <= placements.Count; lane++)
        {
            var y = label.IsBottomAnchored ? preferredY - (lane * step) : preferredY + (lane * step);
            if (y < 0 || y > maxY)
                continue;

            if (!OverlapsAny(x, y, label, placements, labelsByIndex))
                return y;
        }

        return preferredY;
    }

    private static bool OverlapsAny(
        double x,
        double y,
        ScanRoiOverlayLabelLayoutInput label,
        IReadOnlyList<ScanRoiOverlayLabelPlacement> placements,
        IReadOnlyDictionary<int, ScanRoiOverlayLabelLayoutInput> labelsByIndex)
    {
        foreach (var placement in placements)
        {
            var placedLabel = labelsByIndex[placement.Index];
            if (x < placement.X + placedLabel.Width
                && placement.X < x + label.Width
                && y < placement.Y + placedLabel.Height
                && placement.Y < y + label.Height)
            {
                return true;
            }
        }

        return false;
    }
}
