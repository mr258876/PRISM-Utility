namespace PRISM_Utility.Core.Models;

/// <summary>Maps only preview pixels that belong to an individual row of the current RAW capture.</summary>
public static class ScanRawPreviewRowMapping
{
    public static bool TryMapPreviewToRaw(int previewRow, int previewHeight, int completedRows,
        bool isWaterfall, bool isCompressedWaterfall, out int rawRow)
    {
        rawRow = -1;
        if (previewHeight <= 0 || completedRows <= 0 || previewRow < 0 || previewRow >= previewHeight)
            return false;

        // A compressed strip is the mean of a whole capture. Older waterfall strips belong to
        // other captures and cannot be addressed using the current capture's RAW buffer.
        if (isWaterfall && isCompressedWaterfall)
            return false;
        var currentCaptureHeight = isWaterfall ? Math.Min(completedRows, previewHeight) : completedRows;
        if (previewRow >= currentCaptureHeight)
            return false;

        rawRow = previewRow;
        return true;
    }

    public static bool TryMapRawToPreview(int rawRow, int previewHeight, int completedRows,
        bool isWaterfall, bool isCompressedWaterfall, out int previewRow)
    {
        previewRow = -1;
        if (!TryMapPreviewToRaw(rawRow, previewHeight, completedRows,
                isWaterfall, isCompressedWaterfall, out var mapped) || mapped != rawRow)
            return false;
        previewRow = mapped;
        return true;
    }
}
