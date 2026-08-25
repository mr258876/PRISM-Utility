using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileWorkspaceTests
{
    [Fact]
    public void DocumentService_FullV5Fixture_BuildsApplicableDocument()
    {
        IScanFilmProfileDocumentService service = new ScanFilmProfileDocumentService();
        var parsed = service.Parse(ReadFixture("full-v5.json"));
        var draft = ScanFilmProfileDraft.FromDocument(Assert.IsType<ScanFilmParameterProfileSet>(parsed.Document)).Draft;

        var built = service.Build(draft);

        Assert.True(parsed.CanApply);
        Assert.True(built.CanApply);
        Assert.Equal(5, built.Document?.SchemaVersion);
    }

    [Fact]
    public void StageAndDiscard_CauseZeroRepositoryWritesAndPreserveCurrentDraft()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = CreateWorkspace(repository);
        var snapshots = new List<ScanFilmProfileWorkspaceSnapshot>();
        workspace.SnapshotChanged += snapshots.Add;
        var initialDraft = workspace.Snapshot.CurrentDraft;

        var staged = workspace.StageImport(ReadFixture("full-v5.json"));
        workspace.DiscardStagedImport();

        Assert.True(staged.Staged);
        Assert.Equal(0, repository.ReplaceCount);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.True(workspace.Snapshot.CurrentDraft.HasSameContentAs(initialDraft));
        Assert.False(workspace.Snapshot.IsDirty);
        Assert.Equal(2, snapshots.Count);
    }

    [Fact]
    public async Task ApplyStagedImportAsync_ReplacesProfilesAndSelectionExactlyOnce()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = CreateWorkspace(repository);
        workspace.StageImport(ReadFixture("full-v5.json"));

        var result = await workspace.ApplyStagedImportAsync(CancellationToken.None);

        var replacement = Assert.IsType<ScanCalibrationProfileRepositorySnapshot>(repository.LastReplacement);
        Assert.Equal(ScanFilmProfileApplyStatus.Applied, result.Status);
        Assert.Equal(1, repository.ReplaceCount);
        Assert.Equal("Green", replacement.SelectedChannel);
        Assert.Equal(new[] { "Blue", "Green" }, replacement.Profiles.Keys);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.False(workspace.Snapshot.IsDirty);
    }

    [Fact]
    public async Task FailedAndCanceledApply_PreserveStagingAndCurrentState()
    {
        var failingRepository = new RecordingCalibrationProfileRepository { ReplaceFailure = new InvalidOperationException("replace failed") };
        var failedWorkspace = CreateWorkspace(failingRepository);
        failedWorkspace.StageImport(ReadFixture("full-v5.json"));

        var failed = await failedWorkspace.ApplyStagedImportAsync(CancellationToken.None);

        var canceledRepository = new RecordingCalibrationProfileRepository();
        var canceledWorkspace = CreateWorkspace(canceledRepository);
        canceledWorkspace.StageImport(ReadFixture("full-v5.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = await canceledWorkspace.ApplyStagedImportAsync(cancellation.Token);

        Assert.Equal(ScanFilmProfileApplyStatus.Failed, failed.Status);
        Assert.NotNull(failedWorkspace.Snapshot.StagedImport);
        Assert.Equal(1, failingRepository.ReplaceCount);
        Assert.Equal(ScanFilmProfileApplyStatus.Canceled, canceled.Status);
        Assert.NotNull(canceledWorkspace.Snapshot.StagedImport);
        Assert.Equal(0, canceledRepository.ReplaceCount);
    }

    [Fact]
    public void DefaultDraft_UsesUnixEpochAsItsUnsavedSentinel()
        => Assert.Equal(DateTimeOffset.UnixEpoch, ScanFilmProfileDraft.CreateDefault().SavedAtUtc);

    [Fact]
    public async Task InitializeAsync_PristineWorkspaceHydratesPersistedProfilesAndSelectionAsCleanBaseline()
    {
        var repository = new RecordingCalibrationProfileRepository { PersistedSnapshot = CreatePersistedSnapshot() };
        var workspace = CreateWorkspace(repository);
        var notifications = 0;
        workspace.SnapshotChanged += _ => notifications++;

        await workspace.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, repository.InitializeCount);
        Assert.Equal(1, repository.ReadCount);
        Assert.Equal(CreatePersistedSnapshot().Profiles, workspace.Snapshot.CurrentDraft.ChannelProfiles);
        Assert.Equal("Blue", workspace.Snapshot.CurrentDraft.SelectedCalibrationChannel);
        Assert.True(workspace.Snapshot.CurrentDraft.HasSameContentAs(workspace.Snapshot.BaselineDraft));
        Assert.False(workspace.Snapshot.IsDirty);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task InitializeAsync_CapturedScanDebugDraftBeforeEditorNavigationRemainsExact()
    {
        var repository = new RecordingCalibrationProfileRepository { PersistedSnapshot = CreatePersistedSnapshot() };
        var workspace = CreateWorkspace(repository);
        workspace.SetCurrentDraft(CreateCapturedDraft());
        var before = workspace.Snapshot;

        await workspace.InitializeAsync(CancellationToken.None);

        AssertSnapshotEquals(before, workspace.Snapshot);
    }

    [Fact]
    public async Task InitializeAsync_DirtyDraftPreservesCurrentDraftAndBaseline()
    {
        var repository = new RecordingCalibrationProfileRepository { PersistedSnapshot = CreatePersistedSnapshot() };
        var documents = new ScanFilmProfileDocumentService();
        var workspace = CreateWorkspace(repository, documents: documents);
        workspace.StageImport(ReadFixture("full-v5.json"));
        await workspace.ApplyStagedImportAsync(CancellationToken.None);
        var clean = workspace.Snapshot.CurrentDraft;
        workspace.SetCurrentDraft(new ScanFilmProfileDraft(
            "Edited capture",
            clean.SavedAtUtc,
            clean.ChannelProfiles,
            clean.SelectedCalibrationChannel,
            clean.AcquisitionSettings,
            clean.ScanRecipeSettings));
        var before = workspace.Snapshot;

        await workspace.InitializeAsync(CancellationToken.None);

        Assert.True(before.IsDirty);
        AssertSnapshotEquals(before, workspace.Snapshot);
    }

    [Fact]
    public async Task InitializeAsync_StagedImportBeforeInitializationRemainsExact()
    {
        var repository = new RecordingCalibrationProfileRepository { PersistedSnapshot = CreatePersistedSnapshot() };
        var workspace = CreateWorkspace(repository);
        workspace.StageImport(ReadFixture("full-v5.json"));
        var before = workspace.Snapshot;

        await workspace.InitializeAsync(CancellationToken.None);

        Assert.NotNull(before.StagedImport);
        AssertSnapshotEquals(before, workspace.Snapshot);
    }

    [Fact]
    public async Task InitializeAsync_EmptyRepositoryKeepsDefaultDraft()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = CreateWorkspace(repository);
        var initial = workspace.Snapshot;

        await workspace.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, repository.InitializeCount);
        Assert.Equal(1, repository.ReadCount);
        AssertSnapshotEquals(initial, workspace.Snapshot);
    }

    [Fact]
    public async Task InitializeAsync_RepeatedCallDoesNotOverwritePostInitializationDraftOrRaiseAnotherEvent()
    {
        var repository = new RecordingCalibrationProfileRepository { PersistedSnapshot = CreatePersistedSnapshot() };
        var workspace = CreateWorkspace(repository);
        var notifications = 0;
        workspace.SnapshotChanged += _ => notifications++;

        await workspace.InitializeAsync(CancellationToken.None);
        workspace.SetCurrentDraft(CreateCapturedDraft());
        var beforeSecondInitialization = workspace.Snapshot;
        await workspace.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, repository.InitializeCount);
        Assert.Equal(1, repository.ReadCount);
        Assert.Equal(2, notifications);
        AssertSnapshotEquals(beforeSecondInitialization, workspace.Snapshot);
    }

    [Fact]
    public async Task InitializeAsync_ConcurrentCallsInitializeRepositoryAndNotifyOnce()
    {
        var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingCalibrationProfileRepository
        {
            PersistedSnapshot = CreatePersistedSnapshot(),
            InitializeEntered = entered,
            InitializeRelease = release
        };
        var workspace = CreateWorkspace(repository);
        var notifications = 0;
        workspace.SnapshotChanged += _ => Interlocked.Increment(ref notifications);

        var first = workspace.InitializeAsync(CancellationToken.None);
        await entered.Task;
        var concurrent = Enumerable.Range(0, 8)
            .Select(_ => workspace.InitializeAsync(CancellationToken.None))
            .ToArray();
        release.SetResult(null);
        await Task.WhenAll(concurrent.Append(first));

        Assert.Equal(1, repository.InitializeCount);
        Assert.Equal(1, repository.ReadCount);
        Assert.Equal(1, notifications);
        Assert.Equal("Blue", workspace.Snapshot.CurrentDraft.SelectedCalibrationChannel);
        Assert.True(workspace.Snapshot.CurrentDraft.HasSameContentAs(workspace.Snapshot.BaselineDraft));
        Assert.False(workspace.Snapshot.IsDirty);
    }

    [Fact]
    public async Task InitializeAsync_DraftMutationDuringReadPreservesExactSnapshotAndEvent()
    {
        var readEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingCalibrationProfileRepository
        {
            PersistedSnapshot = CreatePersistedSnapshot(),
            ReadEntered = readEntered,
            ReadRelease = readRelease
        };
        var workspace = CreateWorkspace(repository);
        var snapshots = new List<ScanFilmProfileWorkspaceSnapshot>();
        workspace.SnapshotChanged += snapshots.Add;

        var initialization = workspace.InitializeAsync(CancellationToken.None);
        await readEntered.Task;
        workspace.SetCurrentDraft(CreateCapturedDraft());
        var expected = workspace.Snapshot;
        readRelease.SetResult(null);
        await initialization;

        Assert.Equal(1, repository.ReadCount);
        Assert.Single(snapshots);
        AssertSnapshotEquals(expected, snapshots[0]);
        AssertSnapshotEquals(expected, workspace.Snapshot);
    }

    [Fact]
    public async Task InitializeAsync_StagedImportDuringReadPreservesExactSnapshotAndEvent()
    {
        var readEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingCalibrationProfileRepository
        {
            PersistedSnapshot = CreatePersistedSnapshot(),
            ReadEntered = readEntered,
            ReadRelease = readRelease
        };
        var workspace = CreateWorkspace(repository);
        var snapshots = new List<ScanFilmProfileWorkspaceSnapshot>();
        workspace.SnapshotChanged += snapshots.Add;

        var initialization = workspace.InitializeAsync(CancellationToken.None);
        await readEntered.Task;
        var staged = workspace.StageImport(ReadFixture("full-v5.json"));
        var expected = workspace.Snapshot;
        readRelease.SetResult(null);
        await initialization;

        Assert.True(staged.Staged);
        Assert.Equal(1, repository.ReadCount);
        Assert.Single(snapshots);
        AssertSnapshotEquals(expected, snapshots[0]);
        AssertSnapshotEquals(expected, workspace.Snapshot);
    }

    [Fact]
    public void BuildExportDocument_UsesClockAndLeavesWorkspaceStateUnchangedBeforeFileSuccess()
    {
        var documents = new ScanFilmProfileDocumentService();
        var exportedAt = new DateTimeOffset(2026, 7, 17, 12, 34, 56, TimeSpan.Zero);
        var workspace = CreateWorkspace(new RecordingCalibrationProfileRepository(), new FixedTimeProvider(exportedAt), documents);
        var source = ScanFilmProfileDraft.FromDocument(Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document)).Draft;
        var historical = new DateTimeOffset(2021, 5, 4, 3, 2, 1, TimeSpan.Zero);
        workspace.SetCurrentDraft(WithSavedAt(source, historical));
        var before = workspace.Snapshot;

        var export = workspace.BuildExportDocument();
        var document = Assert.IsType<ScanFilmParameterProfileSet>(export.Document.Document);
        var serialized = documents.Serialize(document);

        Assert.True(export.Document.CanApply);
        Assert.Equal(exportedAt, document.SavedAtUtc);
        Assert.Matches("\\\"SavedAtUtc\\\":\\\"2026-07-17T12:34:56(?:\\.0{1,7})?(?:Z|\\+00:00)\\\"", serialized);
        Assert.True(workspace.Snapshot.CurrentDraft.HasSameContentAs(before.CurrentDraft));
        Assert.True(workspace.Snapshot.BaselineDraft.HasSameContentAs(before.BaselineDraft));
        Assert.Equal(before.IsDirty, workspace.Snapshot.IsDirty);
        Assert.Equal(historical, workspace.Snapshot.CurrentDraft.SavedAtUtc);
    }

    [Fact]
    public async Task NewAndImportedReexports_RefreshOnlyTheExportedDocumentTimestamp()
    {
        var documents = new ScanFilmProfileDocumentService();
        var firstExportedAt = new DateTimeOffset(2026, 7, 17, 13, 0, 0, TimeSpan.Zero);
        var secondExportedAt = firstExportedAt.AddMinutes(1);
        var clock = new FixedTimeProvider(firstExportedAt);
        var source = ScanFilmProfileDraft.FromDocument(Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document)).Draft;
        var newWorkspace = CreateWorkspace(new RecordingCalibrationProfileRepository(), clock, documents);
        newWorkspace.SetCurrentDraft(WithSavedAt(source, DateTimeOffset.UnixEpoch));

        var newExport = Assert.IsType<ScanFilmParameterProfileSet>(newWorkspace.BuildExportDocument().Document.Document);

        var importedDocument = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var importedWorkspace = CreateWorkspace(new RecordingCalibrationProfileRepository(), clock, documents);
        importedWorkspace.StageImport(importedDocument);
        await importedWorkspace.ApplyStagedImportAsync(CancellationToken.None);
        var importedTimestamp = importedWorkspace.Snapshot.CurrentDraft.SavedAtUtc;
        var firstExport = Assert.IsType<ScanFilmParameterProfileSet>(importedWorkspace.BuildExportDocument().Document.Document);
        importedWorkspace.MarkExported(firstExport);
        clock.UtcNow = secondExportedAt;
        var secondExport = Assert.IsType<ScanFilmParameterProfileSet>(importedWorkspace.BuildExportDocument().Document.Document);

        Assert.Equal(DateTimeOffset.UnixEpoch, newWorkspace.Snapshot.CurrentDraft.SavedAtUtc);
        Assert.Equal(firstExportedAt, newExport.SavedAtUtc);
        Assert.Equal(importedDocument.SavedAtUtc, importedTimestamp);
        Assert.Equal(firstExportedAt, firstExport.SavedAtUtc);
        Assert.Equal(firstExportedAt, importedWorkspace.Snapshot.CurrentDraft.SavedAtUtc);
        Assert.Equal(secondExportedAt, secondExport.SavedAtUtc);
        Assert.Equal(firstExportedAt, importedWorkspace.Snapshot.CurrentDraft.SavedAtUtc);
    }

    [Fact]
    public async Task MarkExported_AcceptsExactSuccessfullyWrittenDocumentIncludingPatchAndTimestamp()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var exportedAt = new DateTimeOffset(2026, 7, 17, 14, 0, 0, TimeSpan.Zero);
        var workspace = CreateWorkspace(repository, new FixedTimeProvider(exportedAt));
        workspace.StageImport(ReadFixture("full-v5.json"));
        await workspace.ApplyStagedImportAsync(CancellationToken.None);
        var sourceProfile = workspace.Snapshot.CurrentDraft.ChannelProfiles["Green"];
        var patch = sourceProfile with { BlackLevel = 1 };

        var export = workspace.BuildExportDocument(patch);
        var exportedDocument = Assert.IsType<ScanFilmParameterProfileSet>(export.Document.Document);
        workspace.SetCurrentDraft(new ScanFilmProfileDraft(
            workspace.Snapshot.CurrentDraft.ProfileName,
            workspace.Snapshot.CurrentDraft.SavedAtUtc,
            workspace.Snapshot.CurrentDraft.ChannelProfiles,
            "Blue",
            workspace.Snapshot.CurrentDraft.AcquisitionSettings,
            workspace.Snapshot.CurrentDraft.ScanRecipeSettings));
        var dirtyAfterSelection = workspace.Snapshot.IsDirty;
        workspace.MarkExported(exportedDocument);
        var expected = ScanFilmProfileDraft.FromDocument(exportedDocument).Draft;

        Assert.True(export.PatchIncluded);
        Assert.True(export.Document.CanApply);
        Assert.Equal(exportedAt, exportedDocument.SavedAtUtc);
        Assert.Equal((ushort)1, exportedDocument.ChannelProfiles["Green"].BlackLevel);
        Assert.True(workspace.Snapshot.CurrentDraft.HasSameContentAs(expected));
        Assert.True(workspace.Snapshot.BaselineDraft.HasSameContentAs(expected));
        Assert.Equal("Green", workspace.Snapshot.CurrentDraft.SelectedCalibrationChannel);
        Assert.Equal((ushort)1, workspace.Snapshot.CurrentDraft.ChannelProfiles["Green"].BlackLevel);
        Assert.Equal(1, repository.ReplaceCount);
        Assert.True(dirtyAfterSelection);
        Assert.False(workspace.Snapshot.IsDirty);
    }

    [Fact]
    public void InvalidStage_CannotBeAppliedAndDoesNotWrite()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = CreateWorkspace(repository);

        var staged = workspace.StageImport(ReadFixture("future-v6.json"));

        Assert.False(staged.Staged);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public void TypedFileImportStage_DoesNotReparseOrWriteRepository()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = CreateWorkspace(repository);
        var document = new ScanFilmProfileDocumentService().Parse(ReadFixture("full-v5.json")).Document;

        var staged = workspace.StageImport(Assert.IsType<ScanFilmParameterProfileSet>(document));

        Assert.True(staged.Staged);
        Assert.Equal(0, repository.ReplaceCount);
        Assert.NotNull(workspace.Snapshot.StagedImport);
    }

    [Fact]
    public void TypedStage_FutureSchemaCannotStage()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var documents = new ScanFilmProfileDocumentService();
        var source = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var future = source with { SchemaVersion = documents.CurrentSchemaVersion + 1 };
        var workspace = CreateWorkspace(repository, documents: documents);

        var staged = workspace.StageImport(future);

        Assert.False(staged.Staged);
        Assert.Contains(staged.Validation.Issues, issue => issue.Code == ScanFilmProfileValidationCode.UnsupportedSchemaVersion);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public void TypedStage_InvalidAdcGainCannotBypassSuppliedValidation()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var documents = new ScanFilmProfileDocumentService();
        var source = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var profiles = source.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var blue = profiles["Blue"];
        profiles["Blue"] = blue with { Parameters = blue.Parameters with { Adc1Gain = 64 } };
        var invalid = source with { ChannelProfiles = profiles };
        var workspace = CreateWorkspace(repository, documents: documents);

        var staged = workspace.StageImport(invalid, new ScanFilmProfileValidationResult());

        Assert.False(staged.Staged);
        Assert.Contains(staged.Validation.Issues, issue => issue.Code == ScanFilmProfileValidationCode.InvalidChannelParameters);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public void TypedStage_InvalidRecipeEnumCannotStage()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var documents = new ScanFilmProfileDocumentService();
        var source = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var invalid = source with
        {
            ScanRecipeSettings = source.ScanRecipeSettings! with { AlignmentMode = (ScanChannelAlignmentMode)99 }
        };
        var workspace = CreateWorkspace(repository, documents: documents);

        var staged = workspace.StageImport(invalid);

        Assert.False(staged.Staged);
        Assert.Contains(staged.Validation.Issues, issue => issue.Code == ScanFilmProfileValidationCode.InvalidAlignmentMode);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public void TypedStage_ValidV5DocumentStagesNormalizedContent()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var documents = new ScanFilmProfileDocumentService();
        var source = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var acquisition = source.AcquisitionSettings! with
        {
            SteadyMask = byte.MaxValue,
            SyncMask = byte.MaxValue,
            Led1PulseClock = 0,
            MotorIntervalNs = 0
        };
        var document = source with
        {
            ProfileName = "  Imported profile  ",
            SelectedCalibrationChannel = " Green ",
            AcquisitionSettings = acquisition
        };
        var workspace = CreateWorkspace(repository, documents: documents);

        var staged = workspace.StageImport(document, new ScanFilmProfileValidationResult());
        var stagedDraft = Assert.IsType<ScanFilmProfileStagedImport>(workspace.Snapshot.StagedImport).Draft;

        Assert.True(staged.Staged);
        Assert.Equal("Imported profile", stagedDraft.ProfileName);
        Assert.Equal("Green", stagedDraft.SelectedCalibrationChannel);
        Assert.Equal(ScanDebugConstants.IlluminationValidMask, stagedDraft.AcquisitionSettings?.SteadyMask);
        Assert.Equal((byte)0, stagedDraft.AcquisitionSettings?.SyncMask);
        Assert.Equal(ScanDebugConstants.IlluminationMinSyncPulseClock, stagedDraft.AcquisitionSettings?.Led1PulseClock);
        Assert.Equal(ScanDebugConstants.MotionMinIntervalNs, stagedDraft.AcquisitionSettings?.MotorIntervalNs);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public void TypedStage_MixedInvalidDocument_RefusesStagingAndWritesNothing()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var documents = new ScanFilmProfileDocumentService();
        var parsed = documents.Parse(ReadFixture("mixed-invalid-channels.json"));
        var workspace = new ScanFilmProfileWorkspace(repository, documents);

        var staged = workspace.StageImport(Assert.IsType<ScanFilmParameterProfileSet>(parsed.Document), parsed.Validation);

        Assert.False(parsed.CanApply);
        Assert.False(staged.Staged);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Theory]
    [InlineData("invalid-recipe-enums-v5.json", ScanFilmProfileValidationCode.InvalidAlignmentMode)]
    [InlineData("invalid-acquisition-masks-limits-v5.json", ScanFilmProfileValidationCode.InvalidColorManagement)]
    public void TypedStage_InvalidNormalizedDocument_RejectsProvidedValidationWithoutWrites(string fixtureName, ScanFilmProfileValidationCode expectedCode)
    {
        var repository = new RecordingCalibrationProfileRepository();
        var documents = new ScanFilmProfileDocumentService();
        var parsed = documents.Parse(ReadFixture(fixtureName));
        var workspace = new ScanFilmProfileWorkspace(repository, documents);

        var staged = workspace.StageImport(Assert.IsType<ScanFilmParameterProfileSet>(parsed.Document), parsed.Validation);

        Assert.False(parsed.CanApply);
        Assert.Contains(parsed.Validation.Issues, issue => issue.Code == expectedCode);
        Assert.False(staged.Staged);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Equal(0, repository.ReplaceCount);
    }

    private static ScanFilmProfileWorkspace CreateWorkspace(
        RecordingCalibrationProfileRepository repository,
        TimeProvider? timeProvider = null,
        IScanFilmProfileDocumentService? documents = null)
        => new(repository, documents ?? new ScanFilmProfileDocumentService(), timeProvider);

    private static ScanFilmProfileDraft WithSavedAt(ScanFilmProfileDraft draft, DateTimeOffset savedAtUtc)
        => new(
            draft.ProfileName,
            savedAtUtc,
            draft.ChannelProfiles,
            draft.SelectedCalibrationChannel,
            draft.AcquisitionSettings,
            draft.ScanRecipeSettings);

    private static ScanCalibrationProfileRepositorySnapshot CreatePersistedSnapshot()
    {
        var captured = CreateCapturedDraft();
        return new ScanCalibrationProfileRepositorySnapshot(captured.ChannelProfiles, "Blue");
    }

    private static ScanFilmProfileDraft CreateCapturedDraft()
        => ScanFilmProfileDraft.FromDocument(Assert.IsType<ScanFilmParameterProfileSet>(
            new ScanFilmProfileDocumentService().Parse(ReadFixture("full-v5.json")).Document)).Draft;

    private static void AssertSnapshotEquals(ScanFilmProfileWorkspaceSnapshot expected, ScanFilmProfileWorkspaceSnapshot actual)
    {
        Assert.True(actual.CurrentDraft.HasSameContentAs(expected.CurrentDraft));
        Assert.True(actual.BaselineDraft.HasSameContentAs(expected.BaselineDraft));
        Assert.Equal(expected.IsDirty, actual.IsDirty);
        Assert.Equal(expected.StagedImport?.Draft, actual.StagedImport?.Draft);
        Assert.Equal(expected.StagedImport?.Validation.Issues, actual.StagedImport?.Validation.Issues);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }
}
