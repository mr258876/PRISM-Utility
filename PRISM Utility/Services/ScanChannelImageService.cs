using System.Buffers.Binary;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace PRISM_Utility.Services;

public sealed class ScanChannelImageService : IScanChannelImageService
{
    private const string ScannerMake = "Project PRISM";
    private const string ScannerModel = "PRISM Film Scanner";
    private const string ScannerSoftware = "PRISM Utility";
    private static readonly DngColorMetadata LinearRgbColorMetadata = new(
        AnalogBalance: [1.0, 1.0, 1.0],
        CameraNeutral: [1.0, 1.0, 1.0],
        ColorMatrix1: [1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0]);

    private readonly IScanCompositeImageProcessor _processor;
    private readonly IScanChannelAlignmentService _alignment;
    private readonly IScanPreviewPresenter _previewPresenter;
    private readonly IScanImageDecoder _decoder;
    private readonly IDngWriterService _dngWriter;
    private readonly IScanDngGeometrySettingsService _dngGeometrySettings;
    private readonly IDebugOutputMirrorService _debugOutputMirror;

    public ScanChannelImageService(
        IScanCompositeImageProcessor processor,
        IScanChannelAlignmentService alignment,
        IScanPreviewPresenter previewPresenter,
        IScanImageDecoder decoder,
        IDngWriterService dngWriter,
        IScanDngGeometrySettingsService dngGeometrySettings,
        IDebugOutputMirrorService debugOutputMirror)
    {
        _processor = processor;
        _alignment = alignment;
        _previewPresenter = previewPresenter;
        _decoder = decoder;
        _dngWriter = dngWriter;
        _dngGeometrySettings = dngGeometrySettings;
        _debugOutputMirror = debugOutputMirror;
    }

    public bool TryBuildRawPreview(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, int channelIndex, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error)
    {
        bitmap = null;
        error = string.Empty;

        if (channelIndex < 0 || channelIndex >= result.Passes.Count)
        {
            error = "Requested channel preview index is out of range.";
            return false;
        }

        var alignmentResult = _alignment.BuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, CancellationToken.None);
        if (!alignmentResult.HasUsableBuffers)
        {
            error = alignmentResult.GetDiagnosticSummary();
            return false;
        }

        ReportAlignmentDiagnostics(alignmentResult);
        var alignedPasses = alignmentResult.AlignedPassBuffers;

        if (channelIndex >= alignedPasses.Length)
        {
            error = "Aligned channel preview index is out of range.";
            return false;
        }

