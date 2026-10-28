using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.WinUI.Controls;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
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
    private static readonly float[] ZoomLevels = { 0.001f, 0.0025f, 0.005f, 0.01f, 0.025f, 0.05f, 0.1f, 0.125f, 0.2f, 0.25f, 0.5f, 0.75f, 1f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 20f };
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
    private ScanFilmProfileEditRequest? _pendingScanProfileEdit;
    private int _activationEpoch;
    private object? _pageActivationOwner;
    private readonly ScanDebugDialogLifetime _dialogLifetime = ScanDebugDialogLifetime.ForCurrentThread;
    private bool _isUpdatingCurrentCalibrationIlluminationEditor;
    private bool _isSynchronizingWorkbenchSection;
    private int _activeWorkbenchSectionIndex = 1;
    private int _lastWorkbenchTaskSectionIndex = 1;
    private static readonly int[] WorkbenchTaskSectionIndices = [1, 2, 3, 5];
    private static readonly int[] WorkbenchPropertySectionIndices = [0, 4, 5];
    private bool _isNarrowPreviewOpen;
    private bool _isAuxiliaryPanelOpen;
    private bool _isAcquisitionTransportIssueOpen;
    private bool? _inspectionOpenOverride;
    private bool? _previewCollapsedOverride;
    private readonly string _inspectionActionLabel;
    private readonly string _openPreviewActionLabel;
    private readonly string _hidePreviewActionLabel;
    private double _workbenchPreviewEditorRatio = double.NaN;
    private double _auxiliaryPanelEditorRatio = 0.60;
    private int _workbenchFocusHandoffVersion;
    private int _bwFocusRequestVersion;
    private int _issueTargetFocusRequestVersion;
    private EventHandler<object>? _bwIssueLayoutHandler;
    private EventHandler<ScrollViewerViewChangedEventArgs>? _bwIssueViewChangedHandler;
    private ScrollViewer? _bwIssueScrollOwner;
    private DependencyObject? _bwIssueExpectedFocus;
    private double? _bwCompactEditorScrollOffset;
    private EventHandler<object>? _bwCompactReturnLayoutHandler;
    private EventHandler<object>? _issueTargetLayoutHandler;
    private EventHandler<ScrollViewerViewChangedEventArgs>? _issueTargetViewChangedHandler;
    private Control? _issueTargetFocusControl;
    private ScrollViewer? _issueTargetViewport;
    private DependencyObject? _issueTargetExpectedFocus;
    private bool _issueTargetBringIntoViewRequested;
    private bool _isUpdatingCalibrationChannelSelection;
    private bool _calibrationChannelSelectionPending;
    private bool _bwCompactMode;
    private bool _isSynchronizingAcquisitionRows;
    private bool _isCancelingAcquisitionRowsEdit;
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
        _inspectionActionLabel = (string)InspectionToggleButton.Content;
        _openPreviewActionLabel = "ScanDebug_PreviewOpenAction".GetLocalized();
        _hidePreviewActionLabel = "ScanDebug_PreviewHideAction".GetLocalized();
        StabilizePreviewToggleWidth();
        InitializeCalibrationChannelSelector();
        ComposeWorkspaceHeader();
        ArrangeBlackWhiteTaskControls();
        SetActiveWorkbenchSection(_activeWorkbenchSectionIndex);
        UpdateRawSignalMode();
        InitializeCurrentCalibrationIlluminationEditor();
        CurrentIlluminationDraftLabel.Text = "ScanDebug_BwIssueCurrentDraft".GetLocalized();
        NavigationTimingLogger.Write($"ScanDebugPage.ctor InitializeComponent={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanDebugPage.ctor total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _pendingScanProfileEdit = e.Parameter as ScanFilmProfileEditRequest;
    }

    private void DeferredExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.Tag is string contentName)
            FindName(contentName);
    }

    private void AcquisitionRowsComboBox_Loaded(object sender, RoutedEventArgs e)
        => SynchronizeAcquisitionRowsEditor();

    private void SynchronizeAcquisitionRowsEditor()
    {
        _isSynchronizingAcquisitionRows = true;
        try
        {
            AcquisitionRowsComboBox.SelectedItem = ViewModel.SelectedRows;
            AcquisitionRowsComboBox.Text = ViewModel.SelectedRows;
        }
        finally
        {
            _isSynchronizingAcquisitionRows = false;
        }
    }

    private void AcquisitionRowsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isSynchronizingAcquisitionRows && !_isCancelingAcquisitionRowsEdit
            && e.AddedItems.FirstOrDefault() is string rows)
            ViewModel.SelectedRows = rows;
    }

    private void AcquisitionRowsComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        // The editable ComboBox can clear its selection when closing. Only an explicit
        // custom-text submission or committed preset is allowed to change the draft.
        args.Handled = true;
        if (!_isCancelingAcquisitionRowsEdit)
            ViewModel.SelectedRows = args.Text.Trim();
        SynchronizeAcquisitionRowsEditor();
    }

    private void AcquisitionRowsComboBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape)
            return;

        _isCancelingAcquisitionRowsEdit = true;
        SynchronizeAcquisitionRowsEditor();
        EnqueueForCurrentActivation(() =>
        {
            SynchronizeAcquisitionRowsEditor();
            _isCancelingAcquisitionRowsEdit = false;
        });
    }

    private void AcquisitionRowsComboBox_DropDownClosed(object sender, object e)
        => SynchronizeAcquisitionRowsEditor();

    private void ComposeWorkspaceHeader()
    {
        CaptureModeComboBox.Header = null;
        // The details route already has its own title; avoid a second disclosure.
        var detailParent = ChannelCalibrationTaskStack;
        var details = ChannelCalibrationDetailsGrid;
        ChannelCalibrationDetailsExpander.Content = null;
        detailParent.Children.Insert(detailParent.Children.IndexOf(ChannelCalibrationDetailsExpander) + 1, details);

        // Keep image tools beside their canvas; the task rail stays compact.
        var zoomPanel = PreviewZoomStepPanel;
        zoomPanel.Children.Remove(ZoomOutButton);
        zoomPanel.Children.Remove(ZoomInButton);
        PreviewToolbar.Children.Remove(zoomPanel);
        PreviewToolbar.Children.Insert(PreviewToolbar.Children.IndexOf(ZoomScaleComboBox), ZoomOutButton);
        PreviewToolbar.Children.Insert(PreviewToolbar.Children.IndexOf(ZoomScaleComboBox) + 1, ZoomInButton);
        PreviewToolbar.Children.Remove(FreezeObservationButton);
        ((StackPanel)((Flyout)PreviewAuxiliaryToolsButton.Flyout).Content).Children.Insert(0, FreezeObservationButton);
    }

    private void UpdateAcquisitionTransportVisibility()
    {
        if (_isAcquisitionTransportIssueOpen && _activeWorkbenchSectionIndex == 1)
        {
            AcquisitionTransportDetailsExpander.Visibility = Visibility.Visible;
            return;
        }

        if (!ViewModel.IsScanMotorTransportEnabled
            && AcquisitionTransportDetailsExpander.Visibility == Visibility.Visible
            && XamlRoot is { } root
            && FocusManager.GetFocusedElement(root) is DependencyObject focused
            && IsFocusedWithin(focused, AcquisitionTransportDetailsExpander))
            _ = CaptureModeComboBox.Focus(FocusState.Programmatic);

        AcquisitionTransportDetailsExpander.Visibility = ToVisibility(ViewModel.IsScanMotorTransportEnabled);
    }

    private void BwReviewCandidateButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCandidateReview();
    }

    private void ArrangeBlackWhiteTaskControls()
    {
        ChannelCalibrationTaskStack.Children.Remove(ChannelCalibrationIlluminationCard);
        ChannelCalibrationTaskStack.Children.Insert(
            ChannelCalibrationTaskStack.Children.IndexOf(ChannelCalibrationDetailsGrid), ChannelCalibrationIlluminationCard);

        ChannelCalibrationActionsStack.Children.Remove(ColumnSampleGroupTitle);
        ColumnSampleUseStack.Children.Insert(
            ColumnSampleUseStack.Children.IndexOf(UseSampleAsManualBlackLevelButton), ColumnSampleGroupTitle);
        ChannelCalibrationActionsStack.Children.Remove(ImageReferenceRoiEditorCard);
        ColumnSampleUseStack.Children.Insert(
            ColumnSampleUseStack.Children.IndexOf(UseSampleAsManualBlackLevelButton), ImageReferenceRoiEditorCard);
        ChannelCalibrationActionsStack.Children.Remove(ColumnSampleSaveActions);
        ColumnSampleUseStack.Children.Insert(
            ColumnSampleUseStack.Children.IndexOf(ClearManualReferenceLevelsButton), ColumnSampleSaveActions);

        var detailGrid = ChannelCalibrationDetailsGrid;
        foreach (var section in detailGrid.Children.OfType<Border>().OrderBy(Grid.GetRow).ToArray())
        {
            detailGrid.Children.Remove(section);
            detailGrid.Children.Add(section);
        }
    }

    private void MotorAxisComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Motor1DetailsExpander is null || Motor2DetailsExpander is null || Motor3DetailsExpander is null)
            return;

        Motor1DetailsExpander.Visibility = ToVisibility(MotorAxisComboBox.SelectedIndex == 0);
        Motor2DetailsExpander.Visibility = ToVisibility(MotorAxisComboBox.SelectedIndex == 1);
        Motor3DetailsExpander.Visibility = ToVisibility(MotorAxisComboBox.SelectedIndex == 2);
        Motor1SettingsExpander.Visibility = Motor1DetailsExpander.Visibility;
        Motor2SettingsExpander.Visibility = Motor2DetailsExpander.Visibility;
        Motor3SettingsExpander.Visibility = Motor3DetailsExpander.Visibility;
        Motor1StatusTextBlock.Visibility = Motor1DetailsExpander.Visibility;
        Motor2StatusTextBlock.Visibility = Motor2DetailsExpander.Visibility;
        Motor3StatusTextBlock.Visibility = Motor3DetailsExpander.Visibility;
        Motor1MoveButton.Visibility = Motor1DetailsExpander.Visibility;
        Motor2MoveButton.Visibility = Motor2DetailsExpander.Visibility;
        Motor3MoveButton.Visibility = Motor3DetailsExpander.Visibility;
        var stopButtons = new[] { Motor1StopButton, Motor2StopButton, Motor3StopButton };
        var nextOtherStopColumn = 0;
        for (var index = 0; index < stopButtons.Length; index++)
        {
            var isCurrentAxis = MotorAxisComboBox.SelectedIndex == index;
            Grid.SetRow(stopButtons[index], isCurrentAxis ? 0 : 1);
            Grid.SetColumn(stopButtons[index], isCurrentAxis ? 1 : nextOtherStopColumn++);
        }
    }

    private void UpdateMotorAxisLabels()
    {
        if (MotorAxisComboBox is null)
            return;

        var roles = new[] { ViewModel.Motor1RoleText, ViewModel.Motor2RoleText, ViewModel.Motor3RoleText };
        for (var index = 0; index < MotorAxisComboBox.Items.Count; index++)
        {
            if (MotorAxisComboBox.Items[index] is not ComboBoxItem item)
                continue;

            var name = $"Scan_Runtime_MotorOption{index + 1}".GetLocalizedOrFallback($"Motor {index + 1} (M{index + 1})");
            item.Content = string.IsNullOrWhiteSpace(roles[index]) ? name : $"{name} · {roles[index]}";
            AutomationProperties.SetName(item, item.Content.ToString());
        }
    }

    private void PairedField_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Control field)
            return;
        var header = field switch
        {
            TextBox textBox => textBox.Header as string,
            ComboBox comboBox => comboBox.Header as string,
            _ => null
        };
        if (header is not null)
        {
            field.Tag = header;
            if (string.IsNullOrEmpty(AutomationProperties.GetName(field)))
                AutomationProperties.SetName(field, header);
        }
        else if (field.Tag is not string)
            return;

        if (field is TextBox text)
        {
            text.Header = null;
            text.HeaderTemplate = null;
        }
        else if (field is ComboBox combo)
        {
            combo.Header = null;
            combo.HeaderTemplate = null;
        }
    }

    private void AlignMotionUnitHeaders()
    {
        foreach (var unit in new[] { Motor1MoveUnitComboBox, Motor1SpeedUnitComboBox,
            Motor2MoveUnitComboBox, Motor2SpeedUnitComboBox, Motor3MoveUnitComboBox, Motor3SpeedUnitComboBox })
        {
            if (unit.Header is not string localizedHeader)
                continue;

            unit.Tag = localizedHeader;
            AutomationProperties.SetName(unit, localizedHeader);
            unit.Header = null;
        }
    }

    private void InitializeCalibrationChannelSelector()
    {
        var converter = (ScanSelectorDisplayNameConverter)Resources["ScanSelectorDisplayNameConverter"];
        foreach (var role in ViewModel.CalibrationChannelOptions)
        {
            var name = (string)converter.Convert(role, typeof(string), "CalibrationChannel", string.Empty);
            var item = new SelectorBarItem { Text = $"ScanDebug_CalibrationChannelShort{role}".GetLocalized(), Tag = role };
            AutomationProperties.SetName(item, name);
            AutomationProperties.SetAutomationId(item, $"CalibrationChannel_{role}");
            ToolTipService.SetToolTip(item, name);
            CalibrationChannelSelectorBar.Items.Add(item);
        }
        SynchronizeCalibrationChannelSelectors();
    }

    private void SynchronizeCalibrationChannelSelectors()
    {
        _isUpdatingCalibrationChannelSelection = true;
        try
        {
            CalibrationChannelFallbackComboBox.SelectedItem = ViewModel.SelectedCalibrationChannel;
            CalibrationChannelSelectorBar.SelectedItem = CalibrationChannelSelectorBar.Items.FirstOrDefault(item =>
                string.Equals(item.Tag as string, ViewModel.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isUpdatingCalibrationChannelSelection = false;
        }
    }

    private async void CalibrationChannelFallbackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CalibrationChannelFallbackComboBox.SelectedItem is string nextChannel)
            await SelectCalibrationChannelAsync(nextChannel);
    }

    private async void CalibrationChannelSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string nextChannel)
            await SelectCalibrationChannelAsync(nextChannel);
    }

    private async Task SelectCalibrationChannelAsync(string nextChannel)
    {
        if (!_isPageActive || _isUpdatingCalibrationChannelSelection
            || string.Equals(nextChannel, ViewModel.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
            return;

        SynchronizeCalibrationChannelSelectors();
        if (_calibrationChannelSelectionPending)
            return;

        _calibrationChannelSelectionPending = true;
        var activationEpoch = _activationEpoch;
        try
        {
            await ViewModel.TrySelectCalibrationChannelAsync(nextChannel);
        }
        finally
        {
            _calibrationChannelSelectionPending = false;
            if (IsCurrentActivation(activationEpoch) && _activeWorkbenchSectionIndex == 2)
            {
                SynchronizeCalibrationChannelSelectors();
            }
        }
    }

    private void ChannelCalibrationSection_Loaded(object sender, RoutedEventArgs e)
        => UpdateChannelCalibrationRailLayout(ChannelCalibrationSection.ActualHeight);

    private void ChannelCalibrationSection_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        => UpdateChannelCalibrationRailLayout(e.NewSize.Height);

    private void ChannelCalibrationSummary_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        => UpdateChannelCalibrationRailLayout(ChannelCalibrationSection.ActualHeight);

    private void UpdateChannelCalibrationRailLayout(double availableHeight)
    {
        if (_isCandidateReviewOpen)
            return;
        if (ChannelCalibrationRail is null || ChannelCalibrationCompactScrollViewer is null
            || BwRoiSummary is null || !ChannelCalibrationSection.IsLoaded || availableHeight <= 0)
            return;

        var useSingleScroll = availableHeight < Math.Max(440,
            ChannelCalibrationIdentityHeader.ActualHeight + ChannelCalibrationSaveFooter.ActualHeight + 280);
        if (_bwCompactMode == useSingleScroll)
            return;

        _bwCompactMode = useSingleScroll;
        // A pending navigation may have requested visibility before the compact
        // scroll owner was arranged. Rebind it to the owner that will actually scroll.
        _issueTargetBringIntoViewRequested = false;
        if (_issueTargetViewChangedHandler is not null && _issueTargetViewport is not null)
        {
            _issueTargetViewport.ViewChanged -= _issueTargetViewChangedHandler;
            _issueTargetViewport = useSingleScroll ? ChannelCalibrationCompactScrollViewer : ChannelCalibrationScrollViewer;
            _issueTargetViewport.ViewChanged += _issueTargetViewChangedHandler;
        }
        if (useSingleScroll)
        {
            ChannelCalibrationSection.Children.Remove(ChannelCalibrationRail);
            ChannelCalibrationBodyRow.Height = GridLength.Auto;
            ChannelCalibrationScrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            ChannelCalibrationScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ChannelCalibrationCompactScrollViewer.Content = ChannelCalibrationRail;
            ChannelCalibrationCompactScrollViewer.Visibility = Visibility.Visible;
        }
        else
        {
            ChannelCalibrationCompactScrollViewer.Content = null;
            ChannelCalibrationCompactScrollViewer.Visibility = Visibility.Collapsed;
            ChannelCalibrationSection.Children.Insert(0, ChannelCalibrationRail);
            ChannelCalibrationBodyRow.Height = new GridLength(1, GridUnitType.Star);
            ChannelCalibrationScrollViewer.VerticalScrollMode = ScrollMode.Auto;
            ChannelCalibrationScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer.VerticalScrollMode = ScrollMode.Auto;
            ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    private void BwAutoActionGrid_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (BwAutoBlackButton is null || BwAutoWhiteButton is null)
            return;

        var stackSingleActions = e.NewSize.Width < 232;
        Grid.SetColumn(BwAutoWhiteButton, stackSingleActions ? 0 : 1);
        Grid.SetColumnSpan(BwAutoBlackButton, stackSingleActions ? 2 : 1);
        Grid.SetColumnSpan(BwAutoWhiteButton, stackSingleActions ? 2 : 1);
        Grid.SetRow(BwAutoWhiteButton, stackSingleActions ? 1 : 0);
    }

    private void WorkbenchSectionSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())
            return;

        var index = GetWorkbenchSectionSelectorIndex(sender, sender.SelectedItem);
        if ((uint)index < WorkbenchPropertySectionIndices.Length)
            SetActiveWorkbenchSection(WorkbenchPropertySectionIndices[index]);
    }



    private void WorkbenchTaskSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())
            return;

        SelectWorkbenchTask(GetWorkbenchSectionSelectorIndex(sender, sender.SelectedItem));
    }

    private void WorkbenchTaskComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())
            return;

        SelectWorkbenchTask(WorkbenchTaskComboBox.SelectedIndex);
    }

    private void SelectWorkbenchTask(int taskIndex)
    {
        if ((uint)taskIndex >= WorkbenchTaskSectionIndices.Length)
            return;

        _isAuxiliaryPanelOpen = false;
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.ReviewDockThreshold)
            FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.WideThreshold)
            _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(WorkbenchTaskSectionIndices[taskIndex]);
    }

    private void WorkbenchTaskButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_isAuxiliaryPanelOpen || sender is not SelectorBarItem item)
            return;
        var index = WorkbenchTaskSelectorBar.Items.IndexOf(item);
        if ((uint)index >= WorkbenchTaskSectionIndices.Length)
            return;
        _isAuxiliaryPanelOpen = false;
        _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(WorkbenchTaskSectionIndices[index]);
    }

    private void OpenBlackWhiteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        CloseCandidateReview(returnFocus: false);
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.ReviewDockThreshold)
            FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        _isAuxiliaryPanelOpen = false;
        _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(2);
        EnqueueForCurrentActivation(() => GetActiveWorkbenchTaskButton().Focus(FocusState.Programmatic));
    }

    private void InspectionEditRoiButton_Click(object sender, RoutedEventArgs e)
    {
        FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        _isAuxiliaryPanelOpen = false;
        _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(2);
        CancelChannelRouteScrollRestore();
        BwRoiEditButton_Click(sender, e);
    }

    private void OpenConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.ReviewDockThreshold)
            FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        _isAuxiliaryPanelOpen = true;
        _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(0);
        EnqueueForCurrentActivation(() => WorkbenchSectionSelectorBar.Focus(FocusState.Programmatic));
    }

    private void OpenDevicePropertiesButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.ReviewDockThreshold)
            FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        _isAuxiliaryPanelOpen = true;
        _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(4);
        EnqueueForCurrentActivation(() => WorkbenchSectionSelectorBar.Focus(FocusState.Programmatic));
    }

    private void OpenAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigurationFileFlyout.Hide();
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.ReviewDockThreshold)
            FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        _isAuxiliaryPanelOpen = true;
        _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(5);
        EnqueueForCurrentActivation(() => ReturnToWorkbenchTaskButton.Focus(FocusState.Programmatic));
    }

    private void RunScannerConnectionButton_Click(object sender, RoutedEventArgs e)
        => OpenScannerConnectionFlyout(sender as FrameworkElement);

    private static void OpenScannerConnectionFlyout(FrameworkElement? origin)
    {
        if (App.MainWindow.Content is ShellPage shellPage)
            shellPage.ShowScannerConnectionFlyout(origin);
    }

    private void ReturnToWorkbenchTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isAuxiliaryPanelOpen)
            return;

        _isAuxiliaryPanelOpen = false;
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.WideThreshold)
            _inspectionOpenOverride = false;
        SetActiveWorkbenchSection(_lastWorkbenchTaskSectionIndex);
        EnqueueForCurrentActivation(() => GetActiveWorkbenchTaskButton().Focus(FocusState.Programmatic));
    }

    private async void SaveAndOpenScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.SaveFilmProfileJsonCommand.CanExecute(null))
        {
            ViewModel.ValidateFilmProfileCommand.Execute(null);
            ShowFilmProfileReview();
            return;
        }

        var activationEpoch = _activationEpoch;
        var isSaved = await ViewModel.EnsureSavedFilmProfileForScanAsync();
        if (IsCurrentActivation(activationEpoch) && isSaved)
            App.GetService<INavigationService>().NavigateTo(AppRoute.Scan, ScanViewModel.UseSavedFilmProfileNavigationParameter);
    }

    private void ShowFilmProfileReview()
    {
        _inspectionOpenOverride = false;
        FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Visible;
        FilmProfileLifecycleDetailsScrollViewer.ChangeView(null, 0, null);
        UpdateWorkbenchPreviewLayout();
    }

    private void CloseFilmProfileReview()
    {
        FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        UpdateWorkbenchPreviewLayout();
    }

    private void CloseValidationReviewButton_Click(object sender, RoutedEventArgs e)
    {
        CloseFilmProfileReview();
        WorkbenchValidationButton.Focus(FocusState.Programmatic);
    }

    private void RawSignalModeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (RawSignalProfileScrollViewer is not null && PreviewScrollViewer is not null)
            UpdateRawSignalMode();
    }

    private void UpdateRawSignalMode()
    {
        var isProfile = ReferenceEquals(RawSignalModeSelector.SelectedItem, RawSignalProfileModeItem);
        CancelPreviewVisualInteraction();
        if (isProfile)
        {
            ViewModel.IsRoiEditModeEnabled = false;
            ViewModel.IsColumnSampleEditModeEnabled = false;
            ViewModel.IsObservationSelectionEnabled = false;
        }
        RawSignalProfileScrollViewer.Visibility = ToVisibility(isProfile);
        PreviewScrollViewer.Visibility = Visibility.Visible;
        LineProfileImageRow.Height = new GridLength(1, GridUnitType.Star);
        LineProfileChartRow.Height = isProfile ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
        UpdatePreviewEmptyStateVisibility();
        UpdateRawSignalResultSurface();
        DrawRoiOverlays();
    }

    private void UpdateRawSignalResultSurface()
    {
        var result = ViewModel.ObservationRawSignalResult;
        var hasResult = result is not null;
        RawSignalResultDetails.Visibility = ToVisibility(hasResult);
        RawSignalCanvasControl.Invalidate();
    }

    private void RawSignalProfileScrollViewer_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (RawSignalProfileGrid is null || RawSignalCanvasControl is null)
            return;

        RawSignalProfileGrid.Height = e.NewSize.Height;
        RawSignalCanvasControl.Invalidate();
    }

    private void UpdateWorkbenchReviewEntry()
    {
        var hasPendingReview = ViewModel.FilmProfileImportResultReviewVisibility == Visibility.Visible;
        if (hasPendingReview)
            ShowFilmProfileReview();
    }

    private void BwRoiEditButton_Click(object sender, RoutedEventArgs e)
    {
        CloseCandidateReview(returnFocus: false);
        ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();
        ExitBwIssueView(returnFocus: false);
        _ = BwRoiEditButton.Focus(FocusState.Programmatic);
        AdcRoiDetailsExpander.IsExpanded = true;
        BeginIssueTargetFocus(2, ChannelCalibrationScrollViewer, AdcRoiTargetComboBox,
            ViewModel.SelectedCalibrationChannel);
    }

    private void BwViewIssuesButton_Click(object sender, RoutedEventArgs e)
    {
        CloseCandidateReview(returnFocus: false);
        if (double.IsFinite(BwViewIssuesButton.ActualHeight) && BwViewIssuesButton.ActualHeight > 0)
        {
            BwValidationActionPanel.MinHeight = BwViewIssuesButton.ActualHeight;
            var inset = Math.Max(0, (BwViewIssuesButton.ActualHeight - BwValidationSummaryTextBlock.ActualHeight) / 2);
            BwValidationSummaryTextBlock.Margin = new Thickness(0, inset, 0, 0);
            BwViewingIssuesText.Margin = new Thickness(0, inset, 0, 0);
        }
        ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();
        CancelIssueTargetFocus();
        CancelBwIssueFocus();
        var requestVersion = _bwFocusRequestVersion;
        var activationEpoch = _activationEpoch;
        var channelRole = ViewModel.SelectedCalibrationChannel;
        _bwIssueExpectedFocus = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        _bwCompactEditorScrollOffset = _bwCompactMode ? ChannelCalibrationCompactScrollViewer.VerticalOffset : null;
        ChannelCalibrationIssuePanel.Visibility = Visibility.Visible;
        ChannelCalibrationScrollViewer.Visibility = Visibility.Collapsed;
        BwViewIssuesButton.Visibility = Visibility.Collapsed;
        BwViewingIssuesText.Visibility = Visibility.Visible;
        var scrollOwner = _bwCompactMode ? ChannelCalibrationCompactScrollViewer
            : ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer;
        _ = scrollOwner.ChangeView(null, 0, null, true);
        if (BwReturnToParametersButton.Focus(FocusState.Programmatic)
            && XamlRoot is { } focusRoot
            && ReferenceEquals(FocusManager.GetFocusedElement(focusRoot), BwReturnToParametersButton))
            _bwIssueExpectedFocus = BwReturnToParametersButton;

        _bwIssueLayoutHandler = (_, _) => TryFocusBwIssue(requestVersion, activationEpoch, channelRole);
        ChannelCalibrationIssueItemsControl.LayoutUpdated += _bwIssueLayoutHandler;
        _bwIssueScrollOwner = scrollOwner;
        _bwIssueViewChangedHandler = (_, _) => TryFocusBwIssue(requestVersion, activationEpoch, channelRole);
        scrollOwner.ViewChanged += _bwIssueViewChangedHandler;
        EnqueueForCurrentActivation(() => TryFocusBwIssue(requestVersion, activationEpoch, channelRole));
    }

    private void BwReturnToParametersButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();
        ExitBwIssueView(returnFocus: true);
    }

    private void BwOpenChannelDetailsButton_Click(object sender, RoutedEventArgs e)
        => BwConfigureChannelButton_Click(sender, e);

    private void BwManageChannelProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();
        ExitBwIssueView(returnFocus: false);
        _isAuxiliaryPanelOpen = true;
        SetActiveWorkbenchSection(2);
        ChannelCalibrationDetailsExpander.IsExpanded = true;
        _ = ReturnToWorkbenchTaskButton.Focus(FocusState.Programmatic);
        BeginIssueTargetFocus(2, ChannelCalibrationScrollViewer, CalibrationCopySourceComboBox,
            ViewModel.SelectedCalibrationChannel);
    }

    private void CalibrationGuideNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CalibrationGuideActionEnabled)
            NavigateCalibrationGuideStep(ViewModel.CalibrationGuideStep, sender as FrameworkElement);
    }

    private void CalibrationGuideStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string stepName }
            && Enum.TryParse<ScanDebugCalibrationGuideStep>(stepName, out var step))
            NavigateCalibrationGuideStep(step, sender as FrameworkElement);
    }

    private void NavigateCalibrationGuideStep(ScanDebugCalibrationGuideStep step, FrameworkElement? origin = null)
    {
        // The overview's step buttons disappear when its flyout closes. Keep a
        // connection follow-up beside the overview entry that remains on screen.
        if (origin is not null && CalibrationGuideOverviewFlyout.Content is DependencyObject overviewContent
            && IsFocusedWithin(origin, overviewContent))
            origin = CalibrationGuideOverviewExpander;
        CalibrationGuideOverviewFlyout.Hide();
        switch (step)
        {
            case ScanDebugCalibrationGuideStep.ConnectDevice:
                OpenScannerConnectionFlyout(origin);
                break;
            case ScanDebugCalibrationGuideStep.PrepareDeviceClock:
                OpenDevicePropertiesButton_Click(this, new RoutedEventArgs());
                BeginIssueTargetFocus(4, DeviceSettingsScrollViewer, DeviceClockMhzTextBox);
                break;
            case ScanDebugCalibrationGuideStep.PrepareChannelParameters:
                if (!string.IsNullOrWhiteSpace(ViewModel.ChannelClockValidationMessage))
                    NavigateCalibrationGuideStep(ScanDebugCalibrationGuideStep.PrepareDeviceClock, origin);
                else
                    BwConfigureChannelButton_Click(this, new RoutedEventArgs());
                break;
            case ScanDebugCalibrationGuideStep.ReviewAdcRoi:
                BwRoiEditButton_Click(this, new RoutedEventArgs());
                break;
            case ScanDebugCalibrationGuideStep.ShowCalibrationActions:
                ShowGuidedCalibrationActions(origin);
                break;
            case ScanDebugCalibrationGuideStep.ReviewCandidate:
                if (ViewModel.PendingCalibrationCandidateReviewVisibility == Visibility.Visible)
                {
                    _isAuxiliaryPanelOpen = false;
                    SetActiveWorkbenchSection(2);
                    OpenCandidateReview();
                }
                else
                    ShowGuidedCalibrationActions(origin);
                break;
            case ScanDebugCalibrationGuideStep.SaveConfiguration:
                if (SaveConfigurationFileButton.IsEnabled)
                {
                    ConfigurationFileFlyout.ShowAt(ConfigurationFileMenuButton);
                    EnqueueForCurrentActivation(() => SaveConfigurationFileButton.Focus(FocusState.Programmatic));
                    break;
                }
                goto case ScanDebugCalibrationGuideStep.ReviewConfiguration;
            case ScanDebugCalibrationGuideStep.ReviewConfiguration:
                if (ViewModel.ValidateFilmProfileCommand.CanExecute(null))
                    ViewModel.ValidateFilmProfileCommand.Execute(null);
                ShowFilmProfileReview();
                EnqueueForCurrentActivation(() =>
                {
                    ViewModel.FilmProfileOperationIsOpen = false;
                    WorkbenchValidationButton.Focus(FocusState.Programmatic);
                });
                break;
        }
    }

    private void ShowGuidedCalibrationActions(FrameworkElement? origin = null)
    {
        if (!ViewModel.IsConnected)
        {
            OpenScannerConnectionFlyout(origin);
            return;
        }

        CloseCandidateReview(returnFocus: false);
        ExitBwIssueView(returnFocus: false);
        _isAuxiliaryPanelOpen = false;
        SetActiveWorkbenchSection(2);
        var scrollOwner = _bwCompactMode ? ChannelCalibrationCompactScrollViewer : ChannelCalibrationScrollViewer;
        scrollOwner.ChangeView(null, 0, null, true);
        EnqueueForCurrentActivation(() =>
        {
            if (AutoCalibrateButton.IsEnabled)
                AutoCalibrateButton.Focus(FocusState.Programmatic);
            else
                CalibrationGuideNextButton.Focus(FocusState.Programmatic);
        });
    }

    private void BwConfigureChannelButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();
        ExitBwIssueView(returnFocus: false);
        _isAuxiliaryPanelOpen = true;
        SetActiveWorkbenchSection(2);
        ChannelCalibrationDetailsExpander.IsExpanded = true;
        _ = ReturnToWorkbenchTaskButton.Focus(FocusState.Programmatic);
        if (ViewModel.IsWhiteCalibrationChannel)
        {
            ChannelCalibrationScrollViewer.ChangeView(null, 0, null, true);
            return;
        }
        BeginIssueTargetFocus(2, ChannelCalibrationScrollViewer, ExposureMicrosecondsTextBox,
            ViewModel.SelectedCalibrationChannel);
    }

    private async void BwIssueButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ScanFilmProfileValidationIssueDisplay display }
            || ChannelCalibrationIssuePanel.Visibility != Visibility.Visible)
            return;

        CancelBwIssueFocus();
        await ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(display);
    }

    private void TryFocusBwIssue(int requestVersion, int activationEpoch, string channelRole)
    {
        if (requestVersion != _bwFocusRequestVersion)
            return;

        if (!IsCurrentActivation(activationEpoch) || _activeWorkbenchSectionIndex != 2
            || ChannelCalibrationIssuePanel.Visibility != Visibility.Visible
            || !string.Equals(channelRole, ViewModel.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
        {
            CancelBwIssueFocus();
            return;
        }

        if (XamlRoot is not { } root)
            return;
        var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
        if (!ReferenceEquals(focused, _bwIssueExpectedFocus))
        {
            CancelBwIssueFocus();
            return;
        }

        var scrollOwner = _bwCompactMode ? ChannelCalibrationCompactScrollViewer
            : ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer;
        if (scrollOwner.ActualHeight <= 0 || !double.IsFinite(scrollOwner.ActualHeight))
            return;

        var issues = ViewModel.ChannelCalibrationCurrentFilmProfileValidationIssueDisplays;
        var issueButton = issues.Count > 0 && issues[0].CanNavigate
            ? FindFirstIssueButton(ChannelCalibrationIssueItemsControl) : null;
        if (issues.Count > 0 && issues[0].CanNavigate && issueButton is null)
            return;

        var target = issueButton ?? BwReturnToParametersButton;
        FrameworkElement targetViewport = _bwCompactMode ? scrollOwner
            : issueButton is null ? ChannelCalibrationIssuePanel : scrollOwner;
        if (!IsControlFullyVisible(target, targetViewport))
        {
            if (issueButton is null || issueButton.ActualHeight <= scrollOwner.ViewportHeight)
                target.StartBringIntoView();
            else
                target = BwReturnToParametersButton;
            targetViewport = _bwCompactMode ? scrollOwner
                : target == issueButton ? scrollOwner : ChannelCalibrationIssuePanel;
            if (!IsControlFullyVisible(target, targetViewport))
                return;
        }

        if (target.Focus(FocusState.Programmatic)
            && ReferenceEquals(FocusManager.GetFocusedElement(root), target))
            CancelBwIssueFocus();
    }

    private void CancelBwIssueFocus()
    {
        _bwFocusRequestVersion++;
        if (_bwIssueLayoutHandler is not null)
            ChannelCalibrationIssueItemsControl.LayoutUpdated -= _bwIssueLayoutHandler;
        if (_bwIssueScrollOwner is not null && _bwIssueViewChangedHandler is not null)
            _bwIssueScrollOwner.ViewChanged -= _bwIssueViewChangedHandler;
        _bwIssueLayoutHandler = null;
        _bwIssueViewChangedHandler = null;
        _bwIssueScrollOwner = null;
        _bwIssueExpectedFocus = null;
        CancelBwCompactReturnScroll();
    }

    private void ExitBwIssueView(bool returnFocus)
    {
        var restoreOffset = returnFocus && _bwCompactMode ? _bwCompactEditorScrollOffset : null;
        _bwCompactEditorScrollOffset = null;
        CancelBwIssueFocus();
        if (ChannelCalibrationIssuePanel.Visibility != Visibility.Visible)
            return;

        ChannelCalibrationIssuePanel.Visibility = Visibility.Collapsed;
        ChannelCalibrationScrollViewer.Visibility = Visibility.Visible;
        BwValidationSummaryTextBlock.Margin = new Thickness(0);
        BwViewingIssuesText.Margin = new Thickness(0);
        BwViewIssuesButton.Visibility = Visibility.Visible;
        BwViewingIssuesText.Visibility = Visibility.Collapsed;
        if (returnFocus)
        {
            var requestVersion = _bwFocusRequestVersion;
            var channelRole = ViewModel.SelectedCalibrationChannel;
            if (restoreOffset is { } offset)
                RestoreBwCompactEditorScrollAfterLayout(offset, requestVersion, channelRole);

            if (!BwViewIssuesButton.Focus(FocusState.Programmatic))
            {
                EnqueueForCurrentActivation(() =>
                {
                    if (requestVersion == _bwFocusRequestVersion && _activeWorkbenchSectionIndex == 2
                        && string.Equals(channelRole, ViewModel.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)
                        && BwViewIssuesButton.Visibility == Visibility.Visible && XamlRoot is { } root)
                    {
                        var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
                        if (focused is null || ReferenceEquals(focused, BwReturnToParametersButton) || !IsVisibleInTree(focused))
                            _ = BwViewIssuesButton.Focus(FocusState.Programmatic);
                    }
                });
            }
        }
    }

    private void RestoreBwCompactEditorScrollAfterLayout(double offset, int requestVersion, string channelRole)
    {
        var activationEpoch = _activationEpoch;
        _bwCompactReturnLayoutHandler = (_, _) =>
        {
            CancelBwCompactReturnScroll();
            if (requestVersion == _bwFocusRequestVersion && IsCurrentActivation(activationEpoch)
                && _activeWorkbenchSectionIndex == 2 && _bwCompactMode
                && ChannelCalibrationIssuePanel.Visibility != Visibility.Visible
                && string.Equals(channelRole, ViewModel.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
                _ = ChannelCalibrationCompactScrollViewer.ChangeView(null, offset, null, true);
        };
        ChannelCalibrationCompactScrollViewer.LayoutUpdated += _bwCompactReturnLayoutHandler;
    }

    private void CancelBwCompactReturnScroll()
    {
        if (_bwCompactReturnLayoutHandler is not null)
            ChannelCalibrationCompactScrollViewer.LayoutUpdated -= _bwCompactReturnLayoutHandler;
        _bwCompactReturnLayoutHandler = null;
    }

    private static bool IsControlFullyVisible(Control target, FrameworkElement viewport)
    {
        if (!target.IsLoaded || !viewport.IsLoaded || !IsVisibleInTree(target)
            || !double.IsFinite(target.ActualHeight) || target.ActualHeight <= 0
            || !double.IsFinite(target.ActualWidth) || target.ActualWidth <= 0
            || !double.IsFinite(viewport.ActualHeight) || viewport.ActualHeight <= 0
            || !double.IsFinite(viewport.ActualWidth) || viewport.ActualWidth <= 0)
            return false;

        var topLeft = target.TransformToVisual(viewport).TransformPoint(new Point(0, 0));
        return double.IsFinite(topLeft.X) && double.IsFinite(topLeft.Y)
            && topLeft.X >= 0 && topLeft.Y >= 0
            && topLeft.X + target.ActualWidth <= viewport.ActualWidth
            && topLeft.Y + target.ActualHeight <= viewport.ActualHeight;
    }

    private void InspectionToggleButton_Click(object sender, RoutedEventArgs e)
    {
        FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        _inspectionOpenOverride = WorkbenchInspectionRail.Visibility != Visibility.Visible;
        UpdateWorkbenchPreviewLayout();
        EnqueueForCurrentActivation(() =>
        {
            var target = WorkbenchInspectionRail.Visibility == Visibility.Visible
                ? InspectionCloseButton : InspectionToggleButton;
            target.Focus(FocusState.Programmatic);
        });
    }

    private void ScanDebugRootGrid_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (!IsWorkbenchSectionUiReady())
            return;

        EnqueueForCurrentActivation(UpdateWorkbenchPreviewLayout);
    }

    private void InspectionRail_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape)
            return;
        InspectionToggleButton_Click(sender, e);
        e.Handled = true;
    }

    private void UpdateWorkbenchPreviewLayout()
        => UpdateWorkbenchPreviewLayout(ScanDebugRootGrid.ActualWidth);

    private static double MeasureNaturalWidth(FrameworkElement element)
    {
        var visibility = element.Visibility;
        element.Visibility = Visibility.Visible;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = element.DesiredSize.Width;
        element.Visibility = visibility;
        return width;
    }

    private void StabilizePreviewToggleWidth()
    {
        var content = PreviewToggleButton.Content;
        var minimumWidth = PreviewToggleButton.MinWidth;
        PreviewToggleButton.MinWidth = 0;
        PreviewToggleButton.Content = _openPreviewActionLabel;
        var openWidth = MeasureNaturalWidth(PreviewToggleButton);
        PreviewToggleButton.Content = _hidePreviewActionLabel;
        var hideWidth = MeasureNaturalWidth(PreviewToggleButton);
        PreviewToggleButton.MinWidth = Math.Max(minimumWidth, Math.Ceiling(Math.Max(openWidth, hideWidth)));
        PreviewToggleButton.Content = content;
    }

    private void UpdateWorkbenchPreviewLayout(double availableWidth)
    {
        if (!IsWorkbenchSectionUiReady())
            return;

        if (!double.IsFinite(availableWidth) || availableWidth < 0)
            availableWidth = 0;

        ScanDebugRootGrid.RowSpacing = (double)Resources["ScanDebugInlineSpacing"];
        var spacing = (double)Resources["ScanDebugInlineSpacing"];
        UpdateConfigurationHandoffPresentation();
        var isPropertiesPanelOpen = _isAuxiliaryPanelOpen && _activeWorkbenchSectionIndex is 0 or 4 or 5;
        WorkbenchWorkspaceHeader.Visibility = Visibility.Visible;
        // Property editing does not need capture controls. Keep the fixed stop
        // controls available whenever a device or an operation is active.
        WorkbenchRunBar.Visibility = ToVisibility(!isPropertiesPanelOpen || ViewModel.IsConnected
            || ViewModel.IsConnecting || !string.IsNullOrEmpty(ViewModel.RunActivityText)
            || !string.IsNullOrEmpty(ViewModel.RunContextText));
        RunCaptureModePanel.Visibility = ToVisibility(!isPropertiesPanelOpen);
        StartScanActionButton.Visibility = ToVisibility(!isPropertiesPanelOpen);
        StopScanActionButton.Visibility = ToVisibility(!isPropertiesPanelOpen || ViewModel.IsRunning);
        // Keep device state visible when offline. Share the command row only
        // when every group fits at its natural width; otherwise move the whole
        // status group below it with the same spacing as the other controls.
        RunStatusTextPanel.Visibility = Visibility.Visible;
        var modeWidth = RunCaptureModePanel.Visibility == Visibility.Visible ? MeasureNaturalWidth(RunCaptureModePanel) : 0;
        var stopWidth = MeasureNaturalWidth(RunActionPanel);
        var statusWidth = RunStatusTextPanel.Visibility == Visibility.Visible ? MeasureNaturalWidth(RunStatusTextPanel) : 0;
        var recoveryWidth = MeasureNaturalWidth(RunRecoveryActionsPanel);
        UpdatePreviewActionVisibility();
        var wrapIdentity = availableWidth < MeasureNaturalWidth(WorkbenchTitleTextBlock)
            + MeasureNaturalWidth(FilmProfileLifecycleActionsPanel) + spacing;
        var wrapRunActions = availableWidth < modeWidth + stopWidth + spacing;
        var wrapRunStatus = wrapRunActions || availableWidth < modeWidth + statusWidth + recoveryWidth + stopWidth + spacing * 3;
        Grid.SetRow(RunActionPanel, wrapRunActions ? 1 : 0);
        Grid.SetColumn(RunActionPanel, wrapRunActions ? 0 : 2);
        Grid.SetColumnSpan(RunActionPanel, wrapRunActions ? 3 : 1);
        RunActionPanel.Margin = new Thickness(0, wrapRunActions ? spacing : 0, 0, 0);
        Grid.SetRow(RunStatusPanel, wrapRunActions ? 2 : wrapRunStatus ? 1 : 0);
        Grid.SetColumn(RunStatusPanel, wrapRunStatus ? 0 : 1);
        Grid.SetColumnSpan(RunStatusPanel, wrapRunStatus ? 3 : 1);
        RunStatusPanel.Margin = new Thickness(0, wrapRunStatus ? spacing : 0, 0, 0);
        Grid.SetRow(RunProgressBar, wrapRunActions ? 3 : wrapRunStatus ? 2 : 1);
        var statusAvailableWidth = availableWidth - (wrapRunStatus ? 0 : modeWidth + stopWidth + spacing * 2);
        var stackRunStatusActions = statusAvailableWidth < statusWidth + recoveryWidth + spacing;
        RunStatusPanel.RowSpacing = stackRunStatusActions ? spacing : 0;
        RunStatusPanel.ColumnDefinitions[0].MaxWidth = Math.Max(0,
            statusAvailableWidth - (stackRunStatusActions ? 0 : recoveryWidth + spacing));
        Grid.SetRow(RunRecoveryActionsPanel, stackRunStatusActions ? 1 : 0);
        Grid.SetColumn(RunRecoveryActionsPanel, stackRunStatusActions ? 0 : 1);
        Grid.SetColumnSpan(RunRecoveryActionsPanel, stackRunStatusActions ? 2 : 1);
        RunRecoveryActionsPanel.HorizontalAlignment = stackRunStatusActions ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        FilmProfileLifecycleActionsPanel.HorizontalAlignment = wrapIdentity ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Grid.SetRow(FilmProfileLifecycleActionsPanel, wrapIdentity ? 1 : 0);
        Grid.SetColumn(FilmProfileLifecycleActionsPanel, wrapIdentity ? 0 : 1);
        Grid.SetColumnSpan(FilmProfileLifecycleActionsPanel, wrapIdentity ? 2 : 1);
        Grid.SetRow(WorkbenchProfileSummaryGrid, wrapIdentity ? 2 : 1);
        WorkbenchIdentityGrid.MaxWidth = wrapIdentity ? availableWidth
            : Math.Max(0, availableWidth - MeasureNaturalWidth(FilmProfileLifecycleActionsPanel) - spacing);

        var inspectionOpen = _inspectionOpenOverride ?? false;
        var hasImage = HasValidPreviewFrame();
        var reviewVisible = FilmProfileLifecycleDetailsScrollViewer.Visibility == Visibility.Visible;
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            availableWidth, hasImage, _isNarrowPreviewOpen,
            _isAuxiliaryPanelOpen ? _auxiliaryPanelEditorRatio : _workbenchPreviewEditorRatio,
            inspectionOpen, IsConfigurationOpen: isPropertiesPanelOpen,
            IsPreviewCollapsed: _previewCollapsedOverride ?? false, IsReviewOpen: reviewVisible));
        WorkbenchValidationReviewColumn.Width = new GridLength(layout.ReviewWidth + layout.ReviewGapWidth);
        FilmProfileLifecycleDetailsScrollViewer.Width = layout.ReviewWidth;
        FilmProfileLifecycleDetailsScrollViewer.Margin = new Thickness(layout.ReviewGapWidth, 0, 0, 0);
        var feedbackRightInset = reviewVisible && !layout.IsReviewFullWidth
            ? layout.ReviewWidth + layout.ReviewGapWidth + 12 : 12;
        FilmProfileOperationInfoBar.Margin = new Thickness(12, 12, feedbackRightInset, 0);
        FilmProfileOperationInfoBar.MaxWidth = Math.Max(0, Math.Min(420, availableWidth - feedbackRightInset - 12));
        var previewActionLabel = layout.IsPreviewVisible ? _hidePreviewActionLabel : _openPreviewActionLabel;
        PreviewToggleButton.Content = previewActionLabel;
        AutomationProperties.SetName(PreviewToggleButton, previewActionLabel);
        ToolTipService.SetToolTip(PreviewToggleButton, previewActionLabel);
        var inspectionActionLabel = layout.Mode == ScanWorkbenchPreviewLayoutMode.NarrowInspection
            ? (_isNarrowPreviewOpen ? _openPreviewActionLabel : BackToEditorButton.Content)
            : _inspectionActionLabel;
        if (!Equals(InspectionToggleButton.Content, inspectionActionLabel))
            InspectionToggleButton.Content = inspectionActionLabel;
        AutomationProperties.SetName(InspectionToggleButton, inspectionActionLabel?.ToString() ?? string.Empty);
        InspectionToggleButton.Visibility = Visibility.Visible;
        var toolbarWidth = MeasureNaturalWidth(WorkspacePreviewToolbar);
        var taskSelectorWidth = MeasureNaturalWidth(WorkbenchTaskSelectorBar);
        // Keep the preview action at the right edge. Task navigation adapts first.
        var taskNavigationWidth = Math.Max(0, availableWidth - toolbarWidth - spacing * 2);
        var isTaskSelectorBarAvailable = !_isAuxiliaryPanelOpen && taskNavigationWidth >= taskSelectorWidth;
        var focusedElement = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        var focusReplacement = FindWorkbenchFocusReplacement(focusedElement, layout, isTaskSelectorBarAvailable);
        var focusHandoffVersion = ++_workbenchFocusHandoffVersion;
        var activationEpoch = _activationEpoch;
        if (!layout.IsPreviewVisible || layout.IsInspectionOverlay)
            CancelPreviewVisualInteraction();
        if (WorkbenchEditorColumnContent.Visibility == Visibility.Visible && !layout.IsEditorVisible && _activeWorkbenchSectionIndex == 3)
        {
            ManualFocusNegativeButton.ReleasePointerCaptures();
            ManualFocusPositiveButton.ReleasePointerCaptures();
        }

        WorkbenchSectionSelectorBar.Visibility = ToVisibility(isPropertiesPanelOpen);
        Grid.SetColumnSpan(WorkbenchAuxiliaryHeader, isPropertiesPanelOpen ? 3 : 2);
        WorkbenchTaskSelectorBar.Visibility = ToVisibility(isTaskSelectorBarAvailable);
        WorkbenchTaskComboBox.Visibility = ToVisibility(!_isAuxiliaryPanelOpen && !isTaskSelectorBarAvailable);
        WorkbenchTaskComboBox.MaxWidth = Math.Max(WorkbenchTaskComboBox.MinWidth, taskNavigationWidth);
        WorkspacePreviewToolbar.Visibility = ToVisibility(!isPropertiesPanelOpen);
        RawSignalModeSelector.Visibility = ToVisibility(layout.IsPreviewVisible);
        PreviewToolbar.Visibility = ToVisibility(layout.IsPreviewVisible);
        var previewSelectorWidth = MeasureNaturalWidth(RawSignalModeSelector);
        var wrapPreviewTools = layout.PreviewWidth < previewSelectorWidth + MeasureNaturalWidth(PreviewToolbar) + spacing;
        Grid.SetRow(PreviewToolbar, wrapPreviewTools ? 1 : 0);
        Grid.SetColumn(PreviewToolbar, wrapPreviewTools ? 0 : 1);
        Grid.SetColumnSpan(PreviewToolbar, wrapPreviewTools ? 2 : 1);
        PreviewToolbar.MaxWidth = Math.Max(0, layout.PreviewWidth - (wrapPreviewTools ? 0 : previewSelectorWidth + spacing));
        PreviewHeaderGrid.Visibility = ToVisibility(layout.IsPreviewVisible);
        ReturnToWorkbenchTaskButton.Visibility = ToVisibility(_isAuxiliaryPanelOpen);
        WorkbenchAuxiliaryHeader.Visibility = ToVisibility(_isAuxiliaryPanelOpen);
        WorkbenchAuxiliaryTitle.Text = (_activeWorkbenchSectionIndex == 5
            ? "ScanDebug_EngineeringWorkspaceTitle" : _activeWorkbenchSectionIndex == 2
                ? "ScanDebug_ChannelDetailsPanelTitle" : "ScanDebug_ConfigurationWorkspaceTitle").GetLocalized();
        WorkbenchTaskComboBox.PlaceholderText = WorkbenchAuxiliaryTitle.Text;
        var taskIndex = Array.IndexOf(WorkbenchTaskSectionIndices, _lastWorkbenchTaskSectionIndex);
        ReturnToWorkbenchTaskButton.Content = "ScanDebug_ReturnToTaskFormat".GetLocalizedFormatOrFallback(
            "← Back to {0}", WorkbenchTaskSelectorBar.Items[taskIndex].Text);
        var channelTabsFit = layout.EditorWidth >= MeasureNaturalWidth(CalibrationChannelSelectorBar) + 24;
        CalibrationChannelSelectorBar.Visibility = ToVisibility(channelTabsFit);
        CalibrationChannelFallbackComboBox.Visibility = ToVisibility(!channelTabsFit);
        WorkbenchEditorColumn.Width = new GridLength(layout.EditorWidth);
        WorkbenchPreviewSeparatorColumn.Width = new GridLength(layout.SeparatorWidth);
        WorkbenchPreviewColumn.Width = new GridLength(layout.PreviewWidth);
        WorkbenchInspectionSeparatorColumn.Width = new GridLength(layout.InspectionGapWidth);
        WorkbenchInspectionColumn.Width = new GridLength(layout.InspectionWidth);
        WorkbenchEditorRow.Height = new GridLength(1, GridUnitType.Star);
        WorkbenchEditorColumnContent.Visibility = ToVisibility(layout.IsEditorVisible);
        WorkbenchPreviewColumnContent.Visibility = ToVisibility(layout.IsPreviewVisible);
        WorkbenchInspectionRail.Visibility = ToVisibility(layout.IsInspectionVisible);
        // In a medium window, inspect the signal without replacing the active
        // parameter form or changing its width and scroll position.
        Grid.SetColumn(WorkbenchInspectionRail, layout.IsInspectionOverlay ? 2 : 4);
        Grid.SetColumnSpan(WorkbenchInspectionRail, layout.IsInspectionOverlay ? 3 : 1);
        WorkbenchInspectionRail.Width = layout.IsInspectionOverlay ? Math.Min(360, layout.PreviewWidth) : double.NaN;
        WorkbenchInspectionRail.HorizontalAlignment = layout.IsInspectionOverlay
            ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        if (layout.IsInspectionOverlay && ReferenceEquals(focusReplacement, InspectionCloseButton))
            _ = InspectionCloseButton.Focus(FocusState.Programmatic);
        WorkbenchPreviewInteractionHost.IsEnabled = !layout.IsInspectionOverlay;
        InspectionToggleButton.Visibility = Visibility.Visible;
        BackToEditorButton.Visibility = ToVisibility(layout.IsBackToEditorButtonVisible);
        PreviewSplitter.Visibility = ToVisibility(layout.IsSplitterVisible);
        PreviewSplitter.IsTabStop = layout.IsSplitterVisible;
        WorkbenchPreviewColumnContent.VerticalAlignment = VerticalAlignment.Stretch;
        if (focusReplacement is not null)
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (!IsCurrentActivation(activationEpoch) || focusHandoffVersion != _workbenchFocusHandoffVersion || XamlRoot is not { } currentRoot
                    || !IsVisibleInTree(focusReplacement) || !focusReplacement.IsEnabled)
                    return;

                var currentFocus = FocusManager.GetFocusedElement(currentRoot) as DependencyObject;
                if (currentFocus is Control { IsTabStop: true } control
                    && !ReferenceEquals(currentFocus, focusedElement) && IsVisibleInTree(control))
                    return;

                _ = focusReplacement.Focus(FocusState.Programmatic);
            });
    }

    private Control? FindWorkbenchFocusReplacement(DependencyObject? focusedElement,
        ScanWorkbenchPreviewLayoutResult layout, bool isTaskSelectorBarAvailable)
    {
        if (focusedElement is null)
            return null;
        if (layout.IsInspectionOverlay && IsFocusedWithin(focusedElement, WorkbenchPreviewColumnContent))
            return InspectionCloseButton;
        if (layout.IsReviewFullWidth
            && (IsFocusedWithin(focusedElement, WorkbenchEditorColumnContent)
                || IsFocusedWithin(focusedElement, WorkbenchPreviewColumnContent)
                || IsFocusedWithin(focusedElement, WorkbenchInspectionRail)
                || IsFocusedWithin(focusedElement, PreviewSplitter)))
        {
            var reviewFocusTarget = ViewModel.HasPendingFilmProfileImportResult
                ? DiscardStagedFilmProfileImportButton : CloseValidationReviewButton;
            return IsVisibleInTree(reviewFocusTarget) && reviewFocusTarget.IsEnabled
                ? reviewFocusTarget : ConfigurationFileMenuButton;
        }
        if (WorkbenchTaskSelectorBar.Visibility == Visibility.Visible && !isTaskSelectorBarAvailable
            && IsFocusedWithin(focusedElement, WorkbenchTaskSelectorBar))
            return WorkbenchTaskComboBox;
        if (WorkbenchTaskComboBox.Visibility == Visibility.Visible && isTaskSelectorBarAvailable
            && IsFocusedWithin(focusedElement, WorkbenchTaskComboBox))
            return GetActiveWorkbenchTaskButton();
        if (WorkbenchAuxiliaryHeader.Visibility == Visibility.Visible && !_isAuxiliaryPanelOpen
            && IsFocusedWithin(focusedElement, WorkbenchAuxiliaryHeader))
            return GetActiveWorkbenchTaskButton();
        if (WorkbenchEditorColumnContent.Visibility == Visibility.Visible && !layout.IsEditorVisible
            && IsFocusedWithin(focusedElement, WorkbenchEditorColumnContent))
            return layout.IsBackToEditorButtonVisible ? BackToEditorButton : InspectionToggleButton;
        if (WorkbenchPreviewColumnContent.Visibility == Visibility.Visible && !layout.IsPreviewVisible
            && IsFocusedWithin(focusedElement, WorkbenchPreviewColumnContent))
            return PreviewToggleButton;
        if (!layout.IsPreviewVisible
            && (IsFocusedWithin(focusedElement, RawSignalModeSelector) || IsFocusedWithin(focusedElement, PreviewToolbar)))
            return PreviewToggleButton;
        if (BackToEditorButton.Visibility == Visibility.Visible && !layout.IsBackToEditorButtonVisible
            && IsFocusedWithin(focusedElement, BackToEditorButton))
            return PreviewToggleButton;
        if (WorkbenchInspectionRail.Visibility == Visibility.Visible && !layout.IsInspectionVisible
            && IsFocusedWithin(focusedElement, WorkbenchInspectionRail))
            return InspectionToggleButton;
        if (PreviewSplitter.Visibility == Visibility.Visible && !layout.IsSplitterVisible
            && IsFocusedWithin(focusedElement, PreviewSplitter))
            return PreviewToggleButton;
        return null;
    }

    private void ChannelCalibrationContentWidthHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ChannelCalibrationTaskStack is { } taskStack)
            taskStack.Width = Math.Max(0, Math.Min(taskStack.MaxWidth, e.NewSize.Width));
    }

    private void ChannelCalibrationDetailsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateChannelDetailsColumns(e.NewSize.Width);

    private void UpdateChannelDetailsColumns(double availableWidth)
    {
        if (ChannelCalibrationParametersCard is null || ChannelCalibrationLibraryCard is null
            || ChannelCalibrationStatusCard is null)
            return;

        // The form has its own width cap and scroll gutter. Use its arranged width,
        // not the surrounding workbench width, to decide whether two columns fit.
        var useColumns = availableWidth >= 720;
        ChannelCalibrationDetailsGrid.ColumnSpacing = useColumns
            ? (double)Resources["ScanDebugSectionSpacing"] : 0;
        ChannelCalibrationDetailsGrid.ColumnDefinitions[0].Width = useColumns
            ? new GridLength(3, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        ChannelCalibrationDetailsGrid.ColumnDefinitions[1].Width = useColumns
            ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        Grid.SetRow(ChannelCalibrationParametersCard, 0);
        Grid.SetColumn(ChannelCalibrationParametersCard, 0);
        Grid.SetRow(ChannelCalibrationLibraryCard, useColumns ? 0 : 1);
        Grid.SetColumn(ChannelCalibrationLibraryCard, useColumns ? 1 : 0);
        Grid.SetRow(ChannelCalibrationStatusCard, useColumns ? 1 : 2);
        Grid.SetColumn(ChannelCalibrationStatusCard, 0);
        Grid.SetColumnSpan(ChannelCalibrationStatusCard, useColumns ? 2 : 1);
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

    private Control GetActiveWorkbenchTaskButton()
        => WorkbenchTaskComboBox.Visibility == Visibility.Visible
            ? WorkbenchTaskComboBox : WorkbenchTaskSelectorBar.SelectedItem ?? (Control)WorkbenchTaskSelectorBar;

    private bool HasValidPreviewFrame()
        => ViewModel.ObservationFrame is { Width: > 0, Height: > 0 };

    private Visibility RunContextVisibility(string? text)
        => ToVisibility(!string.IsNullOrWhiteSpace(text));

    private Visibility ValidationIssuesVisibility(int count)
        => ToVisibility(count > 0);

    private static Visibility ToVisibility(bool isVisible)
        => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private void PreviewToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScanDebugRootGrid.ActualWidth < ScanWorkbenchPreviewLayout.ReviewDockThreshold)
            FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        if (WorkbenchPreviewColumnContent.Visibility == Visibility.Visible)
            HidePreviewButton_Click(sender, e);
        else
            OpenPreviewButton_Click(sender, e);
    }

    private void OpenPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorkbenchInspectionRail.Visibility == Visibility.Visible
            && WorkbenchEditorColumnContent.Visibility != Visibility.Visible
            && WorkbenchPreviewColumnContent.Visibility != Visibility.Visible)
            _inspectionOpenOverride = false;
        _previewCollapsedOverride = false;
        _isNarrowPreviewOpen = true;
        UpdateWorkbenchPreviewLayout();
        EnqueueForCurrentActivation(() => PreviewToggleButton.Focus(FocusState.Programmatic));
    }

    private void HidePreviewButton_Click(object sender, RoutedEventArgs e)
    {
        _previewCollapsedOverride = true;
        _isNarrowPreviewOpen = false;
        UpdateWorkbenchPreviewLayout();
        EnqueueForCurrentActivation(() => PreviewToggleButton.Focus(FocusState.Programmatic));
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
            ScanDebugRootGrid.ActualWidth - WorkbenchValidationReviewColumn.ActualWidth,
            _isAuxiliaryPanelOpen ? _auxiliaryPanelEditorRatio : _workbenchPreviewEditorRatio, editorDelta,
            WorkbenchInspectionRail.Visibility == Visibility.Visible, HasImage: HasValidPreviewFrame()));
        if (_isAuxiliaryPanelOpen)
            _auxiliaryPanelEditorRatio = split.EditorRatio;
        else
            _workbenchPreviewEditorRatio = split.EditorRatio;
        UpdateWorkbenchPreviewLayout();
    }

    private bool IsWorkbenchSectionUiReady()
        => WorkbenchSectionSelectorBar is not null
            && WorkbenchTaskSelectorBar is not null
            && WorkbenchTaskComboBox is not null
            && ReturnToWorkbenchTaskButton is not null
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
            && PreviewToggleButton is not null
            && BackToEditorButton is not null
            && PreviewSplitter is not null
            && BasicInfoSection is not null
            && AcquisitionPlanSection is not null
            && ChannelCalibrationSection is not null
            && LiveCalibrationSection is not null
            && DeviceSettingsSection is not null
            && EngineeringToolsSection is not null
            && MotionTaskSection is not null;

    private int GetWorkbenchSectionSelectorIndex(SelectorBar selectorBar, SelectorBarItem? selectedItem)
    {
        for (var index = 0; index < selectorBar.Items.Count; index++)
        {
            if (ReferenceEquals(selectorBar.Items[index], selectedItem))
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

        var isChannelRouteChange = (_activeWorkbenchSectionIndex == 2 || index == 2)
            && (_activeWorkbenchSectionIndex != index
                || (ChannelCalibrationDetailsGrid.Visibility == Visibility.Visible) != _isAuxiliaryPanelOpen);
        if (isChannelRouteChange)
        {
            RememberChannelRouteScrollPosition();
            CancelChannelRouteScrollRestore();
        }
        if (_activeWorkbenchSectionIndex != index || isChannelRouteChange)
            CancelIssueTargetFocus();
        if (index != 2 || _isAuxiliaryPanelOpen)
            CloseCandidateReview(returnFocus: false);
        if (index != 2)
        {
            ExitBwIssueView(returnFocus: false);
            ViewModel.CancelPendingCalibrationChannelSelection();
        }

        if (isChannelRouteChange)
        {
            CancelBwCompactReturnScroll();
        }

        if (_activeWorkbenchSectionIndex == 3 && index != 3)
        {
            ManualFocusNegativeButton.ReleasePointerCaptures();
            ManualFocusPositiveButton.ReleasePointerCaptures();
        }

        if (index != 1)
            _isAcquisitionTransportIssueOpen = false;
        _activeWorkbenchSectionIndex = index;
        if ((!_isAuxiliaryPanelOpen || index == 2) && Array.IndexOf(WorkbenchTaskSectionIndices, index) >= 0)
            _lastWorkbenchTaskSectionIndex = index;
        _isNarrowPreviewOpen = false;
        _isSynchronizingWorkbenchSection = true;
        try
        {
            BasicInfoSection.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            AcquisitionPlanSection.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            ChannelCalibrationSection.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            UpdateAcquisitionTransportVisibility();
            LiveCalibrationSection.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            DeviceSettingsSection.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
            EngineeringToolsSection.Visibility = index == 5 && _isAuxiliaryPanelOpen ? Visibility.Visible : Visibility.Collapsed;
            MotionTaskSection.Visibility = index == 5 && !_isAuxiliaryPanelOpen ? Visibility.Visible : Visibility.Collapsed;
            var showChannelDetail = index == 2 && _isAuxiliaryPanelOpen;
            ChannelCalibrationPrimaryActions.Visibility = ToVisibility(index == 2 && !showChannelDetail);
            ChannelCalibrationActionsCard.Visibility = ToVisibility(index == 2 && !showChannelDetail);
            ChannelCalibrationDetailsExpander.Visibility = Visibility.Collapsed;
            ChannelCalibrationDetailsGrid.Visibility = ToVisibility(showChannelDetail);
            BwRoiSummary.Visibility = ToVisibility(index == 2 && !showChannelDetail);
            if (showChannelDetail)
                ChannelCalibrationDetailsExpander.IsExpanded = true;
            if (index == 2 && string.IsNullOrWhiteSpace(ViewModel.SelectedCalibrationChannel))
                _ = ViewModel.TrySelectCalibrationChannelAsync(ViewModel.CalibrationChannelOptions[0]);
            if (index == 5 && !_isAuxiliaryPanelOpen)
            {
                using (ViewModel.BeginMotionInputBindingInitialization())
                {
                    FindName("MotionContent");
                }
                UpdateMotorAxisLabels();
                EnqueueForCurrentActivation(AlignMotionUnitHeaders);
            }
            SynchronizeWorkbenchSectionSelectors(index);
            UpdateWorkbenchPreviewLayout();
            if (isChannelRouteChange && index == 2)
                RestoreChannelRouteScrollPositionAfterLayout(_isAuxiliaryPanelOpen);
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
        var propertyIndex = Array.IndexOf(WorkbenchPropertySectionIndices, index);
        if (propertyIndex >= 0 && !ReferenceEquals(WorkbenchSectionSelectorBar.SelectedItem, WorkbenchSectionSelectorBar.Items[propertyIndex]))
            WorkbenchSectionSelectorBar.SelectedItem = WorkbenchSectionSelectorBar.Items[propertyIndex];
        var taskIndex = Array.IndexOf(WorkbenchTaskSectionIndices,
            _isAuxiliaryPanelOpen && index == 2 ? index : _lastWorkbenchTaskSectionIndex);
        var selectedTaskIndex = _isAuxiliaryPanelOpen && index != 2 ? -1 : taskIndex;
        var selectedTask = selectedTaskIndex >= 0 ? WorkbenchTaskSelectorBar.Items[selectedTaskIndex] : null;
        if (!ReferenceEquals(WorkbenchTaskSelectorBar.SelectedItem, selectedTask))
            WorkbenchTaskSelectorBar.SelectedItem = selectedTask;
        if (WorkbenchTaskComboBox.SelectedIndex != selectedTaskIndex)
            WorkbenchTaskComboBox.SelectedIndex = selectedTaskIndex;
    }

    private void InitializeCurrentCalibrationIlluminationEditor()
    {
        CurrentCalibrationIlluminationWorkModeComboBox.ItemsSource = ViewModel.CurrentCalibrationIlluminationWorkModeOptions;
        RefreshCurrentCalibrationIlluminationEditor();
    }

    private async void WhiteLightSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WhiteLightSourceComboBox.SelectedItem is WhiteLightSourceOption option)
            await ViewModel.ChangeWhiteLightSourceAsync(option.Source);
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
        _isCancelingAcquisitionRowsEdit = false;
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

        if (_pendingScanProfileEdit is { } editRequest)
        {
            _pendingScanProfileEdit = null;
            await ViewModel.EditLoadedScanProfileAsync(editRequest);
            if (!IsCurrentActivation(activationEpoch) || !ViewModel.IsCurrentPageOwner(pageOwner))
                return;
        }

        stepStopwatch.Restart();
        InitializeZoomScaleComboBox();
        StabilizePreviewToggleWidth();
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
        _isCancelingAcquisitionRowsEdit = false;
        if (!_isPageActive)
            return;

        _isPageActive = false;
        CloseCandidateReview(returnFocus: false);
        var unloadEpoch = ++_activationEpoch;
        _bwCompactEditorScrollOffset = null;
        CancelChannelRouteScrollRestore();
        CancelBwIssueFocus();
        CancelIssueTargetFocus();
        ViewModel.CancelPendingCalibrationChannelSelection();
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
        ViewModel.FilmProfileValidationReviewRequested += OnFilmProfileValidationReviewRequested;
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
        ViewModel.FilmProfileValidationReviewRequested -= OnFilmProfileValidationReviewRequested;
        ViewModel.NoticeRequested -= OnNoticeRequested;
        ViewModel.RoiIssueNavigationRequested -= OnRoiIssueNavigationRequested;
        ViewModel.CurrentFilmProfileIssueNavigationRequested -= OnCurrentFilmProfileIssueNavigationRequested;
        _areViewModelEventsSubscribed = false;
    }

    private void OnFilmProfileValidationReviewRequested(object? sender, EventArgs e)
        => EnqueueForCurrentActivation(ShowFilmProfileReview);

    private void OnCurrentFilmProfileIssueNavigationRequested(ScanFilmProfileIssueNavigationRequest request)
    {
        if (!TryResolveCurrentFilmProfileNavigationTarget(request, allowDeferredRealization: false, out var sectionIndex, out var scroller, out var target))
            return;

        FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
        ExitBwIssueView(returnFocus: false);
        CloseCandidateReview(returnFocus: false);
        _isNarrowPreviewOpen = false;
        _inspectionOpenOverride = false;
        _isAcquisitionTransportIssueOpen = sectionIndex == 1 && request.EditorTarget is
            ScanFilmProfileIssueEditorTarget.ScanMotor or ScanFilmProfileIssueEditorTarget.MotorDistancePerLine;
        _isAuxiliaryPanelOpen = sectionIndex is 0 or 4;
        if (sectionIndex == 2 && request.EditorTarget is
            ScanFilmProfileIssueEditorTarget.ChannelStatus or ScanFilmProfileIssueEditorTarget.ChannelParameters)
            _isAuxiliaryPanelOpen = true;
        SetActiveWorkbenchSection(sectionIndex);
        if (sectionIndex == 1 && (request.EditorTarget is ScanFilmProfileIssueEditorTarget.ScanMotor
            or ScanFilmProfileIssueEditorTarget.MotorDistancePerLine))
            AcquisitionTransportDetailsExpander.IsExpanded = true;
        if (request.EditorTarget == ScanFilmProfileIssueEditorTarget.TransportStrategy)
            AcquisitionTransportStrategyExpander.IsExpanded = true;
        if (sectionIndex == 2)
        {
            if (request.EditorTarget is ScanFilmProfileIssueEditorTarget.ChannelStatus
                or ScanFilmProfileIssueEditorTarget.ChannelParameters)
                ChannelCalibrationDetailsExpander.IsExpanded = true;
            if (target is TextBox && (ReferenceEquals(target, Adc1OffsetTextBox)
                || ReferenceEquals(target, Adc2OffsetTextBox) || ReferenceEquals(target, Adc1GainTextBox)
                || ReferenceEquals(target, Adc2GainTextBox)))
                AdvancedAdcAdjustmentExpander.IsExpanded = true;
            if (request.EditorTarget == ScanFilmProfileIssueEditorTarget.ChannelRoiSettings)
                AdcRoiDetailsExpander.IsExpanded = true;
        }
        if (sectionIndex == 2 && _isAuxiliaryPanelOpen)
            _ = ReturnToWorkbenchTaskButton.Focus(FocusState.Programmatic);
        BeginIssueTargetFocus(sectionIndex, scroller, target, request.ChannelRole,
            request.EditorTarget == ScanFilmProfileIssueEditorTarget.ChannelAssignment
                ? () => FindFirstEligibleAssignmentCheckBox() : null,
            requireCurrentProfileValidation: true);
    }

    private bool IsCurrentFilmProfileNavigationStillValid()
        => _areViewModelEventsSubscribed
            && IsLoaded
            && ViewModel.FilmProfileImportResultReviewVisibility != Visibility.Visible;

    private void BeginIssueTargetFocus(int sectionIndex, ScrollViewer scroller, Control target,
        string? channelRole = null, Func<Control?>? resolveRealizedTarget = null,
        bool requireCurrentProfileValidation = false)
    {
        CancelIssueTargetFocus();
        var requestVersion = _issueTargetFocusRequestVersion;
        var activationEpoch = _activationEpoch;
        _issueTargetExpectedFocus = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        _issueTargetLayoutHandler = (_, _) => TryCompleteIssueTargetFocus(requestVersion, activationEpoch,
            sectionIndex, channelRole, scroller, resolveRealizedTarget, requireCurrentProfileValidation);
        _issueTargetFocusControl = target;
        target.LayoutUpdated += _issueTargetLayoutHandler;
        _issueTargetViewport = sectionIndex == 2 && _bwCompactMode ? ChannelCalibrationCompactScrollViewer : scroller;
        _issueTargetViewChangedHandler = (_, _) => TryCompleteIssueTargetFocus(requestVersion, activationEpoch,
            sectionIndex, channelRole, scroller, resolveRealizedTarget, requireCurrentProfileValidation);
        _issueTargetViewport.ViewChanged += _issueTargetViewChangedHandler;
        EnqueueForCurrentActivation(() => TryCompleteIssueTargetFocus(requestVersion, activationEpoch,
            sectionIndex, channelRole, scroller, resolveRealizedTarget, requireCurrentProfileValidation));
    }

    private void TryCompleteIssueTargetFocus(int requestVersion, int activationEpoch, int sectionIndex,
        string? channelRole, ScrollViewer scroller, Func<Control?>? resolveRealizedTarget,
        bool requireCurrentProfileValidation)
    {
        if (requestVersion != _issueTargetFocusRequestVersion)
            return;

        if (!IsCurrentActivation(activationEpoch) || !IsLoaded || sectionIndex != _activeWorkbenchSectionIndex
            || sectionIndex == 2 && ChannelCalibrationScrollViewer.Visibility != Visibility.Visible
            || requireCurrentProfileValidation && !IsCurrentFilmProfileNavigationStillValid()
            || channelRole is not null && !string.Equals(channelRole, ViewModel.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
        {
            CancelIssueTargetFocus();
            return;
        }

        var target = resolveRealizedTarget is null ? _issueTargetFocusControl : resolveRealizedTarget();
        if (XamlRoot is not { } root)
            return;
        var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
        if (focused is not null && !ReferenceEquals(focused, _issueTargetExpectedFocus)
            && !ReferenceEquals(focused, target) && IsVisibleInTree(focused))
        {
            CancelIssueTargetFocus();
            return;
        }

        if (target is null || !target.IsLoaded || !IsVisibleInTree(target)
            || target.ActualWidth <= 0 || target.ActualHeight <= 0)
            return;
        if (!ReferenceEquals(target, _issueTargetFocusControl))
        {
            _issueTargetFocusControl!.LayoutUpdated -= _issueTargetLayoutHandler;
            _issueTargetFocusControl = target;
            target.LayoutUpdated += _issueTargetLayoutHandler;
            _issueTargetBringIntoViewRequested = false;
        }

        var viewport = sectionIndex == 2 && _bwCompactMode ? ChannelCalibrationCompactScrollViewer : scroller;
        if (!IsControlFullyVisible(target, viewport))
        {
            // The compact editor contains a disabled inner ScrollViewer. A bring-
            // into-view request can stop there, so scroll the actual viewport directly.
            var targetTop = target.TransformToVisual(viewport).TransformPoint(default).Y;
            var viewportHeight = viewport.ViewportHeight > 0 ? viewport.ViewportHeight : viewport.ActualHeight;
            if (double.IsFinite(targetTop) && viewportHeight > 0)
            {
                var offset = Math.Clamp(viewport.VerticalOffset + targetTop
                    - Math.Max(0, (viewportHeight - target.ActualHeight) / 2), 0, viewport.ScrollableHeight);
                if (Math.Abs(offset - viewport.VerticalOffset) > 0.5)
                    viewport.ChangeView(null, offset, null, true);
            }
            if (!_issueTargetBringIntoViewRequested)
            {
                _issueTargetBringIntoViewRequested = true;
                target.StartBringIntoView(new BringIntoViewOptions
                {
                    AnimationDesired = false,
                    VerticalAlignmentRatio = 0.5
                });
            }
            return;
        }

        if (!target.Focus(FocusState.Programmatic)
            || !ReferenceEquals(FocusManager.GetFocusedElement(root), target))
            return;

        if (target is TextBox textBox)
            textBox.SelectAll();
        CancelIssueTargetFocus();
    }

    private void CancelIssueTargetFocus()
    {
        _issueTargetFocusRequestVersion++;
        if (_issueTargetFocusControl is not null && _issueTargetLayoutHandler is not null)
            _issueTargetFocusControl.LayoutUpdated -= _issueTargetLayoutHandler;
        if (_issueTargetViewport is not null && _issueTargetViewChangedHandler is not null)
            _issueTargetViewport.ViewChanged -= _issueTargetViewChangedHandler;
        _issueTargetFocusControl = null;
        _issueTargetLayoutHandler = null;
        _issueTargetViewport = null;
        _issueTargetViewChangedHandler = null;
        _issueTargetExpectedFocus = null;
        _issueTargetBringIntoViewRequested = false;
    }

    private bool TryResolveCurrentFilmProfileNavigationTarget(ScanFilmProfileIssueNavigationRequest request, bool allowDeferredRealization, out int sectionIndex, out ScrollViewer scroller, out Control target)
    {
        if (request.EditorTarget == ScanFilmProfileIssueEditorTarget.ChannelParameters
            && ViewModel.CurrentFilmProfileValidationIssueDisplays.Any(display => display.FieldPath == request.FieldPath
                && FilmProfileValidationTextPresenter.IsSystemClockInputIssue(display.Issue)))
        {
            sectionIndex = 4;
            scroller = DeviceSettingsScrollViewer;
            target = DeviceClockMhzTextBox;
            return true;
        }
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
            ScanFilmProfileIssueEditorTarget.ProfileName => ProfileAlignmentModeComboBox,
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
            ScanFilmProfileIssueEditorTarget.ScanMotor => AcquisitionDeviceMappingButton,
            ScanFilmProfileIssueEditorTarget.MotorDistancePerLine => AcquisitionMotorDistancePerLineTextBox,
            ScanFilmProfileIssueEditorTarget.TransportStrategy => AcquisitionTransportStrategyToggleSwitch,
            ScanFilmProfileIssueEditorTarget.ChannelAssignment => allowDeferredRealization ? FindFirstEligibleAssignmentCheckBox() : AcquisitionChannelAssignmentList,
            ScanFilmProfileIssueEditorTarget.Illumination => CurrentCalibrationIlluminationLevelTextBox,
            ScanFilmProfileIssueEditorTarget.ChannelStatus => ExposureMicrosecondsTextBox,
            ScanFilmProfileIssueEditorTarget.ChannelParameters => ResolveChannelParameterIssueTarget(request.FieldPath),
            ScanFilmProfileIssueEditorTarget.ChannelRoiSettings => AdcRoiStartTextBox,
            _ => null!
        };

        target = targetCandidate!;
        return sectionIndex >= 0 && scroller is not null && target is not null;
    }

    private Control ResolveChannelParameterIssueTarget(string fieldPath)
    {
        var issue = ViewModel.CurrentFilmProfileValidationIssueDisplays
            .FirstOrDefault(display => string.Equals(display.FieldPath, fieldPath, StringComparison.Ordinal))?.Issue;
        var isAdc2 = issue?.MessageArguments.ElementAtOrDefault(1) == "ADC2";
        return issue?.MessageKey switch
        {
            "FilmProfile.Validation.AdcOffsetInputInvalid" or "FilmProfile.Validation.AdcOffsetInputMissing" => isAdc2 ? Adc2OffsetTextBox : Adc1OffsetTextBox,
            "FilmProfile.Validation.AdcGainInputInvalid" or "FilmProfile.Validation.AdcGainInputMissing" => isAdc2 ? Adc2GainTextBox : Adc1GainTextBox,
            _ => ExposureMicrosecondsTextBox
        };
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

    private static Button? FindFirstIssueButton(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Button { Visibility: Visibility.Visible, IsEnabled: true } button)
                return button;

            var descendant = FindFirstIssueButton(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    private void OnRoiIssueNavigationRequested(ScanRoiIssueNavigationRequest request)
    {
        if (!TryResolveRoiNavigationTarget(request, out var sectionIndex, out var scroller, out var target))
            return;

        ExitBwIssueView(returnFocus: false);
        CloseCandidateReview(returnFocus: false);
        _inspectionOpenOverride = false;
        _isAuxiliaryPanelOpen = false;
        SetActiveWorkbenchSection(sectionIndex);
        if (request.Target is ScanRoiEditorTarget.AdcEffective or ScanRoiEditorTarget.AdcShield)
            AdcRoiDetailsExpander.IsExpanded = true;
        if (sectionIndex == 3)
            FocusRoiDetailsExpander.IsExpanded = true;
        BeginIssueTargetFocus(sectionIndex, scroller, target,
            sectionIndex == 2 ? ViewModel.SelectedCalibrationChannel : null);
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
        var result = ViewModel.ObservationRawSignalResult;
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
        var showHistogram = height >= 220;
        var availableHeight = height - plotTop - plotBottomInset - (showHistogram ? plotGap : 0);
        if (plotWidth < 1 || availableHeight < 1)
            return;

        var profileBottom = plotTop + availableHeight * (showHistogram ? 2f / 3f : 1f);
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

        // A short viewport prioritizes the line profile; do not overlap two sets of axes.
        if (!showHistogram)
            return;

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
        var frame = ViewModel.ObservationFrame;
        if (frame is null || !EnsurePreviewBitmap(sender, frame))
            return;

        if (ViewModel.IsObservationInverted)
        {
            using var effect = new Microsoft.Graphics.Canvas.Effects.InvertEffect { Source = _previewBitmap };
            args.DrawingSession.DrawImage(effect);
        }
        else
            args.DrawingSession.DrawImage(_previewBitmap);
    }

    private ScanPreviewFrame? _previewBitmapFrame;

    private bool EnsurePreviewBitmap(CanvasControl resourceCreator, ScanPreviewFrame frame)
    {
        if (frame.PixelFormat != ScanPreviewPixelFormat.Bgra8 || frame.Width <= 0 || frame.Height <= 0)
            return false;

        if (_previewBitmap is not null
            && ReferenceEquals(_previewBitmapFrame, frame)
            && _previewBitmapVersion == frame.Version
            && _previewBitmapWidth == frame.Width
            && _previewBitmapHeight == frame.Height)
        {
            return true;
        }

        DisposePreviewBitmap();
        var pixels = GetCanvasPixels(frame);
        _previewBitmap = CanvasBitmap.CreateFromBytes(resourceCreator, pixels, frame.Width, frame.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied);
        _previewBitmapFrame = frame;
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
        _previewBitmapFrame = null;
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
        if (e.PropertyName is nameof(ScanDebugViewModel.ObservationFrame)
            or nameof(ScanDebugViewModel.IsObservationLive)
            or nameof(ScanDebugViewModel.IsWaterfallEnabled)
            or nameof(ScanDebugViewModel.IsWaterfallCompressedEnabled))
            ViewModel.NotifyRawSignalSelectionChanged();
        if (e.PropertyName is nameof(ScanDebugViewModel.RawSignalDisplayRowInput)
            or nameof(ScanDebugViewModel.RawSignalPositionText)
            or nameof(ScanDebugViewModel.CanSelectRawSignalRowFromPreview))
            EnqueueForCurrentActivation(DrawRoiOverlays);
        if (e.PropertyName is nameof(ScanDebugViewModel.SysClockMhz)
            or nameof(ScanDebugViewModel.ChannelClockValidationMessage)
            or nameof(ScanDebugViewModel.ChannelParameterValidationMessage)
            or nameof(ScanDebugViewModel.DeviceClockDisplay)
            or nameof(ScanDebugViewModel.ClockDraftStatusText))
            ViewModel.NotifyCompactClockPresentation();
        if (e.PropertyName is nameof(ScanDebugViewModel.Motor1RoleText)
            or nameof(ScanDebugViewModel.Motor2RoleText)
            or nameof(ScanDebugViewModel.Motor3RoleText))
            EnqueueForCurrentActivation(UpdateMotorAxisLabels);

        if (e.PropertyName is nameof(ScanDebugViewModel.RunPresentationText)
            or nameof(ScanDebugViewModel.WorkbenchDeviceStatusText)
            or nameof(ScanDebugViewModel.WorkbenchParameterStatusText)
            or nameof(ScanDebugViewModel.WorkbenchCalibrationStatusText)
            or nameof(ScanDebugViewModel.IsConnected)
            or nameof(ScanDebugViewModel.IsConnecting)
            or nameof(ScanDebugViewModel.IsRunning)
            or nameof(ScanDebugViewModel.RunContextText)
            or nameof(ScanDebugViewModel.OperationFeedbackActionText)
            or nameof(ScanDebugViewModel.WorkbenchValidationActionText)
            or nameof(ScanDebugViewModel.CanSaveFilmProfileFile)
            or nameof(ScanDebugViewModel.FilmProfileSaveHintText)
            or nameof(ScanDebugViewModel.CurrentProfileNameText)
            or nameof(ScanDebugViewModel.ProfileSaveStateText))
            EnqueueForCurrentActivation(UpdateWorkbenchPreviewLayout);

        if (e.PropertyName == nameof(ScanDebugViewModel.SelectedRows))
            EnqueueForCurrentActivation(SynchronizeAcquisitionRowsEditor);

        if (e.PropertyName == nameof(ScanDebugViewModel.PendingCalibrationCandidateReviewVisibility)
            && ViewModel.PendingCalibrationCandidateReviewVisibility != Visibility.Visible)
            EnqueueForCurrentActivation(() =>
            {
                if (ViewModel.PendingCalibrationCandidateReviewVisibility != Visibility.Visible)
                    CloseCandidateReview(returnFocus: true);
            });
        if (e.PropertyName == nameof(ScanDebugViewModel.IsScanMotorTransportEnabled))
            EnqueueForCurrentActivation(UpdateAcquisitionTransportVisibility);

        if (e.PropertyName == nameof(ScanDebugViewModel.FilmProfileImportResultReviewVisibility))
            EnqueueForCurrentActivation(UpdateWorkbenchReviewEntry);

        if (e.PropertyName == nameof(ScanDebugViewModel.SelectedCalibrationChannel))
        {
            SynchronizeCalibrationChannelSelectors();
            CloseCandidateReview(returnFocus: false);
            ExitBwIssueView(returnFocus: false);
            CancelIssueTargetFocus();
        }

        if (e.PropertyName == nameof(ScanDebugViewModel.IsObservationInverted))
            EnqueueForCurrentActivation(() => PreviewCanvasControl.Invalidate());

        if (e.PropertyName == nameof(ScanDebugViewModel.IsObservationSelectionEnabled)
            || e.PropertyName == nameof(ScanDebugViewModel.ObservationStartInput)
            || e.PropertyName == nameof(ScanDebugViewModel.ObservationEndInput))
            EnqueueForCurrentActivation(DrawRoiOverlays);

        if (e.PropertyName == nameof(ScanDebugViewModel.IsObservationSnapshotVisible))
            EnqueueForCurrentActivation(() =>
            {
                CancelPreviewVisualInteraction();
                DrawRoiOverlays();
            });

        if (e.PropertyName == nameof(ScanDebugViewModel.ObservationFrame))
            EnqueueForCurrentActivation(() =>
            {
                RefreshPreviewLayout();
                PreviewCanvasControl.Invalidate();
            });

        if (e.PropertyName == nameof(ScanDebugViewModel.ObservationRawSignalResult))
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
        var frame = ViewModel.ObservationFrame;
        if (frame is null || frame.Width <= 0 || frame.Height <= 0)
        {
            PreviewCanvasControl.Width = 0;
            PreviewCanvasControl.Height = 0;
            PreviewCanvas.Width = 0;
            PreviewCanvas.Height = 0;
            RefreshPreviewContentScaleDimensions();
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
            _pendingInitialFitZoom = _isPreviewFitMode;
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
        RefreshPreviewContentScaleDimensions();

        DrawAxes(imageWidth, imageHeight);
        DrawRoiOverlays();
        UpdateZoomScaleComboBoxSelection();

        if (_pendingInitialFitZoom)
            EnqueueForCurrentActivation(ApplyInitialFitZoom);
    }

    private void UpdatePreviewActionVisibility()
    {
        var hasImage = HasValidPreviewFrame();
        FreezeObservationButton.Visibility = ToVisibility(hasImage);
        ObservationCompareButton.Visibility = ToVisibility(hasImage);
        foreach (var tool in new Control[] { FitPreviewButton, ActualSizePreviewButton, ZoomOutButton, ZoomInButton, ZoomScaleComboBox })
        {
            tool.Visibility = ToVisibility(hasImage);
            tool.IsEnabled = hasImage;
        }
        UpdatePrecisePreviewZoomControls();
    }

    private void UpdatePreviewEmptyStateVisibility()
    {
        UpdatePreviewActionVisibility();
        var hasPreviewFrame = ViewModel.ObservationFrame is { Width: > 0, Height: > 0 };
        CursorReadoutBorder.Visibility = ToVisibility(hasPreviewFrame
            && !ReferenceEquals(RawSignalModeSelector.SelectedItem, RawSignalProfileModeItem));
        PreviewEmptyStateGrid.Visibility = ToVisibility(!hasPreviewFrame);
        ObservationContextPanel.Visibility = ToVisibility(hasPreviewFrame
            && !ReferenceEquals(RawSignalModeSelector.SelectedItem, RawSignalProfileModeItem));
    }

    private void PreviewScrollViewer_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (_isPreviewFitMode && !_isChangingPreviewContentScale && HasValidPreviewFrame())
        {
            _pendingInitialFitZoom = true;
            ApplyInitialFitZoom();
        }
    }

    private void ApplyInitialFitZoom()
    {
        if (!_pendingInitialFitZoom || !_isPreviewFitMode)
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
        var targetZoom = Math.Clamp((float)fitZoom, MinimumPreviewZoom, MaximumPreviewZoom);

        var offsetX = Math.Max((contentWidth * targetZoom - viewportWidth) / 2.0, 0);
        var offsetY = Math.Max((contentHeight * targetZoom - viewportHeight) / 2.0, 0);

        SetPreviewContentScale(targetZoom);
        _pendingInitialFitZoom = false;
        RememberPreviewFitZoom(targetZoom);
        _ = PreviewScrollViewer.ChangeView(offsetX, offsetY, targetZoom / _previewContentScale, true);
        UpdateZoomScaleComboBoxSelection();
    }

    private void PreviewScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.ObservationFrame is null)
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
        if (ViewModel.ObservationFrame is null)
            return;

        PreviewCanvasControl_PointerPressed(PreviewCanvasControl, e);
        if (e.Handled)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)
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
    {
        ObservePreviewZoomViewChange(e);
        DrawLineProfileOverlay();
    }

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
        var oldZoom = ActualPreviewZoom;
        float? nextZoom = null;

        if (zoomIn)
        {
            foreach (var level in ZoomLevels)
            {
                if (level > oldZoom + PreviewZoomEpsilon)
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
                if (level < oldZoom - PreviewZoomEpsilon)
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
        MarkPreviewZoomAsManual();
        var oldZoom = ActualPreviewZoom;
        var newZoom = Math.Clamp(targetZoom, MinimumPreviewZoom, MaximumPreviewZoom);
        if (Math.Abs(newZoom - oldZoom) < PreviewZoomEpsilon)
        {
            UpdateZoomScaleComboBoxSelection();
            return;
        }

        var contentX = (PreviewScrollViewer.HorizontalOffset + anchorInViewport.X) / oldZoom;
        var contentY = (PreviewScrollViewer.VerticalOffset + anchorInViewport.Y) / oldZoom;

        var newHorizontalOffset = (contentX * newZoom) - anchorInViewport.X;
        var newVerticalOffset = (contentY * newZoom) - anchorInViewport.Y;

        SetPreviewContentScale(newZoom);
        _ = PreviewScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, newZoom / _previewContentScale, true);
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
        => UpdatePrecisePreviewZoomControls();

    private static string FormatZoomLabel(float zoom)
        => $"{zoom * 100:0.###}%";

    private void EndPanning(PointerRoutedEventArgs e)
    {
        ClearPanState();
        PreviewScrollViewer.ReleasePointerCapture(e.Pointer);
    }

    private bool _isObservationDrag;

    private void PreviewCanvasControl_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (TryBeginLineProfileSelection(e)) return;
#if PRISM_VISUAL_QA
        _visualQaPreviewCanvasPressedCount++;
#endif
        if ((!ViewModel.CanMutateRoiFromPreview && !ViewModel.CanMutateColumnSampleFromPreview && !ViewModel.CanMutateObservationFromPreview) || ViewModel.ObservationFrame is null)
            return;

        var point = e.GetCurrentPoint(PreviewCanvasControl);
#if PRISM_VISUAL_QA
        _visualQaLastPointer = $"pressed x={point.Position.X:0.###} y={point.Position.Y:0.###} left={point.Properties.IsLeftButtonPressed}";
#endif
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (point.Position.X < 0 || point.Position.Y < 0 || point.Position.X >= ViewModel.ObservationFrame.Width || point.Position.Y >= ViewModel.ObservationFrame.Height)
            return;

        var x = Math.Clamp((int)Math.Floor(point.Position.X), 0, Math.Max(ViewModel.ObservationFrame.Width - 1, 0));
        _isRoiDragging = true;
        _isObservationDrag = ViewModel.CanMutateObservationFromPreview;
        _isColumnSampleDrag = !_isObservationDrag && ViewModel.CanMutateColumnSampleFromPreview;
        _activeRoiPointerId = point.PointerId;
        _roiDragStartX = x;
        _roiDragImageWidth = ViewModel.ObservationFrame.Width;
        _roiDragFrameVersion = ViewModel.ObservationFrame.Version;
        _roiDragSelection = ViewModel.SelectedRoiSelection;
        _roiDragCalibrationChannel = ViewModel.SelectedCalibrationChannel;
        _roiDragHasAppliedRange = false;
        var hasRange = _isObservationDrag
            ? ViewModel.TryGetObservationRange(ViewModel.ObservationFrame.Width, out _roiOriginalRange)
            : _isColumnSampleDrag
            ? ViewModel.TryGetColumnSampleRange(ViewModel.ObservationFrame.Width, out _roiOriginalRange)
            : ViewModel.TryGetSelectedRoiRange(ViewModel.ObservationFrame.Width, out _roiOriginalRange);
        _isRoiMoveMode = hasRange && x >= _roiOriginalRange.Start && x <= _roiOriginalRange.EndInclusive;
        PreviewCanvasControl.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PreviewCanvasControl_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (TryMoveLineProfileSelection(e)) return;
#if PRISM_VISUAL_QA
        _visualQaPreviewCanvasMovedCount++;
#endif
        var bitmap = ViewModel.ObservationFrame;
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

        if (_isObservationDrag ? !ViewModel.CanMutateObservationFromPreview
            : _isColumnSampleDrag ? !ViewModel.CanMutateColumnSampleFromPreview : !ViewModel.CanMutateRoiFromPreview)
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
        if (TryEndLineProfileSelection(e)) return;
#if PRISM_VISUAL_QA
        _visualQaPreviewCanvasReleasedCount++;
#endif
        if (!_isRoiDragging)
            return;

        var point = e.GetCurrentPoint(PreviewCanvasControl);
        if (point.PointerId != _activeRoiPointerId)
            return;

        var bitmap = ViewModel.ObservationFrame;
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
        if (TryEndLineProfileSelection(e)) return;
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
        CancelLineProfileSelection();
        ClearRoiDragState();
        ClearPanState();
        PreviewCanvasControl.ReleasePointerCaptures();
        PreviewScrollViewer.ReleasePointerCaptures();
    }

    private void ClearRoiDragState()
    {
        _isRoiDragging = false;
        _isColumnSampleDrag = false;
        _isObservationDrag = false;
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
            ScanColumnRange currentRange;
            var currentRangeValid = _isObservationDrag
                ? ViewModel.TryGetObservationRange(imageWidth, out currentRange)
                : _isColumnSampleDrag
                ? ViewModel.TryGetColumnSampleRange(imageWidth, out currentRange)
                : ViewModel.TryGetSelectedRoiRange(imageWidth, out currentRange);
            if (currentRangeValid && currentRange == range)
                return;
        }

        _roiDragHasAppliedRange = true;
        if (_isObservationDrag)
            ViewModel.UpdateObservationRange(range.Start, range.EndInclusive, imageWidth);
        else if (_isColumnSampleDrag)
            ViewModel.UpdateColumnSampleRange(range.Start, range.EndInclusive, imageWidth);
        else
            ViewModel.UpdateSelectedRoiRange(range.Start, range.EndInclusive, imageWidth);
    }

    private bool IsRoiDragTargetCurrent(ScanPreviewFrame bitmap)
        => bitmap.Width == _roiDragImageWidth
            && bitmap.Version == _roiDragFrameVersion
            && string.Equals(ViewModel.SelectedCalibrationChannel, _roiDragCalibrationChannel, StringComparison.Ordinal)
            && (_isObservationDrag || _isColumnSampleDrag || string.Equals(ViewModel.SelectedRoiSelection, _roiDragSelection, StringComparison.Ordinal));

    private void EndRoiDragOrPanForPointer(PointerRoutedEventArgs e)
    {
        if (TryEndLineProfileSelection(e)) return;
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

        var bitmap = ViewModel.ObservationFrame;
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0 || ViewModel.IsObservationSnapshotVisible)
            return;

        if (ViewModel.IsObservationSelectionEnabled && ViewModel.TryGetObservationRange(bitmap.Width, out var observationRange))
        {
            var selection = new Rectangle
            {
                Width = observationRange.Width, Height = bitmap.Height,
                Stroke = RawSignalHistogramSwatch.Foreground, StrokeThickness = 2
            };
            Canvas.SetLeft(selection, AxisMarginLeft + observationRange.Start);
            Canvas.SetTop(selection, AxisMarginTop);
            RoiCanvas.Children.Add(selection);
        }

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
                StrokeThickness = overlay.IsSelected ? 2.5 : 1,
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rectangle, AxisMarginLeft + overlay.Range.Start);
            Canvas.SetTop(rectangle, AxisMarginTop);
            RoiCanvas.Children.Add(rectangle);

            if (!overlay.IsSelected && !ViewModel.IsAllRoiLabelsVisible)
                continue;

            var label = new TextBlock
            {
                Text = overlay.Label,
                Foreground = new SolidColorBrush(GetRoiColor(overlay.Key, true)),
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
                Fill = new SolidColorBrush(Color.FromArgb(ViewModel.IsColumnSampleEditModeEnabled ? (byte)44 : (byte)0, strokeColor.R, strokeColor.G, strokeColor.B)),
                StrokeThickness = ViewModel.IsColumnSampleEditModeEnabled ? 2.5 : 1.5,
                Opacity = ViewModel.IsColumnSampleEditModeEnabled ? 1 : 0.45,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rectangle, AxisMarginLeft + sampleRange.Start);
            Canvas.SetTop(rectangle, AxisMarginTop);
            RoiCanvas.Children.Add(rectangle);

            if (ViewModel.IsColumnSampleEditModeEnabled || ViewModel.IsAllRoiLabelsVisible)
            {
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
        DrawLineProfileOverlay();
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

        return isSelected ? color : Color.FromArgb(110, color.R, color.G, color.B);
    }

    private static Color GetRoiFillColor(string key, bool isSelected)
    {
        var baseColor = GetRoiColor(key, isSelected);
        return Color.FromArgb(isSelected ? (byte)36 : (byte)0, baseColor.R, baseColor.G, baseColor.B);
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
