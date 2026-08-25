using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set003")]
[Trait("Category", "Settings")]
public sealed class Set003CalibrationProfileRepositoryTests
{
    [Fact]
    public async Task InitializeAsync_MigratesLegacyProfilesAndSelectionIntoOneVersionedDocument()
    {
        var storage = new Set003RecordingCalibrationStorage
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
        await repository.InitializeAsync(CancellationToken.None);

        var snapshot = repository.Snapshot;
        var profile = Assert.Single(snapshot.Profiles);
        var document = Assert.IsType<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>>(storage.Document);
        Assert.Equal("blue", profile.Key);
        Assert.Equal("Blue", snapshot.SelectedChannel);
        Assert.Equal(1, storage.SaveDocumentCount);
        Assert.Equal("Blue", document.Payload?.SelectedChannel);
        Assert.True(document.Payload?.Profiles?.ContainsKey("BLUE") ?? false);
        Assert.NotNull(storage.LegacyProfiles);
    }

    [Fact]
    public async Task InitializeAsync_FutureOrMalformedDocumentLeavesStateEmptyWithoutLegacyFallback()
    {
        var future = new Set003RecordingCalibrationStorage
        {
            Document = new VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>(2, new ScanCalibrationProfileSettingsPayload(new Dictionary<string, ScanChannelCalibrationProfile> { ["Red"] = Profile() }, "Red")),
            LegacyProfiles = new Dictionary<string, ScanParameterSnapshot> { ["Blue"] = Snapshot() }
        };
        var futureRepository = new ScanCalibrationProfileRepository(future);

        await futureRepository.InitializeAsync(CancellationToken.None);

        Assert.Empty(futureRepository.Snapshot.Profiles);
        Assert.Null(futureRepository.Snapshot.SelectedChannel);
        Assert.Equal(0, future.LegacyProfilesReadCount);
        Assert.Equal(0, future.SaveDocumentCount);

        var invalid = new Set003RecordingCalibrationStorage
        {
            Document = new VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>(1, null),
            LegacyProfiles = new Dictionary<string, ScanParameterSnapshot> { ["Blue"] = Snapshot() }
        };
        var invalidRepository = new ScanCalibrationProfileRepository(invalid);

        await invalidRepository.InitializeAsync(CancellationToken.None);

        Assert.Empty(invalidRepository.Snapshot.Profiles);
        Assert.Equal(0, invalid.LegacyProfilesReadCount);
        Assert.Equal(0, invalid.SaveDocumentCount);

        var malformed = new Set003RecordingCalibrationStorage
        {
            DocumentReadFailure = new JsonReaderException("Malformed calibration document."),
            LegacyProfiles = new Dictionary<string, ScanParameterSnapshot> { ["Blue"] = Snapshot() }
        };
        var malformedRepository = new ScanCalibrationProfileRepository(malformed);

        await malformedRepository.InitializeAsync(CancellationToken.None);

        Assert.Empty(malformedRepository.Snapshot.Profiles);
        Assert.Equal(0, malformed.LegacyProfilesReadCount);
        Assert.Equal(0, malformed.SaveDocumentCount);
    }

