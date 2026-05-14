using System.Buffers.Binary;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
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

    public ScanChannelImageService(
        IScanCompositeImageProcessor processor,
        IScanChannelAlignmentService alignment,
        IScanPreviewPresenter previewPresenter,
        IScanImageDecoder decoder,
        IDngWriterService dngWriter,
        IScanDngGeometrySettingsService dngGeometrySettings)
    {
        _processor = processor;
        _alignment = alignment;
        _previewPresenter = previewPresenter;
        _decoder = decoder;
        _dngWriter = dngWriter;
        _dngGeometrySettings = dngGeometrySettings;
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

        if (!_alignment.TryBuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, out var alignedPasses, out error))
            return false;

        if (channelIndex >= alignedPasses.Length)
        {
            error = "Aligned channel preview index is out of range.";
            return false;
        }

        return _previewPresenter.TryRender(alignedPasses[channelIndex], result.Rows, new ScanPreviewRenderOptions(false, false, false, 1.0), currentBitmap, out bitmap, out error);
    }

    public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error)
    {
        frame = null;
        if (!_alignment.TryBuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, out var alignedPasses, out error))
            return false;

        var alignedResult = BuildAlignedResult(result, alignedPasses);
        var normalizedAssignment = BuildNormalizedAssignment(assignment);
        if (!_processor.TryBuildRgbComposite(alignedResult, normalizedAssignment, colorManagement, out var buffer, out error) || buffer is null)
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

    public async Task<StorageFile?> PickRgbImageFileAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });
        picker.SuggestedFileName = suggestedFileName;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return await picker.PickSaveFileAsync();
    }

    public async Task SaveRgbImageAsync(StorageFile file, ScanCompositeFrame frame)
    {
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)frame.Width, (uint)frame.Height, 96, 96, frame.Pixels);
        await encoder.FlushAsync();
    }

    public async Task<StorageFolder?> PickDngExportFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return await picker.PickSingleFolderAsync();
    }

    public async Task ExportDngChannelsAsync(StorageFolder folder, ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, ScanDngExportMode exportMode = ScanDngExportMode.LinearRaw4)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");

        if (result.Passes.Count != ScanDebugConstants.IlluminationChannelCount)
            throw new InvalidOperationException($"Expected {ScanDebugConstants.IlluminationChannelCount} scan passes for DNG export, but found {result.Passes.Count}.");

        var width = _decoder.GetDecodedPixelsPerLine();
        if (width <= 0)
            throw new InvalidOperationException("Decoded scan width is invalid for DNG export.");

        await _dngGeometrySettings.InitializeAsync();
        var geometry = _dngGeometrySettings.Settings.Clamp(width);
        var effectiveArea = BuildRectangle(geometry.ActiveRange, result.Rows);
        var maskedAreas = BuildMaskedAreaRectangles(geometry.MaskedBlackRanges, result.Rows);
        if (maskedAreas.Length == 0)
            throw new InvalidOperationException("At least one valid masked pixel range is required for DNG export black-level estimation.");

        if (!_alignment.TryBuildAlignedNormalizedPassBuffers(result, assignment, alignmentMode, out var normalizedPasses, out var alignmentError))
            throw new InvalidOperationException(alignmentError);

        var baseFileName = $"scan_{timestamp}";
        var exposureTime = BuildExposureTime(result);

        switch (exportMode)
        {
            case ScanDngExportMode.LinearRaw4:
                await ExportLinearRaw4Async(folder, baseFileName, result, assignment, normalizedPasses, width, effectiveArea, maskedAreas, exposureTime);
                break;
            case ScanDngExportMode.LinearRgbIrw:
                await ExportLinearRgbIrwAsync(folder, baseFileName, result, assignment, normalizedPasses, width, effectiveArea, maskedAreas, exposureTime);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(exportMode), exportMode, "Unsupported DNG export mode.");
        }
    }

    private static ScanWorkflowResult BuildAlignedResult(ScanWorkflowResult result, byte[][] alignedPasses)
        => new(
            result.Rows,
            result.Passes.Select((capture, index) =>
                new ScanPassCapture(capture.PassIndex, capture.LedChannelIndex, true, capture.Rows, capture.MotorSteps, alignedPasses[index]))
                .ToArray(),
            result.ComputedMotorStepsPerPass,
            result.MotorIntervalUs,
            result.ExposureTicks,
            result.SysClockKhz);

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
        DngRational? exposureTime)
    {
        var file = await folder.CreateFileAsync($"{baseFileName}_linearraw4.dng", CreationCollisionOption.ReplaceExisting);
        var interleaved = BuildPacked16Buffer(normalizedPasses, result.Rows, width);
        var blackLevelPlanes = BuildBlackLevelPlanes(normalizedPasses, result.Rows, maskedAreas);

        _dngWriter.WriteRawDng(new DngWriteRequest(
            file.Path,
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
            WhiteLevel: ushort.MaxValue,
            ActiveArea: effectiveArea,
            DefaultCrop: effectiveArea,
            MaskedAreas: maskedAreas,
            BlackLevelPlanes: blackLevelPlanes));
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
        DngRational? exposureTime)
    {
        var rgbPasses = GetRequiredRolePasses(normalizedPasses, assignment, "Red", "Green", "Blue");
        var auxChannel = GetRequiredAuxiliaryPass(normalizedPasses, assignment);

        var rgbFile = await folder.CreateFileAsync($"{baseFileName}_rgb.dng", CreationCollisionOption.ReplaceExisting);
        var rgbInterleaved = BuildPacked16Buffer(rgbPasses, result.Rows, width);
        var rgbBlackLevelPlanes = BuildBlackLevelPlanes(rgbPasses, result.Rows, maskedAreas);

        _dngWriter.WriteRawDng(new DngWriteRequest(
            rgbFile.Path,
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
            WhiteLevel: ushort.MaxValue,
            ActiveArea: effectiveArea,
            DefaultCrop: effectiveArea,
            MaskedAreas: maskedAreas,
            BlackLevelPlanes: rgbBlackLevelPlanes,
            Color: LinearRgbColorMetadata));

        var auxFile = await folder.CreateFileAsync($"{baseFileName}_irw.dng", CreationCollisionOption.ReplaceExisting);
        var auxPasses = new[] { auxChannel.Pass };
        var auxBuffer = BuildPacked16Buffer(auxPasses, result.Rows, width);
        var auxBlackLevelPlanes = BuildBlackLevelPlanes(auxPasses, result.Rows, maskedAreas);

        _dngWriter.WriteRawDng(new DngWriteRequest(
            auxFile.Path,
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
            WhiteLevel: ushort.MaxValue,
            ActiveArea: effectiveArea,
            DefaultCrop: effectiveArea,
            MaskedAreas: maskedAreas,
            BlackLevelPlanes: auxBlackLevelPlanes));
    }

    private byte[] BuildPacked16Buffer(byte[][] normalizedPasses, int rows, int width)
    {
        ArgumentNullException.ThrowIfNull(normalizedPasses);

        var rowStrideBytes = checked(width * normalizedPasses.Length * sizeof(ushort));
        var combined = new byte[checked(rowStrideBytes * rows)];

        for (var y = 0; y < rows; y++)
        {
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

    private DngBlackLevelPlane[] BuildBlackLevelPlanes(byte[][] normalizedPasses, int rows, DngRectangle[] maskedAreas)
    {
        var blackLevels = new DngBlackLevelPlane[normalizedPasses.Length];

        for (var channel = 0; channel < normalizedPasses.Length; channel++)
        {
            double evenSum = 0;
            double oddSum = 0;
            long evenCount = 0;
            long oddCount = 0;

            foreach (var area in maskedAreas)
            {
                for (var y = (int)area.Top; y < area.Bottom; y++)
                {
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
