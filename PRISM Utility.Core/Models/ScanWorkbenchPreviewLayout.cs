namespace PRISM_Utility.Core.Models;

public enum ScanWorkbenchPreviewLayoutMode
{
    WideCompact,
    WideImage,
    NarrowEditor,
    NarrowPreview
}

public static class ScanWorkbenchPreviewLayout
{
    public const double WideThreshold = 960;
    public const double CompactPreviewWidth = 360;
    public const double WideGapWidth = 12;
    public const double SplitterWidth = 12;
    public const double EditorMinimumWidth = 320;
    public const double PreviewMinimumWidth = 360;
    public const double DefaultEditorRatio = 5.0 / 12.0;
    public const double KeyboardResizeDelta = 8;

    public static ScanWorkbenchPreviewLayoutResult Calculate(ScanWorkbenchPreviewLayoutInput input)
    {
        ThrowIfInvalidAvailableWidth(input.AvailableWidth);

        if (input.AvailableWidth < WideThreshold)
            return input.IsNarrowPreviewOpen ? NarrowPreview(input.AvailableWidth) : NarrowEditor(input.AvailableWidth);

        return input.HasImage ? WideImage(input.AvailableWidth, input.EditorRatio) : WideCompact(input.AvailableWidth);
    }

    public static ScanWorkbenchPreviewSplit ResizeWideImageSplit(ScanWorkbenchPreviewResizeInput input)
    {
        ThrowIfInvalidAvailableWidth(input.AvailableWidth);
        if (!double.IsFinite(input.EditorDelta))
            throw new ArgumentOutOfRangeException(nameof(input), input.EditorDelta, "Editor delta must be finite.");

        var current = CalculateWideImageSplit(input.AvailableWidth, input.EditorRatio);
        if (current.UsableWidth <= 0)
            return new ScanWorkbenchPreviewSplit(0, 0, DefaultEditorRatio);

        var editorWidth = ClampWideImageEditorWidth(current.UsableWidth, current.EditorWidth + input.EditorDelta, input.EditorRatio);
        var previewWidth = current.UsableWidth - editorWidth;
        var ratio = editorWidth / current.UsableWidth;
        return new ScanWorkbenchPreviewSplit(editorWidth, previewWidth, ratio);
    }

    private static ScanWorkbenchPreviewLayoutResult NarrowEditor(double availableWidth) => new(
        ScanWorkbenchPreviewLayoutMode.NarrowEditor,
        availableWidth,
        0,
        0,
        IsEditorVisible: true,
        IsPreviewVisible: false,
        IsSplitterVisible: false,
        IsPreviewOpenButtonVisible: true,
        IsBackToEditorButtonVisible: false,
        HideThumbnail: true,
        ContentRowCount: 1);

    private static ScanWorkbenchPreviewLayoutResult NarrowPreview(double availableWidth) => new(
        ScanWorkbenchPreviewLayoutMode.NarrowPreview,
        0,
        availableWidth,
        0,
        IsEditorVisible: false,
        IsPreviewVisible: true,
        IsSplitterVisible: false,
        IsPreviewOpenButtonVisible: false,
        IsBackToEditorButtonVisible: true,
        HideThumbnail: false,
        ContentRowCount: 1);

    private static ScanWorkbenchPreviewLayoutResult WideCompact(double availableWidth) => new(
        ScanWorkbenchPreviewLayoutMode.WideCompact,
        availableWidth - CompactPreviewWidth - WideGapWidth,
        CompactPreviewWidth,
        WideGapWidth,
        IsEditorVisible: true,
        IsPreviewVisible: true,
        IsSplitterVisible: false,
        IsPreviewOpenButtonVisible: false,
        IsBackToEditorButtonVisible: false,
        HideThumbnail: true,
        ContentRowCount: 1);

    private static ScanWorkbenchPreviewLayoutResult WideImage(double availableWidth, double editorRatio)
    {
        var split = CalculateWideImageSplit(availableWidth, editorRatio);
        return new ScanWorkbenchPreviewLayoutResult(
            ScanWorkbenchPreviewLayoutMode.WideImage,
            split.EditorWidth,
            split.PreviewWidth,
            SplitterWidth,
            IsEditorVisible: true,
            IsPreviewVisible: true,
            IsSplitterVisible: true,
            IsPreviewOpenButtonVisible: false,
            IsBackToEditorButtonVisible: false,
            HideThumbnail: false,
            ContentRowCount: 1);
    }

    private static SplitCalculation CalculateWideImageSplit(double availableWidth, double editorRatio)
    {
        var usableWidth = Math.Max(0, availableWidth - SplitterWidth);
        if (usableWidth <= 0)
            return new SplitCalculation(0, 0, 0);

        var requestedEditorWidth = usableWidth * NormalizeRatio(editorRatio);
        var editorWidth = ClampWideImageEditorWidth(usableWidth, requestedEditorWidth, editorRatio);
        return new SplitCalculation(usableWidth, editorWidth, usableWidth - editorWidth);
    }

    private static double ClampWideImageEditorWidth(double usableWidth, double editorWidth, double editorRatio)
    {
        if (usableWidth < EditorMinimumWidth + PreviewMinimumWidth)
            return Math.Clamp(usableWidth * NormalizeRatio(editorRatio), 0, usableWidth);

        return Math.Clamp(editorWidth, EditorMinimumWidth, usableWidth - PreviewMinimumWidth);
    }

    private static double NormalizeRatio(double editorRatio) => double.IsFinite(editorRatio) ? Math.Clamp(editorRatio, 0, 1) : DefaultEditorRatio;

    private static void ThrowIfInvalidAvailableWidth(double availableWidth)
    {
        if (!double.IsFinite(availableWidth) || availableWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(availableWidth), availableWidth, "Available width must be nonnegative and finite.");
    }

    private readonly record struct SplitCalculation(double UsableWidth, double EditorWidth, double PreviewWidth);
}

public readonly record struct ScanWorkbenchPreviewLayoutInput(
    double AvailableWidth,
    bool HasImage,
    bool IsNarrowPreviewOpen,
    double EditorRatio = ScanWorkbenchPreviewLayout.DefaultEditorRatio);

public readonly record struct ScanWorkbenchPreviewLayoutResult(
    ScanWorkbenchPreviewLayoutMode Mode,
    double EditorWidth,
    double PreviewWidth,
    double SeparatorWidth,
    bool IsEditorVisible,
    bool IsPreviewVisible,
    bool IsSplitterVisible,
    bool IsPreviewOpenButtonVisible,
    bool IsBackToEditorButtonVisible,
    bool HideThumbnail,
    int ContentRowCount);

public readonly record struct ScanWorkbenchPreviewResizeInput(
    double AvailableWidth,
    double EditorRatio,
    double EditorDelta);

public readonly record struct ScanWorkbenchPreviewSplit(
    double EditorWidth,
    double PreviewWidth,
    double EditorRatio);