    [Fact]
    public async Task ReplaceAsync_DocumentWriteFailureLeavesRepositoryAndDocumentUnchanged()
    {
        var storage = new Set003RecordingCalibrationStorage
        {
            Document = new VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>(1, new ScanCalibrationProfileSettingsPayload(new Dictionary<string, ScanChannelCalibrationProfile> { ["Red"] = Profile() }, "Red"))
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(CancellationToken.None);
        var originalHash = storage.Hash;
        storage.SaveDocumentFailure = new IOException("Injected calibration write failure.");

        await Assert.ThrowsAsync<IOException>(() => repository.ReplaceAsync(
            new ScanCalibrationProfileRepositorySnapshot(new Dictionary<string, ScanChannelCalibrationProfile> { ["Green"] = Profile() }, "Green"),
            CancellationToken.None));

        Assert.Equal(originalHash, storage.Hash);
        Assert.Equal(new[] { "Red" }, repository.Snapshot.Profiles.Keys);
        Assert.Equal("Red", repository.Snapshot.SelectedChannel);
    }

    [Fact]
    public async Task InitializeAsync_MigrationDocumentFailureLeavesLegacyPersistenceAndMemoryUnchanged()
    {
        var storage = new Set003RecordingCalibrationStorage
        {
            LegacyProfiles = new Dictionary<string, ScanParameterSnapshot> { ["Blue"] = Snapshot() },
            SelectedChannel = "Blue",
            SaveDocumentFailure = new IOException("Injected calibration migration failure.")
        };
        var repository = new ScanCalibrationProfileRepository(storage);

        await Assert.ThrowsAsync<IOException>(() => repository.InitializeAsync(CancellationToken.None));

        Assert.Empty(repository.Snapshot.Profiles);
        Assert.Null(repository.Snapshot.SelectedChannel);
        Assert.Null(storage.Document);
        Assert.NotNull(storage.LegacyProfiles);
    }

    [Fact]
    public async Task InitializeAndReplaceAsync_AreSerializedAndPersistProfilesWithTheirSelectionTogether()
    {
        var storage = new Set003RecordingCalibrationStorage
        {
            CurrentProfiles = new Dictionary<string, ScanChannelCalibrationProfile> { ["Red"] = Profile() },
            SelectedChannel = "Red"
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        var replacement = new ScanCalibrationProfileRepositorySnapshot(
            new Dictionary<string, ScanChannelCalibrationProfile> { ["Green"] = Profile() },
            "Green");

        await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => repository.InitializeAsync(CancellationToken.None)).Append(repository.ReplaceAsync(replacement, CancellationToken.None)));

        var document = Assert.IsType<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>>(storage.Document);
        Assert.Equal("Green", repository.Snapshot.SelectedChannel);
        Assert.Equal(new[] { "Green" }, repository.Snapshot.Profiles.Keys);
        Assert.Equal("Green", document.Payload?.SelectedChannel);
        Assert.Equal(new[] { "Green" }, Assert.IsType<Dictionary<string, ScanChannelCalibrationProfile>>(document.Payload?.Profiles).Keys);
    }

    [Fact]
    public async Task ReplaceAndSetSelectedAsync_PublishOnlyCompleteClonedGenerationsWhileSaveIsBlocked()
    {
        const int oldProfileCount = 4096;
        var oldProfiles = Enumerable.Range(0, oldProfileCount)
            .ToDictionary(index => $"Old{index:D4}", _ => Profile(), StringComparer.OrdinalIgnoreCase);
        var storage = new Set003RecordingCalibrationStorage
        {
            Document = new VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>(
                1,
                new ScanCalibrationProfileSettingsPayload(oldProfiles, "Old0000")),
            BlockNextDocumentSave = true
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(CancellationToken.None);
        var replacementProfiles = new Dictionary<string, ScanChannelCalibrationProfile> { ["New"] = Profile() };
        var replacement = new ScanCalibrationProfileRepositorySnapshot(replacementProfiles, "New");
        var observations = new ConcurrentBag<(int Count, bool HasOld, bool HasNew, string? Selected)>();

        var replace = repository.ReplaceAsync(replacement, CancellationToken.None);
        await storage.DocumentSaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var blocked = repository.Snapshot;
        observations.Add((blocked.Profiles.Count, blocked.Profiles.ContainsKey("Old0000"), blocked.Profiles.ContainsKey("New"), blocked.SelectedChannel));
        using var readersReady = new CountdownEvent(32);
        var readersStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = Enumerable.Range(0, 32).Select(_ => Task.Run(async () =>
        {
            readersReady.Signal();
            await readersStart.Task;
            for (var iteration = 0; iteration < 64; iteration++)
            {
                var observed = repository.Snapshot;
                observations.Add((
                    observed.Profiles.Count,
                    observed.Profiles.ContainsKey("Old0000"),
                    observed.Profiles.ContainsKey("New"),
                    observed.SelectedChannel));
            }
        })).ToArray();
        Assert.True(readersReady.Wait(TimeSpan.FromSeconds(5)));
        var select = repository.SetSelectedChannelAsync("Blue", CancellationToken.None);

        readersStart.SetResult();
        storage.DocumentSaveRelease.SetResult();
        await Task.WhenAll(readers.Append(replace).Append(select));

        var final = repository.Snapshot;
        observations.Add((final.Profiles.Count, final.Profiles.ContainsKey("Old0000"), final.Profiles.ContainsKey("New"), final.SelectedChannel));
        Assert.All(observations, observation => Assert.True(
            observation is (oldProfileCount, true, false, "Old0000")
                or (1, false, true, "New")
                or (1, false, true, "Blue"),
            $"Observed mixed calibration generation: {observation}."));
        Assert.Contains(observations, observation => observation is (oldProfileCount, true, false, "Old0000"));
        Assert.Contains(observations, observation => observation is (1, false, true, "Blue"));

        replacementProfiles["Alias"] = Profile();
        Assert.DoesNotContain("Alias", repository.Snapshot.Profiles.Keys);
        Assert.IsType<Dictionary<string, ScanChannelCalibrationProfile>>(final.Profiles).Clear();
        Assert.Equal(new[] { "New" }, repository.Snapshot.Profiles.Keys);
        Assert.Equal("Blue", repository.Snapshot.SelectedChannel);
    }

    private static ScanParameterSnapshot Snapshot()
        => new(10, -2, 3, 4, 5, 30_000);

    private static ScanChannelCalibrationProfile Profile()
        => new(Snapshot(), ScanCalibrationRoiSettings.CreateDefault());
}

internal sealed class Set003RecordingCalibrationStorage : IScanCalibrationProfileStorage
{
    public VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? Document { get; set; }
    public Dictionary<string, ScanChannelCalibrationProfile>? CurrentProfiles { get; set; }
    public Dictionary<string, ScanParameterSnapshot>? LegacyProfiles { get; set; }
    public string? SelectedChannel { get; set; }
    public Exception? DocumentReadFailure { get; set; }
    public Exception? SaveDocumentFailure { get; set; }
    public bool BlockNextDocumentSave { get; set; }
    public TaskCompletionSource DocumentSaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource DocumentSaveRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int LegacyProfilesReadCount { get; private set; }
    public int SaveDocumentCount { get; private set; }

