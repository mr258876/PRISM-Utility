namespace PRISM_Utility.Core.Models;

public enum ScanWorkbenchPreviewLayoutMode
{
    WideCompact,
    WideImage,
    MediumImage,
    MediumInspection,
    NarrowEditor,
    NarrowPreview,
    NarrowInspection,
    ConfigurationEditor,
    PreviewCollapsed,
    ReviewOnly
}

public static class ScanWorkbenchPreviewLayout
{
    public const double WideThreshold = 1280;
    public const double CompactThreshold = 900;
    public const double TaskRailWidth = 300;
    public const double InspectionRailWidth = 260;
    public const double WideGapWidth = 12;
    public const double SplitterWidth = 12;
    public const double EditorMinimumWidth = TaskRailWidth;
    public const double EmptyPreviewEditorMinimumWidth = 360;
    public const double PreviewMinimumWidth = 480;
    public const double EmptyPreviewMinimumWidth = 420;
    public const double DefaultEditorRatio = 0.25;
    public const double DefaultEmptyPreviewEditorRatio = 0.48;
    public const double KeyboardResizeDelta = 8;
    public const double ReviewDockThreshold = 720;
    public const double ReviewRailWidth = 320;
    public const double ReviewGapWidth = 12;

    public static ScanWorkbenchPreviewLayoutResult Calculate(ScanWorkbenchPreviewLayoutInput input)
    {
        ThrowIfInvalidAvailableWidth(input.AvailableWidth);

        if (!input.IsReviewOpen)
            return CalculateContent(input);

        if (input.AvailableWidth < ReviewDockThreshold)
            return new ScanWorkbenchPreviewLayoutResult(ScanWorkbenchPreviewLayoutMode.ReviewOnly,
                0, 0, 0,
                IsEditorVisible: false,
                IsPreviewVisible: false,
                IsSplitterVisible: false,
                IsPreviewOpenButtonVisible: false,
                IsBackToEditorButtonVisible: false,
                HideThumbnail: true,
                ContentRowCount: 1,
                ReviewWidth: input.AvailableWidth,
                IsReviewVisible: true,
                IsReviewFullWidth: true);

        return CalculateContent(input with
        {
            AvailableWidth = input.AvailableWidth - ReviewRailWidth - ReviewGapWidth
        }) with
        {
            ReviewWidth = ReviewRailWidth,
            ReviewGapWidth = ReviewGapWidth,
            IsReviewVisible = true
        };
    }

    private static ScanWorkbenchPreviewLayoutResult CalculateContent(ScanWorkbenchPreviewLayoutInput input)
    {
        if (input.IsConfigurationOpen)
            return new ScanWorkbenchPreviewLayoutResult(ScanWorkbenchPreviewLayoutMode.ConfigurationEditor,
                input.AvailableWidth, 0, 0, true, false, false, false, false, false, 1);

        if (input.AvailableWidth < CompactThreshold)
            return input.IsInspectionOpen ? NarrowInspection(input.AvailableWidth)
                : input.IsNarrowPreviewOpen && !input.IsPreviewCollapsed ? NarrowPreview(input.AvailableWidth) : NarrowEditor(input.AvailableWidth);

        if (input.IsPreviewCollapsed)
            return new ScanWorkbenchPreviewLayoutResult(ScanWorkbenchPreviewLayoutMode.PreviewCollapsed,
                input.AvailableWidth - (input.IsInspectionOpen ? InspectionRailWidth + WideGapWidth : 0), 0, 0,
                true, false, false, true, false, true, 1,
                InspectionWidth: input.IsInspectionOpen ? InspectionRailWidth : 0,
                InspectionGapWidth: input.IsInspectionOpen ? WideGapWidth : 0,
                IsInspectionVisible: input.IsInspectionOpen);

        var showInspection = input.AvailableWidth >= WideThreshold && input.IsInspectionOpen;
        var overlayInspection = input.AvailableWidth < WideThreshold && input.IsInspectionOpen;
        var split = CalculateWideImageSplit(input.AvailableWidth, input.EditorRatio, showInspection, input.HasImage);
        return new ScanWorkbenchPreviewLayoutResult(
            input.AvailableWidth >= WideThreshold
                ? input.HasImage ? ScanWorkbenchPreviewLayoutMode.WideImage : ScanWorkbenchPreviewLayoutMode.WideCompact
                : overlayInspection ? ScanWorkbenchPreviewLayoutMode.MediumInspection : ScanWorkbenchPreviewLayoutMode.MediumImage,
            split.EditorWidth, split.PreviewWidth, SplitterWidth,
            true, true, true, false, false, false, 1,
            InspectionWidth: showInspection ? InspectionRailWidth : 0,
            InspectionGapWidth: showInspection ? WideGapWidth : 0,
            IsInspectionVisible: showInspection || overlayInspection,
            IsInspectionOverlay: overlayInspection);
    }

