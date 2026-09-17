using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.ViewModels;
using Windows.UI;
using Windows.Foundation;
using Windows.Graphics.DirectX;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage : Page, IPageViewModelHost<ScanDebugViewModel>
{
    private const double AxisMarginLeft = 48;
    private const double AxisMarginTop = 28;
    private const double AxisMarginRight = 16;
    private const double AxisMarginBottom = 36;
    private static readonly float[] ZoomLevels = { 0.1f, 0.125f, 0.2f, 0.25f, 0.5f, 0.75f, 1f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 20f };
    private const string RoiSelectionBwActive = "BW Active";
    private const string RoiSelectionBwShield = "BW Shield";
    private const string RoiSelectionFocusOverall = "Focus Overall";
    private const string RoiSelectionFocusLeft = "Focus Left";
    private const string RoiSelectionFocusRight = "Focus Right";
    private bool _updatingZoomScaleComboBox;
    private bool _pendingInitialFitZoom;
    private int _lastPreviewImageWidth = -1;
    private int _lastPreviewImageHeight = -1;
    private CanvasBitmap? _previewBitmap;
    private int _previewBitmapVersion = -1;
    private int _previewBitmapWidth = -1;
    private int _previewBitmapHeight = -1;
    private bool _isPanning;
    private uint _activePanPointerId;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private bool _isRoiDragging;
    private bool _isColumnSampleDrag;
    private bool _isRoiMoveMode;
    private uint _activeRoiPointerId;
    private int _roiDragStartX;
    private ScanColumnRange _roiOriginalRange = new(0, 0);
    private int _roiDragImageWidth;
    private int _roiDragFrameVersion = -1;
    private string _roiDragSelection = string.Empty;
    private string _roiDragCalibrationChannel = string.Empty;
    private bool _roiDragHasAppliedRange;
    private bool _areViewModelEventsSubscribed;
    private bool _isUpdatingCurrentCalibrationIlluminationEditor;
    private bool _isSynchronizingWorkbenchSection;
    private int _activeWorkbenchSectionIndex = 0;
    private bool _isNarrowPreviewOpen;
    private double _workbenchPreviewEditorRatio = ScanWorkbenchPreviewLayout.DefaultEditorRatio;
    private ScanWorkbenchPreviewLayoutMode _workbenchPreviewLayoutMode = ScanWorkbenchPreviewLayoutMode.NarrowEditor;
#if PRISM_VISUAL_QA
    private int _visualQaPreviewScrollPressedCount;
    private int _visualQaPreviewScrollMovedCount;
    private int _visualQaPreviewCanvasPressedCount;
    private int _visualQaPreviewCanvasMovedCount;
    private int _visualQaPreviewCanvasReleasedCount;
    private string _visualQaLastPointer = string.Empty;
    private string _visualQaLastPan = string.Empty;
#endif

    public ScanDebugViewModel ViewModel
    {
        get;
    }

    public ScanDebugPage()
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        ViewModel = App.GetService<ScanDebugViewModel>();
        NavigationTimingLogger.Write($"ScanDebugPage.ctor GetService<ScanDebugViewModel>={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        InitializeComponent();
        SetActiveWorkbenchSection(_activeWorkbenchSectionIndex);
        InitializeCurrentCalibrationIlluminationEditor();
        NavigationTimingLogger.Write($"ScanDebugPage.ctor InitializeComponent={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanDebugPage.ctor total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void DeferredExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.Tag is string contentName)
            FindName(contentName);
    }

    private void WorkbenchSectionSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())
            return;

        SetActiveWorkbenchSection(GetWorkbenchSectionSelectorIndex(sender.SelectedItem));
    }

    private void WorkbenchSectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())
            return;

        SetActiveWorkbenchSection(WorkbenchSectionComboBox.SelectedIndex);
    }

    private void ScanDebugRootGrid_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (!IsWorkbenchSectionUiReady())
            return;

        _ = DispatcherQueue.TryEnqueue(() => UpdateWorkbenchPreviewLayout(e.NewSize.Width));
    }

    private void UpdateWorkbenchPreviewLayout()
        => UpdateWorkbenchPreviewLayout(ScanDebugRootGrid.ActualWidth);

    private void UpdateWorkbenchPreviewLayout(double availableWidth)
    {
        if (!IsWorkbenchSectionUiReady())
            return;

        if (!double.IsFinite(availableWidth) || availableWidth < 0)
            availableWidth = 0;

        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(availableWidth, HasValidPreviewFrame(), _isNarrowPreviewOpen, _workbenchPreviewEditorRatio));
        _workbenchPreviewLayoutMode = layout.Mode;
        if (!layout.IsPreviewVisible)
            CancelPreviewVisualInteraction();

        var isWideSelectorAvailable = layout.Mode is ScanWorkbenchPreviewLayoutMode.WideCompact or ScanWorkbenchPreviewLayoutMode.WideImage;
        WorkbenchSectionSelectorBar.Visibility = isWideSelectorAvailable ? Visibility.Visible : Visibility.Collapsed;
        WorkbenchSectionComboBox.Visibility = isWideSelectorAvailable ? Visibility.Collapsed : Visibility.Visible;
        WorkbenchEditorColumn.Width = new GridLength(layout.EditorWidth);
        WorkbenchPreviewSeparatorColumn.Width = new GridLength(layout.SeparatorWidth);
        WorkbenchPreviewColumn.Width = new GridLength(layout.PreviewWidth);
        WorkbenchEditorRow.Height = new GridLength(1, GridUnitType.Star);
        WorkbenchEditorColumnContent.Visibility = ToVisibility(layout.IsEditorVisible);
        WorkbenchPreviewColumnContent.Visibility = ToVisibility(layout.IsPreviewVisible);
        OpenPreviewButton.Visibility = ToVisibility(layout.IsPreviewOpenButtonVisible);
        BackToEditorButton.Visibility = ToVisibility(layout.IsBackToEditorButtonVisible);
        PreviewSplitter.Visibility = ToVisibility(layout.IsSplitterVisible);
        PreviewSplitter.IsTabStop = layout.IsSplitterVisible;
        WorkbenchPreviewColumnContent.VerticalAlignment = layout.Mode == ScanWorkbenchPreviewLayoutMode.WideCompact ? VerticalAlignment.Top : VerticalAlignment.Stretch;
    }

    private bool HasValidPreviewFrame()
        => ViewModel.PreviewFrame is { Width: > 0, Height: > 0 };

    private static Visibility ToVisibility(bool isVisible)
        => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private void OpenPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        _isNarrowPreviewOpen = true;
        UpdateWorkbenchPreviewLayout();
        _ = DispatcherQueue.TryEnqueue(() => BackToEditorButton.Focus(FocusState.Programmatic));
    }

    private void BackToEditorButton_Click(object sender, RoutedEventArgs e)
    {
        _isNarrowPreviewOpen = false;
        UpdateWorkbenchPreviewLayout();
        _ = DispatcherQueue.TryEnqueue(() => WorkbenchSectionComboBox.Focus(FocusState.Programmatic));
    }

    private void PreviewSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        => ResizeWidePreviewSplit(e.HorizontalChange);

    private void PreviewSplitter_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Left && e.Key != Windows.System.VirtualKey.Right)
            return;

        var delta = e.Key == Windows.System.VirtualKey.Left
            ? -ScanWorkbenchPreviewLayout.KeyboardResizeDelta
            : ScanWorkbenchPreviewLayout.KeyboardResizeDelta;
        ResizeWidePreviewSplit(delta);
        e.Handled = true;
    }

    private void ResizeWidePreviewSplit(double editorDelta)
    {
        if (_workbenchPreviewLayoutMode != ScanWorkbenchPreviewLayoutMode.WideImage || PreviewSplitter.Visibility != Visibility.Visible)
            return;

        var split = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(new ScanWorkbenchPreviewResizeInput(ScanDebugRootGrid.ActualWidth, _workbenchPreviewEditorRatio, editorDelta));
        _workbenchPreviewEditorRatio = split.EditorRatio;
        UpdateWorkbenchPreviewLayout();
    }

    private bool IsWorkbenchSectionUiReady()
        => WorkbenchSectionSelectorBar is not null
            && WorkbenchSectionComboBox is not null
            && WorkbenchEditorColumn is not null
            && WorkbenchEditorColumnContent is not null
            && WorkbenchPreviewSeparatorColumn is not null
            && WorkbenchPreviewColumn is not null
            && WorkbenchEditorRow is not null
            && WorkbenchPreviewColumnContent is not null
            && OpenPreviewButton is not null
            && BackToEditorButton is not null
            && PreviewSplitter is not null
            && BasicInfoSection is not null
            && AcquisitionPlanSection is not null
            && ChannelCalibrationSection is not null
            && LiveCalibrationSection is not null
            && DeviceSettingsSection is not null
            && EngineeringToolsSection is not null;

    private int GetWorkbenchSectionSelectorIndex(SelectorBarItem? selectedItem)
    {
        for (var index = 0; index < WorkbenchSectionSelectorBar.Items.Count; index++)
        {
            if (ReferenceEquals(WorkbenchSectionSelectorBar.Items[index], selectedItem))
                return index;
        }

        return -1;
    }

    private void SetActiveWorkbenchSection(int index)
    {
        if ((uint)index >= 6)
        {
            SynchronizeWorkbenchSectionSelectors(_activeWorkbenchSectionIndex);
            return;
        }

        _activeWorkbenchSectionIndex = index;
        _isNarrowPreviewOpen = false;
        _isSynchronizingWorkbenchSection = true;
        try
        {
            BasicInfoSection.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            AcquisitionPlanSection.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            ChannelCalibrationSection.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            LiveCalibrationSection.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            DeviceSettingsSection.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
            EngineeringToolsSection.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
            SynchronizeWorkbenchSectionSelectors(index);
            UpdateWorkbenchPreviewLayout();
        }
        finally
        {
            _isSynchronizingWorkbenchSection = false;
        }
    }

    private void SynchronizeWorkbenchSectionSelectors(int index)
    {
        if ((uint)index >= 6)
            return;

        if (!ReferenceEquals(WorkbenchSectionSelectorBar.SelectedItem, WorkbenchSectionSelectorBar.Items[index]))
            WorkbenchSectionSelectorBar.SelectedItem = WorkbenchSectionSelectorBar.Items[index];

        if (WorkbenchSectionComboBox.SelectedIndex != index)
            WorkbenchSectionComboBox.SelectedIndex = index;
    }

    private void InitializeCurrentCalibrationIlluminationEditor()
    {
        CurrentCalibrationIlluminationWorkModeComboBox.ItemsSource = ViewModel.CurrentCalibrationIlluminationWorkModeOptions;
        RefreshCurrentCalibrationIlluminationEditor();
    }

    private void RefreshCurrentCalibrationIlluminationEditor()
    {
        _isUpdatingCurrentCalibrationIlluminationEditor = true;
        try
        {
            var isEnabled = ViewModel.HasCurrentCalibrationIlluminationChannel;
            CurrentCalibrationIlluminationLevelTextBox.IsEnabled = isEnabled;
            CurrentCalibrationIlluminationPulseClockTextBox.IsEnabled = isEnabled;
            CurrentCalibrationIlluminationWorkModeComboBox.IsEnabled = isEnabled;
            SetTextBoxText(CurrentCalibrationIlluminationLevelTextBox, ViewModel.CurrentCalibrationIlluminationLevel);
            SetTextBoxText(CurrentCalibrationIlluminationPulseClockTextBox, ViewModel.CurrentCalibrationIlluminationPulseClock);
            CurrentCalibrationIlluminationWorkModeComboBox.SelectedItem = ViewModel.CurrentCalibrationIlluminationWorkMode;
        }
        finally
        {
            _isUpdatingCurrentCalibrationIlluminationEditor = false;
        }
    }

    private static void SetTextBoxText(TextBox textBox, string value)
    {
        if (!string.Equals(textBox.Text, value, StringComparison.Ordinal))
            textBox.Text = value;
    }

    private void CurrentCalibrationIlluminationLevelTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingCurrentCalibrationIlluminationEditor)
            ViewModel.CurrentCalibrationIlluminationLevel = CurrentCalibrationIlluminationLevelTextBox.Text;
    }

    private void CurrentCalibrationIlluminationPulseClockTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingCurrentCalibrationIlluminationEditor)
            ViewModel.CurrentCalibrationIlluminationPulseClock = CurrentCalibrationIlluminationPulseClockTextBox.Text;
    }

    private void CurrentCalibrationIlluminationWorkModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingCurrentCalibrationIlluminationEditor && CurrentCalibrationIlluminationWorkModeComboBox.SelectedItem is string workMode)
            ViewModel.CurrentCalibrationIlluminationWorkMode = workMode;
    }

    private void PreviewDisplayToolsFlyout_Opening(object sender, object e)
    {
        FindName("PreviewDisplayToolsContent");
        ConfigureLocalFlyoutBounds(PreviewDisplayToolsFlyout, PreviewDisplayToolsScrollViewer, PreviewDisplayToolsContent);
    }

    private void OverlayToolsFlyout_Opening(object sender, object e)
    {
        FindName("OverlayToolsContent");
        ConfigureLocalFlyoutBounds(OverlayToolsFlyout, OverlayToolsScrollViewer, OverlayToolsContent);
    }

    private void ConfigureLocalFlyoutBounds(Flyout flyout, ScrollViewer scrollViewer, FrameworkElement content)
    {
        const double targetFlyoutWidth = 420;
        const double edgePadding = 24;
        var rootSize = XamlRoot?.Size ?? new Size(targetFlyoutWidth, 520);
        var width = Math.Max(0, Math.Min(targetFlyoutWidth, rootSize.Width - edgePadding));
        var height = Math.Max(0, rootSize.Height - edgePadding);

        var boundedPresenterStyle = new Style(typeof(FlyoutPresenter))
        {
            BasedOn = (Style)Resources["ScanDebugLocalFlyoutPresenterBaseStyle"]
        };
        boundedPresenterStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
        boundedPresenterStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, width));
        boundedPresenterStyle.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, height));
        flyout.FlyoutPresenterStyle = boundedPresenterStyle;

        scrollViewer.MaxHeight = height;
        content.ClearValue(FrameworkElement.WidthProperty);
        content.ClearValue(FrameworkElement.MaxWidthProperty);
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        SubscribeViewModelEvents();
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded SubscribeViewModelEvents={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        ViewModel.AttachRuntimeBindings();
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded AttachRuntimeBindings={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        App.MainWindow.Activated -= MainWindow_Activated;
        App.MainWindow.Activated += MainWindow_Activated;

        stepStopwatch.Restart();
        await ViewModel.RefreshDeviceSettingsBindingsAsync();
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded RefreshDeviceSettingsBindingsAsync={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        InitializeZoomScaleComboBox();
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded InitializeZoomScaleComboBox={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        RefreshPreviewLayout();
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded RefreshPreviewLayout={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

#if PRISM_VISUAL_QA
        PrismVisualQaPendingCalibrationHook.ApplyPageState(this, SetActiveWorkbenchSection);
#endif

        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        App.MainWindow.Activated -= MainWindow_Activated;
        CancelPreviewVisualInteraction();
#if PRISM_VISUAL_QA
        await PrismVisualQaCaptureService.StopAsync(this);
#endif
        UnsubscribeViewModelEvents();
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded UnsubscribeViewModelEvents={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        DisposePreviewBitmap();
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded DisposePreviewBitmap={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        await ViewModel.DeactivateAsync();
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded DeactivateAsync={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
            CancelPreviewVisualInteraction();
    }

    private void SubscribeViewModelEvents()
    {
        if (_areViewModelEventsSubscribed)
            return;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.CalibrationPromptRequested += OnCalibrationPromptRequested;
        ViewModel.FilmProfileDiscardConfirmationRequested += OnFilmProfileDiscardConfirmationRequested;
        ViewModel.NoticeRequested += OnNoticeRequested;
        ViewModel.RoiIssueNavigationRequested += OnRoiIssueNavigationRequested;
        _areViewModelEventsSubscribed = true;
    }

    private void UnsubscribeViewModelEvents()
    {
        if (!_areViewModelEventsSubscribed)
            return;

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.CalibrationPromptRequested -= OnCalibrationPromptRequested;
        ViewModel.FilmProfileDiscardConfirmationRequested -= OnFilmProfileDiscardConfirmationRequested;
        ViewModel.NoticeRequested -= OnNoticeRequested;
        ViewModel.RoiIssueNavigationRequested -= OnRoiIssueNavigationRequested;
        _areViewModelEventsSubscribed = false;
    }

    private void OnRoiIssueNavigationRequested(ScanRoiIssueNavigationRequest request)
    {
        if (!TryResolveRoiNavigationTarget(request, out var sectionIndex, out var scroller, out var target))
            return;

        SetActiveWorkbenchSection(sectionIndex);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            target.StartBringIntoView();
            _ = scroller.ChangeView(null, Math.Max(0, target.ActualOffset.Y - 24), null, true);
            _ = target.Focus(FocusState.Programmatic);
            if (target is TextBox textBox)
                textBox.SelectAll();
        });
    }

    private bool TryResolveRoiNavigationTarget(ScanRoiIssueNavigationRequest request, out int sectionIndex, out ScrollViewer scroller, out Control target)
    {
        sectionIndex = -1;
        scroller = null!;
        target = null!;

        switch (request.Target)
        {
            case ScanRoiEditorTarget.AdcEffective:
            case ScanRoiEditorTarget.AdcShield:
                sectionIndex = 2;
                scroller = ChannelCalibrationScrollViewer;
                target = AdcRoiStartTextBox;
                return true;
            case ScanRoiEditorTarget.FocusOverall:
            case ScanRoiEditorTarget.FocusLeft:
            case ScanRoiEditorTarget.FocusRight:
                sectionIndex = 3;
                scroller = LiveCalibrationScrollViewer;
                target = FocusRoiStartTextBox;
                return true;
            case ScanRoiEditorTarget.ImageReferenceColumn:
                sectionIndex = 2;
                scroller = ChannelCalibrationScrollViewer;
                target = ReferenceColumnSampleStartTextBox;
                return true;
            default:
                return false;
        }
    }

    private void PreviewCanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        sender.Invalidate();
    }

    private void PreviewCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var frame = ViewModel.PreviewFrame;
        if (frame is null || !EnsurePreviewBitmap(sender, frame))
            return;

        args.DrawingSession.DrawImage(_previewBitmap);
    }

    private bool EnsurePreviewBitmap(CanvasControl resourceCreator, ScanPreviewFrame frame)
    {
        if (frame.PixelFormat != ScanPreviewPixelFormat.Bgra8 || frame.Width <= 0 || frame.Height <= 0)
            return false;

        if (_previewBitmap is not null
            && _previewBitmapVersion == frame.Version
            && _previewBitmapWidth == frame.Width
            && _previewBitmapHeight == frame.Height)
        {
            return true;
        }

        DisposePreviewBitmap();
        var pixels = GetCanvasPixels(frame);
        _previewBitmap = CanvasBitmap.CreateFromBytes(resourceCreator, pixels, frame.Width, frame.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied);
        _previewBitmapVersion = frame.Version;
        _previewBitmapWidth = frame.Width;
        _previewBitmapHeight = frame.Height;
        return true;
    }

    private static byte[] GetCanvasPixels(ScanPreviewFrame frame)
    {
        var rowBytes = frame.Width * 4;
        if (frame.StrideBytes == rowBytes)
            return frame.Pixels;

        var pixels = new byte[rowBytes * frame.Height];
        for (var y = 0; y < frame.Height; y++)
            Buffer.BlockCopy(frame.Pixels, y * frame.StrideBytes, pixels, y * rowBytes, rowBytes);

        return pixels;
    }

    private void DisposePreviewBitmap()
    {
        _previewBitmap?.Dispose();
        _previewBitmap = null;
        _previewBitmapVersion = -1;
        _previewBitmapWidth = -1;
        _previewBitmapHeight = -1;
    }

    private async void OnCalibrationPromptRequested(object? sender, ScanCalibrationPromptRequest e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = e.Prompt.Title,
                Content = e.Prompt.Content,
                PrimaryButtonText = e.Prompt.PrimaryButtonText,
                CloseButtonText = e.Prompt.CloseButtonText,
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary);
        }
        catch (Exception ex)
        {
            e.CompletionSource.TrySetException(ex);
        }
    }

    private async void OnFilmProfileDiscardConfirmationRequested(object? sender, ScanFilmProfileDiscardConfirmationRequest e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = e.TitleResourceKey.GetLocalized(),
                Content = e.MessageResourceKey.GetLocalized(),
                PrimaryButtonText = e.PrimaryButtonResourceKey.GetLocalized(),
                CloseButtonText = e.CloseButtonResourceKey.GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary);
        }
        catch (Exception ex)
        {
            e.CompletionSource.TrySetException(ex);
        }
    }

    private async void OnNoticeRequested(object? sender, ScanNoticeRequest e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = e.Title,
                Content = e.Content,
                CloseButtonText = e.CloseButtonText,
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
            e.CompletionSource.TrySetResult();
        }
        catch (Exception ex)
        {
            e.CompletionSource.TrySetException(ex);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanDebugViewModel.PreviewFrame))
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                RefreshPreviewLayout();
                PreviewCanvasControl.Invalidate();
            });

        if (e.PropertyName == nameof(ScanDebugViewModel.RoiOverlayVersion)
            || e.PropertyName == nameof(ScanDebugViewModel.SelectedRoiSelection)
            || e.PropertyName == nameof(ScanDebugViewModel.IsRoiEditModeEnabled)
            || e.PropertyName == nameof(ScanDebugViewModel.IsImageReferenceOverlayVisible)
            || e.PropertyName == nameof(ScanDebugViewModel.ColumnSampleOverlayVersion)
            || e.PropertyName == nameof(ScanDebugViewModel.IsColumnSampleEditModeEnabled))
        {
            _ = DispatcherQueue.TryEnqueue(DrawRoiOverlays);
        }

        if (e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationChannel)
            || e.PropertyName == nameof(ScanDebugViewModel.HasCurrentCalibrationIlluminationChannel)
            || e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationLevel)
            || e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationPulseClock)
            || e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationWorkMode))
        {
            _ = DispatcherQueue.TryEnqueue(RefreshCurrentCalibrationIlluminationEditor);
        }
    }

    private void RefreshPreviewLayout()
    {
        UpdateWorkbenchPreviewLayout();
        UpdatePreviewEmptyStateVisibility();
        var frame = ViewModel.PreviewFrame;
        if (frame is null || frame.Width <= 0 || frame.Height <= 0)
        {
            PreviewCanvasControl.Width = 0;
            PreviewCanvasControl.Height = 0;
            PreviewCanvas.Width = 0;
            PreviewCanvas.Height = 0;
            RoiCanvas.Children.Clear();
            AxisCanvas.Children.Clear();
            SetDefaultCursorText();
            DisposePreviewBitmap();
            _pendingInitialFitZoom = false;
            _lastPreviewImageWidth = -1;
            _lastPreviewImageHeight = -1;
            UpdateZoomScaleComboBoxSelection();
            return;
        }

        var imageWidth = frame.Width;
        var imageHeight = frame.Height;
        var imageChanged = imageWidth != _lastPreviewImageWidth || imageHeight != _lastPreviewImageHeight;
        if (imageChanged)
        {
            _lastPreviewImageWidth = imageWidth;
            _lastPreviewImageHeight = imageHeight;
            _pendingInitialFitZoom = true;
        }

        PreviewCanvasControl.Width = imageWidth;
        PreviewCanvasControl.Height = imageHeight;
        Canvas.SetLeft(PreviewCanvasControl, AxisMarginLeft);
        Canvas.SetTop(PreviewCanvasControl, AxisMarginTop);

        PreviewCanvas.Width = AxisMarginLeft + imageWidth + AxisMarginRight;
        PreviewCanvas.Height = AxisMarginTop + imageHeight + AxisMarginBottom;
        AxisCanvas.Width = PreviewCanvas.Width;
        AxisCanvas.Height = PreviewCanvas.Height;
        RoiCanvas.Width = PreviewCanvas.Width;
        RoiCanvas.Height = PreviewCanvas.Height;

        DrawAxes(imageWidth, imageHeight);
        DrawRoiOverlays();
        UpdateZoomScaleComboBoxSelection();

        if (_pendingInitialFitZoom)
            _ = DispatcherQueue.TryEnqueue(ApplyInitialFitZoom);
    }

    private void UpdatePreviewEmptyStateVisibility()
    {
        var hasPreviewFrame = ViewModel.PreviewFrame is { Width: > 0, Height: > 0 };
        PreviewEmptyStateGrid.Visibility = hasPreviewFrame
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void PreviewScrollViewer_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (_pendingInitialFitZoom)
            ApplyInitialFitZoom();
    }

    private void ApplyInitialFitZoom()
    {
        if (!_pendingInitialFitZoom)
            return;

#if PRISM_VISUAL_QA
        if (string.Equals(Environment.GetEnvironmentVariable("PRISM_VISUAL_QA_SKIP_INITIAL_FIT"), "1", StringComparison.Ordinal))
        {
            _pendingInitialFitZoom = false;
            UpdateZoomScaleComboBoxSelection();
            return;
        }
#endif

        var viewportWidth = PreviewScrollViewer.ViewportWidth;
        var viewportHeight = PreviewScrollViewer.ViewportHeight;
        var contentWidth = PreviewCanvas.Width;
        var contentHeight = PreviewCanvas.Height;
        if (viewportWidth <= 1 || viewportHeight <= 1 || contentWidth <= 0 || contentHeight <= 0)
            return;

        var fitZoom = Math.Min(viewportWidth / contentWidth, viewportHeight / contentHeight);
        var targetZoom = Math.Clamp((float)fitZoom, PreviewScrollViewer.MinZoomFactor, PreviewScrollViewer.MaxZoomFactor);

        var offsetX = Math.Max((contentWidth * targetZoom - viewportWidth) / 2.0, 0);
        var offsetY = Math.Max((contentHeight * targetZoom - viewportHeight) / 2.0, 0);

        _pendingInitialFitZoom = false;
        _ = PreviewScrollViewer.ChangeView(offsetX, offsetY, targetZoom, true);
        UpdateZoomScaleComboBoxSelection();
    }

    private void PreviewScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.PreviewFrame is null)
            return;

        var wheelDelta = e.GetCurrentPoint(PreviewScrollViewer).Properties.MouseWheelDelta;
        if (wheelDelta == 0)
            return;

        var anchor = e.GetCurrentPoint(PreviewScrollViewer).Position;
        StepZoom(wheelDelta > 0, anchor);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
