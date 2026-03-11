using PRISM_Utility.Contracts.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PRISM_Utility.Services;

public sealed class ScanBufferExportService : IScanBufferExportService
{
    public string BuildExportBufferFileName(string selectedRows, int bufferLength, DateTimeOffset timestamp)
    {
        var rowsText = int.TryParse(selectedRows, out var rows) ? rows.ToString() : "unknown";
        return $"scan_{timestamp:yyyyMMdd_HHmmss}_rows{rowsText}_bytes{bufferLength}";
    }

    public async Task<StorageFile?> PickExportFileAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Binary file", new List<string> { ".bin" });
        picker.SuggestedFileName = suggestedFileName;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return await picker.PickSaveFileAsync();
    }

    public Task WriteBufferAsync(StorageFile file, byte[] buffer)
        => FileIO.WriteBytesAsync(file, buffer).AsTask();
}
