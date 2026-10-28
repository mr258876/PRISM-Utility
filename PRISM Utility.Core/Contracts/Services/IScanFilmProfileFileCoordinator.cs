using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public sealed record ScanFilmProfileFileImportResult(bool WasCanceled, ScanFilmParameterProfileSet? Profile, ScanFilmProfileValidationResult? Validation, string? FileName = null);
public sealed record ScanFilmProfileFileExportResult(bool WasCanceled, ScanFilmParameterProfileSet? Profile, string? FileName);

public interface IScanFilmProfileFileCoordinator
{
    Task<ScanFilmProfileFileExportResult> ExportAsync(ScanFilmParameterProfileSet profileSet, CancellationToken ct);
    Task<ScanFilmProfileFileImportResult> ImportAsync(CancellationToken ct);
}