    public static ScanWorkbenchPreviewSplit ResizeWideImageSplit(ScanWorkbenchPreviewResizeInput input)
    {
        ThrowIfInvalidAvailableWidth(input.AvailableWidth);
        if (!double.IsFinite(input.EditorDelta))
            throw new ArgumentOutOfRangeException(nameof(input), input.EditorDelta, "Editor delta must be finite.");

        var current = CalculateWideImageSplit(input.AvailableWidth, input.EditorRatio,
            input.IsInspectionOpen && input.AvailableWidth >= WideThreshold, input.HasImage);
        if (current.UsableWidth <= 0)
            return new ScanWorkbenchPreviewSplit(0, 0, GetDefaultEditorRatio(input.HasImage));

        var editorWidth = ClampWideImageEditorWidth(current.UsableWidth, current.EditorWidth + input.EditorDelta, input.HasImage);
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

    private static ScanWorkbenchPreviewLayoutResult NarrowInspection(double availableWidth) => new(
        ScanWorkbenchPreviewLayoutMode.NarrowInspection,
        0, 0, 0,
        IsEditorVisible: false,
        IsPreviewVisible: false,
        IsSplitterVisible: false,
        IsPreviewOpenButtonVisible: false,
        IsBackToEditorButtonVisible: false,
        HideThumbnail: false,
        ContentRowCount: 1,
        InspectionWidth: availableWidth,
        IsInspectionVisible: true);

    private static SplitCalculation CalculateWideImageSplit(double availableWidth, double editorRatio, bool showInspection, bool hasImage)
    {
        var inspectionWidth = showInspection ? InspectionRailWidth + WideGapWidth : 0;
        var usableWidth = Math.Max(0, availableWidth - SplitterWidth - inspectionWidth);
        if (usableWidth <= 0)
            return new SplitCalculation(0, 0, 0);

        var requestedEditorWidth = usableWidth * NormalizeRatio(editorRatio, hasImage);
        var editorWidth = ClampWideImageEditorWidth(usableWidth, requestedEditorWidth, hasImage);
        return new SplitCalculation(usableWidth, editorWidth, usableWidth - editorWidth);
    }

    private static double ClampWideImageEditorWidth(double usableWidth, double editorWidth, bool hasImage)
    {
        var editorMinimumWidth = hasImage ? EditorMinimumWidth : EmptyPreviewEditorMinimumWidth;
        var previewMinimumWidth = hasImage ? PreviewMinimumWidth : EmptyPreviewMinimumWidth;
        if (usableWidth < editorMinimumWidth + previewMinimumWidth)
            return Math.Clamp(editorWidth, 0, usableWidth);

        return Math.Clamp(editorWidth, editorMinimumWidth, usableWidth - previewMinimumWidth);
    }

    private static double NormalizeRatio(double editorRatio, bool hasImage)
        => double.IsFinite(editorRatio) ? Math.Clamp(editorRatio, 0, 1) : GetDefaultEditorRatio(hasImage);

    private static double GetDefaultEditorRatio(bool hasImage) => hasImage ? DefaultEditorRatio : DefaultEmptyPreviewEditorRatio;

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
    double EditorRatio = double.NaN,
    bool IsInspectionOpen = false,
    bool IsConfigurationOpen = false,
    bool IsPreviewCollapsed = false,
    bool IsReviewOpen = false);

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
    int ContentRowCount,
    double InspectionWidth = 0,
    double InspectionGapWidth = 0,
    bool IsInspectionVisible = false,
    double ReviewWidth = 0,
    double ReviewGapWidth = 0,
    bool IsReviewVisible = false,
    bool IsReviewFullWidth = false,
    bool IsInspectionOverlay = false);

public readonly record struct ScanWorkbenchPreviewResizeInput(
    double AvailableWidth,
    double EditorRatio,
    double EditorDelta,
    bool IsInspectionOpen = false,
    bool HasImage = true);

public readonly record struct ScanWorkbenchPreviewSplit(
    double EditorWidth,
    double PreviewWidth,
    double EditorRatio);
