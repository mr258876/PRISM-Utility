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
    void SetImportError(ScanFilmProfileValidationResult validation);
    void DiscardStagedImport();
    Task<ScanFilmProfileApplyResult> ApplyStagedImportAsync(CancellationToken ct);
    Task<ScanFilmProfileLibrarySyncPlan> PlanCalibrationLibrarySyncAsync(CancellationToken ct);
    Task<ScanFilmProfileLibrarySyncResult> SyncCalibrationLibraryAsync(CancellationToken ct);
    Task<ScanFilmProfileLibrarySyncResult> ReplaceCalibrationLibraryAsync(ScanFilmProfileFullReplacementConfirmation? confirmation, CancellationToken ct);
    ScanFilmProfileWorkspaceExportResult BuildExportDocument(ScanChannelCalibrationProfile? selectedChannelPatch = null);
    void MarkExported(ScanFilmParameterProfileSet exportedDocument);
    void ResetToDefaultDraft();
    void SetCurrentDraft(ScanFilmProfileDraft draft);
    ScanFilmProfileReferenceLevelPatchResult TryApplyReferenceLevels(
        ScanFilmProfileWorkspaceSnapshot expected,
        string channelRole,
        ushort? blackLevel,
        ushort? whiteLevel);
}
