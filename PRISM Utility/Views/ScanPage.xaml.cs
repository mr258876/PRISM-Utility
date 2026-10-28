using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
using Windows.Foundation;
using Windows.System;

namespace PRISM_Utility.Views;

public sealed partial class ScanPage : Page, IPageViewModelHost<ScanViewModel>
{
    private bool _isLoaded;
    private static readonly float[] PreviewZoomLevels = { 0.001f, 0.0025f, 0.005f, 0.01f, 0.025f, 0.05f, 0.1f, 0.125f, 0.2f, 0.25f, 0.5f, 0.75f, 1f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 20f };
    private const float MinimumPreviewZoom = 0.001f;
    private const float MaximumPreviewZoom = 20f;
    private bool _previewFitToWindow = true;
    private bool _previewFitUpdateQueued;
    private bool _updatingPreviewView;
    private float _previewContentScale = 1;
    private float _previewFitZoom = 1;
    private uint? _previewPanPointerId;
    private Point _previewPanStart;
    private double _previewPanHorizontalOffset;
    private double _previewPanVerticalOffset;

    public ScanViewModel ViewModel
    {
        get;
    }

    public ScanPage()
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        ViewModel = App.GetService<ScanViewModel>();
        NavigationTimingLogger.Write($"ScanPage.ctor GetService<ScanViewModel>={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        InitializeComponent();
        PreviewScrollViewer.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(PreviewScrollViewer_PointerWheelChanged), true);
        PreviewScrollViewer.AddHandler(PointerPressedEvent, new PointerEventHandler(PreviewScrollViewer_PointerPressed), true);
        PreviewScrollViewer.AddHandler(PointerMovedEvent, new PointerEventHandler(PreviewScrollViewer_PointerMoved), true);
        PreviewScrollViewer.AddHandler(PointerReleasedEvent, new PointerEventHandler(PreviewScrollViewer_PointerReleased), true);
        PreviewScrollViewer.AddHandler(PointerCanceledEvent, new PointerEventHandler(PreviewScrollViewer_PointerReleased), true);
        UpdateReadinessPresentation();
        NavigationTimingLogger.Write($"ScanPage.ctor InitializeComponent={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.ctor total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

#if PRISM_VISUAL_QA
    private async void OnLoaded(object sender, RoutedEventArgs e)
#else
    private void OnLoaded(object sender, RoutedEventArgs e)
#endif
    {
        if (_isLoaded)
            return;

#if PRISM_VISUAL_QA
        try
        {
            await PrismVisualQaCaptureService.DrainActiveAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scan visual QA drain failed: {ex}");
        }
        if (!IsLoaded)
            return;
#endif
        var stopwatch = Stopwatch.StartNew();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _isLoaded = true;
        ViewModel.Activate();
        RefreshFilmProfileEditorSession();
        UpdatePreviewViewport();
        UpdateReadinessPresentation();
#if PRISM_VISUAL_QA
        PrismVisualQaCaptureService.Start(this);
#endif
        stopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.Loaded Activate={stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshFilmProfileEditorSession();
        if (e.NavigationMode == NavigationMode.New
            && e.Parameter is string parameter
            && string.Equals(parameter, ScanViewModel.UseSavedFilmProfileNavigationParameter, StringComparison.Ordinal))
        {
            ViewModel.Activate();
            await ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.Deactivate();
        base.OnNavigatedFrom(e);
    }

#if PRISM_VISUAL_QA
    private async void OnUnloaded(object sender, RoutedEventArgs e)
#else
    private void OnUnloaded(object sender, RoutedEventArgs e)
#endif
    {
        if (!_isLoaded)
            return;

        var stopwatch = Stopwatch.StartNew();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _isLoaded = false;
        ViewModel.Deactivate();
        _previewPanPointerId = null;
        PreviewScrollViewer.ReleasePointerCaptures();
#if PRISM_VISUAL_QA
        try
        {
            await PrismVisualQaCaptureService.StopAsync(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scan visual QA stop failed: {ex}");
        }
#endif
        stopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.Unloaded Deactivate={stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void OpenScannerConnection_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow.Content is ShellPage shellPage)
            shellPage.ShowScannerConnectionFlyout(sender as FrameworkElement);
    }

    private async void PreparationStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ScanPreparationStep step } || !step.IsEnabled)
            return;

        switch (step.Action)
        {
            case ScanPreparationAction.ShowConnection:
                OpenScannerConnection_Click(sender, e);
                break;
            case ScanPreparationAction.UseSavedConfiguration:
                if (ViewModel.UseSavedFilmProfileCommand.CanExecute(null))
                    await ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
                break;
            case ScanPreparationAction.LoadConfiguration:
            case ScanPreparationAction.ReplaceConfiguration:
                if (ViewModel.LoadConfigProfileCommand.CanExecute(null))
                    await ViewModel.LoadConfigProfileCommand.ExecuteAsync(null);
                break;
            case ScanPreparationAction.ReviewColorSettings:
                ColorManagementExpander.IsExpanded = true;
                ColorManagementExpander.UpdateLayout();
                ColorManagementExpander.StartBringIntoView();
                ColorManagementExpander.Focus(FocusState.Programmatic);
                break;
            case ScanPreparationAction.ReviewMotionSettings:
                BringBlockingCardIntoView(step.CardId);
                if (!int.TryParse(ViewModel.SelectedRows, out var rows) || rows <= 0)
                    ScanRowsComboBox.Focus(FocusState.Programmatic);
                break;
            case ScanPreparationAction.ReviewDeviceMappings:
                OpenDeviceMappings_Click(sender, e);
                break;
            case ScanPreparationAction.ShowOutputStatus:
                OutputPreparationCard.StartBringIntoView();
                break;
        }
    }

    private void EditFilmProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsSetupEditingEnabled)
            App.GetService<INavigationService>().NavigateTo(AppRoute.ScanDebug, ViewModel.LoadedFilmProfileEditRequest);
    }

    private void RefreshFilmProfileEditorSession()
    {
        // Inspect only an editor that has already been opened; scanning must not construct it.
        var hasEditableDraft = App.TryGetCreatedScanDebugViewModel(out var editor)
            && editor is not null
            && (editor.HasUnsavedProfileChanges || !string.IsNullOrWhiteSpace(editor.EditingDraftFileName));
        ViewModel.UpdateFilmProfileEditorSession(hasEditableDraft);
    }

    private void OpenDeviceMappings_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsSetupEditingEnabled)
            App.GetService<INavigationService>().NavigateTo(AppRoute.Settings, SettingsViewModel.DeviceChannelsNavigationParameter);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanViewModel.FirstBlockingCardId))
            BringBlockingCardIntoView(ViewModel.FirstBlockingCardId);

        if (e.PropertyName == nameof(ScanViewModel.PreviewImage))
            UpdatePreviewViewport();

        if (e.PropertyName is nameof(ScanViewModel.IsOutputAvailable)
            or nameof(ScanViewModel.IsOutputOperationRunning) or nameof(ScanViewModel.IsOutputCardBlocked)
            or nameof(ScanViewModel.IsDeviceCardBlocked) or nameof(ScanViewModel.NeedsDeviceConnection)
            or nameof(ScanViewModel.HasOutputFeedback))
            UpdateReadinessPresentation();

        if (e.PropertyName == nameof(ScanViewModel.IsRunning))
            UpdateWorkspaceLayout();
    }

    private void UpdateReadinessPresentation()
    {
        // The preparation action already explains a disconnected scanner.
        DeviceBlockerInfoBar.Visibility = ViewModel.IsDeviceCardBlocked && !ViewModel.NeedsDeviceConnection
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Export actions become relevant once a result or an output operation exists.
        // Keep output failures visible even if the result itself is unavailable.
        OutputPreparationCard.Visibility = ViewModel.IsOutputAvailable
            || ViewModel.IsOutputOperationRunning || ViewModel.IsOutputCardBlocked || ViewModel.HasOutputFeedback
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateWorkspaceLayout();
    }

    private void ContentArea_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateWorkspaceLayout();

    private void ScanResultContent_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateWorkspaceLayout();

    private void UpdateWorkspaceLayout()
    {
        // SizeChanged can run while InitializeComponent is still building the page.
        if (PreviewCard is null || PreviewViewportBorder is null || ScanResultScrollViewer is null || OutputPreparationCard is null)
            return;

        // Keep the image viewport bounded inside the result scroller. When long
        // output messages need more room, users can still reach every action.
        var previewChromeHeight = Math.Max(0, PreviewCard.ActualHeight - PreviewViewportBorder.ActualHeight);
        var outputHeight = OutputPreparationCard.Visibility == Visibility.Visible
            ? OutputPreparationCard.ActualHeight + 12 : 0;
        var previewHeight = Math.Max(180, ScanResultScrollViewer.ActualHeight - previewChromeHeight - outputHeight);
        if (double.IsNaN(PreviewViewportBorder.Height) || Math.Abs(PreviewViewportBorder.Height - previewHeight) > 0.5)
            PreviewViewportBorder.Height = previewHeight;

        if (ContentArea.ActualWidth > 0)
        {
            // Preserve the same columns before, during, and after capture.
            var width = Math.Clamp(ContentArea.ActualWidth * 0.40, 320, 480);
            ScanPreparationColumn.Width = new GridLength(width);
        }
    }

    private void UpdatePreviewViewport()
    {
        var bitmap = ViewModel.PreviewImage;
        var hasPreview = bitmap is not null && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0;
        PreviewZoomTools.IsEnabled = hasPreview;
        PreviewZoomTools.Visibility = hasPreview ? Visibility.Visible : Visibility.Collapsed;
        PreviewScrollViewer.IsEnabled = hasPreview;
        PreviewInteractionHintElement.Visibility = hasPreview ? Visibility.Visible : Visibility.Collapsed;
        UpdateWorkspaceLayout();
        if (!hasPreview)
        {
            _previewPanPointerId = null;
            PreviewScrollViewer.ReleasePointerCaptures();
            UpdatePreviewZoomReadout();
            return;
        }

        // Natural image dimensions make 100% mean one image pixel per logical display pixel.
        // Replacing a live frame does not reset the user's zoom or scroll position.
        PreviewImageElement.Width = bitmap!.PixelWidth * _previewContentScale;
        PreviewImageElement.Height = bitmap.PixelHeight * _previewContentScale;
        if (_previewFitToWindow)
            QueuePreviewFit();
        UpdatePreviewZoomReadout();
    }

    private void QueuePreviewFit()
    {
        if (_previewFitUpdateQueued || !_isLoaded)
            return;

        _previewFitUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _previewFitUpdateQueued = false;
            if (!_isLoaded || !_previewFitToWindow || ViewModel.PreviewImage is null)
                return;

            PreviewScrollViewer.UpdateLayout();
            FitPreviewToWindow();
        }))
            _previewFitUpdateQueued = false;
    }

    private void FitPreviewToWindow()
    {
        if (ViewModel.PreviewImage is null || PreviewScrollViewer.ViewportWidth <= 0 || PreviewScrollViewer.ViewportHeight <= 0)
            return;

        _previewFitZoom = Math.Clamp((float)Math.Min(
            PreviewScrollViewer.ViewportWidth / ViewModel.PreviewImage.PixelWidth,
            PreviewScrollViewer.ViewportHeight / ViewModel.PreviewImage.PixelHeight),
            MinimumPreviewZoom, MaximumPreviewZoom);
        _updatingPreviewView = true;
        try
        {
            SetPreviewContentScale(_previewFitZoom);
            _ = PreviewScrollViewer.ChangeView(0, 0, _previewFitZoom / _previewContentScale, true);
        }
        finally
        {
            _updatingPreviewView = false;
        }
        UpdatePreviewZoomReadout();
    }

    private void PreviewFitButton_Click(object sender, RoutedEventArgs e)
    {
        _previewFitToWindow = true;
        PreviewFitButton.IsChecked = true;
        QueuePreviewFit();
    }

    private void PreviewActualSizeButton_Click(object sender, RoutedEventArgs e)
        => ApplyPreviewZoom(1, PreviewViewportCenter);

    private void PreviewZoomOutButton_Click(object sender, RoutedEventArgs e)
        => StepPreviewZoom(false, PreviewViewportCenter);

    private void PreviewZoomInButton_Click(object sender, RoutedEventArgs e)
        => StepPreviewZoom(true, PreviewViewportCenter);

    private Point PreviewViewportCenter => new(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2);
    private float ActualPreviewZoom => PreviewScrollViewer.ZoomFactor * _previewContentScale;

    private void StepPreviewZoom(bool zoomIn, Point anchor)
    {
        if (ViewModel.PreviewImage is null)
            return;

        var currentZoom = ActualPreviewZoom;
        var nextZoom = zoomIn
            ? PreviewZoomLevels.FirstOrDefault(zoom => zoom > currentZoom + 0.0001f, MaximumPreviewZoom)
            : PreviewZoomLevels.LastOrDefault(zoom => zoom < currentZoom - 0.0001f, MinimumPreviewZoom);
        ApplyPreviewZoom(nextZoom, anchor);
    }

    private void ApplyPreviewZoom(float zoom, Point anchor)
    {
        if (ViewModel.PreviewImage is null)
            return;

        _previewFitToWindow = false;
        PreviewFitButton.IsChecked = false;
        var oldZoom = ActualPreviewZoom;
        var newZoom = Math.Clamp(zoom, MinimumPreviewZoom, MaximumPreviewZoom);
        var width = ViewModel.PreviewImage.PixelWidth;
        var height = ViewModel.PreviewImage.PixelHeight;
        var viewportWidth = PreviewScrollViewer.ViewportWidth;
        var viewportHeight = PreviewScrollViewer.ViewportHeight;

        // Account for centering when the image is smaller than the viewport, so the
        // image point under the mouse stays in place as it grows beyond the window.
        var oldOriginX = Math.Max((viewportWidth - width * oldZoom) / 2, 0);
        var oldOriginY = Math.Max((viewportHeight - height * oldZoom) / 2, 0);
        var imageX = (PreviewScrollViewer.HorizontalOffset + anchor.X - oldOriginX) / oldZoom;
        var imageY = (PreviewScrollViewer.VerticalOffset + anchor.Y - oldOriginY) / oldZoom;
        var newOriginX = Math.Max((viewportWidth - width * newZoom) / 2, 0);
        var newOriginY = Math.Max((viewportHeight - height * newZoom) / 2, 0);
        var horizontalOffset = Math.Max(imageX * newZoom + newOriginX - anchor.X, 0);
        var verticalOffset = Math.Max(imageY * newZoom + newOriginY - anchor.Y, 0);
        _updatingPreviewView = true;
        try
        {
            SetPreviewContentScale(newZoom);
            _ = PreviewScrollViewer.ChangeView(horizontalOffset, verticalOffset, newZoom / _previewContentScale, true);
        }
        finally
        {
            _updatingPreviewView = false;
        }
        UpdatePreviewZoomReadout();
    }

    private void SetPreviewContentScale(float actualZoom)
    {
        if (ViewModel.PreviewImage is not { } bitmap)
            return;

        // ScrollViewer cannot zoom below 10%. Pre-scale its content for long scans,
        // while the displayed percentage and 100% action keep their pixel meaning.
        _previewContentScale = Math.Min(1, actualZoom / PreviewScrollViewer.MinZoomFactor);
        PreviewScrollViewer.MaxZoomFactor = MaximumPreviewZoom / _previewContentScale;
        PreviewImageElement.Width = bitmap.PixelWidth * _previewContentScale;
        PreviewImageElement.Height = bitmap.PixelHeight * _previewContentScale;
        PreviewScrollViewer.UpdateLayout();
    }

    private void UpdatePreviewZoomReadout()
    {
        var hasPreview = ViewModel.PreviewImage is not null;
        var zoom = ActualPreviewZoom;
        PreviewZoomReadout.Text = hasPreview ? $"{zoom * 100:0.#}%" : "—";
        PreviewFitButton.IsChecked = _previewFitToWindow;
        PreviewZoomOutButton.IsEnabled = hasPreview && zoom > MinimumPreviewZoom + 0.0001f;
        PreviewZoomInButton.IsEnabled = hasPreview && zoom < MaximumPreviewZoom - 0.0001f;
    }

    private void PreviewScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_previewFitToWindow)
            QueuePreviewFit();
    }

    private void PreviewScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        // A touch pinch also leaves fit mode; incoming frames then preserve that view.
        if (!e.IsIntermediate && !_updatingPreviewView && !_previewFitUpdateQueued && _previewFitToWindow
            && Math.Abs(ActualPreviewZoom - _previewFitZoom) > 0.0001f)
            _previewFitToWindow = false;
        UpdatePreviewZoomReadout();
    }

    private void PreviewScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.PreviewImage is null)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (point.Properties.MouseWheelDelta == 0 || point.Properties.IsHorizontalMouseWheel)
            return;
        StepPreviewZoom(point.Properties.MouseWheelDelta > 0, point.Position);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.PreviewImage is null || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || IsPreviewScrollBarSource(e.OriginalSource as DependencyObject))
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if ((!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)
            || point.Position.X >= PreviewScrollViewer.ViewportWidth || point.Position.Y >= PreviewScrollViewer.ViewportHeight)
            return;

        _previewPanPointerId = point.PointerId;
        _previewPanStart = point.Position;
        _previewPanHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        _previewPanVerticalOffset = PreviewScrollViewer.VerticalOffset;
        PreviewScrollViewer.Focus(FocusState.Pointer);
        PreviewScrollViewer.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private bool IsPreviewScrollBarSource(DependencyObject? source)
    {
        while (source is not null && source != PreviewScrollViewer)
        {
            if (source is ScrollBar)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void PreviewScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (_previewPanPointerId != point.PointerId)
            return;

        _ = PreviewScrollViewer.ChangeView(
            Math.Max(_previewPanHorizontalOffset - point.Position.X + _previewPanStart.X, 0),
            Math.Max(_previewPanVerticalOffset - point.Position.Y + _previewPanStart.Y, 0), null, true);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_previewPanPointerId != e.Pointer.PointerId)
            return;

        _previewPanPointerId = null;
        PreviewScrollViewer.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        => _previewPanPointerId = null;

    private void PreviewScrollViewer_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel.PreviewImage is null)
            return;

        switch (e.Key)
        {
            case VirtualKey.Home:
                PreviewFitButton_Click(PreviewFitButton, new RoutedEventArgs());
                break;
            case VirtualKey.Number0:
            case VirtualKey.NumberPad0:
                ApplyPreviewZoom(1, PreviewViewportCenter);
                break;
            case VirtualKey.Add:
                StepPreviewZoom(true, PreviewViewportCenter);
                break;
            case VirtualKey.Subtract:
                StepPreviewZoom(false, PreviewViewportCenter);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void BringBlockingCardIntoView(string blockerId)
    {
        if (blockerId == ScanWorkflowBlockerIds.Device)
        {
            ScanPreparationSummary.StartBringIntoView();
            return;
        }

        FrameworkElement? target = blockerId switch
        {
            ScanWorkflowBlockerIds.Configuration => ConfigurationPreparationCard,
            ScanWorkflowBlockerIds.Execution => ExecutionPreparationCard,
            ScanWorkflowBlockerIds.Output => OutputPreparationCard,
            _ => null
        };

        if (target is null)
            return;

        var position = target.TransformToVisual(WorkflowScrollViewer).TransformPoint(new Windows.Foundation.Point());
        WorkflowScrollViewer.ChangeView(null, Math.Max(0, WorkflowScrollViewer.VerticalOffset + position.Y - 24), null);
        target.StartBringIntoView();
    }
}
