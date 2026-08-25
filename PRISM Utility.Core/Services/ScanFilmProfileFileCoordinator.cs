using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanFilmProfileFileCoordinator : IScanFilmProfileFileCoordinator
{
    private readonly IScanFilmProfileFileGateway _gateway;
    private readonly IScanFilmProfileDocumentService _documents;

    public ScanFilmProfileFileCoordinator(IScanFilmProfileFileGateway gateway, IScanFilmProfileDocumentService documents)
    { _gateway = gateway; _documents = documents; }

    public async Task<bool> ExportAsync(ScanFilmParameterProfileSet profileSet, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await _gateway.WriteJsonAsync(ScanFilmProfileFileName.CreateSuggestedName(profileSet.ProfileName), _documents.Serialize(profileSet));
        return !result.WasCanceled;
    }

    public async Task<ScanFilmProfileFileImportResult> ImportAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var file = await _gateway.ReadJsonAsync();
        if (file.WasCanceled)
            return new(true, null, null);
        var parsed = _documents.Parse(file.Text ?? string.Empty);
        return new(false, parsed.CanApply ? parsed.Document : null, parsed.Validation);
    }
}
