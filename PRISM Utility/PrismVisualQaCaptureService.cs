#if PRISM_VISUAL_QA
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Views;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace PRISM_Utility;

internal static class PrismVisualQaCaptureService
{
    internal const string RequestDirectoryName = "PRISM_Utility_VisualQaCaptureRequests";
    internal const string ReadyFileName = "ready.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static ScanDebugPage? _page;
    private static DispatcherTimer? _timer;
    private static Task? _processingTask;
    private static bool _stopping;
    private static bool _shutdownRequested;

    internal static void BeginShutdown() => _shutdownRequested = true;

    internal static void CancelShutdown() => _shutdownRequested = false;

    internal static void Start(ScanDebugPage page)
    {
        if (_shutdownRequested || _stopping || _processingTask is { IsCompleted: false })
            return;

        _page = page;
        _stopping = false;
        Directory.CreateDirectory(GetRequestDirectory());
        WriteReadyFile(page);

        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick -= ProcessRequests;
        _timer.Tick += ProcessRequests;
        _timer.Start();
    }

    internal static Task DrainActiveAsync()
        => _page is { } page ? StopAsync(page) : Task.CompletedTask;

    internal static async Task StopAsync(ScanDebugPage page)
    {
        if (!ReferenceEquals(_page, page))
            return;

        _stopping = true;

        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= ProcessRequests;
        }

