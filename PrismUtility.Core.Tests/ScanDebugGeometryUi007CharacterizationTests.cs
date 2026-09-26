using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UI007")]
[Trait("Category", "ScanDebugGeometry")]
[Trait("Category", "Roi")]
public sealed class ScanDebugGeometryUi007CharacterizationTests
{
    [Fact]
    public void Ui007_ScanDebugGeometry_SourceOnly_CanvasDisplayStateStaysInCodeBehind()
    {
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml");
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var refreshLayout = ExtractMemberBodyAtDeclaration(codeBehind, "private void RefreshPreviewLayout()");
        var initialFit = ExtractMemberBodyAtDeclaration(codeBehind, "private void ApplyInitialFitZoom()");

        Assert.Contains("NavigationCacheMode=\"Enabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PreviewScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ZoomMode=\"Enabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinZoomFactor=\"0.1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxZoomFactor=\"20\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewCanvas\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewCanvasControl\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ClearColor=\"Transparent\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RoiCanvas\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AxisCanvas\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsHitTestVisible=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewEmptyStateGrid\"", xaml, StringComparison.Ordinal);

        Assert.Contains("private const double AxisMarginLeft = 48;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private const double AxisMarginTop = 28;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private const double AxisMarginRight = 16;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private const double AxisMarginBottom = 36;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.Width = 0;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.Height = 0;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvas.Width = 0;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvas.Height = 0;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("RoiCanvas.Children.Clear();", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("AxisCanvas.Children.Clear();", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("SetDefaultCursorText();", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("DisposePreviewBitmap();", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("_pendingInitialFitZoom = false;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.Width = imageWidth;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.Height = imageHeight;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetLeft(PreviewCanvasControl, AxisMarginLeft);", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetTop(PreviewCanvasControl, AxisMarginTop);", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvas.Width = AxisMarginLeft + imageWidth + AxisMarginRight;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvas.Height = AxisMarginTop + imageHeight + AxisMarginBottom;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("RoiCanvas.Width = PreviewCanvas.Width;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("RoiCanvas.Height = PreviewCanvas.Height;", refreshLayout, StringComparison.Ordinal);
        Assert.Contains("if (viewportWidth <= 1 || viewportHeight <= 1 || contentWidth <= 0 || contentHeight <= 0)", initialFit, StringComparison.Ordinal);
        Assert.Contains("Math.Min(viewportWidth / contentWidth, viewportHeight / contentHeight)", initialFit, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp((float)fitZoom, PreviewScrollViewer.MinZoomFactor, PreviewScrollViewer.MaxZoomFactor)", initialFit, StringComparison.Ordinal);
        Assert.Contains("PreviewScrollViewer.ChangeView(offsetX, offsetY, targetZoom, true)", initialFit, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_ScanDebugGeometry_SourceOnly_RoiPointerAndOverlayGeometryStayInCodeBehind()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var pointerPressed = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewCanvasControl_PointerPressed(object sender, PointerRoutedEventArgs e)");
        var pointerMoved = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewCanvasControl_PointerMoved(object sender, PointerRoutedEventArgs e)");
        var pointerReleased = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewCanvasControl_PointerReleased(object sender, PointerRoutedEventArgs e)");
        var drawOverlays = ExtractMemberBodyAtDeclaration(codeBehind, "private void DrawRoiOverlays()");
        var endDrag = ExtractMemberBodyAtDeclaration(codeBehind, "private void EndRoiDrag(PointerRoutedEventArgs e)");
        var clearDrag = ExtractMemberBodyAtDeclaration(codeBehind, "private void ClearRoiDragState()");
        var drawAxes = ExtractMemberBodyAtDeclaration(codeBehind, "private void DrawAxes(int imageWidth, int imageHeight)");

        Assert.Contains("if ((!ViewModel.CanMutateRoiFromPreview && !ViewModel.CanMutateColumnSampleFromPreview) || ViewModel.PreviewFrame is null)", pointerPressed, StringComparison.Ordinal);
        Assert.Contains("if (point.Position.X < 0 || point.Position.Y < 0 || point.Position.X >= ViewModel.PreviewFrame.Width || point.Position.Y >= ViewModel.PreviewFrame.Height)", pointerPressed, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp((int)Math.Floor(point.Position.X), 0, Math.Max(ViewModel.PreviewFrame.Width - 1, 0))", pointerPressed, StringComparison.Ordinal);
        Assert.Contains("_isColumnSampleDrag = ViewModel.CanMutateColumnSampleFromPreview;", pointerPressed, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TryGetColumnSampleRange(ViewModel.PreviewFrame.Width, out _roiOriginalRange)", pointerPressed, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TryGetSelectedRoiRange(ViewModel.PreviewFrame.Width, out _roiOriginalRange)", pointerPressed, StringComparison.Ordinal);
        Assert.Contains("_isRoiMoveMode = hasRange && x >= _roiOriginalRange.Start && x <= _roiOriginalRange.EndInclusive;", pointerPressed, StringComparison.Ordinal);

        Assert.Contains("var x = (int)Math.Floor(point.X);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("var y = (int)Math.Floor(point.Y);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("SetDefaultCursorText();", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("CursorPositionTextBlock.Text = \"ScanDebug_Runtime_CursorPosition\".GetLocalizedFormat(x, y);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TryGetPreviewSample16(x, y, out var sample16)", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("ApplyRoiMoveDragToX(x, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.ShiftColumnSampleRange(x - _roiDragStartX, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.ShiftSelectedRoiRange(x - _roiDragStartX, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("ApplyRoiDragRangeIfChanged(new ScanColumnRange(_roiDragStartX, x), bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.UpdateColumnSampleRange(_roiDragStartX, x, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.UpdateSelectedRoiRange(_roiDragStartX, x, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("if (_isRoiMoveMode)", pointerReleased, StringComparison.Ordinal);
        Assert.Contains("ApplyRoiMoveDragToX(x, bitmap.Width);", pointerReleased, StringComparison.Ordinal);
        Assert.True(
            pointerReleased.IndexOf("ApplyRoiMoveDragToX(x, bitmap.Width);", StringComparison.Ordinal)
                < pointerReleased.IndexOf("PreviewCanvasControl_PointerMoved(PreviewCanvasControl, e);", StringComparison.Ordinal),
            "Move-mode release should apply the original-range total delta before resize-mode forwarding can run.");

        Assert.Contains("if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("foreach (var overlay in ViewModel.GetPreviewRoiOverlays(bitmap.Width))", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Width = overlay.Range.Width", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Height = bitmap.Height", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("StrokeThickness = overlay.IsSelected ? 2.5 : 1.5", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetLeft(rectangle, AxisMarginLeft + overlay.Range.Start);", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetTop(rectangle, AxisMarginTop);", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("var labels = new List<(TextBlock Element, double AnchorX, bool IsSelected, bool IsBottomAnchored)>();", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("labels.Add((label, overlay.Range.Start, overlay.IsSelected, false));", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("ScanRoiOverlayLabelLayout.Arrange(bitmap.Width, bitmap.Height, layoutInputs)", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetLeft(label, AxisMarginLeft + placement.X);", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetTop(label, AxisMarginTop + placement.Y);", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TryGetColumnSampleRange(bitmap.Width, out var sampleRange)", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("StrokeDashArray = new DoubleCollection { 4, 3 }", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("ClearRoiDragState();", endDrag, StringComparison.Ordinal);
        Assert.Contains("_isRoiDragging = false;", clearDrag, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.ReleasePointerCapture(e.Pointer);", endDrag, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewCanvasControl.ReleasePointerCaptures();", endDrag, StringComparison.Ordinal);
        Assert.Contains("var xStep = GetTickStep(imageWidth);", drawAxes, StringComparison.Ordinal);
        Assert.Contains("var yStep = GetTickStep(imageHeight);", drawAxes, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10, 19, 15, 100, 25, 34)]
    [InlineData(10, 19, -20, 100, 0, 9)]
    [InlineData(90, 99, 20, 100, 90, 99)]
    public void Ui007_RoiDragMath_UsesOriginalRangeAndTotalDeltaSoDuplicateCoordinatesAreIdempotent(
        int originalStart,
        int originalEndInclusive,
        int totalDeltaColumns,
        int imageWidth,
        int expectedStart,
        int expectedEndInclusive)
    {
        var original = new ScanColumnRange(originalStart, originalEndInclusive);

        var first = ScanRoiDragMath.CalculateMovedRange(original, totalDeltaColumns, imageWidth);
        var duplicate = ScanRoiDragMath.CalculateMovedRange(original, totalDeltaColumns, imageWidth);

        Assert.Equal(new ScanColumnRange(expectedStart, expectedEndInclusive), first);
        Assert.Equal(first, duplicate);
    }

    [Fact]
    public void Ui007_RoiDragMath_MultipleMoveEventsAndReleaseUseFinalTotalDeltaOnly()
    {
        var original = new ScanColumnRange(10, 19);
        var firstMove = ScanRoiDragMath.CalculateMovedRange(original, 5, 100);
        var secondMove = ScanRoiDragMath.CalculateMovedRange(original, 15, 100);
        var releaseAtSameCoordinate = ScanRoiDragMath.CalculateMovedRange(original, 15, 100);

        Assert.Equal(new ScanColumnRange(15, 24), firstMove);
        Assert.Equal(new ScanColumnRange(25, 34), secondMove);
        Assert.Equal(secondMove, releaseAtSameCoordinate);
    }

    [Fact]
    public void Ui007_Roi_SourceOnly_MoveAndResizeShareCurrentRangeApplyGuardForDuplicateRelease()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var pointerMoved = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewCanvasControl_PointerMoved(object sender, PointerRoutedEventArgs e)");
        var pointerReleased = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewCanvasControl_PointerReleased(object sender, PointerRoutedEventArgs e)");
        var moveApply = ExtractMemberBodyAtDeclaration(codeBehind, "private void ApplyRoiMoveDragToX(int x, int imageWidth)");
        var rangeApply = ExtractMemberBodyAtDeclaration(codeBehind, "private void ApplyRoiDragRangeIfChanged(ScanColumnRange range, int imageWidth)");

        Assert.Contains("ApplyRoiDragRangeIfChanged(range, imageWidth);", moveApply, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.UpdateColumnSampleRange", moveApply, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.UpdateSelectedRoiRange", moveApply, StringComparison.Ordinal);
        Assert.Contains("ApplyRoiDragRangeIfChanged(new ScanColumnRange(_roiDragStartX, x), bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl_PointerMoved(PreviewCanvasControl, e);", pointerReleased, StringComparison.Ordinal);
        Assert.Contains("if (_roiDragHasAppliedRange)", rangeApply, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TryGetColumnSampleRange(imageWidth, out var currentRange)", rangeApply, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TryGetSelectedRoiRange(imageWidth, out currentRange)", rangeApply, StringComparison.Ordinal);
        Assert.Contains("currentRangeValid && currentRange == range", rangeApply, StringComparison.Ordinal);
        Assert.Contains("_roiDragHasAppliedRange = true;", rangeApply, StringComparison.Ordinal);
        Assert.Contains("ViewModel.UpdateColumnSampleRange(range.Start, range.EndInclusive, imageWidth);", rangeApply, StringComparison.Ordinal);
        Assert.Contains("ViewModel.UpdateSelectedRoiRange(range.Start, range.EndInclusive, imageWidth);", rangeApply, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_Roi_RangeValidationSeamsPreserveReleaseWithoutMoveAndRejectedResizeSemantics()
    {
        var releaseWithoutMoveRange = new ScanColumnRange(12, 12);

        Assert.True(ScanImageReferenceColumnRange.TryCreate(releaseWithoutMoveRange, 32).IsValid);
        Assert.Equal(releaseWithoutMoveRange, ScanImageReferenceColumnRange.TryCreate(releaseWithoutMoveRange, 32).Value!.ColumnRange);

        var invertedResizeRange = new ScanColumnRange(12, 8);
        var rejectedReference = ScanImageReferenceColumnRange.TryCreate(invertedResizeRange, 32);
        var rejectedAdc = ScanAdcCalibrationRoi.TryCreate(
            ScanCalibrationRoiSettings.CreateDefault() with { EffectiveRange = invertedResizeRange },
            32);

        Assert.Equal(ScanRoiValidationCode.Inverted, Assert.Single(rejectedReference.Issues).Code);
        Assert.Contains(rejectedAdc.Issues, issue => issue.Code == ScanRoiValidationCode.Inverted && issue.FieldPath == "EffectiveRange");
    }

    [Fact]
    public void Ui007_Roi_ScrollViewerPointerFallbackRoutesVisibleCanvasDragBeforePanning()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var scrollPressed = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)");
        var scrollMoved = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)");
        var scrollReleased = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)");
        var scrollCanceled = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewScrollViewer_PointerCanceled(object sender, PointerRoutedEventArgs e)");

        Assert.Contains("PreviewCanvasControl_PointerPressed(PreviewCanvasControl, e);", scrollPressed, StringComparison.Ordinal);
        Assert.Contains("if (e.Handled)", scrollPressed, StringComparison.Ordinal);
        Assert.True(
            scrollPressed.IndexOf("PreviewCanvasControl_PointerPressed(PreviewCanvasControl, e);", StringComparison.Ordinal)
                < scrollPressed.IndexOf("_isPanning = true;", StringComparison.Ordinal),
            "ROI drag should get first chance before ScrollViewer panning captures the left button.");
        Assert.Contains("if (_isRoiDragging)", scrollMoved, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl_PointerMoved(PreviewCanvasControl, e);", scrollMoved, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl_PointerReleased(PreviewCanvasControl, e);", scrollReleased, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl_PointerCanceled(PreviewCanvasControl, e);", scrollCanceled, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_Roi_SourceOnly_CaptureLostCancelsDragOrPanWithoutPhantomCommit()
    {
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml");
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var canvasCaptureLost = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewCanvasControl_PointerCaptureLost(object sender, PointerRoutedEventArgs e)");
        var scrollCaptureLost = ExtractMemberBodyAtDeclaration(codeBehind, "private void PreviewScrollViewer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)");
        var endLost = ExtractMemberBodyAtDeclaration(codeBehind, "private void EndRoiDragOrPanForPointer(PointerRoutedEventArgs e)");

        Assert.Contains("PointerCaptureLost=\"PreviewCanvasControl_PointerCaptureLost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PointerCaptureLost=\"PreviewScrollViewer_PointerCaptureLost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("EndRoiDragOrPanForPointer(e);", canvasCaptureLost, StringComparison.Ordinal);
        Assert.Contains("EndRoiDragOrPanForPointer(e);", scrollCaptureLost, StringComparison.Ordinal);
        Assert.Contains("if (_isRoiDragging && point == _activeRoiPointerId)", endLost, StringComparison.Ordinal);
        Assert.Contains("if (_isPanning && point == _activePanPointerId)", endLost, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSelectedRoiRange", endLost, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateColumnSampleRange", endLost, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_Roi_SourceOnly_WindowDeactivationCancelsPreviewInteractionBeforeAsyncLifecycleWork()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var loaded = ExtractMemberBodyAtDeclaration(codeBehind, "private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)");
        var unloaded = ExtractMemberBodyAtDeclaration(codeBehind, "private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)");
        var activated = ExtractMemberBodyAtDeclaration(codeBehind, "private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)");

        Assert.Contains("App.MainWindow.Activated -= MainWindow_Activated;", loaded, StringComparison.Ordinal);
        Assert.Contains("App.MainWindow.Activated += MainWindow_Activated;", loaded, StringComparison.Ordinal);
        Assert.True(
            loaded.IndexOf("App.MainWindow.Activated += MainWindow_Activated;", StringComparison.Ordinal)
                < loaded.IndexOf("await ViewModel.RefreshDeviceSettingsBindingsAsync();", StringComparison.Ordinal),
            "Window deactivation must be subscribed before the first awaited loaded operation can leave a held preview interaction stale.");

        Assert.Contains("App.MainWindow.Activated -= MainWindow_Activated;", unloaded, StringComparison.Ordinal);
        Assert.Contains("CancelPreviewVisualInteraction();", unloaded, StringComparison.Ordinal);
        Assert.True(
            unloaded.IndexOf("App.MainWindow.Activated -= MainWindow_Activated;", StringComparison.Ordinal)
                < unloaded.IndexOf("await ViewModel.DeactivateForPageAsync(pageOwner);", StringComparison.Ordinal),
            "Unloaded must unsubscribe before async teardown to avoid retaining the page through the main window event.");
        Assert.True(
            unloaded.IndexOf("CancelPreviewVisualInteraction();", StringComparison.Ordinal)
                < unloaded.IndexOf("await ViewModel.DeactivateForPageAsync(pageOwner);", StringComparison.Ordinal),
            "Preview interaction cancel is page-local state cleanup and must run before VM deactivation.");

        Assert.Contains("WindowActivationState.Deactivated", activated, StringComparison.Ordinal);
        Assert.Contains("CancelPreviewVisualInteraction();", activated, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_Roi_SourceOnly_ResponsivePreviewHideCancelsPreviewInteractionBeforeCollapse()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var owner = ExtractMemberBodyAtDeclaration(codeBehind, "private void UpdateWorkbenchPreviewLayout(double availableWidth)");

        Assert.Contains("if (!layout.IsPreviewVisible)", owner, StringComparison.Ordinal);
        Assert.Contains("CancelPreviewVisualInteraction();", owner, StringComparison.Ordinal);
        Assert.True(
            owner.IndexOf("CancelPreviewVisualInteraction();", StringComparison.Ordinal)
                < owner.IndexOf("WorkbenchPreviewColumnContent.Visibility = ToVisibility(layout.IsPreviewVisible);", StringComparison.Ordinal),
            "Responsive layout owner must clear held preview interactions before collapsing or hiding the preview column.");
    }

    [Fact]
    public void Ui007_Roi_SourceOnly_LifecycleCancelClearsPreviewStateBeforeReleasingCapturesWithoutVmMutation()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var cancel = ExtractMemberBodyAtDeclaration(codeBehind, "private void CancelPreviewVisualInteraction()");
        var clearRoi = ExtractMemberBodyAtDeclaration(codeBehind, "private void ClearRoiDragState()");
        var clearPan = ExtractMemberBodyAtDeclaration(codeBehind, "private void ClearPanState()");
        var endDrag = ExtractMemberBodyAtDeclaration(codeBehind, "private void EndRoiDrag(PointerRoutedEventArgs e)");
        var endPan = ExtractMemberBodyAtDeclaration(codeBehind, "private void EndPanning(PointerRoutedEventArgs e)");

        Assert.Contains("_isRoiDragging = false;", clearRoi, StringComparison.Ordinal);
        Assert.Contains("_isColumnSampleDrag = false;", clearRoi, StringComparison.Ordinal);
        Assert.Contains("_isPanning = false;", clearPan, StringComparison.Ordinal);
        Assert.Contains("_activePanPointerId = 0;", clearPan, StringComparison.Ordinal);

        Assert.Contains("ClearRoiDragState();", endDrag, StringComparison.Ordinal);
        Assert.Contains("ClearPanState();", endPan, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.ReleasePointerCapture(e.Pointer);", endDrag, StringComparison.Ordinal);
        Assert.Contains("PreviewScrollViewer.ReleasePointerCapture(e.Pointer);", endPan, StringComparison.Ordinal);
        Assert.True(
            endDrag.IndexOf("ClearRoiDragState();", StringComparison.Ordinal)
                < endDrag.IndexOf("PreviewCanvasControl.ReleasePointerCapture(e.Pointer);", StringComparison.Ordinal),
            "Pointer-specific ROI cleanup must clear page state before releasing capture because capture release can re-enter handlers.");
        Assert.True(
            endPan.IndexOf("ClearPanState();", StringComparison.Ordinal)
                < endPan.IndexOf("PreviewScrollViewer.ReleasePointerCapture(e.Pointer);", StringComparison.Ordinal),
            "Pointer-specific pan cleanup must clear page state before releasing capture because capture release can re-enter handlers.");

        Assert.Contains("ClearRoiDragState();", cancel, StringComparison.Ordinal);
        Assert.Contains("ClearPanState();", cancel, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.ReleasePointerCaptures();", cancel, StringComparison.Ordinal);
        Assert.Contains("PreviewScrollViewer.ReleasePointerCaptures();", cancel, StringComparison.Ordinal);
        Assert.True(
            cancel.IndexOf("ClearRoiDragState();", StringComparison.Ordinal)
                < cancel.IndexOf("PreviewCanvasControl.ReleasePointerCaptures();", StringComparison.Ordinal),
            "Lifecycle cancel must clear ROI drag state before releasing all canvas captures.");
        Assert.True(
            cancel.IndexOf("ClearPanState();", StringComparison.Ordinal)
                < cancel.IndexOf("PreviewScrollViewer.ReleasePointerCaptures();", StringComparison.Ordinal),
            "Lifecycle cancel must clear pan state before releasing all scroll viewer captures.");

        Assert.DoesNotContain("ViewModel.", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSelectedRoiRange", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateColumnSampleRange", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("DeactivateAsync", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", cancel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_RoiLayout_SourceOnly_EditorsWrapActionsAndKeepTechnicalPathsSecondary()
    {
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml");
        var adc = ExtractNamedRegion(xaml, "AdcRoiEditorCard", "PendingCalibrationCandidateReviewCard");
        var reference = ExtractNamedRegion(xaml, "ImageReferenceRoiEditorCard", "ChannelCalibrationIlluminationCard");
        var focus = ExtractNamedRegion(xaml, "FocusRoiEditorCard", "ScanDebug_ManualFocusTitle");
        var referenceColumnSampleTextBoxIndex = reference.IndexOf("ReferenceColumnSampleStartTextBox", StringComparison.Ordinal);

        Assert.True(referenceColumnSampleTextBoxIndex >= 0, "Reference editor column sample start input should remain in the reference editor card.");
        var referenceColumnSampleOwningGridStart = reference.LastIndexOf("<Grid ColumnSpacing=\"{StaticResource ScanDebugInlineSpacing}\"", referenceColumnSampleTextBoxIndex, StringComparison.Ordinal);
        var referenceColumnSampleOwningGridEnd = reference.IndexOf("ColumnSampleStatusText", referenceColumnSampleTextBoxIndex, StringComparison.Ordinal);
        Assert.True(referenceColumnSampleOwningGridStart >= 0, "Reference column sample input should be owned by the inline two-column grid.");
        Assert.True(referenceColumnSampleOwningGridEnd >= 0, "Reference column sample owning grid should precede the column sample status text.");
        var referenceColumnSampleOwningGrid = reference[referenceColumnSampleOwningGridStart..referenceColumnSampleOwningGridEnd];

        foreach (var editor in new[] { adc, focus })
        {
            Assert.Contains("Grid.ColumnSpan=\"2\"", editor, StringComparison.Ordinal);
            Assert.Contains("<controls:WrapPanel Grid.Row=\"3\"", editor, StringComparison.Ordinal);
            Assert.Contains("ContentTemplate=\"{StaticResource ScanDebugWrappingButtonContentTemplate}\"", editor, StringComparison.Ordinal);
            Assert.Contains("ToolTipService.ToolTip=\"{Binding TechnicalPath}\"", editor, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.HelpText=\"{Binding TechnicalPath}\"", editor, StringComparison.Ordinal);
            Assert.Contains("HorizontalContentAlignment=\"Stretch\"", editor, StringComparison.Ordinal);
            Assert.Contains("TextWrapping=\"Wrap\"", editor, StringComparison.Ordinal);
            Assert.DoesNotContain("Spacing=\"2\"", editor, StringComparison.Ordinal);
        }

        Assert.Contains("ToolTipService.ToolTip=\"{Binding TechnicalPath}\"", reference, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding TechnicalPath}\"", reference, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", reference, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", reference, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource ScanDebugWrappingButtonContentTemplate}\"", reference, StringComparison.Ordinal);
        Assert.Contains("HeaderTemplate=\"{StaticResource ScanDebugWrappingToggleHeaderTemplate}\"", reference, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(referenceColumnSampleOwningGrid, "<RowDefinition Height=\"Auto\"/>"));
        Assert.Contains("ReferenceColumnSampleStartTextBox", referenceColumnSampleOwningGrid, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"0\"", ExtractNamedRegion(referenceColumnSampleOwningGrid, "ReferenceColumnSampleStartTextBox", "ReferenceColumnSampleEndTextBox"), StringComparison.Ordinal);
        Assert.Contains("<ToggleSwitch Grid.Row=\"1\"", referenceColumnSampleOwningGrid, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Grid.Row=\"2\"", referenceColumnSampleOwningGrid, StringComparison.Ordinal);
        Assert.Contains("Grid.ColumnSpan=\"2\"", ExtractNamedRegion(reference, "ScanDebug_ColumnSampleEditToggleSwitch", "ScanDebug_ImageReferenceRoiEditorScopeText"), StringComparison.Ordinal);
        Assert.Contains("<TextBlock Grid.Row=\"2\"", ExtractNamedRegion(reference, "ScanDebug_ColumnSampleEditToggleSwitch", "ColumnSampleStatusText"), StringComparison.Ordinal);
    }

    [Fact]
    public void Todo22_SourceOnly_WorkbenchPreviewLayoutHasOneCodeBehindOwnerWithoutAdaptiveCompetition()
    {
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml");
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var split = ExtractNamedRegion(xaml, "WorkbenchContentSplitGrid", "BasicInfoSection");
        var owner = ExtractMemberBodyAtDeclaration(codeBehind, "private void UpdateWorkbenchPreviewLayout(double availableWidth)");

        Assert.DoesNotContain("<AdaptiveTrigger", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("VisualState.Setters", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchPreviewSplitMinimumWidth", codeBehind, StringComparison.Ordinal);
        Assert.Contains("<RowDefinition x:Name=\"WorkbenchEditorRow\"", split, StringComparison.Ordinal);
        Assert.Contains("Height=\"*\"/>", split, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchPreviewRow", split, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition x:Name=\"WorkbenchPreviewSeparatorColumn\"", split, StringComparison.Ordinal);
        Assert.Contains("ScanWorkbenchPreviewLayout.Calculate", owner, StringComparison.Ordinal);
        Assert.Contains("WorkbenchEditorColumn.Width = new GridLength(layout.EditorWidth);", owner, StringComparison.Ordinal);
        Assert.Contains("WorkbenchPreviewSeparatorColumn.Width = new GridLength(layout.SeparatorWidth);", owner, StringComparison.Ordinal);
        Assert.Contains("WorkbenchPreviewColumn.Width = new GridLength(layout.PreviewWidth);", owner, StringComparison.Ordinal);
        Assert.Contains("WorkbenchEditorColumnContent.Visibility = ToVisibility(layout.IsEditorVisible);", owner, StringComparison.Ordinal);
        Assert.Contains("WorkbenchPreviewColumnContent.Visibility = ToVisibility(layout.IsPreviewVisible);", owner, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_SourceOnly_RoiOverlayTextBoxAndCommandBindingsStayOnViewModelSurface()
    {
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml");
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var constructorDefaults = ExtractMemberBodyAtDeclaration(viewModel, "public ScanDebugViewModel(");
        var selectedChanged = ExtractMemberBodyAtDeclaration(viewModel, "partial void OnSelectedRoiSelectionChanged(string value)");
        var startChanged = ExtractMemberBodyAtDeclaration(viewModel, "partial void OnRoiStartInputChanged(string value)");
        var endChanged = ExtractMemberBodyAtDeclaration(viewModel, "partial void OnRoiEndInputChanged(string value)");
        var applyInputs = ExtractMemberBodyAtDeclaration(viewModel, "private void ApplySelectedRoiInputs()");
        var refreshInputs = ExtractMemberBodyAtDeclaration(viewModel, "private void RefreshRoiInputTexts()");
        var refreshStatus = ExtractMemberBodyAtDeclaration(viewModel, "private void RefreshRoiStatus()");

        foreach (var binding in new[]
        {
            "IsEnabled=\"{x:Bind ViewModel.CanEditRoiSelection, Mode=OneWay}\"",
            "IsOn=\"{x:Bind ViewModel.IsRoiEditModeEnabled, Mode=TwoWay}\"",
            "ItemsSource=\"{x:Bind ViewModel.AdcRoiSelectionOptions, Mode=OneWay}\"",
            "ItemsSource=\"{x:Bind ViewModel.FocusRoiSelectionOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedAdcRoiSelection, Mode=TwoWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedFocusRoiSelection, Mode=TwoWay}\"",
            "Command=\"{x:Bind ViewModel.ResetSelectedRoiCommand}\"",
            "Command=\"{x:Bind ViewModel.ResetAllRoisCommand}\"",
            "IsChecked=\"{x:Bind ViewModel.IsBwActiveRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsBwShieldRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsFocusOverallRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsFocusLeftRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsFocusRightRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsImageReferenceOverlayVisible, Mode=TwoWay}\"",
            "Text=\"{x:Bind ViewModel.RoiStatusText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.RoiStartInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.RoiEndInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Command=\"{x:Bind ViewModel.ApplySelectedRoiInputsCommand}\"",
            "Text=\"{x:Bind ViewModel.RoiInputStatusText, Mode=OneWay}\""
        })
        {
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
        }

        Assert.Contains("private static readonly string[] RoiSelectionLabels = { \"BW Active\", \"BW Shield\", \"Focus Overall\", \"Focus Left\", \"Focus Right\" };", viewModel, StringComparison.Ordinal);
        Assert.Contains("public ObservableCollection<string> RoiSelectionOptions { get; } = new(RoiSelectionLabels);", viewModel, StringComparison.Ordinal);
        Assert.Contains("SelectedAdcRoiSelection = AdcRoiSelectionOptions[0];", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("SelectedFocusRoiSelection = FocusRoiSelectionOptions[0];", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("SelectedRoiSelection = SelectedAdcRoiSelection;", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("IsBwActiveRoiOverlayVisible = true;", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RoiStartInput = \"0\";", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RoiEndInput = \"0\";", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiStatus();", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiInputTexts();", selectedChanged, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiStatus();", selectedChanged, StringComparison.Ordinal);
        Assert.Contains("SynchronizeOwnerRoiSelections(value);", selectedChanged, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiRangeChanged\".GetLocalized();", startChanged, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiRangeChanged\".GetLocalized();", endChanged, StringComparison.Ordinal);
        Assert.Contains("TryBuildRoiEditCandidate(out var candidate, out var issue)", applyInputs, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = FormatRoiEditIssue(issue);", applyInputs, StringComparison.Ordinal);
        Assert.Contains("_roiSettings = candidate;", applyInputs, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSelectedRoiRange", applyInputs, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiRangeApplied\".GetLocalized();", applyInputs, StringComparison.Ordinal);
        Assert.Contains("_isUpdatingRoiInputs = true;", refreshInputs, StringComparison.Ordinal);
        Assert.Contains("RoiStartInput = range.Start.ToString();", refreshInputs, StringComparison.Ordinal);
        Assert.Contains("RoiEndInput = range.EndInclusive.ToString();", refreshInputs, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiNumericMirror\".GetLocalizedFormat", refreshInputs, StringComparison.Ordinal);
        Assert.Contains("EnsureRoiEditModeAvailability();", refreshStatus, StringComparison.Ordinal);
        Assert.Contains("RoiOverlayVersion++;", refreshStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_SourceOnly_DialogFilePickerLifecycleAndPropertySyncBoundariesAreInventoried()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var imageService = ReadHostSource("PRISM Utility", "Services", "ScanChannelImageService.cs");
        var loaded = ExtractMemberBodyAtDeclaration(codeBehind, "private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)");
        var unloaded = ExtractMemberBodyAtDeclaration(codeBehind, "private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)");
        var subscribe = ExtractMemberBodyAtDeclaration(codeBehind, "private void SubscribeViewModelEvents()");
        var unsubscribe = ExtractMemberBodyAtDeclaration(codeBehind, "private void UnsubscribeViewModelEvents()");
        var propertyChanged = ExtractMemberBodyAtDeclaration(codeBehind, "private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)");
        var calibrationDialog = ExtractMemberBodyAtDeclaration(codeBehind, "private async void OnCalibrationPromptRequested(object? sender, ScanCalibrationPromptRequest e)");
        var noticeDialog = ExtractMemberBodyAtDeclaration(codeBehind, "private async void OnNoticeRequested(object? sender, ScanNoticeRequest e)");
        var showDialog = ExtractMemberBodyAtDeclaration(codeBehind, "private Task ShowPageDialogAsync(");

        Assert.Contains("SubscribeViewModelEvents();", loaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AttachRuntimeBindingsForPage(pageOwner)", loaded, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.RefreshDeviceSettingsBindingsAsync();", loaded, StringComparison.Ordinal);
        Assert.Contains("RefreshPreviewLayout();", loaded, StringComparison.Ordinal);
        Assert.Contains("UnsubscribeViewModelEvents();", unloaded, StringComparison.Ordinal);
        Assert.Contains("DisposePreviewBitmap();", unloaded, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.DeactivateForPageAsync(pageOwner);", unloaded, StringComparison.Ordinal);
        Assert.Contains("if (_areViewModelEventsSubscribed)", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged += OnViewModelPropertyChanged;", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CalibrationPromptRequested += OnCalibrationPromptRequested;", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.NoticeRequested += OnNoticeRequested;", subscribe, StringComparison.Ordinal);
        Assert.Contains("if (!_areViewModelEventsSubscribed)", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged -= OnViewModelPropertyChanged;", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CalibrationPromptRequested -= OnCalibrationPromptRequested;", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.NoticeRequested -= OnNoticeRequested;", unsubscribe, StringComparison.Ordinal);

        Assert.Contains("nameof(ScanDebugViewModel.PreviewFrame)", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("RefreshPreviewLayout();", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.Invalidate();", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("nameof(ScanDebugViewModel.RoiOverlayVersion)", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("nameof(ScanDebugViewModel.ColumnSampleOverlayVersion)", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("DrawRoiOverlays", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("RefreshCurrentCalibrationIlluminationEditor", propertyChanged, StringComparison.Ordinal);

        Assert.Contains("new ContentDialog", calibrationDialog, StringComparison.Ordinal);
        Assert.Contains("XamlRoot = XamlRoot", calibrationDialog, StringComparison.Ordinal);
        Assert.Contains("DefaultButton = ContentDialogButton.Primary", calibrationDialog, StringComparison.Ordinal);
        Assert.Contains("e.HostCancellationToken", calibrationDialog, StringComparison.Ordinal);
        Assert.Contains("result => e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary)", calibrationDialog, StringComparison.Ordinal);
        Assert.Contains("() => e.CompletionSource.TrySetResult(false)", calibrationDialog, StringComparison.Ordinal);
        Assert.Contains("new ContentDialog", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("XamlRoot = XamlRoot", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("DefaultButton = ContentDialogButton.Close", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("e.HostCancellationToken", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("_ => e.CompletionSource.TrySetResult()", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("Action completeOnPageRetirement = () => e.CompletionSource.TrySetResult();", noticeDialog, StringComparison.Ordinal);
        Assert.Matches(@"if \(e\.HostCancellationToken\.IsCancellationRequested\)\s+e\.CompletionSource\.TrySetCanceled\(e\.HostCancellationToken\);\s+else\s+completeOnPageRetirement\(\);", noticeDialog);
        Assert.Contains("_dialogLifetime.Retire(this, unloadEpoch - 1);", unloaded, StringComparison.Ordinal);
        Assert.True(unloaded.IndexOf("_dialogLifetime.Retire(this, unloadEpoch - 1);", StringComparison.Ordinal)
            < unloaded.IndexOf("await PrismVisualQaCaptureService.StopAsync(this);", StringComparison.Ordinal));
        Assert.Contains("_dialogLifetime.TryAcquire(this, activationEpoch", showDialog, StringComparison.Ordinal);
        Assert.Contains("dialog.Hide();", showDialog, StringComparison.Ordinal);
        Assert.Contains("dispatcher.TryEnqueue(", showDialog, StringComparison.Ordinal);
        Assert.Contains("Dialog hide dispatch was rejected", showDialog, StringComparison.Ordinal);
        Assert.DoesNotContain("EnqueueForCurrentActivation", showDialog, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(_pageActivationOwner, pageOwner)", showDialog, StringComparison.Ordinal);
        Assert.True(showDialog.IndexOf("!IsCurrentActivation(activationEpoch)", StringComparison.Ordinal)
            < showDialog.IndexOf("var dialog = createDialog();", StringComparison.Ordinal));
        Assert.Contains("return lease.RunAsync(hostCancellationToken", showDialog, StringComparison.Ordinal);

        Assert.DoesNotContain("PickDngExportFolderAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_channelImages.PickDngExportFolderAsync()", viewModel, StringComparison.Ordinal);
        Assert.Contains("FileSavePicker", imageService, StringComparison.Ordinal);
        Assert.Contains("InitializeWithWindow.Initialize", imageService, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-10, 3, 8, 0, 3)]
    [InlineData(6, 2, 8, 2, 6)]
    [InlineData(9, 20, 8, 7, 7)]
    [InlineData(2, 2, 3, 2, 2)]
    [InlineData(5, 5, 3, 2, 2)]
    public void Ui007_Roi_ScanColumnRangeClampDocumentsInvalidAndMinimumSpanCases(int start, int endInclusive, int width, int expectedStart, int expectedEndInclusive)
    {
        var range = new ScanColumnRange(start, endInclusive).Clamp(width);

        Assert.Equal(new ScanColumnRange(expectedStart, expectedEndInclusive), range);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Ui007_Roi_ScanColumnRangeClampDocumentsZeroOrNegativeWidthAsEmptyRange(int width)
    {
        var range = new ScanColumnRange(20, 10).Clamp(width);

        Assert.Equal(0, range.Start);
        Assert.Equal(-1, range.EndInclusive);
        Assert.Equal(0, range.Width);
    }

    [Fact]
    public void Ui007_Roi_CalibrationRoiClampDocumentsOverlappingAndZeroWidthEdgeCases()
    {
        var zeroWidth = ScanCalibrationRoiSettings.CreateDefault().Clamp(0);
        Assert.Equal(new ScanColumnRange(0, -1), zeroWidth.EffectiveRange);
        Assert.Equal(new ScanColumnRange(0, -1), zeroWidth.ShieldRange);
        Assert.Equal(0, zeroWidth.EffectiveRange.Width);

        var overlapping = new ScanCalibrationRoiSettings(
            new ScanColumnRange(10, 30),
            new ScanColumnRange(0, 8),
            new ScanColumnRange(12, 25),
            new ScanColumnRange(14, 28),
            new ScanColumnRange(15, 20));

        var clamped = overlapping.Clamp(40);
        Assert.Equal(new ScanColumnRange(10, 30), clamped.EffectiveRange);
        Assert.Equal(new ScanColumnRange(0, 8), clamped.ShieldRange);
        Assert.True(clamped.FocusLeftRange.Start >= clamped.EffectiveRange.Start);
        Assert.True(clamped.FocusRightRange.EndInclusive <= clamped.EffectiveRange.EndInclusive);
        Assert.True(clamped.FocusLeftRange.EndInclusive < clamped.FocusRightRange.Start);
        Assert.True(clamped.FocusOverallRange.Start <= clamped.FocusLeftRange.Start);
        Assert.True(clamped.FocusOverallRange.EndInclusive >= clamped.FocusRightRange.EndInclusive);
    }

    [Fact]
    public void Ui007_SourceOnly_NoHardwareServiceCallOrUiGeometryExtractionExistsYet()
    {
        var codeBehind = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var app = ReadHostSource("PRISM Utility", "App.xaml.cs");
        var productionSources = EnumerateProductionSources().ToArray();

        foreach (var forbidden in new[]
        {
            "IScanSessionService",
            "IScanWorkflowService",
            "IScanAutoCalibrationService",
            "IScanAutoFocusService",
            "IScanIlluminationService",
            "IScanChannelImageService",
            "IScanParameterService",
            "PickDngExportFolderAsync",
            "ExportDngChannelsAsync",
            "ExportMonochromeDngAsync"
        })
        {
            Assert.DoesNotContain(forbidden, codeBehind, StringComparison.Ordinal);
        }

        foreach (var forbidden in new[]
        {
            "IScanDebugGeometryHelper",
            "ScanDebugGeometryHelper",
            "IScanDebugDisplayState",
            "ScanDebugDisplayState",
            "IScanDebugCanvasPresenter",
            "ScanDebugCanvasPresenter",
            "IScanDebugRoiPresenter",
            "ScanDebugRoiPresenter",
            "ScanDebugGeometryPresenter",
            "ScanDebugPreviewGeometry",
            "ScanDebugRoiGeometry"
        })
        {
            Assert.DoesNotContain(productionSources, source => source.Content.Contains(forbidden, StringComparison.Ordinal));
            Assert.DoesNotContain(forbidden, app, StringComparison.Ordinal);
        }

        Assert.Contains("services.AddSingleton<ScanDebugViewModel>()", app, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<ScanDebugPage>()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui007_SourceOnly_BlockerRegistryAndTask21ContractStayExplicitlyBlocked()
    {
        var task18 = ReadRootSource(".omo", "evidence", "task-18-design-correction-repairs.txt");
        var task19 = ReadRootSource(".omo", "evidence", "task-19-design-correction-repairs.txt");
        var plan = ReadRootSource(".omo", "plans", "design-correction-repairs.md");
        var issues = ReadHostSource("docs", "architecture", "issues-and-remediation.md");
        var priorities = ReadHostSource("docs", "architecture", "design-correction-priorities.md");

        Assert.Contains("Status: EXTRACTION_BLOCKED", task18, StringComparison.Ordinal);
        Assert.Contains("Status: EXTRACTION_BLOCKED", task19, StringComparison.Ordinal);
        Assert.Contains("Todo 20 UI-007 remains blocked by todo 19", task19, StringComparison.Ordinal);
        Assert.Contains("If the hard gate is blocked, add characterization only and mark extraction blocked", plan, StringComparison.Ordinal);
        Assert.Contains("Evidence `.omo/evidence/task-20-design-correction-repairs.txt`", plan, StringComparison.Ordinal);
        Assert.Contains("在 gate 解除前，UI-007 只能补 Canvas/ROI/display/pointer/overlay/dialog/text sync characterization，不能执行 production geometry/display extraction", issues, StringComparison.Ordinal);
        Assert.Contains("gate 解除前只补 Canvas/ROI/display/pointer/overlay/dialog/text sync characterization；不迁出 Page 逻辑", priorities, StringComparison.Ordinal);
        Assert.Contains("21. `VM-002` split ScanDebug responsibilities after UI extraction", plan, StringComparison.Ordinal);
        Assert.Contains("If the hard gate or todo 20 blocks extraction, add characterization only and mark split blocked", plan, StringComparison.Ordinal);
    }

    private static IEnumerable<(string Path, string Content)> EnumerateProductionSources()
    {
        foreach (var root in new[] { "PRISM Utility", "PRISM Utility.Core" })
        {
            var fullRoot = Path.Combine(FindHostSoftwareRoot(), root);
            foreach (var file in Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
                yield return (file, File.ReadAllText(file));
        }
    }

    private static string ReadHostSource(params string[] path)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), Path.Combine(path)));

    private static string ReadRootSource(params string[] path)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string ExtractMemberBodyAtDeclaration(string source, string declaration)
    {
        var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(declarationIndex >= 0, $"Could not find declaration: {declaration}");

        var bodyStart = source.IndexOf('{', declarationIndex);
        Assert.True(bodyStart >= 0, $"Could not find body for declaration: {declaration}");
        return ExtractBodyFromOpeningBrace(source, bodyStart, declaration);
    }

    private static string ExtractBodyFromOpeningBrace(string source, int bodyStart, string context)
    {
        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not extract body for: {context}");
    }

    private static string ExtractNamedRegion(string source, string startName, string endName)
    {
        var start = source.IndexOf(startName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find region start: {startName}");
        var end = source.IndexOf(endName, start + startName.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find region end after {startName}: {endName}");
        return source[start..end];
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".omo")) && Directory.Exists(Path.Combine(directory.FullName, "Host Software")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