#if PRISM_VISUAL_QA
        _visualQaPreviewScrollPressedCount++;
#endif
        if (ViewModel.PreviewFrame is null)
            return;

        PreviewCanvasControl_PointerPressed(PreviewCanvasControl, e);
        if (e.Handled)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _isPanning = true;
        _activePanPointerId = point.PointerId;
        _panStartPoint = point.Position;
        _panStartHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = PreviewScrollViewer.VerticalOffset;
#if PRISM_VISUAL_QA
        _visualQaLastPan = $"pressed pointer={point.PointerId} x={point.Position.X:0.###} y={point.Position.Y:0.###} h={_panStartHorizontalOffset:0.###} v={_panStartVerticalOffset:0.###}";
#endif
        PreviewScrollViewer.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
#if PRISM_VISUAL_QA
        _visualQaPreviewScrollMovedCount++;
#endif
        if (_isRoiDragging)
        {
            PreviewCanvasControl_PointerMoved(PreviewCanvasControl, e);
            e.Handled = true;
            return;
        }

        if (!_isPanning)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (point.PointerId != _activePanPointerId)
        {
#if PRISM_VISUAL_QA
            _visualQaLastPan = $"move-mismatch pointer={point.PointerId} active={_activePanPointerId} x={point.Position.X:0.###} y={point.Position.Y:0.###}";
#endif
            return;
        }

        var deltaX = point.Position.X - _panStartPoint.X;
        var deltaY = point.Position.Y - _panStartPoint.Y;
        var newHorizontalOffset = Math.Max(0, _panStartHorizontalOffset - deltaX);
        var newVerticalOffset = Math.Max(0, _panStartVerticalOffset - deltaY);

        var changeViewAccepted = PreviewScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, null, true);
