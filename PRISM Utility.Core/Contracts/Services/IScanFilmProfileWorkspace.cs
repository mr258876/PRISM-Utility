using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanFilmProfileWorkspace
{
    ScanFilmProfileWorkspaceSnapshot Snapshot { get; }
    event Action<ScanFilmProfileWorkspaceSnapshot>? SnapshotChanged;

    Task InitializeAsync(CancellationToken ct);
    ScanFilmProfileStageImportResult StageImport(string json);
    ScanFilmProfileStageImportResult StageImport(ScanFilmParameterProfileSet document);
    ScanFilmProfileStageImportResult StageImport(ScanFilmParameterProfileSet document, ScanFilmProfileValidationResult validation);
    void DiscardStagedImport();
    Task<ScanFilmProfileApplyResult> ApplyStagedImportAsync(CancellationToken ct);
    ScanFilmProfileWorkspaceExportResult BuildExportDocument(ScanChannelCalibrationProfile? selectedChannelPatch = null);
    void MarkExported(ScanFilmParameterProfileSet exportedDocument);
    void ResetToDefaultDraft();
    void SetCurrentDraft(ScanFilmProfileDraft draft);
}
