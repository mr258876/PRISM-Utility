using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PrismUtility.Core.Tests;

internal sealed class RecordingCalibrationProfileRepository : IScanCalibrationProfileRepository
{
    private readonly object _mutationLock = new();
    private ScanCalibrationProfileRepositorySnapshot _snapshot = CreateEmptySnapshot();
    private int _initializeCount;
    private int _readCount;
    private int _isInitialized;

    public ScanCalibrationProfileRepositorySnapshot Snapshot => _snapshot;
    public int ReplaceCount { get; private set; }
    public int SaveProfileCount { get; private set; }
    public int ClearProfileCount { get; private set; }
    public IReadOnlyList<string> SavedProfileRoles
    {
        get
        {
            lock (_mutationLock)
                return _savedProfileRoles.ToArray();
        }
    }

    public IReadOnlyList<string> ClearedProfileRoles
    {
        get
        {
            lock (_mutationLock)
                return _clearedProfileRoles.ToArray();
        }
    }

    public IReadOnlyList<string> SelectedChannelWriteRoles
    {
        get
        {
            lock (_mutationLock)
                return _selectedChannelWriteRoles.ToArray();
        }
    }

    public IReadOnlyList<string> CallLog
    {
        get
        {
            lock (_mutationLock)
                return _callLog.ToArray();
        }
    }

    public int InitializeCount => Volatile.Read(ref _initializeCount);
    public int ReadCount => Volatile.Read(ref _readCount);
    public ScanCalibrationProfileRepositorySnapshot? LastReplacement { get; private set; }
    public Exception? ReplaceFailure { get; init; }
    public ScanCalibrationProfileRepositorySnapshot PersistedSnapshot { get; set; } = CreateEmptySnapshot();
    public TaskCompletionSource<object?>? InitializeEntered { get; init; }
    public TaskCompletionSource<object?>? InitializeRelease { get; init; }
    public TaskCompletionSource<object?>? ReadEntered { get; init; }
    public TaskCompletionSource<object?>? ReadRelease { get; init; }
    private readonly List<string> _savedProfileRoles = [];
    private readonly List<string> _clearedProfileRoles = [];
    private readonly List<string> _selectedChannelWriteRoles = [];
    private readonly List<string> _callLog = [];
    private readonly Dictionary<string, ControlledOperation> _profileSaveOperations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ControlledOperation> _profileClearOperations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ControlledOperation> _selectedChannelOperations = new(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _initializeCount);
        InitializeEntered?.TrySetResult(null);
        if (InitializeRelease is not null)
            await InitializeRelease.Task.WaitAsync(ct);

