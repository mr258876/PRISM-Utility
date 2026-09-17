namespace PRISM_Utility.Core.Models;

public sealed record ScanCalibrationProfileRepositorySnapshot(
    IReadOnlyDictionary<string, ScanChannelCalibrationProfile> Profiles,
    string? SelectedChannel);

public sealed record ScanFilmProfileStagedImport(
    ScanFilmProfileDraft Draft,
    ScanFilmProfileValidationResult Validation);

public enum ScanFilmProfileImportResultState
{
    None,
    ValidStaged,
    Error
}

public sealed record ScanFilmProfileImportResult
{
    private ScanFilmProfileImportResult(
        ScanFilmProfileImportResultState state,
        ScanFilmProfileStagedImport? stagedImport,
        ScanFilmProfileValidationResult validation)
    {
        State = state;
        StagedImport = stagedImport;
        Validation = validation;
    }

    public ScanFilmProfileImportResultState State { get; }

    public ScanFilmProfileStagedImport? StagedImport { get; }

    public ScanFilmProfileValidationResult Validation { get; }

    public static ScanFilmProfileImportResult None { get; } = new(
        ScanFilmProfileImportResultState.None,
        null,
        new ScanFilmProfileValidationResult());

    public static ScanFilmProfileImportResult FromValidStaged(ScanFilmProfileStagedImport stagedImport)
        => new(ScanFilmProfileImportResultState.ValidStaged, stagedImport, stagedImport.Validation);

    public static ScanFilmProfileImportResult FromError(
        ScanFilmProfileValidationResult validation,
        ScanFilmProfileStagedImport? stagedImport = null)
        => new(ScanFilmProfileImportResultState.Error, stagedImport, validation);
}

public sealed record ScanFilmProfileWorkspaceSnapshot
{
    public ScanFilmProfileWorkspaceSnapshot(
        ScanFilmProfileDraft currentDraft,
        ScanFilmProfileDraft baselineDraft,
        bool isDirty,
        ScanFilmProfileStagedImport? stagedImport)
        : this(
            currentDraft,
            baselineDraft,
            isDirty,
            stagedImport is null
                ? ScanFilmProfileImportResult.None
                : ScanFilmProfileImportResult.FromValidStaged(stagedImport))
    {
    }

    public ScanFilmProfileWorkspaceSnapshot(
        ScanFilmProfileDraft currentDraft,
        ScanFilmProfileDraft baselineDraft,
        bool isDirty,
        ScanFilmProfileImportResult importResult)
    {
        CurrentDraft = currentDraft;
        BaselineDraft = baselineDraft;
        IsDirty = isDirty;
        ImportResult = importResult;
    }

    public ScanFilmProfileDraft CurrentDraft { get; }

    public ScanFilmProfileDraft BaselineDraft { get; }

    public bool IsDirty { get; }

    public ScanFilmProfileImportResult ImportResult { get; }

    public ScanFilmProfileStagedImport? StagedImport => ImportResult.StagedImport;
}

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

public sealed record ScanFilmProfileLibrarySyncPlan(
    IReadOnlyDictionary<string, ScanChannelCalibrationProfile> Additions,
    IReadOnlyDictionary<string, ScanChannelCalibrationProfile> Updates,
    IReadOnlyDictionary<string, ScanChannelCalibrationProfile> Removals,
    bool SelectedChannelWillChange)
{
    public bool HasAdditionsOrUpdates => Additions.Count != 0 || Updates.Count != 0;
    public bool HasFullReplacementChanges => HasAdditionsOrUpdates || Removals.Count != 0 || SelectedChannelWillChange;
}

public enum ScanFilmProfileLibrarySyncStatus
{
    Applied,
    NoChanges,
    ConfirmationRequired,
    Canceled,
    Failed,
    Stale
}

public sealed record ScanFilmProfileLibrarySyncResult(
    ScanFilmProfileLibrarySyncStatus Status,
    ScanFilmProfileLibrarySyncPlan? Plan = null,
    Exception? Error = null);

public sealed class ScanFilmProfileFullReplacementConfirmation
{
    private ScanFilmProfileFullReplacementConfirmation()
    {
    }

    public static ScanFilmProfileFullReplacementConfirmation Confirmed { get; } = new();
}

public sealed record ScanFilmProfileWorkspaceExportResult(
    ScanFilmProfileDocumentBuildResult Document,
    bool PatchIncluded);