#if PRISM_VISUAL_QA
        _visualQaLastPan = $"moved pointer={point.PointerId} dx={deltaX:0.###} dy={deltaY:0.###} newH={newHorizontalOffset:0.###} newV={newVerticalOffset:0.###} accepted={changeViewAccepted}";
#endif
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isRoiDragging)
        {
            PreviewCanvasControl_PointerReleased(PreviewCanvasControl, e);
            return;
        }

        if (!_isPanning)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (point.PointerId != _activePanPointerId)
            return;

        EndPanning(e);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_isRoiDragging)
        {
            PreviewCanvasControl_PointerCanceled(PreviewCanvasControl, e);
            return;
        }

        if (!_isPanning)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (point.PointerId != _activePanPointerId)
            return;

        EndPanning(e);
        e.Handled = true;
    }

    private void PreviewScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        => UpdateZoomScaleComboBoxSelection();

    private void ZoomInButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => StepZoom(true, new Point(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2));

    private void ZoomOutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => StepZoom(false, new Point(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2));

    private void ZoomScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingZoomScaleComboBox || ZoomScaleComboBox.SelectedIndex < 0)
            return;

        var selectedZoom = ZoomLevels[ZoomScaleComboBox.SelectedIndex];
        ApplyZoom(selectedZoom, new Point(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2));
    }

    private void StepZoom(bool zoomIn, Point anchorInViewport)
    {
        var oldZoom = PreviewScrollViewer.ZoomFactor;
        float? nextZoom = null;

        if (zoomIn)
        {
            foreach (var level in ZoomLevels)
            {
                if (level > oldZoom + 0.001f)
                {
                    nextZoom = level;
                    break;
                }
            }
        }
        else
        {
            for (var i = ZoomLevels.Length - 1; i >= 0; i--)
            {
                var level = ZoomLevels[i];
                if (level < oldZoom - 0.001f)
                {
                    nextZoom = level;
                    break;
                }
            }
        }

        if (nextZoom is null)
            return;

        ApplyZoom(nextZoom.Value, anchorInViewport);
    }

    private void ApplyZoom(float targetZoom, Point anchorInViewport)
    {
        var oldZoom = PreviewScrollViewer.ZoomFactor;
        var newZoom = Math.Clamp(targetZoom, PreviewScrollViewer.MinZoomFactor, PreviewScrollViewer.MaxZoomFactor);
        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
        {
            UpdateZoomScaleComboBoxSelection();
            return;
        }

        var contentX = (PreviewScrollViewer.HorizontalOffset + anchorInViewport.X) / oldZoom;
        var contentY = (PreviewScrollViewer.VerticalOffset + anchorInViewport.Y) / oldZoom;

        var newHorizontalOffset = (contentX * newZoom) - anchorInViewport.X;
        var newVerticalOffset = (contentY * newZoom) - anchorInViewport.Y;

        _ = PreviewScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, newZoom, true);
        UpdateZoomScaleComboBoxSelection();
    }

    private void InitializeZoomScaleComboBox()
    {
        if (ZoomScaleComboBox.Items.Count > 0)
            return;

        foreach (var zoom in ZoomLevels)
            ZoomScaleComboBox.Items.Add(FormatZoomLabel(zoom));

        UpdateZoomScaleComboBoxSelection();
    }

    private void UpdateZoomScaleComboBoxSelection()
    {
        if (ZoomScaleComboBox.Items.Count == 0)
            return;

        var currentZoom = PreviewScrollViewer.ZoomFactor;
        var nearestIndex = 0;
        var nearestDelta = float.MaxValue;
        for (var i = 0; i < ZoomLevels.Length; i++)
        {
            var delta = Math.Abs(ZoomLevels[i] - currentZoom);
            if (delta < nearestDelta)
            {
                nearestDelta = delta;
                nearestIndex = i;
            }
        }

        if (ZoomScaleComboBox.SelectedIndex == nearestIndex)
            return;

        _updatingZoomScaleComboBox = true;
        ZoomScaleComboBox.SelectedIndex = nearestIndex;
        _updatingZoomScaleComboBox = false;
    }

    private static string FormatZoomLabel(float zoom)
        => $"{zoom * 100:0.###}%";

    private void EndPanning(PointerRoutedEventArgs e)
    {
        ClearPanState();
        PreviewScrollViewer.ReleasePointerCapture(e.Pointer);
    }

    private void PreviewCanvasControl_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