        _snapshot = CopySnapshot(PersistedSnapshot);
        Volatile.Write(ref _isInitialized, 1);
    }

    public async Task<ScanCalibrationProfileRepositorySnapshot> ReadAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _readCount);
        ReadEntered?.TrySetResult(null);
        if (ReadRelease is not null)
            await ReadRelease.Task.WaitAsync(ct);

        if (Volatile.Read(ref _isInitialized) == 0)
            await InitializeAsync(ct);

        return _snapshot;
    }

    public bool TryGetProfile(string channelRole, out ScanChannelCalibrationProfile profile)
    {
        return _snapshot.Profiles.TryGetValue(channelRole.Trim(), out profile!);
    }

    public async Task SaveProfileAsync(string channelRole, ScanChannelCalibrationProfile profile, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var role = channelRole.Trim();
        lock (_mutationLock)
            _callLog.Add($"SaveProfile:{role}");
        var operation = GetOperation(_profileSaveOperations, role);
        operation.Entered.TrySetResult(null);
        try
        {
            await operation.Release.Task.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            operation.ThrowIfConfigured();

            lock (_mutationLock)
            {
                var profiles = _snapshot.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                profiles[role] = profile;
                _snapshot = new ScanCalibrationProfileRepositorySnapshot(profiles, _snapshot.SelectedChannel);
                PersistedSnapshot = CopySnapshot(_snapshot);
                SaveProfileCount++;
                _savedProfileRoles.Add(role);
            }
        }
        finally
        {
            operation.Completed.TrySetResult(null);
        }
    }

    public async Task<bool> ClearProfileAsync(string channelRole, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var role = channelRole.Trim();
        var operation = GetOperation(_profileClearOperations, role);
        operation.Entered.TrySetResult(null);
        try
        {
            await operation.Release.Task.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            operation.ThrowIfConfigured();

            lock (_mutationLock)
            {
                var profiles = _snapshot.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var removed = profiles.Remove(role);
                if (removed)
                {
                    _snapshot = new ScanCalibrationProfileRepositorySnapshot(profiles, _snapshot.SelectedChannel);
                    PersistedSnapshot = CopySnapshot(_snapshot);
                }

                ClearProfileCount++;
                _clearedProfileRoles.Add(role);
                return removed;
            }
        }
        finally
        {
            operation.Completed.TrySetResult(null);
        }
    }

    public async Task ReplaceAsync(ScanCalibrationProfileRepositorySnapshot snapshot, CancellationToken ct)
    {
        await InitializeAsync(ct);
        ReplaceCount++;
        if (ReplaceFailure is not null)
            throw ReplaceFailure;

        LastReplacement = new ScanCalibrationProfileRepositorySnapshot(
            snapshot.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            snapshot.SelectedChannel);
        _snapshot = CopySnapshot(LastReplacement);
        PersistedSnapshot = CopySnapshot(LastReplacement);
    }

    public Task<string?> GetSelectedChannelAsync(CancellationToken ct)
        => Task.FromResult(_snapshot.SelectedChannel);

    public async Task SetSelectedChannelAsync(string? channelRole, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var role = channelRole?.Trim() ?? string.Empty;
        var operation = GetOperation(_selectedChannelOperations, role);
        operation.Entered.TrySetResult(null);
        try
        {
            await operation.Release.Task.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            operation.ThrowIfConfigured();

            lock (_mutationLock)
            {
                _snapshot = new ScanCalibrationProfileRepositorySnapshot(
                    _snapshot.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                    channelRole?.Trim());
                PersistedSnapshot = CopySnapshot(_snapshot);
                _selectedChannelWriteRoles.Add(role);
            }
        }
        finally
        {
            operation.Completed.TrySetResult(null);
        }
    }

    public void BlockProfileSave(string channelRole, Exception? failure = null)
        => ConfigureOperation(_profileSaveOperations, channelRole, block: true, failure);

    public void BlockProfileClear(string channelRole, Exception? failure = null)
        => ConfigureOperation(_profileClearOperations, channelRole, block: true, failure);

    public void BlockSelectedChannelWrite(string channelRole, Exception? failure = null)
        => ConfigureOperation(_selectedChannelOperations, channelRole, block: true, failure);

    public void FailSelectedChannelWrite(string channelRole, Exception failure)
        => ConfigureOperation(_selectedChannelOperations, channelRole, block: false, failure);

    public void FailProfileSave(string channelRole, Exception failure)
        => ConfigureOperation(_profileSaveOperations, channelRole, block: false, failure);

    public Task WaitForProfileSaveAsync(string channelRole)
        => GetOperation(_profileSaveOperations, channelRole).Entered.Task;

    public Task WaitForProfileClearAsync(string channelRole)
        => GetOperation(_profileClearOperations, channelRole).Entered.Task;

    public Task WaitForSelectedChannelWriteAsync(string channelRole)
        => GetOperation(_selectedChannelOperations, channelRole).Entered.Task;

    public Task WaitForProfileSaveCompletionAsync(string channelRole)
        => GetOperation(_profileSaveOperations, channelRole).Completed.Task;

    public Task WaitForProfileClearCompletionAsync(string channelRole)
        => GetOperation(_profileClearOperations, channelRole).Completed.Task;

    public Task WaitForSelectedChannelWriteCompletionAsync(string channelRole)
        => GetOperation(_selectedChannelOperations, channelRole).Completed.Task;

    public void ReleaseProfileSave(string channelRole)
        => GetOperation(_profileSaveOperations, channelRole).Release.TrySetResult(null);

    public void ReleaseProfileClear(string channelRole)
        => GetOperation(_profileClearOperations, channelRole).Release.TrySetResult(null);

    public void ReleaseSelectedChannelWrite(string channelRole)
        => GetOperation(_selectedChannelOperations, channelRole).Release.TrySetResult(null);

    private void ConfigureOperation(
        Dictionary<string, ControlledOperation> operations,
        string channelRole,
        bool block,
        Exception? failure)
    {
        var operation = new ControlledOperation(failure);
        if (!block)
            operation.Release.TrySetResult(null);

        lock (_mutationLock)
            operations[channelRole.Trim()] = operation;
    }

    private ControlledOperation GetOperation(Dictionary<string, ControlledOperation> operations, string channelRole)
    {
        lock (_mutationLock)
        {
            if (operations.TryGetValue(channelRole.Trim(), out var operation))
                return operation;

            operation = new ControlledOperation();
            operation.Release.TrySetResult(null);
            operations[channelRole.Trim()] = operation;
            return operation;
        }
    }

    private sealed class ControlledOperation
    {
        public ControlledOperation(Exception? failure = null)
        {
            Failure = failure;
        }

        public Exception? Failure { get; }
        public TaskCompletionSource<object?> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ThrowIfConfigured()
        {
            if (Failure is not null)
                throw Failure;
        }
    }

    private static ScanCalibrationProfileRepositorySnapshot CreateEmptySnapshot()
        => new(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase), null);

    private static ScanCalibrationProfileRepositorySnapshot CopySnapshot(ScanCalibrationProfileRepositorySnapshot snapshot)
        => new(
            snapshot.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            snapshot.SelectedChannel);
}
