using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanCalibrationProfileRepositoryTests
{
    [Fact]
    public async Task Snapshot_AfterInitializationAndMutationsIsImmutableAndConsistent()
    {
        var storage = new RecordingScanCalibrationProfileStorage { CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile> { ["Blue"] = Profile() } };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(CancellationToken.None);
        var initial = repository.Snapshot;
        await repository.SaveProfileAsync("Green", Profile(), CancellationToken.None);
        var saved = repository.Snapshot;
        await repository.ClearProfileAsync("Blue", CancellationToken.None);

        Assert.True(initial.Profiles.ContainsKey("Blue"));
        Assert.False(initial.Profiles.ContainsKey("Green"));
        Assert.True(saved.Profiles.ContainsKey("Green"));
        Assert.False(repository.Snapshot.Profiles.ContainsKey("Blue"));
    }
    [Fact]
    public async Task InitializeAsync_CurrentProfilesNormalizeAndDoNotReadLegacy()
    {
        var storage = new RecordingScanCalibrationProfileStorage
        {
            CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile>
            {
                [" blue "] = Profile(blackLevel: 100, whiteLevel: 100)
            },
            LegacyProfiles = new Dictionary<string, ScanParameterSnapshot>
            {
                ["legacy"] = Snapshot()
            },
            SelectedChannel = " Green "
        };
        var repository = new ScanCalibrationProfileRepository(storage);

        await repository.InitializeAsync(CancellationToken.None);
        var snapshot = await repository.ReadAsync(CancellationToken.None);

        var profile = Assert.Single(snapshot.Profiles);
        Assert.Equal("blue", profile.Key);
        Assert.True(snapshot.Profiles.ContainsKey("BLUE"));
        Assert.Equal((ushort)99, profile.Value.BlackLevel);
        Assert.Equal((ushort)100, profile.Value.WhiteLevel);
        Assert.Equal("Green", snapshot.SelectedChannel);
        Assert.Equal(1, storage.CurrentProfilesReadCount);
        Assert.Equal(0, storage.LegacyProfilesReadCount);
        Assert.Equal(1, storage.SaveDocumentCount);
    }

    [Fact]
    public async Task Todo19_ExactMinimumRoi_SaveReloadsUnchangedAndInvalidNewSaveDoesNotPersist()
    {
        var storage = new RecordingScanCalibrationProfileStorage
        {
            CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile>()
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        var exact = ExactMinimumProfile();

        await repository.InitializeAsync(CancellationToken.None);
        storage.ResetWriteCounts();
        await repository.SaveProfileAsync("Blue", exact, CancellationToken.None);

        Assert.Equal(exact, storage.Document?.Payload?.Profiles?["Blue"]);
        var reloaded = new ScanCalibrationProfileRepository(storage);
        var reloadedSnapshot = await reloaded.ReadAsync(CancellationToken.None);
        Assert.Equal(exact, reloadedSnapshot.Profiles["Blue"]);

        var writesBeforeInvalidSave = storage.SaveDocumentCount;
        var invalid = exact with { RoiSettings = exact.RoiSettings with { FocusLeftRange = new ScanColumnRange(100, 101) } };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SaveProfileAsync("Green", invalid, CancellationToken.None));

        Assert.Equal(writesBeforeInvalidSave, storage.SaveDocumentCount);
        Assert.False(repository.Snapshot.Profiles.ContainsKey("Green"));
    }

    [Fact]
    public async Task InitializeAsync_NullCurrentProfilesMigratesLegacyAndPersistsCurrentForm()
    {
        var storage = new RecordingScanCalibrationProfileStorage
        {
            LegacyProfiles = new Dictionary<string, ScanParameterSnapshot>
            {
                [" blue "] = Snapshot(),
                ["invalid"] = new ScanParameterSnapshot(9, 0, 64, 0, 1, 30_000)
            },
            SelectedChannel = " Blue "
        };
        var repository = new ScanCalibrationProfileRepository(storage);

        await repository.InitializeAsync(CancellationToken.None);
        var snapshot = await repository.ReadAsync(CancellationToken.None);

        var profile = Assert.Single(snapshot.Profiles);
        Assert.Equal("blue", profile.Key);
        Assert.Equal(ScanCalibrationRoiSettings.CreateDefault().Normalize(), profile.Value.RoiSettings);
        Assert.Equal("Blue", snapshot.SelectedChannel);
        Assert.Equal(1, storage.CurrentProfilesReadCount);
        Assert.Equal(1, storage.LegacyProfilesReadCount);
        Assert.Equal(1, storage.SaveDocumentCount);
        Assert.NotNull(storage.CurrentProfiles);
    }

    [Fact]
    public async Task InitializeAsync_MalformedDeserializedProfileIsDiscardedWithoutPublishingState()
    {
        var malformedProfiles = await Json.ToObjectAsync<Dictionary<string, ScanChannelCalibrationProfile>>(
            "{\"Malformed\":{\"Parameters\":null,\"RoiSettings\":null}}");
        var storage = new RecordingScanCalibrationProfileStorage
        {
            CurrentProfiles = Assert.IsType<Dictionary<string, ScanChannelCalibrationProfile>>(malformedProfiles)
        };
        var repository = new ScanCalibrationProfileRepository(storage);

        await repository.InitializeAsync(CancellationToken.None);
        var snapshot = await repository.ReadAsync(CancellationToken.None);

        Assert.Empty(snapshot.Profiles);
        Assert.Equal(1, storage.SaveDocumentCount);
    }

    [Fact]
    public async Task SaveClearAndSelectedChannel_UseCaseInsensitiveRolesWithoutChangingSelectionOnClear()
    {
        var storage = new RecordingScanCalibrationProfileStorage
        {
            CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile>()
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(CancellationToken.None);
        storage.ResetWriteCounts();

        await repository.SaveProfileAsync(" blue ", Profile(), CancellationToken.None);
        await repository.SetSelectedChannelAsync(" Blue ", CancellationToken.None);
        var removed = await repository.ClearProfileAsync("BLUE", CancellationToken.None);
        var selected = await repository.GetSelectedChannelAsync(CancellationToken.None);

        Assert.True(removed);
        Assert.False(repository.TryGetProfile("blue", out _));
        Assert.Equal("Blue", selected);
        Assert.Equal(3, storage.SaveDocumentCount);
    }

    [Fact]
    public async Task InvalidInputAndProfileWriteFailure_DoNotCorruptLoadedState()
    {
        var storage = new RecordingScanCalibrationProfileStorage
        {
            CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile>
            {
                ["Red"] = Profile()
            },
            SelectedChannel = "Red"
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(CancellationToken.None);
        storage.ResetWriteCounts();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveProfileAsync(" ", Profile(), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SaveProfileAsync("Blue", InvalidProfile(), CancellationToken.None));

        storage.SaveDocumentFailure = new IOException("profile write failed");
        await Assert.ThrowsAsync<IOException>(() => repository.SaveProfileAsync("Blue", Profile(), CancellationToken.None));
        await Assert.ThrowsAsync<IOException>(() => repository.SaveProfileAsync("Blue", Profile(), CancellationToken.None));
        var snapshot = await repository.ReadAsync(CancellationToken.None);

        Assert.Equal(new[] { "Red" }, snapshot.Profiles.Keys);
        Assert.Equal("Red", snapshot.SelectedChannel);
        Assert.True(repository.TryGetProfile("red", out _));
        Assert.False(repository.TryGetProfile("blue", out _));
        Assert.Equal(2, storage.SaveDocumentCount);
    }

    [Fact]
    public async Task ReplaceAsync_DocumentWriteFailureKeepsRepositoryStateUnchanged()
    {
        var storage = new RecordingScanCalibrationProfileStorage
        {
            CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile>
            {
                ["Red"] = Profile()
            },
            SelectedChannel = "Red"
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(CancellationToken.None);
        storage.ResetWriteCounts();
        storage.SaveDocumentFailure = new IOException("document write failed");

        await Assert.ThrowsAsync<IOException>(() => repository.ReplaceAsync(
            new ScanCalibrationProfileRepositorySnapshot(
                new Dictionary<string, ScanChannelCalibrationProfile> { ["Green"] = Profile() },
                "Green"),
            CancellationToken.None));
        var snapshot = await repository.ReadAsync(CancellationToken.None);

        Assert.Equal(new[] { "Red" }, snapshot.Profiles.Keys);
        Assert.Equal("Red", snapshot.SelectedChannel);
        Assert.True(repository.TryGetProfile("red", out _));
        Assert.False(repository.TryGetProfile("green", out _));
        Assert.Equal(1, storage.SaveDocumentCount);
    }

    [Fact]
    public async Task CanceledMutation_DoesNotWriteOrPublishNewProfile()
    {
        var storage = new RecordingScanCalibrationProfileStorage
        {
            CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile>
            {
                ["Red"] = Profile()
            }
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(CancellationToken.None);
        storage.ResetWriteCounts();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveProfileAsync("Green", Profile(), cancellation.Token));
        var snapshot = await repository.ReadAsync(CancellationToken.None);

        Assert.Equal(new[] { "Red" }, snapshot.Profiles.Keys);
        Assert.False(repository.TryGetProfile("green", out _));
        Assert.Equal(0, storage.SaveProfilesCount);
    }

    private static ScanChannelCalibrationProfile Profile(ushort? blackLevel = null, ushort? whiteLevel = null)
        => new(Snapshot(), ScanCalibrationRoiSettings.CreateDefault(), blackLevel, whiteLevel);

    private static ScanChannelCalibrationProfile InvalidProfile()
        => new(new ScanParameterSnapshot(10, 0, 64, 0, 1, 30_000), ScanCalibrationRoiSettings.CreateDefault());

    private static ScanParameterSnapshot Snapshot()
        => new(10, -2, 3, 4, 5, 30_000);

    private static ScanChannelCalibrationProfile ExactMinimumProfile()
        => new(
            Snapshot(),
            new ScanCalibrationRoiSettings(
                new ScanColumnRange(100, 101),
                new ScanColumnRange(0, 1),
                new ScanColumnRange(100, 102),
                new ScanColumnRange(104, 106),
                new ScanColumnRange(100, 106)));
}

internal sealed class RecordingScanCalibrationProfileStorage : IScanCalibrationProfileStorage
{
    public VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? Document { get; set; }
    public Dictionary<string, ScanChannelCalibrationProfile>? CurrentProfiles { get; set; }
    public Dictionary<string, ScanParameterSnapshot>? LegacyProfiles { get; init; }
    public string? SelectedChannel { get; set; }
    public Exception? SaveProfilesFailure { get; set; }
    public Exception? SaveSelectedChannelFailure { get; set; }
    public Exception? SaveDocumentFailure { get; set; }
    public int CurrentProfilesReadCount { get; private set; }
    public int LegacyProfilesReadCount { get; private set; }
    public int SaveProfilesCount { get; private set; }
    public int SaveSelectedChannelCount { get; private set; }
    public int SaveDocumentCount { get; private set; }

    public Task<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>?> ReadDocumentAsync(CancellationToken ct)
        => Task.FromResult(CloneDocument(Document));

    public Task SaveDocumentAsync(VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload> document, CancellationToken ct)
    {
        SaveDocumentCount++;
        if (SaveDocumentFailure is not null)
            return Task.FromException(SaveDocumentFailure);

        Document = CloneDocument(document);
        CurrentProfiles = CloneProfiles(document.Payload?.Profiles);
        SelectedChannel = document.Payload?.SelectedChannel;
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, ScanChannelCalibrationProfile>?> ReadCurrentProfilesAsync(CancellationToken ct)
    {
        CurrentProfilesReadCount++;
        return Task.FromResult(CloneProfiles(CurrentProfiles));
    }

    public Task<Dictionary<string, ScanParameterSnapshot>?> ReadLegacyProfilesAsync(CancellationToken ct)
    {
        LegacyProfilesReadCount++;
        return Task.FromResult(LegacyProfiles is null ? null : new Dictionary<string, ScanParameterSnapshot>(LegacyProfiles));
    }

    public Task<string?> ReadSelectedChannelAsync(CancellationToken ct)
        => Task.FromResult(SelectedChannel);

    public Task SaveCurrentProfilesAsync(Dictionary<string, ScanChannelCalibrationProfile> profiles, CancellationToken ct)
    {
        SaveProfilesCount++;
        if (SaveProfilesFailure is not null)
            return Task.FromException(SaveProfilesFailure);

        CurrentProfiles = CloneProfiles(profiles);
        return Task.CompletedTask;
    }

    public Task SaveSelectedChannelAsync(string? selectedChannel, CancellationToken ct)
    {
        SaveSelectedChannelCount++;
        if (SaveSelectedChannelFailure is not null)
            return Task.FromException(SaveSelectedChannelFailure);

        SelectedChannel = selectedChannel;
        return Task.CompletedTask;
    }

    public void ResetWriteCounts()
    {
        SaveProfilesCount = 0;
        SaveSelectedChannelCount = 0;
        SaveDocumentCount = 0;
    }

    private static VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? CloneDocument(VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? document)
        => document is null
            ? null
            : new VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>(
                document.SchemaVersion,
                document.Payload is null
                    ? null
                    : new ScanCalibrationProfileSettingsPayload(CloneProfiles(document.Payload.Profiles), document.Payload.SelectedChannel));

    private static Dictionary<string, ScanChannelCalibrationProfile>? CloneProfiles(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? profiles)
        => profiles is null
            ? null
            : profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
}
