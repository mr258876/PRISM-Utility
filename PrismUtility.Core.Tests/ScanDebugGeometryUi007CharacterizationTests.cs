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
        var drawOverlays = ExtractMemberBodyAtDeclaration(codeBehind, "private void DrawRoiOverlays()");
        var endDrag = ExtractMemberBodyAtDeclaration(codeBehind, "private void EndRoiDrag()");
        var drawAxes = ExtractMemberBodyAtDeclaration(codeBehind, "private void DrawAxes(int imageWidth, int imageHeight)");

        Assert.Contains("if ((!ViewModel.CanMutateRoiFromPreview && !ViewModel.CanMutateColumnSampleFromPreview) || ViewModel.PreviewFrame is null)", pointerPressed, StringComparison.Ordinal);
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
        Assert.Contains("ViewModel.ShiftColumnSampleRange(x - _roiDragStartX, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("ViewModel.ShiftSelectedRoiRange(x - _roiDragStartX, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("ViewModel.UpdateColumnSampleRange(_roiDragStartX, x, bitmap.Width);", pointerMoved, StringComparison.Ordinal);
        Assert.Contains("ViewModel.UpdateSelectedRoiRange(_roiDragStartX, x, bitmap.Width);", pointerMoved, StringComparison.Ordinal);

        Assert.Contains("if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("foreach (var overlay in ViewModel.GetPreviewRoiOverlays(bitmap.Width))", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Width = overlay.Range.Width", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Height = bitmap.Height", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("StrokeThickness = overlay.IsSelected ? 2.5 : 1.5", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetLeft(rectangle, AxisMarginLeft + overlay.Range.Start);", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetTop(rectangle, AxisMarginTop);", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("Canvas.SetLeft(label, AxisMarginLeft + overlay.Range.Start + 4);", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TryGetColumnSampleRange(bitmap.Width, out var sampleRange)", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("StrokeDashArray = new DoubleCollection { 4, 3 }", drawOverlays, StringComparison.Ordinal);
        Assert.Contains("_isRoiDragging = false;", endDrag, StringComparison.Ordinal);
        Assert.Contains("PreviewCanvasControl.ReleasePointerCaptures();", endDrag, StringComparison.Ordinal);
        Assert.Contains("var xStep = GetTickStep(imageWidth);", drawAxes, StringComparison.Ordinal);
        Assert.Contains("var yStep = GetTickStep(imageHeight);", drawAxes, StringComparison.Ordinal);
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
            "ItemsSource=\"{x:Bind ViewModel.RoiSelectionOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedRoiSelection, Mode=TwoWay}\"",
            "Command=\"{x:Bind ViewModel.ResetSelectedRoiCommand}\"",
            "Command=\"{x:Bind ViewModel.ResetAllRoisCommand}\"",
            "IsChecked=\"{x:Bind ViewModel.IsBwActiveRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsBwShieldRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsFocusOverallRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsFocusLeftRoiOverlayVisible, Mode=TwoWay}\"",
            "IsChecked=\"{x:Bind ViewModel.IsFocusRightRoiOverlayVisible, Mode=TwoWay}\"",
            "Text=\"{x:Bind ViewModel.RoiStatusText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.RoiStartInput, Mode=TwoWay}\"",
            "Text=\"{x:Bind ViewModel.RoiEndInput, Mode=TwoWay}\"",
            "Command=\"{x:Bind ViewModel.ApplySelectedRoiInputsCommand}\"",
            "Text=\"{x:Bind ViewModel.RoiInputStatusText, Mode=OneWay}\""
        })
        {
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
        }

        Assert.Contains("private static readonly string[] RoiSelectionLabels = { \"BW Active\", \"BW Shield\", \"Focus Overall\", \"Focus Left\", \"Focus Right\" };", viewModel, StringComparison.Ordinal);
        Assert.Contains("public ObservableCollection<string> RoiSelectionOptions { get; } = new(RoiSelectionLabels);", viewModel, StringComparison.Ordinal);
        Assert.Contains("SelectedRoiSelection = RoiSelectionOptions[0];", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("IsBwActiveRoiOverlayVisible = true;", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RoiStartInput = \"0\";", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RoiEndInput = \"0\";", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiStatus();", constructorDefaults, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiInputTexts();", selectedChanged, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiStatus();", selectedChanged, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiRangeChanged\".GetLocalized();", startChanged, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiRangeChanged\".GetLocalized();", endChanged, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiStartIntegerRequired\".GetLocalized();", applyInputs, StringComparison.Ordinal);
        Assert.Contains("RoiInputStatusText = \"ScanDebug_Runtime_RoiEndIntegerRequired\".GetLocalized();", applyInputs, StringComparison.Ordinal);
        Assert.Contains("UpdateSelectedRoiRange(start, endInclusive, GetRoiEditingWidth());", applyInputs, StringComparison.Ordinal);
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

        Assert.Contains("SubscribeViewModelEvents();", loaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AttachRuntimeBindings();", loaded, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.RefreshDeviceSettingsBindingsAsync();", loaded, StringComparison.Ordinal);
        Assert.Contains("RefreshPreviewLayout();", loaded, StringComparison.Ordinal);
        Assert.Contains("UnsubscribeViewModelEvents();", unloaded, StringComparison.Ordinal);
        Assert.Contains("DisposePreviewBitmap();", unloaded, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.DeactivateAsync();", unloaded, StringComparison.Ordinal);
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
        Assert.Contains("e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary);", calibrationDialog, StringComparison.Ordinal);
        Assert.Contains("new ContentDialog", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("XamlRoot = XamlRoot", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("DefaultButton = ContentDialogButton.Close", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("e.CompletionSource.TrySetResult();", noticeDialog, StringComparison.Ordinal);

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

        Assert.Contains("services.AddTransient<ScanDebugViewModel>()", app, StringComparison.Ordinal);
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
