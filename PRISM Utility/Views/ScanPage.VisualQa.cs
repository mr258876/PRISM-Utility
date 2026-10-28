#if PRISM_VISUAL_QA
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
using Windows.Graphics;

namespace PRISM_Utility.Views;

public sealed partial class ScanPage
{
    private bool _scanVisualQaFixtureLoaded;
    private string? _scanVisualQaScenario;
    private static readonly BindingFlags ScanVisualQaPrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    internal async Task ApplyScanVisualQaAsync(string? scenario, double? rootWidth, double? rootHeight, string? theme)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRISM_VISUAL_QA_REQUEST_DIRECTORY"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LocalSettingsOptions__ApplicationDataFolder")))
            throw new InvalidOperationException("Scan visual QA requires isolated requests and application settings.");
        if (ViewModel.IsConnected || App.GetService<IScannerDeviceSessionManager>().TryGetConnectedSession() is not null)
            throw new InvalidOperationException("Scan visual QA requires a disconnected scanner session.");

        ((Flyout)ScanPreviewSettingsButton.Flyout).Hide();
        if (FindName("ScanExecutionDetailsButton") is Button detailsButton && detailsButton.Flyout is Flyout detailsFlyout)
            detailsFlyout.Hide();

        if (Enum.TryParse<ElementTheme>(theme, true, out var requestedTheme)
            && App.MainWindow.Content is FrameworkElement shell)
            shell.RequestedTheme = requestedTheme;
        if (rootWidth is > 0 && rootHeight is > 0)
            await ResizeScanVisualQaRootAsync(rootWidth.Value, rootHeight.Value);

        switch (scenario)
        {
            case "offline":
                if (_scanVisualQaFixtureLoaded)
                    throw new InvalidOperationException("Capture the untouched offline state before loading the fixture. Use a new process to reset it.");
                break;
            case "loadedModified":
                if (_scanVisualQaFixtureLoaded)
                    throw new InvalidOperationException("The synthetic configuration may only be loaded once per process.");
                await LoadScanVisualQaFixtureAsync();
                ViewModel.SelectedRows = "512";
                break;
            case "restored":
                RequireScanVisualQaFixture();
                if (!ScanRestoreSettingsButton.IsEnabled)
                    throw new InvalidOperationException("The restore action is disabled for modified session settings.");
                var restorePeer = FrameworkElementAutomationPeer.CreatePeerForElement(ScanRestoreSettingsButton)
                    ?? new ButtonAutomationPeer(ScanRestoreSettingsButton);
                if (restorePeer.GetPattern(PatternInterface.Invoke) is not IInvokeProvider restoreInvoke)
                    throw new InvalidOperationException("The restore action does not expose UI Automation Invoke.");
                restoreInvoke.Invoke();
                break;
            case "running":
                RequireScanVisualQaFixture();
                InvokeScanVisualQa("ResetScanPresentationForNewRun");
                ViewModel.IsOutputAvailable = false;
                ViewModel.IsRunning = true;
                ViewModel.OutputSummaryText = "Scan_Runtime_OutputSummaryInProgress".GetLocalized();
                InvokeScanVisualQa("ApplyProgress", new ScanWorkflowProgress(2, 4, 2, true, "Scanning") { ChannelRole = "Red" });
                ViewModel.PreviewImage = CreateScanVisualQaPreview();
                break;
            case "completed":
                RequireScanVisualQaFixture();
                ViewModel.IsRunning = false;
                ViewModel.IsOutputAvailable = true;
                ViewModel.ScanProgressValue = ViewModel.ScanProgressMaximum = 4;
                ViewModel.IsScanProgressIndeterminate = false;
                ViewModel.OutputSummaryText = "Scan_Runtime_OutputSummaryCaptured".GetLocalizedFormat(4, 256, 1024);
                InvokeScanVisualQa("RecordScanCompletion", "Scan_Runtime_StatusCompleted".GetLocalized(), InfoBarSeverity.Success);
                break;
            case "exported":
                RequireScanVisualQaFixture();
                // The path is presentation data only. No export, file picker, or shell launch occurs.
                var exportPath = Path.Combine(Environment.GetEnvironmentVariable("LocalSettingsOptions__ApplicationDataFolder")!,
                    "synthetic-export", "PRISM scan 001");
                InvokeScanVisualQa("RecordOutputSuccess", exportPath, false,
                    "Scan_Runtime_StatusDngExported".GetLocalizedFormat(exportPath));
                break;
            case "error":
                RequireScanVisualQaFixture();
                InvokeScanVisualQa("ResetScanPresentationForNewRun");
                ViewModel.IsRunning = false;
                ViewModel.IsOutputAvailable = false;
                ViewModel.OutputSummaryText = "Scan_Runtime_OutputSummaryFailed".GetLocalized();
                var error = "Scan_Runtime_StatusFailed".GetLocalizedFormat(
                    "Scan_Runtime_ReadinessConnectFirst".GetLocalized());
                InvokeScanVisualQa("RecordScanCompletion", error, InfoBarSeverity.Error);
                InvokeScanVisualQa("ShowTopRiskBanner",
                    "Scan_Runtime_TopRiskScanFailedTitle".GetLocalizedOrFallback("Scan failed"), error, InfoBarSeverity.Error);
                break;
            case "scrollOutput":
                OutputPreparationCard.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
                break;
            case "previewSettings":
                ScanPreviewSettingsButton.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
                await Task.Delay(150);
                ((Flyout)ScanPreviewSettingsButton.Flyout).ShowAt(ScanPreviewSettingsButton);
                break;
            case "executionDetails":
                if (FindName("ScanExecutionDetailsButton") is not Button executionDetailsButton
                    || executionDetailsButton.Flyout is not Flyout executionDetailsFlyout)
                    throw new InvalidOperationException("The execution summary details flyout is unavailable.");
                executionDetailsFlyout.ShowAt(executionDetailsButton);
                break;
            default:
                throw new ArgumentException($"Unknown Scan visual QA scenario '{scenario}'.", nameof(scenario));
        }

        _scanVisualQaScenario = scenario;
        await Task.Delay(250);
        UpdateLayout();
        if (rootWidth is > 0 && rootHeight is > 0)
            await ResizeScanVisualQaRootAsync(rootWidth.Value, rootHeight.Value);
    }

    private async Task LoadScanVisualQaFixtureAsync()
    {
        var parameters = new ScanParameterSnapshot(1200, -4, 21, 3, 22, 52000);
        var profiles = new[] { "Blue", "White", "Red", "Green" }.ToDictionary(role => role,
            _ => new ScanChannelCalibrationProfile(parameters, ScanCalibrationRoiSettings.CreateDefault(), 512, 60120));
        var acquisition = ScanFilmAcquisitionSettings.CreateDefault() with { Rows = 256, TargetLinePitchMicrometers = 5 };
        var draft = new ScanFilmProfileDraft("PRISM 5207 · QA", DateTimeOffset.UtcNow, profiles, "Red", acquisition, null);
        var document = new ScanFilmProfileDocumentService().Build(draft).Document
            ?? throw new InvalidOperationException("The synthetic scan configuration is invalid.");
        var epoch = typeof(ScanViewModel).GetField("_configurationActivationEpoch", ScanVisualQaPrivate)!.GetValue(ViewModel)!;
        // ApplyConfigProfileAsync uses the same projection and baseline as a real load. Its
        // calibration repository is confined to this runner's unique application-data folder.
        await (Task)InvokeScanVisualQa("ApplyConfigProfileAsync", document, epoch, CancellationToken.None)!;
        _scanVisualQaFixtureLoaded = true;
    }

    private void RequireScanVisualQaFixture()
    {
        if (!_scanVisualQaFixtureLoaded)
            throw new InvalidOperationException("Load the synthetic configuration before simulating scan presentation.");
    }

    private object? InvokeScanVisualQa(string method, params object?[] arguments)
        => (typeof(ScanViewModel).GetMethod(method, ScanVisualQaPrivate)
            ?? throw new InvalidOperationException($"Missing ScanViewModel visual QA entry point '{method}'."))
            .Invoke(ViewModel, arguments);

    private static WriteableBitmap CreateScanVisualQaPreview()
    {
        const int width = 640, height = 320;
        var bitmap = new WriteableBitmap(width, height);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                var stripe = (x / 80 + y / 80) % 2 == 0;
                pixels[offset] = (byte)(stripe ? 90 + x / 6 : 30 + y / 2);
                pixels[offset + 1] = (byte)(40 + y / 2);
                pixels[offset + 2] = (byte)(40 + x / 4);
                pixels[offset + 3] = 255;
            }
        using var stream = bitmap.PixelBuffer.AsStream();
        stream.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    internal object GetScanVisualQaState()
    {
        var root = ContentArea;
        var elements = EnumerateScanVisualQaElements(this)
            .Where(element => !string.IsNullOrWhiteSpace(element.Name))
            .GroupBy(element => element.Name).Select(group => group.First()).Select(element =>
            {
                var point = element.IsLoaded ? element.TransformToVisual(root).TransformPoint(default) : default;
                var visible = element.IsLoaded && IsScanVisualQaVisible(element);
                return new
                {
                    name = element.Name, type = element.GetType().Name, visible,
                    inViewport = visible && IsScanVisualQaWithinViewport(element),
                    x = point.X, y = point.Y, width = element.ActualWidth, height = element.ActualHeight,
                    enabled = element is not Control control || control.IsEnabled,
                    text = element is TextBlock text ? text.Text : element is Button button ? button.Content?.ToString() : null,
                    progressValue = element is ProgressBar bar ? bar.Value : element is ProgressRing ring ? ring.Value : (double?)null,
                    progressMaximum = element is ProgressBar maximumBar ? maximumBar.Maximum : element is ProgressRing maximumRing ? maximumRing.Maximum : (double?)null,
                    progressIndeterminate = element is ProgressBar indefiniteBar ? indefiniteBar.IsIndeterminate
                        : element is ProgressRing indefiniteRing ? indefiniteRing.IsIndeterminate : (bool?)null
                };
            }).ToArray();
        return new
        {
            scenario = _scanVisualQaScenario, rootWidth = root.ActualWidth, rootHeight = root.ActualHeight,
            scale = XamlRoot.RasterizationScale, theme = ActualTheme.ToString(),
            ViewModel.IsConnected, ViewModel.IsRunning, ViewModel.IsConfigProfileLoaded,
            ViewModel.IsOutputAvailable, ViewModel.IsPreviewPlaceholderVisible,
            canStart = ViewModel.StartScanCommand.CanExecute(null), canStop = ViewModel.StopScanCommand.CanExecute(null),
            canExport = ViewModel.ExportDngChannelsCommand.CanExecute(null), canSaveRgb = ViewModel.SaveRgbImageCommand.CanExecute(null),
            ViewModel.ScanProgressValue, ViewModel.ScanProgressMaximum, ViewModel.IsScanProgressIndeterminate,
            ViewModel.RuntimeFeedbackText, ViewModel.HasRuntimeFeedback, runtimeFeedbackSeverity = ViewModel.RuntimeFeedbackSeverity.ToString(),
            ViewModel.OutputFeedbackText, ViewModel.HasOutputFeedback, ViewModel.LastOutputPath,
            canOpenOutputFolder = ViewModel.OpenLastOutputFolderCommand.CanExecute(null),
            configLoadActionEnabled = EnumerateScanVisualQaElements(ConfigurationPreparationCard).OfType<Button>()
                .Any(button => ReferenceEquals(button.Command, ViewModel.LoadConfigProfileCommand) && button.IsEnabled
                    && IsScanVisualQaVisible(button)),
            configEditActionEnabled = EnumerateScanVisualQaElements(ConfigurationPreparationCard).OfType<Button>()
                .Any(button => button.Content?.ToString() == ViewModel.FilmProfileEditorActionText && button.IsEnabled
                    && IsScanVisualQaVisible(button)),
            canLoadConfiguration = ViewModel.LoadConfigProfileCommand.CanExecute(null),
            ViewModel.IsOfflineGuidanceVisible, ViewModel.OfflineGuidanceText,
            ViewModel.HasSessionChanges, ViewModel.SessionChangesText,
            canRestore = ViewModel.RestoreLoadedSettingsCommand.CanExecute(null),
            ViewModel.ExecutionRowsSummaryText, ViewModel.ExecutionChannelsSummaryText,
            ViewModel.ExecutionMotionSummaryText, ViewModel.ExecutionPreviewSummaryText,
            ViewModel.SelectedRows, ViewModel.SelectedConfigProfileName, ViewModel.HeaderScanStatusText,
            ViewModel.PreparationTitle, ViewModel.IsPreparationVisible, ViewModel.IsTopRiskBannerVisible,
            ViewModel.TopRiskBannerText, ViewModel.OutputSummaryText,
            runtimeMessageSurfaceCount = EnumerateScanVisualQaElements(this).OfType<InfoBar>()
                .Count(bar => bar.IsOpen && IsScanVisualQaVisible(bar) && !string.IsNullOrEmpty(ViewModel.RuntimeFeedbackText)
                    && bar.Message == ViewModel.RuntimeFeedbackText),
            actualSessionConnected = App.GetService<IScannerDeviceSessionManager>().TryGetConnectedSession() is not null,
            previewSettings = GetScanVisualQaPopupState(),
            executionDetails = GetScanVisualQaPopupState("ScanExecutionDetailsPopup"),
            previewLayout = GetScanVisualQaPreviewLayout(),
            executionSummary = GetScanVisualQaExecutionSummary(),
            commonControls = GetScanVisualQaCommonControls(),
            elements
        };
    }

    private object GetScanVisualQaPreviewLayout()
    {
        var card = GetScanVisualQaBounds(PreviewCard);
        var viewport = GetScanVisualQaBounds(PreviewViewportBorder);
        var output = GetScanVisualQaBounds(OutputPreparationCard);
        var gap = output.Visible ? Math.Max(0, output.Y - card.Y - card.Height) : 0;
        var content = ScanResultScrollViewer.Content as FrameworkElement;
        return new
        {
            card, viewport, output,
            resultScroller = GetScanVisualQaBounds(ScanResultScrollViewer),
            scrollerViewportHeight = ScanResultScrollViewer.ViewportHeight,
            scrollerExtentHeight = ScanResultScrollViewer.ExtentHeight,
            scrollerScrollableHeight = ScanResultScrollViewer.ScrollableHeight,
            scrollerVerticalOffset = ScanResultScrollViewer.VerticalOffset,
            contentHeight = content?.ActualHeight ?? 0,
            minimumViewportHeight = PreviewViewportBorder.MinHeight,
            previewChromeHeight = Math.Max(0, card.Height - viewport.Height),
            outputHeight = output.Visible ? output.Height : 0,
            outputGap = gap,
            viewportBackground = ScanVisualQaBrushColor(PreviewViewportBorder.Background),
            emptyStateBackground = FindName("PreviewEmptyState") is Panel emptyPanel
                ? ScanVisualQaBrushColor(emptyPanel.Background) : null,
            previewCardBackground = ScanVisualQaBrushColor(PreviewCard.Background),
            // Geometry and resolved colors are evidence; screenshots still require visual review.
            expectedBackgroundResource = "SolidBackgroundFillColorSecondaryBrush"
        };
    }

    private object GetScanVisualQaExecutionSummary()
    {
        var panel = FindName("ScanExecutionSummaryPanel") as FrameworkElement;
        string[] names = ["ScanExecutionPrimaryText", "ScanExecutionSecondaryText"];
        var detailsButton = FindName("ScanExecutionDetailsButton") as Button;
        return new
        {
            panel = GetScanVisualQaBounds(panel),
            hasExpanderAncestor = HasScanVisualQaExpanderAncestor(panel),
            detailsButton = GetScanVisualQaBounds(detailsButton),
            detailsButtonEnabled = detailsButton?.IsEnabled ?? false,
            values = names.Select(name =>
            {
                var element = FindName(name) as TextBlock;
                return new
                {
                    name, text = element?.Text,
                    bounds = GetScanVisualQaBounds(element),
                    hasExpanderAncestor = HasScanVisualQaExpanderAncestor(element)
                };
            }).ToArray()
        };
    }

    private object GetScanVisualQaCommonControls()
    {
        string[] names = ["ScanRowsComboBox", "ScanDirectionComboBox", "ScanLineDistanceTextBox",
            "ScanLineDistanceUnitComboBox", "ScanWarmUpToggle", "ScanAlternateDirectionToggle"];
        return new
        {
            workflowScroller = GetScanVisualQaBounds(WorkflowScrollViewer),
            workflowVerticalOffset = WorkflowScrollViewer.VerticalOffset,
            workflowViewportHeight = WorkflowScrollViewer.ViewportHeight,
            workflowScrollableHeight = WorkflowScrollViewer.ScrollableHeight,
            controls = names.Select(name =>
            {
                var element = FindName(name) as FrameworkElement;
                return new
                {
                    name, bounds = GetScanVisualQaBounds(element),
                    enabled = element is Control control && control.IsEnabled
                };
            }).ToArray()
        };
    }

    private static string? ScanVisualQaBrushColor(Brush? brush)
        => brush is SolidColorBrush solid ? $"#{solid.Color.A:X2}{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : null;

    private sealed record ScanVisualQaBounds(bool Present, bool Visible, bool InViewport,
        double X, double Y, double Width, double Height);

    private ScanVisualQaBounds GetScanVisualQaBounds(FrameworkElement? element)
    {
        if (element is null)
            return new(false, false, false, 0, 0, 0, 0);
        var point = element.IsLoaded ? element.TransformToVisual(ContentArea).TransformPoint(default) : default;
        var visible = element.IsLoaded && IsScanVisualQaVisible(element);
        return new(true, visible, visible && IsScanVisualQaWithinViewport(element),
            point.X, point.Y, element.ActualWidth, element.ActualHeight);
    }

    private bool IsScanVisualQaWithinViewport(FrameworkElement element)
    {
        if (!element.IsLoaded)
            return false;
        var bounds = element.TransformToVisual(ContentArea).TransformBounds(
            new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
        if (bounds.Left < -1 || bounds.Top < -1 || bounds.Right > ContentArea.ActualWidth + 1
            || bounds.Bottom > ContentArea.ActualHeight + 1)
            return false;
        for (DependencyObject? parent = VisualTreeHelper.GetParent(element);
            parent is not null && !ReferenceEquals(parent, this); parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is not ScrollViewer scroller)
                continue;
            var clip = scroller.TransformToVisual(ContentArea).TransformBounds(
                new Windows.Foundation.Rect(0, 0, scroller.ActualWidth, scroller.ActualHeight));
            if (bounds.Left < clip.Left - 1 || bounds.Top < clip.Top - 1
                || bounds.Right > clip.Right + 1 || bounds.Bottom > clip.Bottom + 1)
                return false;
        }
        return true;
    }

    private static bool HasScanVisualQaExpanderAncestor(FrameworkElement? element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is Expander)
                return true;
        return false;
    }

    internal FrameworkElement? ResolveScanVisualQaTarget(string? name)
    {
        var button = name switch
        {
            "ScanPreviewSettingsPopup" => ScanPreviewSettingsButton,
            "ScanExecutionDetailsPopup" => FindName("ScanExecutionDetailsButton") as Button,
            _ => null
        };
        if (button?.Flyout is not Flyout flyout)
            return null;
        DependencyObject? current = flyout.Content as DependencyObject;
        while (current is not null && current is not FlyoutPresenter)
            current = VisualTreeHelper.GetParent(current);
        return current as FrameworkElement;
    }

    private object GetScanVisualQaPopupState(string target = "ScanPreviewSettingsPopup")
    {
        var popup = ResolveScanVisualQaTarget(target);
        if (popup is not { IsLoaded: true })
            return new { isOpen = false };
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(popup) ?? new FrameworkElementAutomationPeer(popup);
        var screen = peer.GetBoundingRectangle();
        return new
        {
            isOpen = IsScanVisualQaVisible(popup), width = popup.ActualWidth, height = popup.ActualHeight,
            screenX = screen.X, screenY = screen.Y, screenWidth = screen.Width, screenHeight = screen.Height,
            texts = EnumerateScanVisualQaElements(popup).OfType<TextBlock>()
                .Select(text => text.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray()
        };
    }

    private async Task ResizeScanVisualQaRootAsync(double width, double height)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(150);
            UpdateLayout();
            var widthError = width - ContentArea.ActualWidth;
            var heightError = height - ContentArea.ActualHeight;
            if (Math.Abs(widthError) <= 0.5 && Math.Abs(heightError) <= 0.5)
                return;
            var size = App.MainWindow.AppWindow.Size;
            var scale = XamlRoot.RasterizationScale;
            App.MainWindow.AppWindow.Resize(new SizeInt32(size.Width + (int)Math.Round(widthError * scale),
                size.Height + (int)Math.Round(heightError * scale)));
        }
        throw new InvalidOperationException($"Scan visual QA root size did not converge: requested {width}x{height}; actual {ContentArea.ActualWidth}x{ContentArea.ActualHeight}.");
    }

    private static IEnumerable<FrameworkElement> EnumerateScanVisualQaElements(DependencyObject root)
    {
        if (root is FrameworkElement element)
            yield return element;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in EnumerateScanVisualQaElements(VisualTreeHelper.GetChild(root, index)))
                yield return child;
    }

    private static bool IsScanVisualQaVisible(FrameworkElement element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed })
                return false;
        return element.ActualWidth > 0 && element.ActualHeight > 0;
    }
}
#endif
