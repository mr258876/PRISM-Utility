using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileWorkspaceLibrarySyncTests
{
    [Fact]
    public async Task PlanCalibrationLibrarySyncAsync_ComputesAdditionsUpdatesAndRemovalsWithoutWriting()
    {
        var draft = CreateImportedDraft();
        var repository = CreateLibraryRepository(draft);
        var workspace = await CreateWorkspaceWithAppliedImportAsync(repository);

        var plan = await workspace.PlanCalibrationLibrarySyncAsync(CancellationToken.None);

        Assert.Equal(new[] { "Green" }, plan.Additions.Keys);
        Assert.Equal(new[] { "Blue" }, plan.Updates.Keys);
        Assert.Equal(new[] { "Legacy" }, plan.Removals.Keys);
        Assert.True(plan.SelectedChannelWillChange);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public async Task SyncCalibrationLibraryAsync_AddsAndUpdatesWithoutRemovingAndIsIdempotent()
    {
        var draft = CreateImportedDraft();
        var repository = CreateLibraryRepository(draft);
        var workspace = await CreateWorkspaceWithAppliedImportAsync(repository);
        var beforeWorkspace = workspace.Snapshot;

        var first = await workspace.SyncCalibrationLibraryAsync(CancellationToken.None);
        var second = await workspace.SyncCalibrationLibraryAsync(CancellationToken.None);

        var firstPlan = Assert.IsType<ScanFilmProfileLibrarySyncPlan>(first.Plan);
        var secondPlan = Assert.IsType<ScanFilmProfileLibrarySyncPlan>(second.Plan);
        Assert.Equal(ScanFilmProfileLibrarySyncStatus.Applied, first.Status);
        Assert.Equal(new[] { "Green" }, firstPlan.Additions.Keys);
        Assert.Equal(new[] { "Blue" }, firstPlan.Updates.Keys);
        Assert.Equal(new[] { "Legacy" }, firstPlan.Removals.Keys);
        Assert.Equal(ScanFilmProfileLibrarySyncStatus.NoChanges, second.Status);
        Assert.Empty(secondPlan.Additions);
        Assert.Empty(secondPlan.Updates);
        Assert.Equal(new[] { "Legacy" }, secondPlan.Removals.Keys);
        Assert.Equal(1, repository.ReplaceCount);
        Assert.Equal("Legacy", repository.Snapshot.SelectedChannel);
        Assert.Equal(draft.ChannelProfiles["Blue"], repository.Snapshot.Profiles["Blue"]);
        Assert.Equal(draft.ChannelProfiles["Green"], repository.Snapshot.Profiles["Green"]);
        Assert.True(repository.Snapshot.Profiles.ContainsKey("Legacy"));
        AssertWorkspaceSnapshotEquals(beforeWorkspace, workspace.Snapshot);
    }

    [Fact]
    public async Task ReplaceCalibrationLibraryAsync_RequiresTypedConfirmationBeforeItCanRemoveProfiles()
    {
        var draft = CreateImportedDraft();
        var repository = CreateLibraryRepository(draft);
        var workspace = await CreateWorkspaceWithAppliedImportAsync(repository);
        var before = await repository.ReadAsync(CancellationToken.None);

        var declined = await workspace.ReplaceCalibrationLibraryAsync(null, CancellationToken.None);
        var confirmed = await workspace.ReplaceCalibrationLibraryAsync(
            ScanFilmProfileFullReplacementConfirmation.Confirmed,
            CancellationToken.None);

        Assert.Equal(ScanFilmProfileLibrarySyncStatus.ConfirmationRequired, declined.Status);
        Assert.Null(declined.Plan);
        Assert.Equal(ScanFilmProfileLibrarySyncStatus.Applied, confirmed.Status);
        Assert.Equal(1, repository.ReplaceCount);
        Assert.Equal("Green", repository.Snapshot.SelectedChannel);
        Assert.Equal(new[] { "Blue", "Green" }, repository.Snapshot.Profiles.Keys.OrderBy(key => key));
        Assert.DoesNotContain("Legacy", repository.Snapshot.Profiles.Keys);
        Assert.NotEqual(before, repository.Snapshot);
    }

    [Fact]
    public async Task CanceledOrFailedSyncAndFullReplacement_LeaveRepositoryAndWorkspaceUnchanged()
    {
        var draft = CreateImportedDraft();
        var repository = CreateLibraryRepository(draft);
        var workspace = await CreateWorkspaceWithAppliedImportAsync(repository);
        var beforeRepository = await repository.ReadAsync(CancellationToken.None);
        var beforeWorkspace = workspace.Snapshot;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var canceledSync = await workspace.SyncCalibrationLibraryAsync(cancellation.Token);
        var canceledReplacement = await workspace.ReplaceCalibrationLibraryAsync(
            ScanFilmProfileFullReplacementConfirmation.Confirmed,
            cancellation.Token);

        var failingRepository = CreateLibraryRepository(draft, new IOException("replace failed"));
        var failingWorkspace = await CreateWorkspaceWithAppliedImportAsync(failingRepository);
        var failingBeforeRepository = await failingRepository.ReadAsync(CancellationToken.None);
        var failingBeforeWorkspace = failingWorkspace.Snapshot;
        var failedSync = await failingWorkspace.SyncCalibrationLibraryAsync(CancellationToken.None);

        Assert.Equal(ScanFilmProfileLibrarySyncStatus.Canceled, canceledSync.Status);
        Assert.Equal(ScanFilmProfileLibrarySyncStatus.Canceled, canceledReplacement.Status);
        Assert.Equal(0, repository.ReplaceCount);
        AssertRepositorySnapshotEquals(beforeRepository, repository.Snapshot);
        AssertWorkspaceSnapshotEquals(beforeWorkspace, workspace.Snapshot);
        Assert.Equal(ScanFilmProfileLibrarySyncStatus.Failed, failedSync.Status);
        Assert.IsType<IOException>(failedSync.Error);
        Assert.Equal(1, failingRepository.ReplaceCount);
        AssertRepositorySnapshotEquals(failingBeforeRepository, failingRepository.Snapshot);
        AssertWorkspaceSnapshotEquals(failingBeforeWorkspace, failingWorkspace.Snapshot);
    }

    [Fact]
    public async Task SyncCalibrationLibraryAsync_DetectsStaleWorkspaceAndDoesNotWrite()
    {
        var draft = CreateImportedDraft();
        var readEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = CreateLibraryRepository(draft, readEntered: readEntered, readRelease: readRelease);
        var workspace = await CreateWorkspaceWithAppliedImportAsync(repository);
        var beforeRepository = repository.PersistedSnapshot;

        var sync = workspace.SyncCalibrationLibraryAsync(CancellationToken.None);
        await readEntered.Task;
        workspace.SetCurrentDraft(WithProfileName(workspace.Snapshot.CurrentDraft, "Changed while syncing"));
        readRelease.SetResult(null);
        var result = await sync;

        Assert.Equal(ScanFilmProfileLibrarySyncStatus.Stale, result.Status);
        Assert.Equal(0, repository.ReplaceCount);
        AssertRepositorySnapshotEquals(beforeRepository, repository.Snapshot);
        Assert.Equal("Changed while syncing", workspace.Snapshot.CurrentDraft.ProfileName);
    }

    private static async Task<ScanFilmProfileWorkspace> CreateWorkspaceWithAppliedImportAsync(RecordingCalibrationProfileRepository repository)
    {
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        Assert.True(workspace.StageImport(ReadFixture("full-v5.json")).Staged);
        Assert.Equal(ScanFilmProfileApplyStatus.Applied, (await workspace.ApplyStagedImportAsync(CancellationToken.None)).Status);
        Assert.Equal(0, repository.ReplaceCount);
        return workspace;
    }

    private static RecordingCalibrationProfileRepository CreateLibraryRepository(
        ScanFilmProfileDraft draft,
        Exception? replaceFailure = null,
        TaskCompletionSource<object?>? readEntered = null,
        TaskCompletionSource<object?>? readRelease = null)
    {
        var profiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Blue"] = draft.ChannelProfiles["Blue"] with { BlackLevel = 1 },
            ["Legacy"] = draft.ChannelProfiles["Blue"]
        };
        return new RecordingCalibrationProfileRepository
        {
            PersistedSnapshot = new ScanCalibrationProfileRepositorySnapshot(profiles, "Legacy"),
            ReplaceFailure = replaceFailure,
            ReadEntered = readEntered,
            ReadRelease = readRelease
        };
    }

    private static ScanFilmProfileDraft CreateImportedDraft()
    {
        var document = Assert.IsType<ScanFilmParameterProfileSet>(
            new ScanFilmProfileDocumentService().Parse(ReadFixture("full-v5.json")).Document);
        return ScanFilmProfileDraft.FromDocument(document).Draft;
    }

    private static ScanFilmProfileDraft WithProfileName(ScanFilmProfileDraft draft, string profileName)
        => new(
            profileName,
            draft.SavedAtUtc,
            draft.ChannelProfiles,
            draft.SelectedCalibrationChannel,
            draft.AcquisitionSettings,
            draft.ScanRecipeSettings);

    private static void AssertWorkspaceSnapshotEquals(ScanFilmProfileWorkspaceSnapshot expected, ScanFilmProfileWorkspaceSnapshot actual)
    {
        Assert.True(actual.CurrentDraft.HasSameContentAs(expected.CurrentDraft));
        Assert.True(actual.BaselineDraft.HasSameContentAs(expected.BaselineDraft));
        Assert.Equal(expected.IsDirty, actual.IsDirty);
        Assert.Equal(expected.StagedImport, actual.StagedImport);
    }

    private static void AssertRepositorySnapshotEquals(ScanCalibrationProfileRepositorySnapshot expected, ScanCalibrationProfileRepositorySnapshot actual)
    {
        Assert.Equal(expected.SelectedChannel, actual.SelectedChannel);
        Assert.Equal(expected.Profiles, actual.Profiles);
    }

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }
}
