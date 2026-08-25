namespace PRISM_Utility.Core.Contracts.Services;

public sealed record ScanFilmProfileFileReadResult(bool WasCanceled, string? Text);
public sealed record ScanFilmProfileFileWriteResult(bool WasCanceled);

public interface IScanFilmProfileFileGateway
{
    Task<ScanFilmProfileFileReadResult> ReadJsonAsync();
    Task<ScanFilmProfileFileWriteResult> WriteJsonAsync(string suggestedFileName, string text);
}