#if PRISM_VISUAL_QA
        _visualQaPreviewCanvasPressedCount++;
#endif
        if ((!ViewModel.CanMutateRoiFromPreview && !ViewModel.CanMutateColumnSampleFromPreview) || ViewModel.PreviewFrame is null)
            return;

        var point = e.GetCurrentPoint(PreviewCanvasControl);
#if PRISM_VISUAL_QA
        _visualQaLastPointer = $"pressed x={point.Position.X:0.###} y={point.Position.Y:0.###} left={point.Properties.IsLeftButtonPressed}";
#endif
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (point.Position.X < 0 || point.Position.Y < 0 || point.Position.X >= ViewModel.PreviewFrame.Width || point.Position.Y >= ViewModel.PreviewFrame.Height)
            return;

        var x = Math.Clamp((int)Math.Floor(point.Position.X), 0, Math.Max(ViewModel.PreviewFrame.Width - 1, 0));
        _isRoiDragging = true;
        _isColumnSampleDrag = ViewModel.CanMutateColumnSampleFromPreview;
        _activeRoiPointerId = point.PointerId;
        _roiDragStartX = x;
        _roiDragImageWidth = ViewModel.PreviewFrame.Width;
        _roiDragFrameVersion = ViewModel.PreviewFrame.Version;
        _roiDragSelection = ViewModel.SelectedRoiSelection;
        _roiDragCalibrationChannel = ViewModel.SelectedCalibrationChannel;
        _roiDragHasAppliedRange = false;
        var hasRange = _isColumnSampleDrag
            ? ViewModel.TryGetColumnSampleRange(ViewModel.PreviewFrame.Width, out _roiOriginalRange)
            : ViewModel.TryGetSelectedRoiRange(ViewModel.PreviewFrame.Width, out _roiOriginalRange);
        _isRoiMoveMode = hasRange && x >= _roiOriginalRange.Start && x <= _roiOriginalRange.EndInclusive;
        PreviewCanvasControl.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PreviewCanvasControl_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
