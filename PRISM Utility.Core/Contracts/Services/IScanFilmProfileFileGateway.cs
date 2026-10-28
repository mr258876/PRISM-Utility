namespace PRISM_Utility.Core.Contracts.Services;

public sealed record ScanFilmProfileFileReadResult(bool WasCanceled, string? Text, string? FileName = null);
public sealed record ScanFilmProfileFileWriteResult(bool WasCanceled, string? FileName = null);

public interface IScanFilmProfileFileGateway
{
    Task<ScanFilmProfileFileReadResult> ReadJsonAsync();
    Task<ScanFilmProfileFileWriteResult> WriteJsonAsync(string suggestedFileName, Func<string, string> createText);

    Task<ScanFilmProfileFileReadResult> ReadDraftAsync()
        => throw new NotSupportedException("This file gateway does not support editing drafts.");

    Task<ScanFilmProfileFileWriteResult> WriteDraftAsync(string suggestedFileName, Func<string, string> createText)
        => throw new NotSupportedException("This file gateway does not support editing drafts.");
}