        var processingTask = _processingTask;
        try
        {
            if (processingTask is not null)
                await processingTask;
        }
        finally
        {
            if ((processingTask is null || processingTask.IsCompleted)
                && ReferenceEquals(_page, page)
                && ReferenceEquals(_processingTask, processingTask)
                && _stopping)
            {
                _page = null;
                _timer = null;
                _processingTask = null;
                _stopping = false;
            }
        }
    }

    private static async void ProcessRequests(object? sender, object e)
    {
        if (_shutdownRequested)
            return;

        if (_stopping)
            return;

        if (_processingTask is { IsCompleted: false })
            return;

        var page = _page;
        if (page is null)
            return;

        _processingTask = ProcessRequestsAsync(page);
        try
        {
            await _processingTask;
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "VisualQaCapture", $"Capture request processing failed: {ex}\n");
        }
    }

    private static async Task ProcessRequestsAsync(ScanDebugPage page)
    {
        foreach (var requestPath in Directory.GetFiles(GetRequestDirectory(), "*.request.json"))
        {
            if (_shutdownRequested)
                break;

            if (_stopping)
                break;

            var resultPath = Path.ChangeExtension(requestPath, ".result.json");
            if (File.Exists(resultPath))
                continue;

            try
            {
                var request = JsonSerializer.Deserialize<CaptureRequest>(await File.ReadAllTextAsync(requestPath), JsonOptions)
                    ?? throw new InvalidOperationException("Capture request was empty.");
                var result = await ProcessRequestAsync(page, request);
                await WriteResultAsync(resultPath, result);
            }
            catch (Exception ex)
            {
                await WriteResultAsync(resultPath, CaptureResult.Failed(Path.GetFileNameWithoutExtension(requestPath), ex.Message));
            }
        }
    }

    private static async Task<object> ProcessRequestAsync(ScanDebugPage page, CaptureRequest request)
    {
        if (string.Equals(request.Kind, "openFlyout", StringComparison.OrdinalIgnoreCase))
        {
            OpenFlyout(page, request.TargetAutomationId);
            await Task.Delay(350);
            return StateResult.Succeeded(request.Id, BuildState(page, request.TargetAutomationId), DateTimeOffset.UtcNow);
        }

        if (string.Equals(request.Kind, "selectFocusLeft", StringComparison.OrdinalIgnoreCase))
        {
            page.ViewModel.SelectedRoiSelection = "Focus Left";
            await Task.Delay(150);
            return StateResult.Succeeded(request.Id, new { page.ViewModel.SelectedRoiSelection }, DateTimeOffset.UtcNow);
        }

        if (string.Equals(request.Kind, "dragPreviewColumnSample", StringComparison.OrdinalIgnoreCase))
        {
            var previewWidth = page.ViewModel.PreviewFrame?.Width ?? 0;
            page.ViewModel.UpdateColumnSampleRange(42, 86, previewWidth);
            await Task.Delay(150);
            return StateResult.Succeeded(request.Id, BuildState(page, request.TargetAutomationId), DateTimeOffset.UtcNow);
        }

        if (string.Equals(request.Kind, "capturePreviewFrame", StringComparison.OrdinalIgnoreCase))
            return await CapturePreviewFrameAsync(page, request);

        if (string.Equals(request.Kind, "state", StringComparison.OrdinalIgnoreCase))
        {
            BringTargetIntoView(page, request.TargetAutomationId);
            await Task.Delay(250);
            return StateResult.Succeeded(request.Id, BuildState(page, request.TargetAutomationId), DateTimeOffset.UtcNow);
        }

        return await CaptureAsync(page, request);
    }

    private static void OpenFlyout(ScanDebugPage page, string? targetAutomationId)
    {
        var target = FindByAutomationId(page, targetAutomationId)
            ?? throw new InvalidOperationException($"Missing flyout target AutomationId '{targetAutomationId}'.");
        if (target is not Button button || button.Flyout is null)
            throw new InvalidOperationException($"Target '{targetAutomationId}' is not a Button with a Flyout.");

        button.Flyout.ShowAt(button);
    }

    private static void BringTargetIntoView(ScanDebugPage page, string? targetAutomationId)
    {
        var target = FindByAutomationId(page, targetAutomationId);
        target?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false, VerticalAlignmentRatio = 0.5 });
    }

    private static async Task<CaptureResult> CaptureAsync(ScanDebugPage page, CaptureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new InvalidOperationException("Capture request id is required.");
        if (string.IsNullOrWhiteSpace(request.OutputPath))
            throw new InvalidOperationException("Capture output path is required.");

        var scrollTarget = ResolveTarget(page, request.ScrollAutomationId ?? request.TargetAutomationId);
        scrollTarget?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false, VerticalAlignmentRatio = 0.5 });
        await Task.Delay(350);

        var focusTarget = ResolveTarget(page, request.FocusAutomationId);
        var focusApplied = focusTarget?.Focus(FocusState.Keyboard) == true;
        if (focusTarget is null)
            page.Focus(FocusState.Programmatic);
        await Task.Delay(150);

        var target = ResolveTarget(page, request.TargetAutomationId) ?? throw new InvalidOperationException($"Missing target AutomationId '{request.TargetAutomationId}'.");
        target.UpdateLayout();

        var renderTarget = new RenderTargetBitmap();
        await renderTarget.RenderAsync(target);
        var pixels = await renderTarget.GetPixelsAsync();
        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var temporaryPath = request.OutputPath + ".tmp";
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);
        using (File.Create(temporaryPath))
        {
        }

        var temporaryFile = await StorageFile.GetFileFromPathAsync(temporaryPath);
        using (var stream = await temporaryFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                (uint)renderTarget.PixelWidth,
                (uint)renderTarget.PixelHeight,
                96,
                96,
                pixels.ToArray());
            await encoder.FlushAsync();
        }

        if (File.Exists(request.OutputPath))
            File.Delete(request.OutputPath);
        File.Move(temporaryPath, request.OutputPath);

        return new CaptureResult(
            request.Id,
            true,
            string.Empty,
            request.OutputPath,
            renderTarget.PixelWidth,
            renderTarget.PixelHeight,
            request.TargetAutomationId,
            request.FocusAutomationId,
            focusApplied,
            DateTimeOffset.UtcNow);
    }

    private static async Task<CaptureResult> CapturePreviewFrameAsync(ScanDebugPage page, CaptureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new InvalidOperationException("Capture request id is required.");
        if (string.IsNullOrWhiteSpace(request.OutputPath))
            throw new InvalidOperationException("Capture output path is required.");

        var frame = page.ViewModel.PreviewFrame ?? throw new InvalidOperationException("Preview frame is unavailable.");
        if (frame.PixelFormat != Core.Models.ScanPreviewPixelFormat.Bgra8)
            throw new InvalidOperationException($"Preview frame pixel format '{frame.PixelFormat}' is not capturable as BGRA8.");

        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var pixels = GetPreviewPixels(frame);
        var temporaryPath = request.OutputPath + ".tmp";
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);
        using (File.Create(temporaryPath))
        {
        }

        var temporaryFile = await StorageFile.GetFileFromPathAsync(temporaryPath);
        using (var stream = await temporaryFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                (uint)frame.Width,
                (uint)frame.Height,
                96,
                96,
                pixels);
            await encoder.FlushAsync();
        }

        if (File.Exists(request.OutputPath))
            File.Delete(request.OutputPath);
        File.Move(temporaryPath, request.OutputPath);

        return new CaptureResult(
            request.Id,
            true,
            string.Empty,
            request.OutputPath,
            frame.Width,
            frame.Height,
            request.TargetAutomationId,
            request.FocusAutomationId,
            false,
            DateTimeOffset.UtcNow);
    }

    private static byte[] GetPreviewPixels(Core.Models.ScanPreviewFrame frame)
    {
        var rowBytes = frame.Width * 4;
        if (frame.StrideBytes == rowBytes)
            return frame.Pixels;

        var pixels = new byte[rowBytes * frame.Height];
        for (var y = 0; y < frame.Height; y++)
            Buffer.BlockCopy(frame.Pixels, y * frame.StrideBytes, pixels, y * rowBytes, rowBytes);

        return pixels;
    }

    private static FrameworkElement? FindByAutomationId(DependencyObject root, string? automationId)
    {
        if (string.IsNullOrWhiteSpace(automationId))
            return null;

        if (root is FrameworkElement element && string.Equals(AutomationProperties.GetAutomationId(element), automationId, StringComparison.Ordinal))
            return element;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var match = FindByAutomationId(VisualTreeHelper.GetChild(root, index), automationId);
            if (match is not null)
                return match;
        }

        return null;
    }

    private static FrameworkElement? ResolveTarget(ScanDebugPage page, string? automationId)
    {
        var target = FindByAutomationId(page, automationId);
        if (target is not null || string.IsNullOrWhiteSpace(automationId))
            return target;

        return page.FindName(automationId) as FrameworkElement;
    }

    private static object BuildState(ScanDebugPage page, string? targetAutomationId)
    {
        var viewModel = page.ViewModel;
        var previewFrame = viewModel.PreviewFrame;
        var previewWidth = previewFrame?.Width ?? 0;
        var profileDraftSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            App.GetService<Core.Contracts.Services.IScanFilmProfileWorkspace>().Snapshot.CurrentDraft,
            JsonOptions)));
        var currentFilmProfileValidationIssues = viewModel.CurrentFilmProfileValidationIssues.Select(ToIssue).ToArray();
        var stagedFilmProfileImportValidationIssues = viewModel.StagedFilmProfileImportValidationIssues.Select(ToIssue).ToArray();
        var selectedRoiRange = viewModel.TryGetSelectedRoiRange(previewWidth, out var selectedRange)
            ? selectedRange
            : null;
        var columnSampleRange = viewModel.TryGetColumnSampleRange(previewWidth, out var sampleRange)
            ? sampleRange
            : null;

        return new
        {
            targetAutomationId,
            targetGeometry = GetTargetGeometry(page, targetAutomationId),
            viewModel.SelectedRoiSelection,
            viewModel.RoiStartInput,
            viewModel.RoiEndInput,
            viewModel.RoiInputStatusText,
            viewModel.ColumnSampleStartInput,
            viewModel.ColumnSampleEndInput,
            viewModel.ColumnSampleStatusText,
            viewModel.HasUnsavedProfileChanges,
            viewModel.IsRoiEditModeEnabled,
            viewModel.IsColumnSampleEditModeEnabled,
            viewModel.IsBwActiveRoiOverlayVisible,
            viewModel.IsBwShieldRoiOverlayVisible,
            viewModel.IsFocusOverallRoiOverlayVisible,
            viewModel.IsFocusLeftRoiOverlayVisible,
            viewModel.IsFocusRightRoiOverlayVisible,
            viewModel.IsImageReferenceOverlayVisible,
            viewModel.IsGammaCorrectionEnabled,
            viewModel.PreviewGamma,
            profileDraftSha256,
            selectedCalibrationChannel = viewModel.SelectedCalibrationChannel,
            hasPendingFilmProfileImportResult = viewModel.HasPendingFilmProfileImportResult,
            hasStagedFilmProfileImport = viewModel.HasStagedFilmProfileImport,
            canApplyStagedFilmProfileImport = viewModel.CanApplyStagedFilmProfileImport,
            isCurrentFilmProfileValidationValid = viewModel.IsCurrentFilmProfileValidationValid,
            isStagedFilmProfileImportValid = viewModel.IsStagedFilmProfileImportValid,
            currentFilmProfileValidationIssues,
            stagedFilmProfileImportValidationIssues,
            viewModel.ScanRecipeOutputGamma,
            viewModel.IsWaterfallEnabled,
            viewModel.IsWaterfallCompressedEnabled,
            viewModel.CanEditWaterfall,
            selectedAcquisitionChannelCount = viewModel.AcquisitionChannels.Count(channel => channel.IsSelected),
            acquisitionChannels = viewModel.AcquisitionChannels.Select(channel => new { channel.Role, channel.ChannelLedBindingText, channel.IsSelected }).ToArray(),
            previewFrame = previewFrame is null ? null : new
            {
                previewFrame.Width,
                previewFrame.Height,
                previewFrame.Version,
                pixelSha256 = Convert.ToHexString(SHA256.HashData(GetPreviewPixels(previewFrame)))
            },
            previewScrollViewer = GetScrollViewerState(page, "PreviewScrollViewer"),
            pointerDiagnostics = page.GetVisualQaPointerDiagnostics(),
            roiDiagnostics = viewModel.GetVisualQaRoiDiagnostics(previewWidth),
            selectedRoiRange,
            columnSampleRange,
            adcIssues = viewModel.AdcRoiValidationIssues.Select(ToIssue).ToArray(),
            focusIssues = viewModel.FocusRoiValidationIssues.Select(ToIssue).ToArray(),
            imageReferenceIssues = viewModel.ImageReferenceRoiValidationIssues.Select(ToIssue).ToArray()
        };
    }

    private static object? GetScrollViewerState(ScanDebugPage page, string automationId)
    {
        if (ResolveTarget(page, automationId) is not ScrollViewer scrollViewer)
            return null;

        var scrollableWidth = Math.Max(0, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);
        var scrollableHeight = Math.Max(0, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        return new
        {
            scrollViewer.ExtentWidth,
            scrollViewer.ExtentHeight,
            scrollViewer.ViewportWidth,
            scrollViewer.ViewportHeight,
            ScrollableWidth = scrollableWidth,
            ScrollableHeight = scrollableHeight,
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset,
            scrollViewer.ZoomFactor,
            scrollViewer.MinZoomFactor,
            scrollViewer.MaxZoomFactor
        };
    }

    private static object? GetTargetGeometry(ScanDebugPage page, string? targetAutomationId)
    {
        var target = ResolveTarget(page, targetAutomationId);
        if (target is null)
            return null;

        var point = new Windows.Foundation.Point(0, 0);
        try
        {
            point = target.TransformToVisual(page).TransformPoint(point);
        }
        catch (ArgumentException)
        {
        }

        return new
        {
            x = point.X,
            y = point.Y,
            width = target.ActualWidth,
            height = target.ActualHeight,
            rootActualWidth = page.ActualWidth,
            rootActualHeight = page.ActualHeight,
            isVisible = target.Visibility == Visibility.Visible,
            targetAutomationId
        };
    }

    private static object ToIssue(Core.Models.ScanRoiValidationIssue issue)
        => new { owner = issue.Owner.ToString(), code = issue.Code.ToString(), issue.FieldPath };

    private static object ToIssue(Core.Models.ScanFilmProfileValidationIssue issue)
        => new
        {
            code = issue.Code.ToString(),
            fieldPath = issue.FieldPath,
            severity = issue.Severity.ToString(),
            messageKey = issue.MessageKey,
            messageArguments = issue.MessageArguments.ToArray()
        };

    private static async Task WriteResultAsync(string resultPath, object result)
        => await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, JsonOptions));

    private static void WriteReadyFile(ScanDebugPage page)
    {
        var ready = new
        {
            ready = true,
            page = nameof(ScanDebugPage),
            marker = PrismVisualQaPendingCalibrationHook.Marker,
            timestampUtc = DateTimeOffset.UtcNow,
            actualWidth = page.ActualWidth,
            actualHeight = page.ActualHeight
        };
        File.WriteAllText(Path.Combine(GetRequestDirectory(), ReadyFileName), JsonSerializer.Serialize(ready, JsonOptions));
    }

    private static string GetRequestDirectory()
        => Path.Combine(Path.GetTempPath(), RequestDirectoryName);

    private sealed record CaptureRequest(string Id, string TargetAutomationId, string OutputPath, string? ScrollAutomationId, string? FocusAutomationId, string? Kind);

    private sealed record CaptureResult(
        string Id,
        bool Success,
        string Error,
        string OutputPath,
        int PixelWidth,
        int PixelHeight,
        string? TargetAutomationId,
        string? FocusAutomationId,
        bool FocusApplied,
        DateTimeOffset CompletedUtc)
    {
        public static CaptureResult Failed(string id, string error)
            => new(id, false, error, string.Empty, 0, 0, null, null, false, DateTimeOffset.UtcNow);
    }

    private sealed record StateResult(string Id, bool Success, string Error, object State, DateTimeOffset CompletedUtc)
    {
        public static StateResult Succeeded(string id, object state, DateTimeOffset completedUtc)
            => new(id, true, string.Empty, state, completedUtc);
    }
}
#endif
