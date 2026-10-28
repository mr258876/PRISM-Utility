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
    private ScanFilmProfileDraft? _lastSavedDraft;
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
        _snapshot = new ScanFilmProfileWorkspaceSnapshot(initial, initial, false, ScanFilmProfileImportResult.None);
    }

    public ScanFilmProfileWorkspaceSnapshot Snapshot => Volatile.Read(ref _snapshot);

    // A clean baseline can come from the local channel library. Only a file that was
    // successfully saved or opened is eligible for the scan-page handoff.
    public ScanFilmParameterProfileSet? LastSavedDocument
        => Volatile.Read(ref _lastSavedDraft) is { } saved ? _documents.Build(saved).Document : null;

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
                    defaultDraft.AcquisitionSettings! with { WhiteChannelSettings = persisted.WhiteChannelSettings },
                    defaultDraft.ScanRecipeSettings);
                if (!hydrated.HasSameContentAs(snapshotBeforeRead.CurrentDraft))
                    UpdateIfUnchanged(snapshotBeforeRead, hydrated, hydrated, ScanFilmProfileImportResult.None);
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
        {
            SetImportError(parsed.Validation);
            return new ScanFilmProfileStageImportResult(false, parsed.Validation);
        }

        return StageImport(parsed.Document!);
    }

    public ScanFilmProfileStageImportResult StageImport(ScanFilmParameterProfileSet document)
        => StageImport(document, new ScanFilmProfileValidationResult());

    public ScanFilmProfileStageImportResult StageImport(ScanFilmParameterProfileSet document, ScanFilmProfileValidationResult validation)
    {
        if (!validation.IsValid)
        {
            SetImportError(validation);
            return new ScanFilmProfileStageImportResult(false, validation);
        }

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
            SetImportError(schemaValidation);
            return new ScanFilmProfileStageImportResult(false, schemaValidation);
        }

        var conversion = ScanFilmProfileDraft.FromDocument(document);
        if (!conversion.Validation.IsValid)
        {
            SetImportError(conversion.Validation);
            return new ScanFilmProfileStageImportResult(false, conversion.Validation);
        }

        var validated = _documents.Build(conversion.Draft);
        if (!validated.CanApply)
        {
            SetImportError(validated.Validation);
            return new ScanFilmProfileStageImportResult(false, validated.Validation);
        }

        var normalized = ScanFilmProfileDraft.FromDocument(validated.Document);
        var staged = new ScanFilmProfileStagedImport(normalized.Draft, validated.Validation);
        var snapshot = Snapshot;
        Update(snapshot.CurrentDraft, snapshot.BaselineDraft, ScanFilmProfileImportResult.FromValidStaged(staged));
        return new ScanFilmProfileStageImportResult(true, validated.Validation);
    }

    public void DiscardStagedImport()
    {
        var snapshot = Snapshot;
        if (snapshot.ImportResult.State != ScanFilmProfileImportResultState.None)
            Update(snapshot.CurrentDraft, snapshot.BaselineDraft, ScanFilmProfileImportResult.None);
    }

    public Task<ScanFilmProfileApplyResult> ApplyStagedImportAsync(CancellationToken ct)
    {
        var staged = Snapshot.StagedImport;
        if (staged is null)
            return Task.FromResult(new ScanFilmProfileApplyResult(ScanFilmProfileApplyStatus.NoStagedImport));

        if (ct.IsCancellationRequested)
            return Task.FromResult(new ScanFilmProfileApplyResult(
                ScanFilmProfileApplyStatus.Canceled,
                new OperationCanceledException(ct)));

        Volatile.Write(ref _lastSavedDraft, staged.Draft);
        Update(staged.Draft, staged.Draft, ScanFilmProfileImportResult.None);
        return Task.FromResult(new ScanFilmProfileApplyResult(ScanFilmProfileApplyStatus.Applied));
    }

    public async Task<ScanFilmProfileLibrarySyncPlan> PlanCalibrationLibrarySyncAsync(CancellationToken ct)
    {
        var source = Snapshot.CurrentDraft;
        var repository = await _repository.ReadAsync(ct);
        return CreateLibrarySyncPlan(source, repository);
    }

    public Task<ScanFilmProfileLibrarySyncResult> SyncCalibrationLibraryAsync(CancellationToken ct)
        => SyncCalibrationLibraryAsync(fullReplacement: false, ct);

    public Task<ScanFilmProfileLibrarySyncResult> ReplaceCalibrationLibraryAsync(
        ScanFilmProfileFullReplacementConfirmation? confirmation,
        CancellationToken ct)
        => confirmation is null
            ? Task.FromResult(new ScanFilmProfileLibrarySyncResult(ScanFilmProfileLibrarySyncStatus.ConfirmationRequired))
            : SyncCalibrationLibraryAsync(fullReplacement: true, ct);

    private async Task<ScanFilmProfileLibrarySyncResult> SyncCalibrationLibraryAsync(bool fullReplacement, CancellationToken ct)
    {
        var sourceSnapshot = Snapshot;
        try
        {
            ct.ThrowIfCancellationRequested();
            var repository = await _repository.ReadAsync(ct);
            ct.ThrowIfCancellationRequested();
            var plan = CreateLibrarySyncPlan(sourceSnapshot.CurrentDraft, repository);
            if (!ReferenceEquals(sourceSnapshot, Snapshot))
                return new ScanFilmProfileLibrarySyncResult(ScanFilmProfileLibrarySyncStatus.Stale, plan);

            if (fullReplacement ? !plan.HasFullReplacementChanges : !plan.HasAdditionsOrUpdates)
                return new ScanFilmProfileLibrarySyncResult(ScanFilmProfileLibrarySyncStatus.NoChanges, plan);

            var replacement = fullReplacement
                ? ToRepositorySnapshot(sourceSnapshot.CurrentDraft)
                : ApplyAdditionsAndUpdates(repository, plan);
            await _repository.ReplaceAsync(replacement, ct);
            return new ScanFilmProfileLibrarySyncResult(ScanFilmProfileLibrarySyncStatus.Applied, plan);
        }
        catch (OperationCanceledException exception)
        {
            return new ScanFilmProfileLibrarySyncResult(ScanFilmProfileLibrarySyncStatus.Canceled, Error: exception);
        }
        catch (IOException exception)
        {
            return new ScanFilmProfileLibrarySyncResult(ScanFilmProfileLibrarySyncStatus.Failed, Error: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new ScanFilmProfileLibrarySyncResult(ScanFilmProfileLibrarySyncStatus.Failed, Error: exception);
        }
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
        Volatile.Write(ref _lastSavedDraft, exportedDraft);
        Update(exportedDraft, exportedDraft, Snapshot.ImportResult);
    }

    public void MarkEditingDraftSaved(ScanFilmProfileDraft savedDraft)
    {
        ArgumentNullException.ThrowIfNull(savedDraft);
        var snapshot = Snapshot;
        // The file contains the values captured before writing. Edits made while
        // the picker or write was pending must remain visible and remain dirty.
        Update(snapshot.CurrentDraft, savedDraft, snapshot.ImportResult);
    }

    public void RestoreEditingDraft(ScanFilmProfileDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        // Opening an editing file must not carry a previous runnable file into
        // the scan-page handoff, even when the editing file has usable values.
        Volatile.Write(ref _lastSavedDraft, null);
        Update(draft, draft, ScanFilmProfileImportResult.None);
    }

    public void ResetToDefaultDraft()
    {
        var initial = ScanFilmProfileDraft.CreateDefault();
        Volatile.Write(ref _lastSavedDraft, null);
        Update(initial, initial, ScanFilmProfileImportResult.None);
    }

    public void SetCurrentDraft(ScanFilmProfileDraft draft)
    {
        var snapshot = Snapshot;
        Update(draft, snapshot.BaselineDraft, snapshot.ImportResult);
    }

    public ScanFilmProfileReferenceLevelPatchResult TryApplyReferenceLevels(
        ScanFilmProfileWorkspaceSnapshot expected,
        string channelRole,
        ushort? blackLevel,
        ushort? whiteLevel)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!ReferenceEquals(Snapshot, expected))
            return new(ScanFilmProfileReferenceLevelPatchStatus.Stale);

        var draft = expected.CurrentDraft;
        if (string.IsNullOrWhiteSpace(channelRole) || !draft.ChannelProfiles.TryGetValue(channelRole, out var profile))
            return new(ScanFilmProfileReferenceLevelPatchStatus.MissingTarget);

        if (whiteLevel is 0 || (blackLevel.HasValue && whiteLevel.HasValue && blackLevel >= whiteLevel))
            return new(ScanFilmProfileReferenceLevelPatchStatus.InvalidValues);

        var profiles = draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        profiles[channelRole] = profile with { BlackLevel = blackLevel, WhiteLevel = whiteLevel };
        var updatedDraft = new ScanFilmProfileDraft(
            draft.ProfileName,
            draft.SavedAtUtc,
            profiles,
            draft.SelectedCalibrationChannel,
            draft.AcquisitionSettings,
            draft.ScanRecipeSettings);
        var updated = CreateSnapshot(updatedDraft, expected.BaselineDraft, expected.ImportResult);
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, updated, expected), expected))
            return new(ScanFilmProfileReferenceLevelPatchStatus.Stale);

        SnapshotChanged?.Invoke(updated);
        return new(ScanFilmProfileReferenceLevelPatchStatus.Applied, updated);
    }

    private static bool IsPristine(ScanFilmProfileWorkspaceSnapshot snapshot)
    {
        var defaultDraft = ScanFilmProfileDraft.CreateDefault();
        return snapshot.ImportResult.State == ScanFilmProfileImportResultState.None
            && !snapshot.IsDirty
            && snapshot.CurrentDraft.HasSameContentAs(defaultDraft)
            && snapshot.BaselineDraft.HasSameContentAs(defaultDraft);
    }

    public void SetImportError(ScanFilmProfileValidationResult validation)
    {
        var snapshot = Snapshot;
        Update(
            snapshot.CurrentDraft,
            snapshot.BaselineDraft,
            ScanFilmProfileImportResult.FromError(validation, snapshot.StagedImport));
    }

    private void Update(ScanFilmProfileDraft current, ScanFilmProfileDraft baseline, ScanFilmProfileImportResult importResult)
    {
        var updated = CreateSnapshot(current, baseline, importResult);
        Interlocked.Exchange(ref _snapshot, updated);
        SnapshotChanged?.Invoke(updated);
    }

    private void UpdateIfUnchanged(
        ScanFilmProfileWorkspaceSnapshot expected,
        ScanFilmProfileDraft current,
        ScanFilmProfileDraft baseline,
        ScanFilmProfileImportResult importResult)
    {
        var updated = CreateSnapshot(current, baseline, importResult);
        if (ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, updated, expected), expected))
            SnapshotChanged?.Invoke(updated);
    }

    private static ScanFilmProfileWorkspaceSnapshot CreateSnapshot(
        ScanFilmProfileDraft current,
        ScanFilmProfileDraft baseline,
        ScanFilmProfileImportResult importResult)
        => new(current, baseline, !current.HasSameContentAs(baseline), importResult);

    private static ScanCalibrationProfileRepositorySnapshot ToRepositorySnapshot(ScanFilmProfileDraft draft)
        => new(
            draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            draft.SelectedCalibrationChannel,
            GetPersistedWhiteSettings(draft));

    private static ScanFilmProfileLibrarySyncPlan CreateLibrarySyncPlan(
        ScanFilmProfileDraft draft,
        ScanCalibrationProfileRepositorySnapshot repository)
    {
        var additions = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        var updates = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        var removals = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in draft.ChannelProfiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!repository.Profiles.TryGetValue(profile.Key, out var existing))
                additions.Add(profile.Key, profile.Value);
            else if (!EqualityComparer<ScanChannelCalibrationProfile>.Default.Equals(profile.Value, existing))
                updates.Add(profile.Key, profile.Value);
        }

        foreach (var profile in repository.Profiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!draft.ChannelProfiles.ContainsKey(profile.Key))
                removals.Add(profile.Key, profile.Value);
        }

        var whiteSettings = GetPersistedWhiteSettings(draft);
        return new ScanFilmProfileLibrarySyncPlan(
            additions,
            updates,
            removals,
            !string.Equals(draft.SelectedCalibrationChannel, repository.SelectedChannel, StringComparison.OrdinalIgnoreCase),
            whiteSettings,
            whiteSettings is not null && whiteSettings != (repository.WhiteChannelSettings ?? WhiteChannelSettings.FromLegacy(repository.Profiles.GetValueOrDefault("White"))));
    }

    private static WhiteChannelSettings? GetPersistedWhiteSettings(ScanFilmProfileDraft draft)
        => draft.AcquisitionSettings?.WhiteChannelSettings is not null || draft.ChannelProfiles.ContainsKey("White")
            ? draft.WhiteChannelSettings : null;

    private static ScanCalibrationProfileRepositorySnapshot ApplyAdditionsAndUpdates(
        ScanCalibrationProfileRepositorySnapshot repository,
        ScanFilmProfileLibrarySyncPlan plan)
    {
        var profiles = repository.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var profile in plan.Additions)
            profiles.Add(profile.Key, profile.Value);
        foreach (var profile in plan.Updates)
            profiles[profile.Key] = profile.Value;

        var white = repository.WhiteChannelSettings;
        if (plan.WhiteChannelSettingsWillChange && plan.WhiteChannelSettings is { } incoming)
        {
            white = incoming with
            {
                DedicatedLed = MergeSource(incoming.DedicatedLed, white?.DedicatedLed),
                RgbComposite = MergeSource(incoming.RgbComposite, white?.RgbComposite)
            };
            if (white.ActiveSettings?.Profile is { } activeProfile)
                profiles["White"] = activeProfile;
            else
                profiles.Remove("White");
        }
        return new ScanCalibrationProfileRepositorySnapshot(profiles, repository.SelectedChannel, white);
    }

    private static WhiteChannelSourceSettings? MergeSource(WhiteChannelSourceSettings? incoming, WhiteChannelSourceSettings? existing)
        => incoming?.Profile is not null ? incoming : existing ?? incoming;
}
