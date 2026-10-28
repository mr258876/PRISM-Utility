using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanFilmProfileFileCoordinator : IScanFilmProfileFileCoordinator
{
    private readonly IScanFilmProfileFileGateway _gateway;
    private readonly IScanFilmProfileDocumentService _documents;

    public ScanFilmProfileFileCoordinator(IScanFilmProfileFileGateway gateway, IScanFilmProfileDocumentService documents)
    { _gateway = gateway; _documents = documents; }

    public async Task<ScanFilmProfileFileExportResult> ExportAsync(ScanFilmParameterProfileSet profileSet, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ScanFilmParameterProfileSet? exportedProfile = null;
        var result = await _gateway.WriteJsonAsync(ScanFilmProfileFileName.CreateSuggestedName(profileSet.ProfileName), fileName =>
        {
            exportedProfile = profileSet with { ProfileName = ScanFilmProfileFileName.FromFileName(fileName) ?? profileSet.ProfileName };
            return _documents.Serialize(exportedProfile);
        });
        if (result.WasCanceled)
            return new(true, null, null);
        return new(false, exportedProfile ?? throw new InvalidOperationException("The file gateway did not serialize the selected configuration file."), result.FileName);
    }

    public async Task<ScanFilmProfileFileImportResult> ImportAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var file = await _gateway.ReadJsonAsync();
        if (file.WasCanceled)
            return new(true, null, null);
        var parsed = _documents.Parse(file.Text ?? string.Empty);
        var profile = parsed.CanApply ? parsed.Document : null;
        if (profile is not null && ScanFilmProfileFileName.FromFileName(file.FileName) is { } name)
            profile = profile with { ProfileName = name };
        return new(false, profile, parsed.Validation, file.FileName);
    }
}
