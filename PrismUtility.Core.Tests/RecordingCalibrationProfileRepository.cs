using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PrismUtility.Core.Tests;

internal sealed class RecordingCalibrationProfileRepository : IScanCalibrationProfileRepository
{
    private ScanCalibrationProfileRepositorySnapshot _snapshot = CreateEmptySnapshot();
    private int _initializeCount;
    private int _readCount;

    public ScanCalibrationProfileRepositorySnapshot Snapshot => _snapshot;
    public int ReplaceCount { get; private set; }
    public int InitializeCount => Volatile.Read(ref _initializeCount);
    public int ReadCount => Volatile.Read(ref _readCount);
    public ScanCalibrationProfileRepositorySnapshot? LastReplacement { get; private set; }
    public Exception? ReplaceFailure { get; init; }
    public ScanCalibrationProfileRepositorySnapshot PersistedSnapshot { get; set; } = CreateEmptySnapshot();
    public TaskCompletionSource<object?>? InitializeEntered { get; init; }
    public TaskCompletionSource<object?>? InitializeRelease { get; init; }
    public TaskCompletionSource<object?>? ReadEntered { get; init; }
    public TaskCompletionSource<object?>? ReadRelease { get; init; }

    public async Task InitializeAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _initializeCount);
        InitializeEntered?.TrySetResult(null);
        if (InitializeRelease is not null)
            await InitializeRelease.Task.WaitAsync(ct);

        _snapshot = CopySnapshot(PersistedSnapshot);
    }

    public async Task<ScanCalibrationProfileRepositorySnapshot> ReadAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _readCount);
        ReadEntered?.TrySetResult(null);
        if (ReadRelease is not null)
            await ReadRelease.Task.WaitAsync(ct);

        return _snapshot;
    }

    public bool TryGetProfile(string channelRole, out ScanChannelCalibrationProfile profile)
    {
        profile = new ScanChannelCalibrationProfile(
            new ScanParameterSnapshot(ScanDebugConstants.MinExposureTicks, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            ScanCalibrationRoiSettings.CreateDefault().Normalize());
        return false;
    }

    public Task SaveProfileAsync(string channelRole, ScanChannelCalibrationProfile profile, CancellationToken ct)
        => Task.CompletedTask;

    public Task<bool> ClearProfileAsync(string channelRole, CancellationToken ct)
        => Task.FromResult(false);

    public Task ReplaceAsync(ScanCalibrationProfileRepositorySnapshot snapshot, CancellationToken ct)
    {
        ReplaceCount++;
        if (ReplaceFailure is not null)
            return Task.FromException(ReplaceFailure);

        LastReplacement = new ScanCalibrationProfileRepositorySnapshot(
            snapshot.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            snapshot.SelectedChannel);
        return Task.CompletedTask;
    }

    public Task<string?> GetSelectedChannelAsync(CancellationToken ct)
        => Task.FromResult<string?>(null);

    public Task SetSelectedChannelAsync(string? channelRole, CancellationToken ct)
        => Task.CompletedTask;

    private static ScanCalibrationProfileRepositorySnapshot CreateEmptySnapshot()
        => new(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase), null);

    private static ScanCalibrationProfileRepositorySnapshot CopySnapshot(ScanCalibrationProfileRepositorySnapshot snapshot)
        => new(
            snapshot.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            snapshot.SelectedChannel);
}