        return _previewPresenter.TryRender(alignedPasses[channelIndex], result.Rows, new ScanPreviewRenderOptions(false, false, false, 1.0, false, 0), currentBitmap, out bitmap, out error);
    }

    public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false)
    {
        frame = null;
        if (!TryBuildRgbCompositeBuffer(result, assignment, colorManagement, alignmentMode, CancellationToken.None, out var buffer, out error, channelProfiles, applyWhiteLevel) || buffer is null)
            return false;

        var bitmap = currentBitmap;
        if (bitmap is null || bitmap.PixelWidth != buffer.Width || bitmap.PixelHeight != buffer.Height)
            bitmap = new WriteableBitmap(buffer.Width, buffer.Height);

        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Position = 0;
            stream.Write(buffer.Pixels, 0, buffer.Pixels.Length);
        }

        bitmap.Invalidate();
        frame = new ScanCompositeFrame(buffer, bitmap);
        return true;
    }

    public bool TryBuildPartialRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, IReadOnlyDictionary<int, int> completedRowsByPassIndex, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false)
    {
        frame = null;
        if (!TryBuildPartialRgbCompositeBuffer(result, assignment, colorManagement, completedRowsByPassIndex, out var buffer, out error, channelProfiles, applyWhiteLevel) || buffer is null)
            return false;

        var bitmap = currentBitmap;
        if (bitmap is null || bitmap.PixelWidth != buffer.Width || bitmap.PixelHeight != buffer.Height)
            bitmap = new WriteableBitmap(buffer.Width, buffer.Height);

        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Position = 0;
            stream.Write(buffer.Pixels, 0, buffer.Pixels.Length);
        }

        bitmap.Invalidate();
        frame = new ScanCompositeFrame(buffer, bitmap);
        return true;
    }

    public async Task<ScanCompositePixelBuffer> BuildRgbCompositeBufferAsync(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (buffer, error) = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryBuildRgbCompositeBuffer(result, assignment, colorManagement, alignmentMode, cancellationToken, out var compositeBuffer, out var compositeError, channelProfiles, applyWhiteLevel) || compositeBuffer is null)
                return (Buffer: (ScanCompositePixelBuffer?)null, Error: compositeError);

            cancellationToken.ThrowIfCancellationRequested();
            return (Buffer: compositeBuffer, Error: string.Empty);
        }, cancellationToken);

        if (buffer is null)
            throw new InvalidOperationException(error);

        return buffer;
    }

    public async Task<StorageFile?> PickRgbImageFileAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });
        picker.SuggestedFileName = suggestedFileName;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return await picker.PickSaveFileAsync();
    }

    public async Task SaveRgbImageAsync(StorageFile file, ScanCompositeFrame frame, CancellationToken cancellationToken = default)
        => await SaveRgbImageAsync(file, frame.Buffer, cancellationToken);

    public async Task SaveRgbImageAsync(StorageFile file, ScanCompositePixelBuffer buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = new ScanOutputFileTransaction(file.Path);
        var temporaryFile = await StorageFile.GetFileFromPathAsync(transaction.TemporaryPath);
        using var stream = await temporaryFile.OpenAsync(FileAccessMode.ReadWrite);
        cancellationToken.ThrowIfCancellationRequested();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        cancellationToken.ThrowIfCancellationRequested();
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)buffer.Width, (uint)buffer.Height, 96, 96, buffer.Pixels);
        cancellationToken.ThrowIfCancellationRequested();
        await encoder.FlushAsync();
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Publish(cancellationToken);
    }

    public async Task<StorageFolder?> PickDngExportFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return await picker.PickSingleFolderAsync();
    }

    public async Task<ScanDngExportResult> ExportDngChannelsAsync(StorageFolder folder, ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, ScanDngExportMode exportMode = ScanDngExportMode.LinearRaw4, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var captureTime = DateTimeOffset.Now;
        var timestamp = captureTime.ToString("yyyyMMdd_HHmmss");

        if (result.Passes.Count != ScanDebugConstants.IlluminationChannelCount)
            throw new InvalidOperationException($"Expected {ScanDebugConstants.IlluminationChannelCount} scan passes for DNG export, but found {result.Passes.Count}.");

        var width = _decoder.GetDecodedPixelsPerLine();
        if (width <= 0)
            throw new InvalidOperationException("Decoded scan width is invalid for DNG export.");

        await _dngGeometrySettings.InitializeAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var geometry = _dngGeometrySettings.Settings.Clamp(width);
        var effectiveArea = BuildRectangle(geometry.ActiveRange, result.Rows);
        var maskedAreas = BuildMaskedAreaRectangles(geometry.MaskedBlackRanges, result.Rows);
        if (maskedAreas.Length == 0)
            throw new InvalidOperationException("At least one valid masked pixel range is required for DNG export black-level estimation.");

        var alignmentResult = await Task.Run(
            () => _alignment.BuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, cancellationToken),
            cancellationToken);
        ReportAlignmentDiagnostics(alignmentResult);
        if (!alignmentResult.HasUsableBuffers)
            throw new InvalidOperationException(alignmentResult.GetDiagnosticSummary());

        var normalizedPasses = alignmentResult.AlignedPassBuffers;

        var baseFileName = $"scan_{timestamp}";
        var exposureTime = BuildExposureTime(result);

        switch (exportMode)
        {
            case ScanDngExportMode.LinearRaw4:
                await ExportLinearRaw4Async(folder, baseFileName, result, assignment, normalizedPasses, width, effectiveArea, maskedAreas, exposureTime, captureTime, channelProfiles, cancellationToken);
                break;
            case ScanDngExportMode.LinearRgbIrw:
                await ExportLinearRgbIrwAsync(folder, baseFileName, result, assignment, normalizedPasses, width, effectiveArea, maskedAreas, exposureTime, captureTime, channelProfiles, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(exportMode), exportMode, "Unsupported DNG export mode.");
        }

        return ScanDngExportResult.FromAlignmentResult(alignmentResult);
    }

    public async Task ExportMonochromeDngAsync(StorageFolder folder, byte[] pixelData, int rows, ushort exposureTicks, uint sysClockKhz, string channelLabel, ScanChannelCalibrationProfile? profile = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(pixelData);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelLabel);

        var width = _decoder.GetDecodedPixelsPerLine();
        if (rows <= 0 || width <= 0)
            throw new ArgumentException("Rows and decoded width must be positive.");

        await _dngGeometrySettings.InitializeAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var geometry = _dngGeometrySettings.Settings.Clamp(width);
        var effectiveArea = BuildRectangle(geometry.ActiveRange, rows);
        var maskedAreas = BuildMaskedAreaRectangles(geometry.MaskedBlackRanges, rows);
        if (maskedAreas.Length == 0)
            throw new InvalidOperationException("At least one valid masked pixel range is required for DNG export black-level estimation.");

        DngRational? exposureTime = null;
        if (sysClockKhz != 0)
        {
            var exposureNanoseconds = ScanTimingMath.ExposureTicksToNanosecondsFloor(exposureTicks, sysClockKhz);
            if (exposureNanoseconds != 0)
                exposureTime = new DngRational(exposureNanoseconds, 1_000_000_000u);
        }

        var captureTime = DateTimeOffset.Now;
        var timestamp = captureTime.ToString("yyyyMMdd_HHmmss");
        var targetPath = Path.Combine(folder.Path, $"scan_{timestamp}_{channelLabel.ToLowerInvariant()}.dng");
        using var transaction = new ScanOutputFileTransaction(targetPath);
        var outputPath = transaction.TemporaryPath;
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var blackLevelPlane = BuildSingleBlackLevelPlane(pixelData, rows, maskedAreas, profile?.BlackLevel, cancellationToken);
                var blackLevelPlanes = new[] { blackLevelPlane };
                var packedPixelData = BuildPacked16Buffer(pixelData, rows, width, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _dngWriter.WriteRawDng(new DngWriteRequest(
                    outputPath,
                    packedPixelData,
                    (uint)width,
                    (uint)rows,
                    (uint)(width * sizeof(ushort)),
                    16,
                    1,
                    DngPixelLayout.MonochromeRaw,
                    DngCfaPattern.Unknown,
                    Make: ScannerMake,
                    Model: $"{ScannerModel} {channelLabel}",
                    Software: ScannerSoftware,
                    ExposureTime: exposureTime,
                    WhiteLevel: profile?.WhiteLevel ?? ushort.MaxValue,
                    CaptureTime: captureTime,
                    ActiveArea: effectiveArea,
                    DefaultCrop: effectiveArea,
                    MaskedAreas: maskedAreas,
                    BlackLevelPlanes: blackLevelPlanes));
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        transaction.Publish(cancellationToken);
    }

    public bool TryComputeAlignedChannelColumnAverage(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, string channelRole, ScanColumnRange range, out ushort average, out string error)
    {
        average = 0;
        error = string.Empty;

        var alignmentResult = _alignment.BuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, CancellationToken.None);
        if (!alignmentResult.HasUsableBuffers)
        {
            error = alignmentResult.GetDiagnosticSummary();
            return false;
        }

        ReportAlignmentDiagnostics(alignmentResult);
        var alignedPasses = alignmentResult.AlignedPassBuffers;

        var channelIndex = FindRoleIndex(assignment, channelRole);
        if (channelIndex < 0 || channelIndex >= alignedPasses.Length)
        {
            error = $"Channel role '{channelRole}' is not present in the current scan preview.";
            return false;
        }

        return TryComputeColumnAverage(alignedPasses[channelIndex], result.Rows, range, out average, out error);
    }

    private static ScanWorkflowResult BuildAlignedResult(ScanWorkflowResult result, byte[][] alignedPasses)
        => new(
            result.Rows,
            result.Passes.Select((capture, index) =>
                new ScanPassCapture(capture.PassIndex, capture.LedChannelIndex, true, capture.Rows, capture.MotorSteps, alignedPasses[index]))
                .ToArray(),
            result.ComputedMotorStepsPerPass,
            result.MotorIntervalNs,
            result.ExposureTicks,
            result.SysClockKhz);

    private bool TryBuildRgbCompositeBuffer(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, CancellationToken cancellationToken, out ScanCompositePixelBuffer? buffer, out string error, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        buffer = null;
        var alignmentResult = _alignment.BuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, cancellationToken);
        if (!alignmentResult.HasUsableBuffers)
        {
            error = alignmentResult.GetDiagnosticSummary();
            return false;
        }

        ReportAlignmentDiagnostics(alignmentResult);
        var alignedPasses = alignmentResult.AlignedPassBuffers;

        var previewPasses = applyWhiteLevel ? ApplyWhiteLevelOverrides(alignedPasses, assignment.Roles.ToArray(), result.Rows, channelProfiles, cancellationToken) : alignedPasses;
        var alignedResult = BuildAlignedResult(result, previewPasses);
        var normalizedAssignment = BuildNormalizedAssignment(assignment);
        return _processor.TryBuildRgbComposite(alignedResult, normalizedAssignment, colorManagement, cancellationToken, out buffer, out error);
    }

    private void ReportAlignmentDiagnostics(ScanChannelAlignmentResult result)
    {
        if (result.Diagnostics.Count != 0)
            _debugOutputMirror.Mirror("Scan.Alignment", result.GetDiagnosticSummary());
    }

    private bool TryBuildPartialRgbCompositeBuffer(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, IReadOnlyDictionary<int, int> completedRowsByPassIndex, out ScanCompositePixelBuffer? buffer, out string error, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false)
    {
        buffer = null;

        var normalizedPasses = BuildNormalizedPassBuffers(result, assignment);
        var previewPasses = applyWhiteLevel ? ApplyWhiteLevelOverrides(normalizedPasses, assignment.Roles.ToArray(), result.Rows, channelProfiles, CancellationToken.None) : normalizedPasses;
        var normalizedResult = BuildAlignedResult(result, previewPasses);
        var normalizedAssignment = BuildNormalizedAssignment(assignment);
        var availableRowsByRole = BuildRoleAvailabilityMap(result, assignment, completedRowsByPassIndex);
        return _processor.TryBuildPartialRgbComposite(normalizedResult, normalizedAssignment, colorManagement, availableRowsByRole, CancellationToken.None, out buffer, out error);
    }

    private byte[][] BuildNormalizedPassBuffers(ScanWorkflowResult result, ScanChannelAssignment assignment)
    {
        var normalizedPasses = new byte[result.Passes.Count][];
        for (var index = 0; index < result.Passes.Count; index++)
            normalizedPasses[index] = _processor.NormalizePassBuffer(result.Passes[index], index < assignment.ReversedFlags.Count && assignment.ReversedFlags[index], CancellationToken.None);

        return normalizedPasses;
    }

    private static IReadOnlyDictionary<string, ScanRowAvailability> BuildRoleAvailabilityMap(ScanWorkflowResult result, ScanChannelAssignment assignment, IReadOnlyDictionary<int, int> completedRowsByPassIndex)
    {
        var availability = new Dictionary<string, ScanRowAvailability>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < result.Passes.Count && index < assignment.Roles.Count; index++)
        {
            var role = assignment.Roles[index];
            if (string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase) || availability.ContainsKey(role))
                continue;

            var completedRows = completedRowsByPassIndex.TryGetValue(index, out var explicitCompletedRows)
                ? explicitCompletedRows
                : result.Rows;
            completedRows = Math.Clamp(completedRows, 0, result.Rows);

            var shouldReverse = !result.Passes[index].DirectionPositive ^ assignment.ReversedFlags[index];
            var startRow = shouldReverse ? result.Rows - completedRows : 0;
            availability[role] = new ScanRowAvailability(startRow, completedRows);
        }

        return availability;
    }

    private static ScanChannelAssignment BuildNormalizedAssignment(ScanChannelAssignment assignment)
        => new(
            assignment.Channel1Role,
            assignment.Channel2Role,
            assignment.Channel3Role,
            assignment.Channel4Role,
            false,
            false,
            false,
            false);

    private async Task ExportLinearRaw4Async(
        StorageFolder folder,
        string baseFileName,
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        byte[][] normalizedPasses,
        int width,
        DngRectangle effectiveArea,
        DngRectangle[] maskedAreas,
        DngRational? exposureTime,
        DateTimeOffset captureTime,
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles,
        CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(folder.Path, $"{baseFileName}_linearraw4.dng");
        using var transaction = new ScanOutputFileTransaction(targetPath);
        var outputPath = transaction.TemporaryPath;
        try
        {
            await Task.Run(() =>
            {
                var interleaved = BuildPacked16Buffer(normalizedPasses, result.Rows, width, cancellationToken);
                var blackLevelPlanes = BuildBlackLevelPlanes(normalizedPasses, result.Rows, maskedAreas, assignment.Roles.ToArray(), channelProfiles, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _dngWriter.WriteRawDng(new DngWriteRequest(
                    outputPath,
                    interleaved,
                    (uint)width,
                    (uint)result.Rows,
                    (uint)(width * normalizedPasses.Length * sizeof(ushort)),
                    16,
                    (ushort)normalizedPasses.Length,
                    DngPixelLayout.LinearRawMultiChannel,
                    DngCfaPattern.Unknown,
                    BuildChannelColors(assignment),
                    Make: ScannerMake,
                    Model: $"{ScannerModel} LinearRaw4",
                    Software: ScannerSoftware,
                    ExposureTime: exposureTime,
                    WhiteLevel: ResolveWhiteLevel(assignment.Roles, channelProfiles),
                    CaptureTime: captureTime,
                    ActiveArea: effectiveArea,
                    DefaultCrop: effectiveArea,
                    MaskedAreas: maskedAreas,
                    BlackLevelPlanes: blackLevelPlanes));
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        transaction.Publish(cancellationToken);
    }

    private async Task ExportLinearRgbIrwAsync(
        StorageFolder folder,
        string baseFileName,
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        byte[][] normalizedPasses,
        int width,
        DngRectangle effectiveArea,
        DngRectangle[] maskedAreas,
        DngRational? exposureTime,
        DateTimeOffset captureTime,
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles,
        CancellationToken cancellationToken)
    {
        var rgbPasses = GetRequiredRolePasses(normalizedPasses, assignment, "Red", "Green", "Blue");
        var auxChannel = GetRequiredAuxiliaryPass(normalizedPasses, assignment);
        var rgbRoles = new[] { "Red", "Green", "Blue" };

        var rgbTargetPath = Path.Combine(folder.Path, $"{baseFileName}_rgb.dng");
        var auxTargetPath = Path.Combine(folder.Path, $"{baseFileName}_irw.dng");
        using var transaction = new ScanOutputFilePairTransaction(rgbTargetPath, auxTargetPath);
        var rgbOutputPath = transaction.FirstTemporaryPath;
        var auxOutputPath = transaction.SecondTemporaryPath;
        try
        {
            await Task.Run(() =>
            {
                var rgbInterleaved = BuildPacked16Buffer(rgbPasses, result.Rows, width, cancellationToken);
                var rgbBlackLevelPlanes = BuildBlackLevelPlanes(rgbPasses, result.Rows, maskedAreas, rgbRoles, channelProfiles, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _dngWriter.WriteRawDng(new DngWriteRequest(
                    rgbOutputPath,
                    rgbInterleaved,
                    (uint)width,
                    (uint)result.Rows,
                    (uint)(width * rgbPasses.Length * sizeof(ushort)),
                    16,
                    (ushort)rgbPasses.Length,
                    DngPixelLayout.LinearRgb,
                    DngCfaPattern.Unknown,
                    Make: ScannerMake,
                    Model: $"{ScannerModel} RGB",
                    Software: ScannerSoftware,
                    ExposureTime: exposureTime,
                    WhiteLevel: ResolveWhiteLevel(rgbRoles, channelProfiles),
                    CaptureTime: captureTime,
                    ActiveArea: effectiveArea,
                    DefaultCrop: effectiveArea,
                    MaskedAreas: maskedAreas,
                    BlackLevelPlanes: rgbBlackLevelPlanes,
                    Color: LinearRgbColorMetadata));
                cancellationToken.ThrowIfCancellationRequested();

                var auxPasses = new[] { auxChannel.Pass };
                var auxBuffer = BuildPacked16Buffer(auxPasses, result.Rows, width, cancellationToken);
                var auxBlackLevelPlanes = BuildBlackLevelPlanes(auxPasses, result.Rows, maskedAreas, new[] { auxChannel.Role }, channelProfiles, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _dngWriter.WriteRawDng(new DngWriteRequest(
                    auxOutputPath,
                    auxBuffer,
                    (uint)width,
                    (uint)result.Rows,
                    (uint)(width * sizeof(ushort)),
                    16,
                    1,
                    DngPixelLayout.MonochromeRaw,
                    DngCfaPattern.Unknown,
                    Make: ScannerMake,
                    Model: $"{ScannerModel} {auxChannel.Role}",
                    Software: ScannerSoftware,
                    ExposureTime: exposureTime,
                    WhiteLevel: ResolveWhiteLevel(new[] { auxChannel.Role }, channelProfiles),
                    CaptureTime: captureTime,
                    ActiveArea: effectiveArea,
                    DefaultCrop: effectiveArea,
                    MaskedAreas: maskedAreas,
                    BlackLevelPlanes: auxBlackLevelPlanes));
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Publish(cancellationToken);
    }

    private byte[] BuildPacked16Buffer(byte[][] normalizedPasses, int rows, int width, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedPasses);

        var rowStrideBytes = checked(width * normalizedPasses.Length * sizeof(ushort));
        var combined = new byte[checked(rowStrideBytes * rows)];

        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowOffset = y * rowStrideBytes;
            for (var x = 0; x < width; x++)
            {
                var pixelOffset = rowOffset + (x * normalizedPasses.Length * sizeof(ushort));
                for (var channel = 0; channel < normalizedPasses.Length; channel++)
                {
                    if (!_decoder.TryGetSample16(normalizedPasses[channel], rows, x, y, out var sample))
                        throw new InvalidOperationException($"Failed to decode sample at ({x}, {y}) for channel {channel + 1}.");

                    BinaryPrimitives.WriteUInt16LittleEndian(combined.AsSpan(pixelOffset + (channel * sizeof(ushort)), sizeof(ushort)), sample);
                }
            }
        }

        return combined;
    }

    private byte[] BuildPacked16Buffer(byte[] lineBuffer, int rows, int width, CancellationToken cancellationToken)
    {
        var rowStrideBytes = checked(width * sizeof(ushort));
        var packed = new byte[checked(rowStrideBytes * rows)];

        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowOffset = y * rowStrideBytes;
            for (var x = 0; x < width; x++)
            {
                if (!_decoder.TryGetSample16(lineBuffer, rows, x, y, out var sample))
                    throw new InvalidOperationException($"Failed to decode sample at ({x}, {y}) for monochrome DNG export.");

                BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(rowOffset + (x * sizeof(ushort)), sizeof(ushort)), sample);
            }
        }

        return packed;
    }

    private DngBlackLevelPlane[] BuildBlackLevelPlanes(byte[][] normalizedPasses, int rows, DngRectangle[] maskedAreas, IReadOnlyList<string> roles, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles, CancellationToken cancellationToken)
    {
        var blackLevels = new DngBlackLevelPlane[normalizedPasses.Length];

        for (var channel = 0; channel < normalizedPasses.Length; channel++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (channel < roles.Count && TryGetProfileBlackLevel(roles[channel], channelProfiles, out var configuredBlackLevel))
            {
                blackLevels[channel] = BuildConstantBlackLevelPlane(configuredBlackLevel);
                continue;
            }

            double evenSum = 0;
            double oddSum = 0;
            long evenCount = 0;
            long oddCount = 0;

            foreach (var area in maskedAreas)
            {
                for (var y = (int)area.Top; y < area.Bottom; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var x = (int)area.Left; x < area.Right; x++)
                    {
                        if (!_decoder.TryGetSample16(normalizedPasses[channel], rows, x, y, out var sample))
                            continue;

                        if ((x & 1) == 0)
                        {
                            evenSum += sample;
                            evenCount++;
                        }
                        else
                        {
                            oddSum += sample;
                            oddCount++;
                        }
                    }
                }
            }

            if (evenCount == 0 || oddCount == 0)
            {
                throw new InvalidOperationException($"Masked pixel ranges must contain both even and odd columns for channel {channel + 1} black-level estimation.");
            }

            var evenMean = evenSum / evenCount;
            var oddMean = oddSum / oddCount;
            blackLevels[channel] = new DngBlackLevelPlane(evenMean, oddMean, evenMean, oddMean);
        }

        return blackLevels;
    }

    private DngBlackLevelPlane BuildSingleBlackLevelPlane(byte[] pixelData, int rows, DngRectangle[] maskedAreas, ushort? configuredBlackLevel, CancellationToken cancellationToken)
    {
        if (configuredBlackLevel is ushort blackLevel)
            return BuildConstantBlackLevelPlane(blackLevel);

        double evenSum = 0;
        double oddSum = 0;
        long evenCount = 0;
        long oddCount = 0;

        foreach (var area in maskedAreas)
        {
            for (var y = (int)area.Top; y < area.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = (int)area.Left; x < area.Right; x++)
                {
                    if (!_decoder.TryGetSample16(pixelData, rows, x, y, out var sample))
                        continue;

                    if ((x & 1) == 0)
                    {
                        evenSum += sample;
                        evenCount++;
                    }
                    else
                    {
                        oddSum += sample;
                        oddCount++;
                    }
                }
            }
        }

        if (evenCount == 0 || oddCount == 0)
            throw new InvalidOperationException("Masked pixel ranges must contain both even and odd columns for black-level estimation.");

        var evenMean = evenSum / evenCount;
        var oddMean = oddSum / oddCount;
        return new DngBlackLevelPlane(evenMean, oddMean, evenMean, oddMean);
    }

    private static DngBlackLevelPlane BuildConstantBlackLevelPlane(ushort blackLevel)
        => new(blackLevel, blackLevel, blackLevel, blackLevel);

    private bool TryComputeColumnAverage(byte[] lineBuffer, int rows, ScanColumnRange range, out ushort average, out string error)
    {
        average = 0;
        error = string.Empty;

        var width = _decoder.GetDecodedPixelsPerLine();
        if (rows <= 0 || width <= 0)
        {
            error = "Scan dimensions are invalid for column sampling.";
            return false;
        }

        var referenceRange = ScanImageReferenceColumnRange.TryCreate(range, width).Value;
        if (referenceRange is null)
        {
            error = "The selected columns are outside the decoded image bounds.";
            return false;
        }

        ulong sum = 0;
        long count = 0;
        for (var y = 0; y < rows; y++)
        {
            for (var x = referenceRange.ColumnRange.Start; x <= referenceRange.ColumnRange.EndInclusive; x++)
            {
                if (!_decoder.TryGetSample16(lineBuffer, rows, x, y, out var sample))
                    continue;

                sum += sample;
                count++;
            }
        }

        if (count == 0)
        {
            error = "No valid pixels were available in the selected columns.";
            return false;
        }

        average = (ushort)Math.Clamp((int)Math.Round(sum / (double)count), 0, ushort.MaxValue);
        return true;
    }

    private byte[][] ApplyWhiteLevelOverrides(byte[][] alignedPasses, IReadOnlyList<string> roles, int rows, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles, CancellationToken cancellationToken)
    {
        var adjusted = new byte[alignedPasses.Length][];
        for (var index = 0; index < alignedPasses.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index < roles.Count && TryGetProfileWhiteLevel(roles[index], channelProfiles, out var whiteLevel))
                adjusted[index] = ScalePassToWhiteLevel(alignedPasses[index], rows, whiteLevel, cancellationToken);
            else
                adjusted[index] = alignedPasses[index];
        }

        return adjusted;
    }

    private static bool TryGetProfileWhiteLevel(string role, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles, out ushort whiteLevel)
    {
        whiteLevel = 0;
        if (channelProfiles is null
            || !channelProfiles.TryGetValue(role, out var profile)
            || profile.WhiteLevel is not ushort configuredWhiteLevel
            || configuredWhiteLevel == 0)
        {
            return false;
        }

        whiteLevel = configuredWhiteLevel;
        return true;
    }

    private static bool TryGetProfileBlackLevel(string role, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles, out ushort blackLevel)
    {
        blackLevel = 0;
        if (channelProfiles is null
            || !channelProfiles.TryGetValue(role, out var profile)
            || profile.BlackLevel is not ushort configuredBlackLevel)
        {
            return false;
        }

        blackLevel = configuredBlackLevel;
        return true;
    }

    private static uint ResolveWhiteLevel(IEnumerable<string> roles, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles)
    {
        ushort resolved = ushort.MaxValue;
        var found = false;
        foreach (var role in roles)
        {
            if (!TryGetProfileWhiteLevel(role, channelProfiles, out var whiteLevel))
                continue;

            resolved = found ? (ushort)Math.Max(resolved, whiteLevel) : whiteLevel;
            found = true;
        }

        return found ? resolved : ushort.MaxValue;
    }

    private static int FindRoleIndex(ScanChannelAssignment assignment, string role)
        => assignment.Roles.Select((assignedRole, index) => new { assignedRole, index })
            .FirstOrDefault(entry => string.Equals(entry.assignedRole, role, StringComparison.OrdinalIgnoreCase))?.index ?? -1;

    private static byte[] ScalePassToWhiteLevel(byte[] lineBuffer, int rows, ushort whiteLevel, CancellationToken cancellationToken)
    {
        if (whiteLevel == 0 || whiteLevel == ushort.MaxValue)
            return lineBuffer;

        var scaled = (byte[])lineBuffer.Clone();
        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowStart = y * ScanDebugConstants.BytesPerLine;
            var decodeStart = rowStart + ScanDebugConstants.LineBufferMarginLeft;
            var decodeEndExclusive = rowStart + ScanDebugConstants.BytesPerLine - ScanDebugConstants.LineBufferMarginRight;
            for (var i = decodeStart; i + (ScanDebugConstants.PackedGroupBytes - 1) < decodeEndExclusive; i += ScanDebugConstants.PackedGroupBytes)
            {
                var sample0 = ReadPackedSample0(scaled, i);
                var sample1 = ReadPackedSample1(scaled, i);
                WritePackedGroupSamples(scaled, i, ScaleSampleToWhiteLevel(sample0, whiteLevel), ScaleSampleToWhiteLevel(sample1, whiteLevel));
            }
        }

        return scaled;
    }

    private static ushort ScaleSampleToWhiteLevel(ushort sample, ushort whiteLevel)
        => (ushort)Math.Clamp((int)Math.Round(sample * (ushort.MaxValue / (double)whiteLevel)), 0, ushort.MaxValue);

    private static ushort ReadPackedSample0(byte[] buffer, int startIndex)
        => (ushort)((buffer[startIndex + 1] << 8) | buffer[startIndex + 3]);

    private static ushort ReadPackedSample1(byte[] buffer, int startIndex)
        => (ushort)((buffer[startIndex] << 8) | buffer[startIndex + 2]);

    private static void WritePackedGroupSamples(byte[] buffer, int startIndex, ushort sample0, ushort sample1)
    {
        buffer[startIndex] = (byte)(sample1 >> 8);
        buffer[startIndex + 1] = (byte)(sample0 >> 8);
        buffer[startIndex + 2] = (byte)(sample1 & 0xFF);
        buffer[startIndex + 3] = (byte)(sample0 & 0xFF);
    }

    private static DngChannelColor[] BuildChannelColors(ScanChannelAssignment assignment)
        => assignment.Roles.Select(MapRoleToChannelColor).ToArray();

    private static byte[][] GetRequiredRolePasses(byte[][] normalizedPasses, ScanChannelAssignment assignment, params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(normalizedPasses);
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(roles);

        var selectedPasses = new byte[roles.Length][];

        for (var roleIndex = 0; roleIndex < roles.Length; roleIndex++)
        {
            var role = roles[roleIndex];
            var matchingIndexes = assignment.Roles
                .Select((assignedRole, index) => new { assignedRole, index })
                .Where(entry => string.Equals(entry.assignedRole, role, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.index)
                .ToArray();

            if (matchingIndexes.Length != 1)
                throw new InvalidOperationException($"DNG export mode requires exactly one '{role}' channel assignment, but found {matchingIndexes.Length}.");

            var passIndex = matchingIndexes[0];
            if (passIndex < 0 || passIndex >= normalizedPasses.Length)
                throw new InvalidOperationException($"Aligned pass index {passIndex} for role '{role}' is out of range.");

            selectedPasses[roleIndex] = normalizedPasses[passIndex];
        }

        return selectedPasses;
    }

    private static (string Role, byte[] Pass) GetRequiredAuxiliaryPass(byte[][] normalizedPasses, ScanChannelAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(normalizedPasses);
        ArgumentNullException.ThrowIfNull(assignment);

        var auxMatches = assignment.Roles
            .Select((role, index) => new { role, index })
            .Where(entry => string.Equals(entry.role, "IR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.role, "White", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (auxMatches.Length != 1)
            throw new InvalidOperationException($"DNG export mode requires exactly one auxiliary IR or White channel assignment, but found {auxMatches.Length}.");

        var auxMatch = auxMatches[0];
        if (auxMatch.index < 0 || auxMatch.index >= normalizedPasses.Length)
            throw new InvalidOperationException($"Aligned pass index {auxMatch.index} for role '{auxMatch.role}' is out of range.");

        return (auxMatch.role, normalizedPasses[auxMatch.index]);
    }

    private static DngChannelColor MapRoleToChannelColor(string role)
        => role switch
        {
            "Red" => DngChannelColor.Red,
            "Green" => DngChannelColor.Green,
            "Blue" => DngChannelColor.Blue,
            "White" => DngChannelColor.White,
            "IR" => DngChannelColor.White,
            "Unused" => DngChannelColor.White,
            _ => DngChannelColor.White
        };

    private static DngRectangle BuildRectangle(ScanColumnRange range, int rows)
        => new(0, (uint)range.Start, (uint)rows, (uint)(range.EndInclusive + 1));

    private static DngRectangle[] BuildMaskedAreaRectangles(ScanColumnRange[] ranges, int rows)
        => ranges.Select(range => BuildRectangle(range, rows)).ToArray();

    private static DngRational? BuildExposureTime(ScanWorkflowResult result)
    {
        if (result.SysClockKhz == 0)
            return null;

        var exposureNanoseconds = ScanTimingMath.ExposureTicksToNanosecondsFloor(result.ExposureTicks, result.SysClockKhz);
        return exposureNanoseconds == 0
            ? null
            : new DngRational(exposureNanoseconds, 1_000_000_000u);
    }

}