#if PRISM_VISUAL_QA
        _visualQaPreviewCanvasMovedCount++;
#endif
        var bitmap = ViewModel.PreviewFrame;
        if (bitmap is null)
            return;

        var point = e.GetCurrentPoint(PreviewCanvasControl).Position;
#if PRISM_VISUAL_QA
        _visualQaLastPointer = $"moved x={point.X:0.###} y={point.Y:0.###} dragging={_isRoiDragging}";
#endif
        var x = (int)Math.Floor(point.X);
        var y = (int)Math.Floor(point.Y);

        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
        {
            SetDefaultCursorText();
            return;
        }

        CursorPositionTextBlock.Text = "ScanDebug_Runtime_CursorPosition".GetLocalizedFormat(x, y);
        if (ViewModel.TryGetPreviewSample16(x, y, out var sample16))
            CursorIntensityTextBlock.Text = "ScanDebug_Runtime_CursorIntensity".GetLocalizedFormat(sample16);
        else
            CursorIntensityTextBlock.Text = "ScanDebug_Runtime_CursorIntensityDefault".GetLocalized();

        if (!_isRoiDragging)
            return;

        if (_isColumnSampleDrag ? !ViewModel.CanMutateColumnSampleFromPreview : !ViewModel.CanMutateRoiFromPreview)
        {
            EndRoiDrag(e);
            return;
        }

        if (!IsRoiDragTargetCurrent(bitmap))
        {
            EndRoiDrag(e);
            return;
        }

        var currentPoint = e.GetCurrentPoint(PreviewCanvasControl);
        if (currentPoint.PointerId != _activeRoiPointerId)
            return;

        if (_isRoiMoveMode)
            ApplyRoiMoveDragToX(x, bitmap.Width);
        else
            ApplyRoiDragRangeIfChanged(new ScanColumnRange(_roiDragStartX, x), bitmap.Width);

        e.Handled = true;
    }

    private void PreviewCanvasControl_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
