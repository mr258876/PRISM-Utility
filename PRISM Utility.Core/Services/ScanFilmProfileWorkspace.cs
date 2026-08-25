using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanFilmProfileWorkspace : IScanFilmProfileWorkspace
{
    private readonly IScanCalibrationProfileRepository _repository;
    private readonly IScanFilmProfileDocumentService _documents;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private ScanFilmProfileWorkspaceSnapshot _snapshot;
    private int _isInitialized;

    public ScanFilmProfileWorkspace(
        IScanCalibrationProfileRepository repository,
        IScanFilmProfileDocumentService documents,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _documents = documents;
        _timeProvider = timeProvider ?? TimeProvider.System;
        var initial = ScanFilmProfileDraft.CreateDefault();
        _snapshot = new ScanFilmProfileWorkspaceSnapshot(initial, initial, false, null);
    }

    public ScanFilmProfileWorkspaceSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public event Action<ScanFilmProfileWorkspaceSnapshot>? SnapshotChanged;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _isInitialized) != 0)
            return;

        await _initializeGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _isInitialized) != 0)
                return;

            await _repository.InitializeAsync(ct);
            var snapshotBeforeRead = Snapshot;
            if (IsPristine(snapshotBeforeRead))
            {
                var persisted = await _repository.ReadAsync(ct);
                var defaultDraft = ScanFilmProfileDraft.CreateDefault();
                var hydrated = new ScanFilmProfileDraft(
                    defaultDraft.ProfileName,
                    defaultDraft.SavedAtUtc,
                    persisted.Profiles,
                    persisted.SelectedChannel,
                    defaultDraft.AcquisitionSettings,
                    defaultDraft.ScanRecipeSettings);
                if (!hydrated.HasSameContentAs(snapshotBeforeRead.CurrentDraft))
                    UpdateIfUnchanged(snapshotBeforeRead, hydrated, hydrated, null);
            }

            Volatile.Write(ref _isInitialized, 1);
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public ScanFilmProfileStageImportResult StageImport(string json)
    {
        var parsed = _documents.Parse(json);
        if (!parsed.CanApply)
            return new ScanFilmProfileStageImportResult(false, parsed.Validation);

        return StageImport(parsed.Document!);
    }

    public ScanFilmProfileStageImportResult StageImport(ScanFilmParameterProfileSet document)
        => StageImport(document, new ScanFilmProfileValidationResult());

    public ScanFilmProfileStageImportResult StageImport(ScanFilmParameterProfileSet document, ScanFilmProfileValidationResult validation)
    {
        if (!validation.IsValid)
            return new ScanFilmProfileStageImportResult(false, validation);

        if (document.SchemaVersion != _documents.CurrentSchemaVersion)
        {
            var schemaValidation = new ScanFilmProfileValidationResult(
            [
                new ScanFilmProfileValidationIssue(
                    ScanFilmProfileValidationCode.UnsupportedSchemaVersion,
                    "schemaVersion",
                    ScanFilmProfileValidationSeverity.Error,
                    "FilmProfile.Validation.SchemaVersionUnsupported")
            ]);
            return new ScanFilmProfileStageImportResult(false, schemaValidation);
        }

        var conversion = ScanFilmProfileDraft.FromDocument(document);
        if (!conversion.Validation.IsValid)
            return new ScanFilmProfileStageImportResult(false, conversion.Validation);

        var validated = _documents.Build(conversion.Draft);
        if (!validated.CanApply)
            return new ScanFilmProfileStageImportResult(false, validated.Validation);

        var normalized = ScanFilmProfileDraft.FromDocument(validated.Document);
        var staged = new ScanFilmProfileStagedImport(normalized.Draft, validated.Validation);
        var snapshot = Snapshot;
        Update(snapshot.CurrentDraft, snapshot.BaselineDraft, staged);
        return new ScanFilmProfileStageImportResult(true, validated.Validation);
    }

    public void DiscardStagedImport()
    {
        var snapshot = Snapshot;
        if (snapshot.StagedImport is not null)
            Update(snapshot.CurrentDraft, snapshot.BaselineDraft, null);
    }

    public async Task<ScanFilmProfileApplyResult> ApplyStagedImportAsync(CancellationToken ct)
    {
        var staged = Snapshot.StagedImport;
        if (staged is null)
            return new ScanFilmProfileApplyResult(ScanFilmProfileApplyStatus.NoStagedImport);

        try
        {
            ct.ThrowIfCancellationRequested();
            await _repository.ReplaceAsync(ToRepositorySnapshot(staged.Draft), ct);
        }
        catch (OperationCanceledException exception)
        {
            return new ScanFilmProfileApplyResult(ScanFilmProfileApplyStatus.Canceled, exception);
        }
        catch (Exception exception)
        {
            return new ScanFilmProfileApplyResult(ScanFilmProfileApplyStatus.Failed, exception);
        }

        Update(staged.Draft, staged.Draft, null);
        return new ScanFilmProfileApplyResult(ScanFilmProfileApplyStatus.Applied);
    }

    public ScanFilmProfileWorkspaceExportResult BuildExportDocument(ScanChannelCalibrationProfile? selectedChannelPatch = null)
    {
        var draft = Snapshot.CurrentDraft;
        var patchIncluded = selectedChannelPatch is not null && !string.IsNullOrWhiteSpace(draft.SelectedCalibrationChannel);
        var profiles = draft.ChannelProfiles;
        if (patchIncluded)
        {
            var patchedProfiles = draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            patchedProfiles[draft.SelectedCalibrationChannel!] = selectedChannelPatch!;
            profiles = patchedProfiles;
        }

        var exportedDraft = new ScanFilmProfileDraft(
            draft.ProfileName,
            _timeProvider.GetUtcNow(),
            profiles,
            draft.SelectedCalibrationChannel,
            draft.AcquisitionSettings,
            draft.ScanRecipeSettings);
        return new ScanFilmProfileWorkspaceExportResult(_documents.Build(exportedDraft), patchIncluded);
    }

    public void MarkExported(ScanFilmParameterProfileSet exportedDocument)
    {
        ArgumentNullException.ThrowIfNull(exportedDocument);
        var exportedDraft = ScanFilmProfileDraft.FromDocument(exportedDocument).Draft;
        Update(exportedDraft, exportedDraft, Snapshot.StagedImport);
    }

    public void ResetToDefaultDraft()
    {
        var initial = ScanFilmProfileDraft.CreateDefault();
        Update(initial, initial, null);
    }

    public void SetCurrentDraft(ScanFilmProfileDraft draft)
    {
        var snapshot = Snapshot;
        Update(draft, snapshot.BaselineDraft, snapshot.StagedImport);
    }

    private static bool IsPristine(ScanFilmProfileWorkspaceSnapshot snapshot)
    {
        var defaultDraft = ScanFilmProfileDraft.CreateDefault();
        return snapshot.StagedImport is null
            && !snapshot.IsDirty
            && snapshot.CurrentDraft.HasSameContentAs(defaultDraft)
            && snapshot.BaselineDraft.HasSameContentAs(defaultDraft);
    }

    private void Update(ScanFilmProfileDraft current, ScanFilmProfileDraft baseline, ScanFilmProfileStagedImport? staged)
    {
        var updated = CreateSnapshot(current, baseline, staged);
        Interlocked.Exchange(ref _snapshot, updated);
        SnapshotChanged?.Invoke(updated);
    }

    private void UpdateIfUnchanged(
        ScanFilmProfileWorkspaceSnapshot expected,
        ScanFilmProfileDraft current,
        ScanFilmProfileDraft baseline,
        ScanFilmProfileStagedImport? staged)
    {
        var updated = CreateSnapshot(current, baseline, staged);
        if (ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, updated, expected), expected))
            SnapshotChanged?.Invoke(updated);
    }

    private static ScanFilmProfileWorkspaceSnapshot CreateSnapshot(
        ScanFilmProfileDraft current,
        ScanFilmProfileDraft baseline,
        ScanFilmProfileStagedImport? staged)
        => new(current, baseline, !current.HasSameContentAs(baseline), staged);

    private static ScanCalibrationProfileRepositorySnapshot ToRepositorySnapshot(ScanFilmProfileDraft draft)
        => new(
            draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            draft.SelectedCalibrationChannel);
}
