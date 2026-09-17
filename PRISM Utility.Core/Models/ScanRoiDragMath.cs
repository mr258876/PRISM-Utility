namespace PRISM_Utility.Core.Models;

public static class ScanRoiDragMath
{
    public static ScanColumnRange CalculateMovedRange(ScanColumnRange originalRange, int totalDeltaColumns, int imageWidth)
    {
        var width = originalRange.Width;
        if (width <= 0 || imageWidth <= 0)
            return originalRange;

        var start = Math.Clamp(originalRange.Start + totalDeltaColumns, 0, Math.Max(0, imageWidth - width));
        return new ScanColumnRange(start, start + width - 1);
    }
}