#if PRISM_VISUAL_QA
        _visualQaPreviewCanvasReleasedCount++;
#endif
        if (!_isRoiDragging)
            return;

        var point = e.GetCurrentPoint(PreviewCanvasControl);
        if (point.PointerId != _activeRoiPointerId)
            return;

        var bitmap = ViewModel.PreviewFrame;
        if (bitmap is not null)
        {
            var x = (int)Math.Floor(point.Position.X);
            if (x >= 0 && point.Position.Y >= 0 && x < bitmap.Width && point.Position.Y < bitmap.Height && IsRoiDragTargetCurrent(bitmap))
            {
                if (_isRoiMoveMode)
                    ApplyRoiMoveDragToX(x, bitmap.Width);
                else
                    PreviewCanvasControl_PointerMoved(PreviewCanvasControl, e);
            }
        }

        EndRoiDrag(e);
        e.Handled = true;
    }

    private void PreviewCanvasControl_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!_isRoiDragging)
            return;

        var point = e.GetCurrentPoint(PreviewCanvasControl);
        if (point.PointerId != _activeRoiPointerId)
            return;

        EndRoiDrag(e);
        e.Handled = true;
    }

    private void PreviewCanvasControl_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndRoiDragOrPanForPointer(e);
    }

    private void PreviewScrollViewer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndRoiDragOrPanForPointer(e);
    }

    private void PreviewCanvasControl_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetDefaultCursorText();
    }

    private void EndRoiDrag(PointerRoutedEventArgs e)
    {
        ClearRoiDragState();
        PreviewCanvasControl.ReleasePointerCapture(e.Pointer);
    }

    private void CancelPreviewVisualInteraction()
    {
        ClearRoiDragState();
        ClearPanState();
        PreviewCanvasControl.ReleasePointerCaptures();
        PreviewScrollViewer.ReleasePointerCaptures();
    }

    private void ClearRoiDragState()
    {
        _isRoiDragging = false;
        _isColumnSampleDrag = false;
        _isRoiMoveMode = false;
        _activeRoiPointerId = 0;
        _roiDragStartX = 0;
        _roiOriginalRange = new ScanColumnRange(0, 0);
        _roiDragImageWidth = 0;
        _roiDragFrameVersion = -1;
        _roiDragSelection = string.Empty;
        _roiDragCalibrationChannel = string.Empty;
        _roiDragHasAppliedRange = false;
    }

    private void ClearPanState()
    {
        _isPanning = false;
        _activePanPointerId = 0;
        _panStartPoint = default;
        _panStartHorizontalOffset = 0;
        _panStartVerticalOffset = 0;
    }

    private void ApplyRoiMoveDragToX(int x, int imageWidth)
    {
        var range = ScanRoiDragMath.CalculateMovedRange(_roiOriginalRange, x - _roiDragStartX, imageWidth);
        ApplyRoiDragRangeIfChanged(range, imageWidth);
    }

    private void ApplyRoiDragRangeIfChanged(ScanColumnRange range, int imageWidth)
    {
        if (_roiDragHasAppliedRange)
        {
            var currentRangeValid = _isColumnSampleDrag
                ? ViewModel.TryGetColumnSampleRange(imageWidth, out var currentRange)
                : ViewModel.TryGetSelectedRoiRange(imageWidth, out currentRange);
            if (currentRangeValid && currentRange == range)
                return;
        }

        _roiDragHasAppliedRange = true;
        if (_isColumnSampleDrag)
            ViewModel.UpdateColumnSampleRange(range.Start, range.EndInclusive, imageWidth);
        else
            ViewModel.UpdateSelectedRoiRange(range.Start, range.EndInclusive, imageWidth);
    }

    private bool IsRoiDragTargetCurrent(ScanPreviewFrame bitmap)
        => bitmap.Width == _roiDragImageWidth
            && bitmap.Version == _roiDragFrameVersion
            && string.Equals(ViewModel.SelectedCalibrationChannel, _roiDragCalibrationChannel, StringComparison.Ordinal)
            && (_isColumnSampleDrag || string.Equals(ViewModel.SelectedRoiSelection, _roiDragSelection, StringComparison.Ordinal));

    private void EndRoiDragOrPanForPointer(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewCanvasControl).PointerId;
        if (_isRoiDragging && point == _activeRoiPointerId)
        {
            EndRoiDrag(e);
            e.Handled = true;
            return;
        }

        if (_isPanning && point == _activePanPointerId)
        {
            EndPanning(e);
            e.Handled = true;
        }
    }

    private void ManualFocusNegativeButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        BeginManualFocusHold(sender, e, positive: false);
    }

    private void ManualFocusPositiveButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        BeginManualFocusHold(sender, e, positive: true);
    }

    private void AutoFocusButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.CanRunAutoFocusAction)
            ViewModel.AutoFocusCommand.Execute(null);
    }

    private void ManualFocusButton_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndManualFocusHold(sender, e);
    }

    private void ManualFocusButton_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndManualFocusHold(sender, e);
    }

    private void ManualFocusButton_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.EndManualFocusHold();
    }

    private void BeginManualFocusHold(object sender, PointerRoutedEventArgs e, bool positive)
    {
        if (sender is Button button)
            button.CapturePointer(e.Pointer);

        ViewModel.BeginManualFocusHold(positive);
        e.Handled = true;
    }

    private void EndManualFocusHold(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
            button.ReleasePointerCapture(e.Pointer);

        ViewModel.EndManualFocusHold();
        e.Handled = true;
    }

    private void DrawRoiOverlays()
    {
        RoiCanvas.Children.Clear();

        var bitmap = ViewModel.PreviewFrame;
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            return;

        var labels = new List<(TextBlock Element, double AnchorX, bool IsSelected, bool IsBottomAnchored)>();

        foreach (var overlay in ViewModel.GetPreviewRoiOverlays(bitmap.Width))
        {
            var stroke = new SolidColorBrush(GetRoiColor(overlay.Key, overlay.IsSelected));
            var fill = new SolidColorBrush(GetRoiFillColor(overlay.Key, overlay.IsSelected));
            var rectangle = new Rectangle
            {
                Width = overlay.Range.Width,
                Height = bitmap.Height,
                Stroke = stroke,
                Fill = fill,
                StrokeThickness = overlay.IsSelected ? 2.5 : 1.5,
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rectangle, AxisMarginLeft + overlay.Range.Start);
            Canvas.SetTop(rectangle, AxisMarginTop);
            RoiCanvas.Children.Add(rectangle);

            var label = new TextBlock
            {
                Text = overlay.Label,
                Foreground = stroke,
                FontSize = overlay.IsSelected ? 13 : 11,
                FontWeight = overlay.IsSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            labels.Add((label, overlay.Range.Start, overlay.IsSelected, false));
        }

        if (ViewModel.IsImageReferenceOverlayVisible && ViewModel.TryGetColumnSampleRange(bitmap.Width, out var sampleRange))
        {
            var strokeColor = ViewModel.IsColumnSampleEditModeEnabled ? Colors.Gold : Colors.Khaki;
            var rectangle = new Rectangle
            {
                Width = sampleRange.Width,
                Height = bitmap.Height,
                Stroke = new SolidColorBrush(strokeColor),
                Fill = new SolidColorBrush(Color.FromArgb(ViewModel.IsColumnSampleEditModeEnabled ? (byte)44 : (byte)24, strokeColor.R, strokeColor.G, strokeColor.B)),
                StrokeThickness = ViewModel.IsColumnSampleEditModeEnabled ? 2.5 : 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rectangle, AxisMarginLeft + sampleRange.Start);
            Canvas.SetTop(rectangle, AxisMarginTop);
            RoiCanvas.Children.Add(rectangle);

            var label = new TextBlock
            {
                Text = "ScanDebug_Runtime_ImageReferenceOverlayLabel".GetLocalized(),
                Foreground = new SolidColorBrush(strokeColor),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            labels.Add((label, sampleRange.Start, false, true));
        }

        var layoutInputs = labels
            .Select((label, index) => new ScanRoiOverlayLabelLayoutInput(
                index,
                label.AnchorX,
                label.Element.DesiredSize.Width,
                label.Element.DesiredSize.Height,
                label.IsSelected,
                label.IsBottomAnchored))
            .ToArray();

        foreach (var placement in ScanRoiOverlayLabelLayout.Arrange(bitmap.Width, bitmap.Height, layoutInputs))
        {
            var label = labels[placement.Index].Element;
            Canvas.SetLeft(label, AxisMarginLeft + placement.X);
            Canvas.SetTop(label, AxisMarginTop + placement.Y);
            RoiCanvas.Children.Add(label);
        }
    }

    private static Color GetRoiColor(string key, bool isSelected)
    {
        var color = key switch
        {
            RoiSelectionBwActive => Colors.DodgerBlue,
            RoiSelectionBwShield => Colors.DarkOrange,
            RoiSelectionFocusOverall => Colors.MediumPurple,
            RoiSelectionFocusLeft => Colors.LimeGreen,
            RoiSelectionFocusRight => Colors.HotPink,
            _ => Colors.Gray
        };

        return isSelected ? color : Color.FromArgb(220, color.R, color.G, color.B);
    }

    private static Color GetRoiFillColor(string key, bool isSelected)
    {
        var baseColor = GetRoiColor(key, isSelected);
        return Color.FromArgb(isSelected ? (byte)52 : (byte)28, baseColor.R, baseColor.G, baseColor.B);
    }

    private void SetDefaultCursorText()
    {
        CursorPositionTextBlock.Text = "ScanDebug_Runtime_CursorPositionDefault".GetLocalized();
        CursorIntensityTextBlock.Text = "ScanDebug_Runtime_CursorIntensityDefault".GetLocalized();
    }

    private void DrawAxes(int imageWidth, int imageHeight)
    {
        AxisCanvas.Children.Clear();

        var axisBrush = new SolidColorBrush(Colors.Gray);
        var textBrush = new SolidColorBrush(Colors.DarkGray);
        var originX = AxisMarginLeft;
        var originY = AxisMarginTop;
        var xEnd = originX + imageWidth;
        var yEnd = originY + imageHeight;

        AxisCanvas.Children.Add(new Line
        {
            X1 = originX,
            Y1 = originY,
            X2 = xEnd,
            Y2 = originY,
            Stroke = axisBrush,
            StrokeThickness = 1
        });

        AxisCanvas.Children.Add(new Line
        {
            X1 = originX,
            Y1 = originY,
            X2 = originX,
            Y2 = yEnd,
            Stroke = axisBrush,
            StrokeThickness = 1
        });

        var xStep = GetTickStep(imageWidth);
        for (var x = 0; x <= imageWidth; x += xStep)
        {
            var tickX = originX + x;
            AxisCanvas.Children.Add(new Line
            {
                X1 = tickX,
                Y1 = originY - 4,
                X2 = tickX,
                Y2 = originY,
                Stroke = axisBrush,
                StrokeThickness = 1
            });

            var label = new TextBlock { Text = x.ToString(), Foreground = textBrush, FontSize = 11 };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, tickX - (label.DesiredSize.Width / 2));
            Canvas.SetTop(label, 4);
            AxisCanvas.Children.Add(label);
        }

        var yStep = GetTickStep(imageHeight);
        for (var y = yStep; y <= imageHeight; y += yStep)
        {
            var tickY = originY + y;
            AxisCanvas.Children.Add(new Line
            {
                X1 = originX - 4,
                Y1 = tickY,
                X2 = originX,
                Y2 = tickY,
                Stroke = axisBrush,
                StrokeThickness = 1
            });

            var label = new TextBlock { Text = y.ToString(), Foreground = textBrush, FontSize = 11 };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Max(0, originX - 8 - label.DesiredSize.Width));
            Canvas.SetTop(label, tickY - (label.DesiredSize.Height / 2));
            AxisCanvas.Children.Add(label);
        }
    }

    private static int GetTickStep(int span)
    {
        if (span <= 0)
            return 1;

        var rough = Math.Max(1.0, span / 12.0);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var normalized = rough / magnitude;

        var nice = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };

        return (int)(nice * magnitude);
    }

#if PRISM_VISUAL_QA
    internal object GetVisualQaPointerDiagnostics()
        => new
        {
            _visualQaPreviewScrollPressedCount,
            _visualQaPreviewScrollMovedCount,
            _visualQaPreviewCanvasPressedCount,
            _visualQaPreviewCanvasMovedCount,
            _visualQaPreviewCanvasReleasedCount,
            _visualQaLastPointer,
            _visualQaLastPan,
            _isRoiDragging,
            _isPanning
        };
#endif
}

