using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public sealed record ScanFilmProfileFileImportResult(bool WasCanceled, ScanFilmParameterProfileSet? Profile, ScanFilmProfileValidationResult? Validation);

public interface IScanFilmProfileFileCoordinator
{
    Task<bool> ExportAsync(ScanFilmParameterProfileSet profileSet, CancellationToken ct);
    Task<ScanFilmProfileFileImportResult> ImportAsync(CancellationToken ct);
}
