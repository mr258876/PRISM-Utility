using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace PRISM_Utility.Services;

public sealed class ScanChannelImageService : IScanChannelImageService
{
    private readonly IScanImageDecoder _decoder;
    private readonly IScanPreviewPresenter _previewPresenter;

    public ScanChannelImageService(IScanImageDecoder decoder, IScanPreviewPresenter previewPresenter)
    {
        _decoder = decoder;
        _previewPresenter = previewPresenter;
    }

    public bool TryBuildRawPreview(ScanPassCapture capture, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error)
    {
        var buffer = NormalizePassBuffer(capture, false);
        return _previewPresenter.TryRender(buffer, capture.Rows, new ScanPreviewRenderOptions(false, false, false, 1.0), currentBitmap, out bitmap, out error);
    }

    public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error)
    {
        frame = null;
        error = string.Empty;

        if (result.Passes.Count == 0)
        {
            error = "No scan passes are available for RGB composition.";
            return false;
        }

        if (!TryValidateRgbAssignment(assignment, out error))
            return false;

        var passByRole = BuildRoleMap(result, assignment);
        var width = _decoder.GetDecodedPixelsPerLine();
        var rows = result.Rows;
        if (width <= 0 || rows <= 0)
        {
            error = "Scan dimensions are invalid for RGB composition.";
            return false;
        }

        var pixelBytes = new byte[width * rows * 4];
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = GetRoleSample(passByRole, "Red", x, y, rows);
                var green = GetRoleSample(passByRole, "Green", x, y, rows);
                var blue = GetRoleSample(passByRole, "Blue", x, y, rows);

                var pixelIndex = ((y * width) + x) * 4;
                pixelBytes[pixelIndex] = ToByte(blue);
                pixelBytes[pixelIndex + 1] = ToByte(green);
                pixelBytes[pixelIndex + 2] = ToByte(red);
                pixelBytes[pixelIndex + 3] = 255;
            }
        }

        var bitmap = currentBitmap;
        if (bitmap is null || bitmap.PixelWidth != width || bitmap.PixelHeight != rows)
            bitmap = new WriteableBitmap(width, rows);

        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Position = 0;
            stream.Write(pixelBytes, 0, pixelBytes.Length);
        }

        bitmap.Invalidate();
        frame = new ScanCompositeFrame(pixelBytes, width, rows, bitmap);
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

    public async Task<StorageFolder?> PickRawExportFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return await picker.PickSingleFolderAsync();
    }

    public async Task ExportRawChannelsAsync(StorageFolder folder, ScanWorkflowResult result, ScanChannelAssignment assignment)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var roles = assignment.Roles;

        for (var index = 0; index < result.Passes.Count; index++)
        {
            var pass = result.Passes[index];
            var safeRole = SanitizeFileComponent(index < roles.Count ? roles[index] : $"Channel{index + 1}");
            var file = await folder.CreateFileAsync($"scan_{timestamp}_pass{pass.PassIndex}_led{pass.LedChannelIndex + 1}_{safeRole}.bin", CreationCollisionOption.ReplaceExisting);
            var shouldReverse = index < assignment.ReversedFlags.Count && assignment.ReversedFlags[index];
            await FileIO.WriteBytesAsync(file, NormalizePassBuffer(pass, shouldReverse));
        }
    }

    private Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)> BuildRoleMap(ScanWorkflowResult result, ScanChannelAssignment assignment)
    {
        var map = new Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < result.Passes.Count && index < assignment.Roles.Count; index++)
        {
            var role = assignment.Roles[index];
            if (string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!map.ContainsKey(role))
                map[role] = (result.Passes[index], assignment.ReversedFlags[index]);
        }

        return map;
    }

    private ushort GetRoleSample(Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)> passByRole, string role, int x, int y, int rows)
    {
        if (!passByRole.TryGetValue(role, out var entry))
            return 0;

        var sampleY = ResolveSampleRow(entry.Capture, y, rows, entry.ManuallyReversed);
        return _decoder.TryGetSample16(entry.Capture.ImageBytes, rows, x, sampleY, out var sample) ? sample : (ushort)0;
    }

    private static bool TryValidateRgbAssignment(ScanChannelAssignment assignment, out string error)
    {
        var roles = assignment.Roles;
        var rgbRoles = new[] { "Red", "Green", "Blue" };
        foreach (var role in rgbRoles)
        {
            var count = roles.Count(selected => string.Equals(selected, role, StringComparison.OrdinalIgnoreCase));
            if (count == 0)
            {
                error = $"RGB Composite requires exactly one {role} channel assignment.";
                return false;
            }

            if (count > 1)
            {
                error = $"RGB Composite cannot use duplicate {role} channel assignments.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static byte[] NormalizePassBuffer(ScanPassCapture capture, bool manuallyReverse)
    {
        var shouldReverse = !capture.DirectionPositive ^ manuallyReverse;
        if (!shouldReverse)
            return capture.ImageBytes;

        var normalized = new byte[capture.ImageBytes.Length];
        var rowBytes = ScanDebugConstants.BytesPerLine;
        for (var y = 0; y < capture.Rows; y++)
        {
            var sourceOffset = y * rowBytes;
            var destinationOffset = (capture.Rows - 1 - y) * rowBytes;
            System.Buffer.BlockCopy(capture.ImageBytes, sourceOffset, normalized, destinationOffset, rowBytes);
        }

        return normalized;
    }

    private static int ResolveSampleRow(ScanPassCapture capture, int y, int rows, bool manuallyReverse)
    {
        var shouldReverse = !capture.DirectionPositive ^ manuallyReverse;
        return shouldReverse ? (rows - 1 - y) : y;
    }

    private static byte ToByte(ushort sample)
        => (byte)(sample / 256);

    private static string SanitizeFileComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }
}
