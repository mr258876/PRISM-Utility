namespace PRISM_Utility.Core.Models;

public sealed record ScanCalibrationProfileRepositorySnapshot(
    IReadOnlyDictionary<string, ScanChannelCalibrationProfile> Profiles,
    string? SelectedChannel);

public sealed record ScanFilmProfileStagedImport(
    ScanFilmProfileDraft Draft,
    ScanFilmProfileValidationResult Validation);

public sealed record ScanFilmProfileWorkspaceSnapshot(
    ScanFilmProfileDraft CurrentDraft,
    ScanFilmProfileDraft BaselineDraft,
    bool IsDirty,
    ScanFilmProfileStagedImport? StagedImport);

public sealed record ScanFilmProfileStageImportResult(
    bool Staged,
    ScanFilmProfileValidationResult Validation);

public enum ScanFilmProfileApplyStatus
{
    Applied,
    NoStagedImport,
    Canceled,
    Failed
}

public sealed record ScanFilmProfileApplyResult(
    ScanFilmProfileApplyStatus Status,
    Exception? Error = null);

public sealed record ScanFilmProfileWorkspaceExportResult(
    ScanFilmProfileDocumentBuildResult Document,
    bool PatchIncluded);
