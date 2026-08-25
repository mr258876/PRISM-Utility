using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PRISM_Utility.Services;

public sealed class ScanFilmProfileFileGateway : IScanFilmProfileFileGateway
{
    public async Task<ScanFilmProfileFileReadResult> ReadJsonAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        Initialize(picker);
        var file = await picker.PickSingleFileAsync();
        return file is null ? new ScanFilmProfileFileReadResult(true, null) : new ScanFilmProfileFileReadResult(false, await FileIO.ReadTextAsync(file));
    }

    public async Task<ScanFilmProfileFileWriteResult> WriteJsonAsync(string suggestedFileName, string text)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("JSON file", new List<string> { ".json" });
        picker.SuggestedFileName = suggestedFileName;
        Initialize(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return new ScanFilmProfileFileWriteResult(true);
        await FileIO.WriteTextAsync(file, text);
        return new ScanFilmProfileFileWriteResult(false);
    }

    private static void Initialize(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}
