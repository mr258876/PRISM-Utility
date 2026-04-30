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
    private readonly IScanCompositeImageProcessor _processor;
    private readonly IScanPreviewPresenter _previewPresenter;

    public ScanChannelImageService(IScanCompositeImageProcessor processor, IScanPreviewPresenter previewPresenter)
    {
        _processor = processor;
        _previewPresenter = previewPresenter;
    }

    public bool TryBuildRawPreview(ScanPassCapture capture, bool manuallyReverse, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error)
    {
        var buffer = _processor.NormalizePassBuffer(capture, manuallyReverse);
        return _previewPresenter.TryRender(buffer, capture.Rows, new ScanPreviewRenderOptions(false, false, false, 1.0), currentBitmap, out bitmap, out error);
    }

    public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error)
    {
        frame = null;
        if (!_processor.TryBuildRgbComposite(result, assignment, colorManagement, out var buffer, out error) || buffer is null)
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
            await FileIO.WriteBytesAsync(file, _processor.NormalizePassBuffer(pass, shouldReverse));
        }
    }

    private static string SanitizeFileComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }
}