    public string Hash
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Document))));

    public Task<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>?> ReadDocumentAsync(CancellationToken ct)
    {
        if (DocumentReadFailure is not null)
            return Task.FromException<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>?>(DocumentReadFailure);

        return Task.FromResult(CloneDocument(Document));
    }

    public async Task SaveDocumentAsync(VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload> document, CancellationToken ct)
    {
        SaveDocumentCount++;
        if (SaveDocumentFailure is not null)
            throw SaveDocumentFailure;

        if (BlockNextDocumentSave)
        {
            BlockNextDocumentSave = false;
            DocumentSaveEntered.SetResult();
            await DocumentSaveRelease.Task.WaitAsync(ct);
        }

        Document = CloneDocument(document);
    }

    public Task<Dictionary<string, ScanChannelCalibrationProfile>?> ReadCurrentProfilesAsync(CancellationToken ct)
        => Task.FromResult(CloneProfiles(CurrentProfiles));

    public Task<Dictionary<string, ScanParameterSnapshot>?> ReadLegacyProfilesAsync(CancellationToken ct)
    {
        LegacyProfilesReadCount++;
        return Task.FromResult(LegacyProfiles is null ? null : new Dictionary<string, ScanParameterSnapshot>(LegacyProfiles));
    }

    public Task<string?> ReadSelectedChannelAsync(CancellationToken ct)
        => Task.FromResult(SelectedChannel);

    public Task SaveCurrentProfilesAsync(Dictionary<string, ScanChannelCalibrationProfile> profiles, CancellationToken ct)
    {
        CurrentProfiles = CloneProfiles(profiles);
        return Task.CompletedTask;
    }

    public Task SaveSelectedChannelAsync(string? selectedChannel, CancellationToken ct)
    {
        SelectedChannel = selectedChannel;
        return Task.CompletedTask;
    }

    private static VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? CloneDocument(VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? document)
        => document is null
            ? null
            : new VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>(
                document.SchemaVersion,
                document.Payload is null
                    ? null
                    : new ScanCalibrationProfileSettingsPayload(CloneProfiles(document.Payload.Profiles), document.Payload.SelectedChannel));

    private static Dictionary<string, ScanChannelCalibrationProfile>? CloneProfiles(IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? profiles)
        => profiles is null
            ? null
            : profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
}
