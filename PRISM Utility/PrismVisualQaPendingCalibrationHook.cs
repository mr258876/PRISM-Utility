#if PRISM_VISUAL_QA
using System.Reflection;

using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;
using PRISM_Utility.ViewModels;
using PRISM_Utility.Views;

namespace PRISM_Utility;

internal static class PrismVisualQaPendingCalibrationHook
{
    internal const string Marker = "PRISM_VISUAL_QA_PENDING_CALIBRATION_HOOK";

    internal static void Activate(INavigationService navigationService)
    {
        navigationService.NavigateTo(AppRoute.ScanDebug, clearNavigation: true);
        Seed(App.GetService<ScanDebugViewModel>());
    }

    internal static void ApplyPageState(ScanDebugPage page, Action<int> setActiveWorkbenchSection)
    {
        Seed(page.ViewModel);
        var forceMotionReadRequired = string.Equals(
            Environment.GetEnvironmentVariable("PRISM_VISUAL_QA_MOTION_READ_REQUIRED"),
            "1",
            StringComparison.Ordinal);
        setActiveWorkbenchSection(forceMotionReadRequired ? 5 : 2);
        if (int.TryParse(Environment.GetEnvironmentVariable("PRISM_VISUAL_QA_SECTION_INDEX"), out var sectionIndex)
            && sectionIndex is >= 0 and <= 5)
        {
            setActiveWorkbenchSection(sectionIndex);
        }

        if (forceMotionReadRequired)
            MarkMotionReadRequired(page.ViewModel);

        PrismVisualQaCaptureService.Start(page);
    }

    private static void Seed(ScanDebugViewModel viewModel)
    {
        var previewWidth = GetPreviewDimension("PRISM_VISUAL_QA_PREVIEW_WIDTH", 320);
        var previewHeight = GetPreviewDimension("PRISM_VISUAL_QA_PREVIEW_HEIGHT", 120);
        var populateScanBuffer = string.Equals(
            Environment.GetEnvironmentVariable("PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER"),
            "1",
            StringComparison.Ordinal);
        viewModel.SelectedCalibrationChannel = "Red";
        if (populateScanBuffer)
        {
            viewModel.SelectedCaptureMode = ScanDebugCaptureMode.Single;
            viewModel.IsPreviewEnabled = true;
            ApplySyntheticScanBuffer(viewModel, Math.Min(previewHeight, ScanDebugConstants.MaxPreviewRows));
        }
        else
        {
            viewModel.PreviewFrame = CreateSyntheticPreviewFrame(previewWidth, previewHeight);
        }

        var roiSettings = CreateVisualQaRoiSettings();
        ApplyRoiSettings(viewModel, roiSettings);
        viewModel.UpdateColumnSampleRange(
            80,
            120,
            populateScanBuffer ? ScanDebugConstants.DecodedPixelsPerLine : previewWidth);
        viewModel.PendingCalibrationResult = PendingCalibrationResult.Create(
            new ScanParameterSnapshot(1200, -14, 18, 11, 19, 24000),
            new ScanParameterSnapshot(1184, -4, 21, 3, 22, 24000),
            new ScanCalibrationMetrics(5.25m, 34.5m, 0.42m, 12.8m),
            new ScanCalibrationMetrics(0.72m, 4.1m, 0.03m, 6.2m),
            new CalibrationCandidateContext("Red", "VISUAL-QA-WORKSPACE", "VISUAL-QA-DEVICE"),
            ScanCalibrationCandidateValidation.Valid,
            roiSettings,
            blackLevel: 512,
            whiteLevel: 60120);
    }

    private static void ApplyRoiSettings(ScanDebugViewModel viewModel, ScanCalibrationRoiSettings roiSettings)
    {
        typeof(ScanDebugViewModel).GetField("_roiSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(viewModel, roiSettings);
        typeof(ScanDebugViewModel).GetMethod("RefreshRoiStatus", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(viewModel, null);
    }

    private static ScanCalibrationRoiSettings CreateVisualQaRoiSettings()
        => new(
            new ScanColumnRange(128, 260),
            new ScanColumnRange(26, 115),
            new ScanColumnRange(145, 170),
            new ScanColumnRange(210, 235),
            new ScanColumnRange(145, 260));

    private static int GetPreviewDimension(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var value)
            ? Math.Clamp(value, fallback, 4096)
            : fallback;
    }

    private static ScanPreviewFrame CreateSyntheticPreviewFrame(int width, int height)
    {
        var strideBytes = width * 4;
        var pixels = new byte[strideBytes * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * strideBytes) + (x * 4);
                pixels[offset] = (byte)(48 + (x % 96));
                pixels[offset + 1] = (byte)(64 + (y % 96));
                pixels[offset + 2] = (byte)(96 + ((x + y) % 96));
                pixels[offset + 3] = 255;
            }
        }

        return new ScanPreviewFrame(pixels, width, height, strideBytes, ScanPreviewPixelFormat.Bgra8, 19);
    }

    private static void ApplySyntheticScanBuffer(ScanDebugViewModel viewModel, int rows)
    {
        var method = typeof(ScanDebugViewModel).GetMethod(
            "ApplyScanFrame",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing ScanDebugViewModel.ApplyScanFrame for visual QA raw buffer seed.");
        method.Invoke(viewModel, [CreateSyntheticScanBuffer(rows), rows, "Visual QA raw scan buffer seeded."]);
    }

    private static byte[] CreateSyntheticScanBuffer(int rows)
    {
        var imageBytes = new byte[checked(ScanDebugConstants.BytesPerLine * rows)];
        var width = ScanDebugConstants.DecodedPixelsPerLine;
        for (var y = 0; y < rows; y++)
        {
            var rowStart = (y * ScanDebugConstants.BytesPerLine) + ScanDebugConstants.LineBufferMarginLeft;
            for (var x = 0; x < width; x += ScanDebugConstants.PackedGroupPixels)
            {
                var offset = rowStart + ((x / ScanDebugConstants.PackedGroupPixels) * ScanDebugConstants.PackedGroupBytes);
                var evenSample = CreateSyntheticScanSample(x, y);
                var oddSample = CreateSyntheticScanSample(x + 1, y);
                imageBytes[offset] = (byte)(oddSample >> 8);
                imageBytes[offset + 1] = (byte)(evenSample >> 8);
                imageBytes[offset + 2] = (byte)oddSample;
                imageBytes[offset + 3] = (byte)evenSample;
            }
        }

        return imageBytes;
    }

    private static ushort CreateSyntheticScanSample(int x, int y)
        => (ushort)(4096 + (((x * 31) + (y * 521)) % 49152));

    private static void MarkMotionReadRequired(ScanDebugViewModel viewModel)
    {
        var method = typeof(ScanDebugViewModel).GetMethod(
            "MarkMotionStateReadRequired",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing ScanDebugViewModel.MarkMotionStateReadRequired for visual QA seed.");
        method.Invoke(viewModel, null);
    }
}
#endif
