using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PRISM_Utility.Services;

public sealed class ScanFilmProfileFileGateway : IScanFilmProfileFileGateway
{
    public async Task<ScanFilmProfileFileReadResult> ReadJsonAsync()
        => await ReadAsync(".json");

    public async Task<ScanFilmProfileFileReadResult> ReadDraftAsync()
        => await ReadAsync(ScanFilmProfileDraftFileService.FileExtension);

    private static async Task<ScanFilmProfileFileReadResult> ReadAsync(string extension)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(extension);
        Initialize(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return new ScanFilmProfileFileReadResult(true, null);
        if (extension == ScanFilmProfileDraftFileService.FileExtension
            && (await file.GetBasicPropertiesAsync()).Size > ScanFilmProfileDraftFileService.MaximumFileBytes)
            throw new InvalidDataException("The editing draft exceeds the maximum file size.");
        return new ScanFilmProfileFileReadResult(false, await FileIO.ReadTextAsync(file), file.Name);
    }

    public async Task<ScanFilmProfileFileWriteResult> WriteJsonAsync(string suggestedFileName, Func<string, string> createText)
        => await WriteAsync(suggestedFileName, createText, "JSON file", ".json");

    public async Task<ScanFilmProfileFileWriteResult> WriteDraftAsync(string suggestedFileName, Func<string, string> createText)
        => await WriteAsync(suggestedFileName, createText, "PRISM editing draft", ScanFilmProfileDraftFileService.FileExtension);

    private static async Task<ScanFilmProfileFileWriteResult> WriteAsync(
        string suggestedFileName, Func<string, string> createText, string fileTypeName, string extension)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add(fileTypeName, new List<string> { extension });
        picker.SuggestedFileName = suggestedFileName;
        Initialize(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return new ScanFilmProfileFileWriteResult(true);
        await FileIO.WriteTextAsync(file, createText(file.Name));
        return new ScanFilmProfileFileWriteResult(false, file.Name);
    }

    private static void Initialize(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}
