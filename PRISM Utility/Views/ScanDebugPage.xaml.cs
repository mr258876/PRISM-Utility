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
using PRISM_Utility.Core.Services;
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
    private bool _isPageActive;
    private int _activationEpoch;
    private object? _pageActivationOwner;
    private readonly ScanDebugDialogLifetime _dialogLifetime = ScanDebugDialogLifetime.ForCurrentThread;
    private bool _isUpdatingCurrentCalibrationIlluminationEditor;
    private bool _isSynchronizingWorkbenchSection;
    private int _activeWorkbenchSectionIndex = 1;
    private bool _isNarrowPreviewOpen;
    private bool _isConfigurationWorkspaceOpen;
    private bool? _inspectionOpenOverride;
    private double _workbenchPreviewEditorRatio = ScanWorkbenchPreviewLayout.DefaultEditorRatio;
    private int _workbenchFocusHandoffVersion;
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
        UpdateRawSignalMode();
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

    private void SelectWorkbenchTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section } || !int.TryParse(section, out var index))
            return;

        _isConfigurationWorkspaceOpen = false;
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.WideThreshold)
            _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(index);
    }

    private void OpenBlackWhiteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        _isConfigurationWorkspaceOpen = false;
        _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(2);
        EnqueueForCurrentActivation(() => BlackWhiteTaskButton.Focus(FocusState.Programmatic));
    }

    private void OpenConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        _isConfigurationWorkspaceOpen = true;
        SetActiveWorkbenchSection(0);
        EnqueueForCurrentActivation(() => ProfileNameTextBox.Focus(FocusState.Programmatic));
    }

    private void OpenAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        _isConfigurationWorkspaceOpen = true;
        SetActiveWorkbenchSection(4);
        EnqueueForCurrentActivation(() =>
        {
            if (WorkbenchSectionComboBox.Visibility == Visibility.Visible)
                WorkbenchSectionComboBox.Focus(FocusState.Programmatic);
            else
                WorkbenchSectionSelectorBar.Focus(FocusState.Programmatic);
        });
    }

    private void WorkbenchReviewButton_Click(object sender, RoutedEventArgs e)
        => FilmProfileLifecycleDetailsScrollViewer.Visibility = FilmProfileLifecycleDetailsScrollViewer.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;

    private void RawSignalModeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (RawSignalProfileScrollViewer is not null && PreviewScrollViewer is not null)
            UpdateRawSignalMode();
    }

    private void UpdateRawSignalMode()
    {
        var isProfile = ReferenceEquals(RawSignalModeSelector.SelectedItem, RawSignalProfileModeItem);
        if (isProfile)
            CancelPreviewVisualInteraction();

        RawSignalProfileScrollViewer.Visibility = ToVisibility(isProfile);
        PreviewScrollViewer.Visibility = ToVisibility(!isProfile);
        foreach (var imageTool in new Control[] { ZoomOutButton, ZoomInButton, ZoomScaleComboBox, PreviewDisplayToolsButton, OverlayToolsButton })
            imageTool.Visibility = ToVisibility(!isProfile);

        UpdatePreviewEmptyStateVisibility();
        UpdateRawSignalResultSurface();
    }

    private void UpdateRawSignalResultSurface()
    {
        var hasResult = ViewModel.RawSignalResult is not null;
        RawSignalResultDetails.Visibility = ToVisibility(hasResult);
        RawSignalInspectionDetails.Visibility = ToVisibility(hasResult
            && ReferenceEquals(RawSignalModeSelector.SelectedItem, RawSignalProfileModeItem));
        RawSignalCanvasControl.Invalidate();
    }

    private void UpdateWorkbenchReviewEntry()
    {
        var hasPendingReview = ViewModel.FilmProfileImportResultReviewVisibility == Visibility.Visible;
        WorkbenchReviewButton.Content = (hasPendingReview
            ? "ScanDebug_WorkbenchPendingReviewAction"
            : "ScanDebug_WorkbenchReviewAction").GetLocalized();
        if (hasPendingReview)
            FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Visible;
    }

    private void InspectionToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _inspectionOpenOverride = WorkbenchInspectionRail.Visibility != Visibility.Visible;
        UpdateWorkbenchPreviewLayout();
        EnqueueForCurrentActivation(() => InspectionToggleButton.Focus(FocusState.Programmatic));
    }

    private void ScanDebugRootGrid_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (!IsWorkbenchSectionUiReady())
            return;

        EnqueueForCurrentActivation(UpdateWorkbenchPreviewLayout);
    }

    private void UpdateWorkbenchPreviewLayout()
        => UpdateWorkbenchPreviewLayout(ScanDebugRootGrid.ActualWidth);

    private void UpdateWorkbenchPreviewLayout(double availableWidth)
    {
        if (!IsWorkbenchSectionUiReady())
            return;

        if (!double.IsFinite(availableWidth) || availableWidth < 0)
            availableWidth = 0;

        var isCompact = availableWidth < ScanWorkbenchPreviewLayout.CompactThreshold;
        ScanDebugRootGrid.RowSpacing = isCompact
            ? (double)Resources["ScanDebugInlineSpacing"]
            : (double)Resources["ScanDebugSectionSpacing"];
        WorkbenchIdentityGrid.RowSpacing = isCompact ? 0 : (double)Resources["ScanDebugInlineSpacing"];
        Grid.SetColumnSpan(WorkbenchTitleTextBlock, isCompact ? 1 : 2);
        Grid.SetRow(WorkbenchProfileSummaryGrid, isCompact ? 0 : 1);
        Grid.SetColumn(WorkbenchProfileSummaryGrid, isCompact ? 1 : 0);
        Grid.SetColumnSpan(WorkbenchProfileSummaryGrid, isCompact ? 1 : 2);

        var inspectionOpen = _inspectionOpenOverride ?? availableWidth >= ScanWorkbenchPreviewLayout.WideThreshold;
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            availableWidth, HasValidPreviewFrame(), _isNarrowPreviewOpen, _workbenchPreviewEditorRatio,
            inspectionOpen, _isConfigurationWorkspaceOpen));
        var isWideSelectorAvailable = availableWidth >= ScanWorkbenchPreviewLayout.WideThreshold;
        var focusedElement = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        var focusReplacement = FindWorkbenchFocusReplacement(focusedElement, layout, isWideSelectorAvailable);
        var focusHandoffVersion = ++_workbenchFocusHandoffVersion;
        var activationEpoch = _activationEpoch;
        if (!layout.IsPreviewVisible)
            CancelPreviewVisualInteraction();
        if (WorkbenchEditorColumnContent.Visibility == Visibility.Visible && !layout.IsEditorVisible && _activeWorkbenchSectionIndex == 3)
        {
            ManualFocusNegativeButton.ReleasePointerCaptures();
            ManualFocusPositiveButton.ReleasePointerCaptures();
        }

        WorkbenchSectionSelectorBar.Visibility = _isConfigurationWorkspaceOpen && isWideSelectorAvailable ? Visibility.Visible : Visibility.Collapsed;
        WorkbenchSectionComboBox.Visibility = _isConfigurationWorkspaceOpen && !isWideSelectorAvailable ? Visibility.Visible : Visibility.Collapsed;
        WorkbenchEditorColumn.Width = new GridLength(layout.EditorWidth);
        WorkbenchPreviewSeparatorColumn.Width = new GridLength(layout.SeparatorWidth);
        WorkbenchPreviewColumn.Width = new GridLength(layout.PreviewWidth);
        WorkbenchInspectionSeparatorColumn.Width = new GridLength(layout.InspectionGapWidth);
        WorkbenchInspectionColumn.Width = new GridLength(layout.InspectionWidth);
        WorkbenchEditorRow.Height = new GridLength(1, GridUnitType.Star);
        WorkbenchEditorColumnContent.Visibility = ToVisibility(layout.IsEditorVisible);
        WorkbenchPreviewColumnContent.Visibility = ToVisibility(layout.IsPreviewVisible);
        WorkbenchInspectionRail.Visibility = ToVisibility(layout.IsInspectionVisible);
        InspectionToggleButton.Visibility = _isConfigurationWorkspaceOpen ? Visibility.Collapsed : Visibility.Visible;
        OpenPreviewButton.Visibility = ToVisibility(layout.IsPreviewOpenButtonVisible);
        BackToEditorButton.Visibility = ToVisibility(layout.IsBackToEditorButtonVisible);
        PreviewSplitter.Visibility = ToVisibility(layout.IsSplitterVisible);
        PreviewSplitter.IsTabStop = layout.IsSplitterVisible;
        WorkbenchPreviewColumnContent.VerticalAlignment = VerticalAlignment.Stretch;
        if (focusReplacement is not null)
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (!IsCurrentActivation(activationEpoch) || focusHandoffVersion != _workbenchFocusHandoffVersion || XamlRoot is not { } currentRoot
                    || focusReplacement.Visibility != Visibility.Visible || !focusReplacement.IsEnabled)
                    return;

                var currentFocus = FocusManager.GetFocusedElement(currentRoot) as DependencyObject;
                if (currentFocus is Control { IsTabStop: true } control
                    && !ReferenceEquals(currentFocus, focusedElement) && IsVisibleInTree(control))
                    return;

                _ = focusReplacement.Focus(FocusState.Programmatic);
            });
    }

    private Control? FindWorkbenchFocusReplacement(
        DependencyObject? focusedElement, ScanWorkbenchPreviewLayoutResult layout, bool isWideSelectorAvailable)
    {
        if (focusedElement is null)
            return null;

        Control selector = isWideSelectorAvailable ? WorkbenchSectionSelectorBar : WorkbenchSectionComboBox;
        if (WorkbenchSectionSelectorBar.Visibility == Visibility.Visible && IsFocusedWithin(focusedElement, WorkbenchSectionSelectorBar))
        {
            if (!_isConfigurationWorkspaceOpen)
                return GetActiveWorkbenchTaskButton();
            if (!isWideSelectorAvailable)
                return WorkbenchSectionComboBox;
        }
        if (WorkbenchSectionComboBox.Visibility == Visibility.Visible && IsFocusedWithin(focusedElement, WorkbenchSectionComboBox))
        {
            if (!_isConfigurationWorkspaceOpen)
                return GetActiveWorkbenchTaskButton();
            if (isWideSelectorAvailable)
                return WorkbenchSectionSelectorBar;
        }

        if (WorkbenchEditorColumnContent.Visibility == Visibility.Visible && !layout.IsEditorVisible
            && IsFocusedWithin(focusedElement, WorkbenchEditorColumnContent))
            return layout.IsBackToEditorButtonVisible ? BackToEditorButton : InspectionToggleButton;
        if (WorkbenchPreviewColumnContent.Visibility == Visibility.Visible && !layout.IsPreviewVisible
            && IsFocusedWithin(focusedElement, WorkbenchPreviewColumnContent))
            return layout.IsPreviewOpenButtonVisible ? OpenPreviewButton
                : _isConfigurationWorkspaceOpen ? selector : InspectionToggleButton;
        if (WorkbenchInspectionRail.Visibility == Visibility.Visible && !layout.IsInspectionVisible
            && IsFocusedWithin(focusedElement, WorkbenchInspectionRail))
            return _isConfigurationWorkspaceOpen ? selector : InspectionToggleButton;
        if (PreviewSplitter.Visibility == Visibility.Visible && !layout.IsSplitterVisible
            && IsFocusedWithin(focusedElement, PreviewSplitter))
            return layout.IsPreviewOpenButtonVisible ? OpenPreviewButton
                : _isConfigurationWorkspaceOpen ? selector : InspectionToggleButton;

        return null;
    }

    private static bool IsFocusedWithin(DependencyObject focusedElement, DependencyObject ancestor)
    {
        for (DependencyObject? current = focusedElement; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }

        return false;
    }

    private static bool IsVisibleInTree(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement { Visibility: Visibility.Collapsed })
                return false;
        }

        return true;
    }

    private Button GetActiveWorkbenchTaskButton() => _activeWorkbenchSectionIndex switch
    {
        2 => BlackWhiteTaskButton,
        3 => FocusTaskButton,
        5 => MotionTaskButton,
        _ => SamplingTaskButton
    };

    private bool HasValidPreviewFrame()
        => ViewModel.PreviewFrame is { Width: > 0, Height: > 0 };

    private static Visibility ToVisibility(bool isVisible)
        => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private void OpenPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        _isNarrowPreviewOpen = true;
        UpdateWorkbenchPreviewLayout();
        EnqueueForCurrentActivation(() =>
        {
            if (WorkbenchPreviewColumnContent.Visibility == Visibility.Visible && BackToEditorButton.Visibility == Visibility.Visible)
                _ = BackToEditorButton.Focus(FocusState.Programmatic);
        });
    }

    private void BackToEditorButton_Click(object sender, RoutedEventArgs e)
    {
        _isNarrowPreviewOpen = false;
        UpdateWorkbenchPreviewLayout();
        var taskButton = GetActiveWorkbenchTaskButton();
        EnqueueForCurrentActivation(() => taskButton.Focus(FocusState.Programmatic));
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
        if (PreviewSplitter.Visibility != Visibility.Visible)
            return;

        var split = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(new ScanWorkbenchPreviewResizeInput(
            ScanDebugRootGrid.ActualWidth, _workbenchPreviewEditorRatio, editorDelta,
            WorkbenchInspectionRail.Visibility == Visibility.Visible));
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
            && WorkbenchInspectionRail is not null
            && WorkbenchInspectionColumn is not null
            && WorkbenchInspectionSeparatorColumn is not null
            && WorkbenchIdentityGrid is not null
            && WorkbenchTitleTextBlock is not null
            && WorkbenchProfileSummaryGrid is not null
            && InspectionToggleButton is not null
            && SamplingTaskButton is not null
            && FocusTaskButton is not null
            && MotionTaskButton is not null
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

        if (_activeWorkbenchSectionIndex == 3 && index != 3)
        {
            ManualFocusNegativeButton.ReleasePointerCaptures();
            ManualFocusPositiveButton.ReleasePointerCaptures();
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

    private void WorkbenchDataExportFlyout_Opening(object sender, object e)
        => ConfigureLocalFlyoutBounds(WorkbenchDataExportFlyout, WorkbenchDataExportScrollViewer, WorkbenchDataExportContent);

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
#if PRISM_VISUAL_QA
        if (App.IsShuttingDown)
            return;
#endif
        if (_isPageActive)
            return;

        _isPageActive = true;
        var activationEpoch = ++_activationEpoch;
        var pageOwner = ViewModel.ReservePageActivation();
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
#if PRISM_VISUAL_QA
        try
        {
            await PrismVisualQaCaptureService.DrainActiveAsync();
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "VisualQaCapture", $"Page load capture drain failed: {ex}\n");
        }
#endif
        if (!IsCurrentActivation(activationEpoch) || !ViewModel.IsCurrentPageOwner(pageOwner))
            return;

        SubscribeViewModelEvents();
        UpdateWorkbenchReviewEntry();
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded SubscribeViewModelEvents={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        if (!ViewModel.AttachRuntimeBindingsForPage(pageOwner))
            return;
        _pageActivationOwner = pageOwner;
        NavigationTimingLogger.Write($"ScanDebugPage.Loaded AttachRuntimeBindings={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        App.MainWindow.Activated -= MainWindow_Activated;
        App.MainWindow.Activated += MainWindow_Activated;

        stepStopwatch.Restart();
        await ViewModel.RefreshDeviceSettingsBindingsAsync();
        if (!IsCurrentActivation(activationEpoch) || !ViewModel.IsCurrentPageOwner(pageOwner))
            return;

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
        if (!_isPageActive)
            return;

        _isPageActive = false;
        var unloadEpoch = ++_activationEpoch;
        var pageOwner = _pageActivationOwner;
        ViewModel.InvalidatePageActivation(pageOwner);
        _dialogLifetime.Retire(this, unloadEpoch - 1);
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        App.MainWindow.Activated -= MainWindow_Activated;
        CancelPreviewVisualInteraction();
#if PRISM_VISUAL_QA
        try
        {
            await PrismVisualQaCaptureService.StopAsync(this);
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "VisualQaCapture", $"Page unload capture drain failed: {ex}\n");
        }
#endif
        if (_isPageActive || unloadEpoch != _activationEpoch)
            return;

        UnsubscribeViewModelEvents();
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded UnsubscribeViewModelEvents={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        if (pageOwner is not null)
            await ViewModel.DeactivateForPageAsync(pageOwner);
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded DeactivateAsync={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        if (_isPageActive || unloadEpoch != _activationEpoch)
            return;

        _pageActivationOwner = null;
        stepStopwatch.Restart();
        DisposePreviewBitmap();
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded DisposePreviewBitmap={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanDebugPage.Unloaded total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private bool IsCurrentActivation(int activationEpoch)
    {
#if PRISM_VISUAL_QA
        return !App.IsShuttingDown && _isPageActive && activationEpoch == _activationEpoch;
#else
        return _isPageActive && activationEpoch == _activationEpoch;
#endif
    }

    private void EnqueueForCurrentActivation(Action action)
    {
        var activationEpoch = _activationEpoch;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (IsCurrentActivation(activationEpoch))
                action();
        });
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
        ViewModel.CurrentFilmProfileIssueNavigationRequested += OnCurrentFilmProfileIssueNavigationRequested;
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
        ViewModel.CurrentFilmProfileIssueNavigationRequested -= OnCurrentFilmProfileIssueNavigationRequested;
        _areViewModelEventsSubscribed = false;
    }

    private void OnCurrentFilmProfileIssueNavigationRequested(ScanFilmProfileIssueNavigationRequest request)
    {
        if (!TryResolveCurrentFilmProfileNavigationTarget(request, allowDeferredRealization: false, out var sectionIndex, out var scroller, out var target))
            return;

        _isNarrowPreviewOpen = false;
        _inspectionOpenOverride = false;
        _isConfigurationWorkspaceOpen = sectionIndex is 0 or 1;
        SetActiveWorkbenchSection(sectionIndex);
        EnqueueForCurrentActivation(() =>
        {
            if (!IsCurrentFilmProfileNavigationStillValid())
                return;

            if (request.EditorTarget == ScanFilmProfileIssueEditorTarget.ChannelAssignment)
            {
                AcquisitionChannelAssignmentList.StartBringIntoView();
                AcquisitionChannelAssignmentList.UpdateLayout();
            }

            if (!TryResolveCurrentFilmProfileNavigationTarget(request, allowDeferredRealization: true, out _, out scroller, out target))
            {
                return;
            }

            target.StartBringIntoView();
            _ = target.Focus(FocusState.Programmatic);
            if (target is TextBox textBox)
                textBox.SelectAll();
        });
    }

    private bool IsCurrentFilmProfileNavigationStillValid()
        => _areViewModelEventsSubscribed
            && IsLoaded
            && ViewModel.FilmProfileImportResultReviewVisibility != Visibility.Visible;

    private bool TryResolveCurrentFilmProfileNavigationTarget(ScanFilmProfileIssueNavigationRequest request, bool allowDeferredRealization, out int sectionIndex, out ScrollViewer scroller, out Control target)
    {
        sectionIndex = request.Section switch
        {
            ScanFilmProfileIssueNavigationSection.BasicInfo => 0,
            ScanFilmProfileIssueNavigationSection.AcquisitionPlan => 1,
            ScanFilmProfileIssueNavigationSection.ChannelCalibration => 2,
            _ => -1
        };
        scroller = request.Section switch
        {
            ScanFilmProfileIssueNavigationSection.BasicInfo => BasicInfoScrollViewer,
            ScanFilmProfileIssueNavigationSection.AcquisitionPlan => AcquisitionPlanScrollViewer,
            ScanFilmProfileIssueNavigationSection.ChannelCalibration => ChannelCalibrationScrollViewer,
            _ => null!
        };
        Control? targetCandidate = request.EditorTarget switch
        {
            ScanFilmProfileIssueEditorTarget.ProfileName => ProfileNameTextBox,
            ScanFilmProfileIssueEditorTarget.ColorManagement => ProfileColorManagementToggleSwitch,
            ScanFilmProfileIssueEditorTarget.AlignmentMode => ProfileAlignmentModeComboBox,
            ScanFilmProfileIssueEditorTarget.DngExportMode => ProfileDngExportModeComboBox,
            ScanFilmProfileIssueEditorTarget.RedWavelength => ProfileRedWavelengthTextBox,
            ScanFilmProfileIssueEditorTarget.GreenWavelength => ProfileGreenWavelengthTextBox,
            ScanFilmProfileIssueEditorTarget.BlueWavelength => ProfileBlueWavelengthTextBox,
            ScanFilmProfileIssueEditorTarget.OutputGamma => ProfileOutputGammaTextBox,
            ScanFilmProfileIssueEditorTarget.TargetWhitePointMode => ProfileTargetWhitePointComboBox,
            ScanFilmProfileIssueEditorTarget.ManualWhitePointColorTemperature => ProfileManualWhitePointColorTemperatureTextBox,
            ScanFilmProfileIssueEditorTarget.Rows => AcquisitionRowsComboBox,
            ScanFilmProfileIssueEditorTarget.ScanMotor => AcquisitionScanMotorComboBox,
            ScanFilmProfileIssueEditorTarget.MotorDistancePerLine => AcquisitionMotorDistancePerLineTextBox,
            ScanFilmProfileIssueEditorTarget.TransportStrategy => AcquisitionTransportStrategyToggleSwitch,
            ScanFilmProfileIssueEditorTarget.ChannelAssignment => allowDeferredRealization ? FindFirstEligibleAssignmentCheckBox() : AcquisitionChannelAssignmentList,
            ScanFilmProfileIssueEditorTarget.Illumination => CurrentCalibrationIlluminationLevelTextBox,
            ScanFilmProfileIssueEditorTarget.ChannelStatus => CalibrationChannelStatusListView,
            ScanFilmProfileIssueEditorTarget.ChannelParameters => ExposureMicrosecondsTextBox,
            ScanFilmProfileIssueEditorTarget.ChannelRoiSettings => AdcRoiStartTextBox,
            _ => null!
        };

        target = targetCandidate!;
        return sectionIndex >= 0 && scroller is not null && target is not null;
    }

    private CheckBox? FindFirstEligibleAssignmentCheckBox()
    {
        return FindDescendantCheckBox(AcquisitionChannelAssignmentList);
    }

    private static CheckBox? FindDescendantCheckBox(DependencyObject parent)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is CheckBox { Visibility: Visibility.Visible, IsEnabled: true } checkBox)
                return checkBox;

            var descendant = FindDescendantCheckBox(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    private void OnRoiIssueNavigationRequested(ScanRoiIssueNavigationRequest request)
    {
        if (!TryResolveRoiNavigationTarget(request, out var sectionIndex, out var scroller, out var target))
            return;

        _inspectionOpenOverride = false;
        _isConfigurationWorkspaceOpen = false;
        SetActiveWorkbenchSection(sectionIndex);
        EnqueueForCurrentActivation(() =>
        {
            target.StartBringIntoView();
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

    private void RawSignalCanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        => sender.Invalidate();

    private void RawSignalCanvasControl_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        => RawSignalCanvasControl.Invalidate();

    private void RawSignalCanvasControl_ActualThemeChanged(FrameworkElement sender, object args)
        => RawSignalCanvasControl.Invalidate();

    private void RawSignalCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var result = ViewModel.RawSignalResult;
        if (result is null || RawSignalProfileScrollViewer.Visibility != Visibility.Visible)
            return;

        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;
        const float plotLeft = 48;
        const float plotTop = 16;
        const float plotRightInset = 16;
        const float plotBottomInset = 28;
        const float plotGap = 24;
        var plotRight = width - plotRightInset;
        var plotWidth = plotRight - plotLeft;
        var availableHeight = height - plotTop - plotBottomInset - plotGap;
        if (plotWidth < 1 || availableHeight < 1)
            return;

        var profileBottom = plotTop + availableHeight * 2 / 3;
        var histogramTop = profileBottom + plotGap;
        var histogramBottom = height - plotBottomInset;
        var profileColor = ((SolidColorBrush)RawSignalProfileSwatch.Foreground).Color;
        var histogramColor = ((SolidColorBrush)RawSignalHistogramSwatch.Foreground).Color;
        var axisColor = ((SolidColorBrush)RawSignalAxisSwatch.Foreground).Color;
        var gridColor = ((SolidColorBrush)RawSignalGridSwatch.Foreground).Color;
        var drawing = args.DrawingSession;

        drawing.DrawLine(plotLeft, plotTop, plotRight, plotTop, gridColor);
        drawing.DrawLine(plotLeft, profileBottom, plotRight, profileBottom, axisColor);
        drawing.DrawLine(plotLeft, plotTop, plotLeft, profileBottom, axisColor);
        drawing.DrawText("65535", 0, plotTop, axisColor);
        drawing.DrawText("0", 24, profileBottom - 16, axisColor);

        var profile = result.Profile;
        if (profile.Count > 0)
        {
            var columnSpan = Math.Max(1, profile[^1].Column - profile[0].Column);
            var buckets = ScanRawSignalAnalyzer.ReduceProfile(profile, Math.Min(profile.Count, Math.Max(1, (int)plotWidth)));
            if (buckets.Count == profile.Count)
            {
                ScanRawProfileSample? previous = null;
                foreach (var bucket in buckets)
                {
                    var current = bucket.Minimum;
                    var x = plotLeft + (current.Column - profile[0].Column) * plotWidth / columnSpan;
                    var y = profileBottom - current.Value * (profileBottom - plotTop) / ushort.MaxValue;
                    if (previous is { } last)
                    {
                        var previousX = plotLeft + (last.Column - profile[0].Column) * plotWidth / columnSpan;
                        var previousY = profileBottom - last.Value * (profileBottom - plotTop) / ushort.MaxValue;
                        drawing.DrawLine(previousX, previousY, x, y, profileColor, 2);
                    }
                    else
                    {
                        drawing.FillCircle(x, y, 2, profileColor);
                    }
                    previous = current;
                }
            }
            else
            {
                foreach (var bucket in buckets)
                {
                    var column = bucket.Minimum.Column / 2.0 + bucket.Maximum.Column / 2.0;
                    var x = plotLeft + (float)((column - profile[0].Column) * plotWidth / columnSpan);
                    var minimumY = profileBottom - bucket.Minimum.Value * (profileBottom - plotTop) / ushort.MaxValue;
                    var maximumY = profileBottom - bucket.Maximum.Value * (profileBottom - plotTop) / ushort.MaxValue;
                    if (bucket.Minimum.Value == bucket.Maximum.Value)
                        drawing.FillCircle(x, minimumY, 1, profileColor);
                    else
                        drawing.DrawLine(x, minimumY, x, maximumY, profileColor, 2);
                }
            }

            drawing.DrawText(profile[0].Column.ToString(), plotLeft, profileBottom + 4, axisColor);
            drawing.DrawText(profile[^1].Column.ToString(), plotRight - 40, profileBottom + 4, axisColor);
        }

        drawing.DrawLine(plotLeft, histogramBottom, plotRight, histogramBottom, axisColor);
        drawing.DrawLine(plotLeft, histogramTop, plotLeft, histogramBottom, axisColor);
        var maximumCount = result.Histogram.Max();
        if (maximumCount > 0)
        {
            var barWidth = plotWidth / result.Histogram.Count;
            for (var bin = 0; bin < result.Histogram.Count; bin++)
            {
                var count = result.Histogram[bin];
                if (count == 0)
                    continue;

                var barHeight = (float)(count / (double)maximumCount * (histogramBottom - histogramTop));
                drawing.FillRectangle(plotLeft + bin * barWidth, histogramBottom - barHeight,
                    barWidth, barHeight, histogramColor);
            }
        }

        drawing.DrawText("0", plotLeft, histogramBottom + 4, axisColor);
        drawing.DrawText(ushort.MaxValue.ToString(), plotRight - 40, histogramBottom + 4, axisColor);
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
            await ShowPageDialogAsync(() => new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = e.Prompt.Title,
                Content = e.Prompt.Content,
                PrimaryButtonText = e.Prompt.PrimaryButtonText,
                CloseButtonText = e.Prompt.CloseButtonText,
                DefaultButton = ContentDialogButton.Primary
            }, e.HostCancellationToken,
                result => e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary),
                () => e.CompletionSource.TrySetResult(false),
                ex => e.CompletionSource.TrySetException(ex));
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
            await ShowPageDialogAsync(() => new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = e.TitleResourceKey.GetLocalized(),
                Content = e.MessageResourceKey.GetLocalized(),
                PrimaryButtonText = e.PrimaryButtonResourceKey.GetLocalized(),
                CloseButtonText = e.CloseButtonResourceKey.GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            }, e.HostCancellationToken,
                result => e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary),
                () => e.CompletionSource.TrySetResult(false),
                ex => e.CompletionSource.TrySetException(ex));
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
            Action completeOnPageRetirement = () => e.CompletionSource.TrySetResult();
            await ShowPageDialogAsync(() => new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = e.Title,
                Content = e.Content,
                CloseButtonText = e.CloseButtonText,
                DefaultButton = ContentDialogButton.Close
            }, e.HostCancellationToken,
                _ => e.CompletionSource.TrySetResult(),
                () =>
                {
                    if (e.HostCancellationToken.IsCancellationRequested)
                        e.CompletionSource.TrySetCanceled(e.HostCancellationToken);
                    else
                        completeOnPageRetirement();
                },
                ex => e.CompletionSource.TrySetException(ex));
        }
        catch (Exception ex)
        {
            e.CompletionSource.TrySetException(ex);
        }
    }

    private Task ShowPageDialogAsync(
        Func<ContentDialog> createDialog,
        CancellationToken hostCancellationToken,
        Action<ContentDialogResult> complete,
        Action retire,
        Action<Exception> fault)
    {
        var activationEpoch = _activationEpoch;
        var pageOwner = _pageActivationOwner;
        if (pageOwner is null || !IsCurrentActivation(activationEpoch)
            || !ViewModel.IsCurrentPageOwner(pageOwner) || hostCancellationToken.IsCancellationRequested)
        {
            retire();
            return Task.CompletedTask;
        }

        var dialog = createDialog();
        var dispatcher = DispatcherQueue;
        var lease = _dialogLifetime.TryAcquire(this, activationEpoch, showStarted =>
        {
            retire();
            if (!showStarted)
                return;

            try
            {
                if (!dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        dialog.Hide();
                    }
                    catch (Exception ex)
                    {
                        Debugger.Log(0, "ScanDebugDialog", $"Dialog hide failed: {ex}\n");
                    }
                }))
                {
                    Debugger.Log(0, "ScanDebugDialog", "Dialog hide dispatch was rejected; request retired.\n");
                }
            }
            catch (Exception ex)
            {
                Debugger.Log(0, "ScanDebugDialog", $"Dialog hide dispatch failed; request retired: {ex}\n");
            }
        });
        if (lease is null)
        {
            retire();
            return Task.CompletedTask;
        }

        return lease.RunAsync(hostCancellationToken,
            () => IsCurrentActivation(activationEpoch)
                && ReferenceEquals(_pageActivationOwner, pageOwner)
                && ViewModel.IsCurrentPageOwner(pageOwner),
            async () => await dialog.ShowAsync(), complete, fault);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanDebugViewModel.FilmProfileImportResultReviewVisibility))
            EnqueueForCurrentActivation(UpdateWorkbenchReviewEntry);

        if (e.PropertyName == nameof(ScanDebugViewModel.PreviewFrame))
            EnqueueForCurrentActivation(() =>
            {
                RefreshPreviewLayout();
                PreviewCanvasControl.Invalidate();
            });

        if (e.PropertyName == nameof(ScanDebugViewModel.RawSignalResult))
            EnqueueForCurrentActivation(UpdateRawSignalResultSurface);

        if (e.PropertyName == nameof(ScanDebugViewModel.RoiOverlayVersion)
            || e.PropertyName == nameof(ScanDebugViewModel.SelectedRoiSelection)
            || e.PropertyName == nameof(ScanDebugViewModel.IsRoiEditModeEnabled)
            || e.PropertyName == nameof(ScanDebugViewModel.IsImageReferenceOverlayVisible)
            || e.PropertyName == nameof(ScanDebugViewModel.ColumnSampleOverlayVersion)
            || e.PropertyName == nameof(ScanDebugViewModel.IsColumnSampleEditModeEnabled))
        {
            EnqueueForCurrentActivation(DrawRoiOverlays);
        }

        if (e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationChannel)
            || e.PropertyName == nameof(ScanDebugViewModel.HasCurrentCalibrationIlluminationChannel)
            || e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationLevel)
            || e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationPulseClock)
            || e.PropertyName == nameof(ScanDebugViewModel.CurrentCalibrationIlluminationWorkMode))
        {
            EnqueueForCurrentActivation(RefreshCurrentCalibrationIlluminationEditor);
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
            EnqueueForCurrentActivation(ApplyInitialFitZoom);
    }

    private void UpdatePreviewEmptyStateVisibility()
    {
        var hasPreviewFrame = ViewModel.PreviewFrame is { Width: > 0, Height: > 0 };
        PreviewEmptyStateGrid.Visibility = hasPreviewFrame || ReferenceEquals(RawSignalModeSelector.SelectedItem, RawSignalProfileModeItem)
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

public sealed class FilmProfileValidationIssueTemplateSelector : DataTemplateSelector
{
    public DataTemplate? NavigableTemplate { get; set; }

    public DataTemplate? PassiveTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => SelectFilmProfileValidationIssueTemplate(item);

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectFilmProfileValidationIssueTemplate(item);

    private DataTemplate? SelectFilmProfileValidationIssueTemplate(object item)
        => CanNavigateFilmProfileValidationIssue(item) ? NavigableTemplate : PassiveTemplate;

    private static bool CanNavigateFilmProfileValidationIssue(object item)
        => item is ScanFilmProfileValidationIssueDisplay { CanNavigate: true };
}
