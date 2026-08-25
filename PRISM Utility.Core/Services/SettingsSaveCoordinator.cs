namespace PRISM_Utility.Core.Services;

public enum SettingsSaveOperationStatus
{
    Succeeded,
    Failed,
    Canceled
}

public sealed record SettingsSaveOperationResult(
    Guid OwnerId,
    long OperationId,
    string Scope,
    SettingsSaveOperationStatus Status,
    Exception? Error);

public sealed class SettingsSaveOwnerHandle : IDisposable
{
    private readonly SettingsSaveCoordinator _coordinator;
    private int _disposed;

    internal SettingsSaveOwnerHandle(SettingsSaveCoordinator coordinator, Guid ownerId)
    {
        _coordinator = coordinator;
        OwnerId = ownerId;
    }

    public Guid OwnerId { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _coordinator.ReleaseOwner(OwnerId);
    }
}

public sealed class SettingsSaveCoordinator : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<SettingsSaveOperationKey, long> _latestOperationByScope = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _ownerCancellations = [];
    private readonly Dictionary<Guid, HashSet<Task<SettingsSaveOperationResult>>> _ownerResults = [];
    private readonly HashSet<Guid> _releasedOwners = [];
    private CancellationTokenSource _allOperationsCancellation = new();
    private Task _tail = Task.CompletedTask;
    private long _nextOperationId;
    private bool _disposed;

    public SettingsSaveOwnerHandle CreateOwner()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var ownerId = Guid.NewGuid();
            _ownerCancellations[ownerId] = new CancellationTokenSource();
            return new SettingsSaveOwnerHandle(this, ownerId);
        }
    }

    public Task<SettingsSaveOperationResult> EnqueueAsync(
        string scope,
        Func<CancellationToken, Task> saveAsync,
        CancellationToken cancellationToken = default)
        => EnqueueAsync(Guid.Empty, scope, saveAsync, cancellationToken);

    public Task<SettingsSaveOperationResult> EnqueueAsync(
        Guid ownerId,
        string scope,
        Func<CancellationToken, Task> saveAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(saveAsync);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var operationId = ++_nextOperationId;
            if (ownerId != Guid.Empty && _releasedOwners.Contains(ownerId))
            {
                return Task.FromResult(new SettingsSaveOperationResult(
                    ownerId,
                    operationId,
                    scope,
                    SettingsSaveOperationStatus.Canceled,
                    new OperationCanceledException()));
            }

            _latestOperationByScope[new SettingsSaveOperationKey(ownerId, scope)] = operationId;
            var ownerCancellation = GetOrCreateOwnerCancellationLocked(ownerId);
            var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _allOperationsCancellation.Token,
                ownerCancellation.Token,
                cancellationToken);
            var operation = new PendingSettingsSaveOperation(ownerId, operationId, scope, saveAsync, linkedCancellation);
            TrackOwnerResultLocked(ownerId, operation.ResultTask);

            var previous = _tail;
            _tail = RunLaneAsync(previous, operation);
            return operation.ResultTask;
        }
    }

    public void CancelPendingOperations(Guid ownerId)
    {
        CancellationTokenSource? cancellation = null;
        lock (_gate)
        {
            if (_disposed || !_ownerCancellations.TryGetValue(ownerId, out cancellation))
                return;

            _ownerCancellations[ownerId] = new CancellationTokenSource();
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    public void CancelPendingOperations()
    {
        CancellationTokenSource allCancellation;
        List<CancellationTokenSource> ownerCancellations;
        lock (_gate)
        {
            if (_disposed)
                return;

            allCancellation = _allOperationsCancellation;
            _allOperationsCancellation = new CancellationTokenSource();
            ownerCancellations = _ownerCancellations.Values.ToList();
            foreach (var ownerId in _ownerCancellations.Keys.ToArray())
                _ownerCancellations[ownerId] = new CancellationTokenSource();
        }

        allCancellation.Cancel();
        allCancellation.Dispose();
        foreach (var cancellation in ownerCancellations)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    public Task WhenIdleAsync()
    {
        lock (_gate)
            return _tail;
    }

    public Task WhenIdleAsync(Guid ownerId)
    {
        lock (_gate)
        {
            return _ownerResults.TryGetValue(ownerId, out var results)
                ? Task.WhenAll(results.ToArray())
                : Task.CompletedTask;
        }
    }

    public bool IsLatest(SettingsSaveOperationResult result)
    {
        lock (_gate)
        {
            return _latestOperationByScope.TryGetValue(new SettingsSaveOperationKey(result.OwnerId, result.Scope), out var latestOperationId)
                && latestOperationId == result.OperationId;
        }
    }

    internal void ReleaseOwner(Guid ownerId)
    {
        CancellationTokenSource? cancellation = null;
        lock (_gate)
        {
            if (!_ownerCancellations.Remove(ownerId, out cancellation))
                return;

            _releasedOwners.Add(ownerId);
            if (_ownerResults.TryGetValue(ownerId, out var results) && results.Count == 0)
                _ownerResults.Remove(ownerId);
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    internal int GetTrackedOwnerResultCount(Guid ownerId)
    {
        lock (_gate)
            return _ownerResults.TryGetValue(ownerId, out var results) ? results.Count : 0;
    }

    private CancellationTokenSource GetOrCreateOwnerCancellationLocked(Guid ownerId)
    {
        if (!_ownerCancellations.TryGetValue(ownerId, out var cancellation))
        {
            cancellation = new CancellationTokenSource();
            _ownerCancellations[ownerId] = cancellation;
        }

        return cancellation;
    }

    private void TrackOwnerResultLocked(Guid ownerId, Task<SettingsSaveOperationResult> resultTask)
    {
        if (!_ownerResults.TryGetValue(ownerId, out var results))
        {
            results = [];
            _ownerResults[ownerId] = results;
        }

        results.Add(resultTask);
        resultTask.GetAwaiter().OnCompleted(() => RemoveOwnerResult(ownerId, resultTask));
    }

    private void RemoveOwnerResult(Guid ownerId, Task<SettingsSaveOperationResult> resultTask)
    {
        _ = resultTask.Exception;
        lock (_gate)
        {
            if (_ownerResults.TryGetValue(ownerId, out var results))
            {
                results.Remove(resultTask);

                if (results.Count == 0 && !_ownerCancellations.ContainsKey(ownerId))
                    _ownerResults.Remove(ownerId);
            }
        }
    }

    private static async Task RunLaneAsync(Task previous, PendingSettingsSaveOperation operation)
    {
        try
        {
            operation.ConnectCancellation();
            await previous.ConfigureAwait(false);

            if (!operation.TryStart())
                return;

            var cancellationToken = operation.CancellationToken;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await operation.SaveAsync(cancellationToken).ConfigureAwait(false);
                operation.Succeed();
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken || cancellationToken.IsCancellationRequested)
            {
                operation.Cancel(ex);
            }
            catch (Exception ex)
            {
                operation.Fail(ex);
            }
        }
        finally
        {
            operation.Dispose();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource allCancellation;
        List<CancellationTokenSource> ownerCancellations;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            allCancellation = _allOperationsCancellation;
            ownerCancellations = _ownerCancellations.Values.ToList();
            _ownerCancellations.Clear();
            _ownerResults.Clear();
            _releasedOwners.Clear();
        }

        allCancellation.Cancel();
        allCancellation.Dispose();
        foreach (var cancellation in ownerCancellations)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private readonly record struct SettingsSaveOperationKey(Guid OwnerId, string Scope);

    private sealed class PendingSettingsSaveOperation : IDisposable
    {
        private readonly TaskCompletionSource<SettingsSaveOperationResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _cancellation;
        private readonly Func<CancellationToken, Task> _saveAsync;
        private CancellationTokenRegistration _cancellationRegistration;
        private int _state;

        public PendingSettingsSaveOperation(Guid ownerId, long operationId, string scope, Func<CancellationToken, Task> saveAsync, CancellationTokenSource cancellation)
        {
            OwnerId = ownerId;
            OperationId = operationId;
            Scope = scope;
            _saveAsync = saveAsync;
            _cancellation = cancellation;
        }

        public Guid OwnerId { get; }
        public long OperationId { get; }
        public string Scope { get; }
        public CancellationToken CancellationToken => _cancellation.Token;
        public Task<SettingsSaveOperationResult> ResultTask => _result.Task;
        public Task SaveAsync(CancellationToken cancellationToken) => _saveAsync(cancellationToken);

        public void ConnectCancellation()
            => _cancellationRegistration = CancellationToken.Register(static state => ((PendingSettingsSaveOperation)state!).CancelQueued(), this);

        public bool TryStart()
            => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

        public void Succeed()
            => _result.TrySetResult(new SettingsSaveOperationResult(OwnerId, OperationId, Scope, SettingsSaveOperationStatus.Succeeded, null));

        public void Cancel(Exception error)
            => _result.TrySetResult(new SettingsSaveOperationResult(OwnerId, OperationId, Scope, SettingsSaveOperationStatus.Canceled, error));

        public void Fail(Exception error)
            => _result.TrySetResult(new SettingsSaveOperationResult(OwnerId, OperationId, Scope, SettingsSaveOperationStatus.Failed, error));

        public void Dispose()
        {
            _cancellationRegistration.Dispose();
            _cancellation.Dispose();
        }

        private void CancelQueued()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
            {
                _result.TrySetResult(new SettingsSaveOperationResult(
                    OwnerId,
                    OperationId,
                    Scope,
                    SettingsSaveOperationStatus.Canceled,
                    new OperationCanceledException(CancellationToken)));
            }
        }
    }
}
