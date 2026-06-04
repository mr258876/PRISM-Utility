using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class UsbUsageCoordinator : IUsbUsageCoordinator
{
    private const string LegacyScanOperation = "Legacy scan ownership";
    private const string LegacyUsbOperation = "Legacy raw USB ownership";

    private readonly object _gate = new();
    private readonly Dictionary<string, IUsbUsageLease> _legacyLeases = new(StringComparer.OrdinalIgnoreCase);
    private ActiveLeaseState? _activeLease;

    public event EventHandler<UsbUsageLeaseSnapshot?>? ActiveLeaseChanged;

    public bool IsScanDebugInUse
    {
        get
        {
            lock (_gate)
                return _activeLease?.Snapshot.OwnerType == UsbUsageOwnerType.Scanner;
        }
    }

    public bool IsUsbDebugInUse
    {
        get
        {
            lock (_gate)
                return _activeLease?.Snapshot.OwnerType == UsbUsageOwnerType.RawUsb;
        }
    }

    public UsbUsageLeaseSnapshot? ActiveLease
    {
        get
        {
            lock (_gate)
                return _activeLease?.Snapshot;
        }
    }

    public ValueTask<UsbUsageLeaseAcquireResult> TryAcquireLeaseAsync(string ownerId, UsbUsageOwnerType ownerType, string operation, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Owner id is required.", nameof(ownerId));

        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));

        UsbUsageLeaseAcquireResult result;
        lock (_gate)
        {
            if (_activeLease is not null)
            {
                return ValueTask.FromResult(new UsbUsageLeaseAcquireResult(
                    false,
                    null,
                    _activeLease.Snapshot,
                    $"USB usage is already owned by '{_activeLease.Snapshot.OwnerId}' for '{_activeLease.Snapshot.Operation}'."));
            }

            var cancellationSource = new CancellationTokenSource();
            var snapshot = new UsbUsageLeaseSnapshot(
                ownerId,
                ownerType,
                operation,
                DateTimeOffset.UtcNow,
                Guid.NewGuid());

            var lease = new UsbUsageLease(this, snapshot, cancellationSource.Token);
            _activeLease = new ActiveLeaseState(snapshot, cancellationSource, lease);

            result = new UsbUsageLeaseAcquireResult(true, lease, snapshot, string.Empty);
        }

        PublishActiveLeaseChanged(result.ActiveLease);
        return ValueTask.FromResult(result);
    }

    public ValueTask<bool> ReleaseAsync(Guid releaseToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return ReleaseCore(releaseToken);
    }

    public ValueTask<bool> ForceReleaseAsync(string ownerId, UsbUsageOwnerType ownerType, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        CancellationTokenSource? cancellationSource = null;
        Guid releaseToken;
        lock (_gate)
        {
            if (_activeLease is null)
                return ValueTask.FromResult(false);

            if (!string.Equals(_activeLease.Snapshot.OwnerId, ownerId, StringComparison.Ordinal)
                || _activeLease.Snapshot.OwnerType != ownerType)
            {
                return ValueTask.FromResult(false);
            }

            cancellationSource = _activeLease.CancellationSource;
            releaseToken = _activeLease.Snapshot.ReleaseToken;
        }

        cancellationSource.Cancel();
        return ReleaseCore(releaseToken);
    }

    public bool CanObserveReadOnly(string ownerId, UsbUsageOwnerType ownerType)
    {
        _ = ownerId;
        _ = ownerType;
        return true;
    }

    public void SetScanDebugInUse(bool inUse, string callerFilePath = "")
        => SetLegacyUsage(inUse, callerFilePath, UsbUsageOwnerType.Scanner, LegacyScanOperation);

    public void SetUsbDebugInUse(bool inUse, string callerFilePath = "")
        => SetLegacyUsage(inUse, callerFilePath, UsbUsageOwnerType.RawUsb, LegacyUsbOperation);

    private void SetLegacyUsage(bool inUse, string callerFilePath, UsbUsageOwnerType ownerType, string operation)
    {
        var ownerId = BuildLegacyOwnerId(callerFilePath, ownerType);
        if (inUse)
        {
            lock (_gate)
            {
                if (_legacyLeases.ContainsKey(ownerId))
                    return;
            }

            var acquireResult = TryAcquireLeaseAsync(ownerId, ownerType, operation).GetAwaiter().GetResult();
            if (!acquireResult.Success || acquireResult.Lease is null)
                return;

            lock (_gate)
            {
                if (_legacyLeases.ContainsKey(ownerId))
                {
                    _ = acquireResult.Lease.ReleaseAsync().GetAwaiter().GetResult();
                    return;
                }

                _legacyLeases[ownerId] = acquireResult.Lease;
            }

            return;
        }

        IUsbUsageLease? legacyLease;
        lock (_gate)
        {
            if (!_legacyLeases.TryGetValue(ownerId, out legacyLease))
                return;

            _legacyLeases.Remove(ownerId);
        }

        _ = legacyLease.ReleaseAsync().GetAwaiter().GetResult();
    }

    private void RemoveLegacyLeaseReference(IUsbUsageLease lease)
    {
        var keysToRemove = new List<string>();
        foreach (var entry in _legacyLeases)
        {
            if (ReferenceEquals(entry.Value, lease))
                keysToRemove.Add(entry.Key);
        }

        foreach (var key in keysToRemove)
            _legacyLeases.Remove(key);
    }

    private static string BuildLegacyOwnerId(string callerFilePath, UsbUsageOwnerType ownerType)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(callerFilePath) ? "unknown-caller" : callerFilePath.Replace('\\', '/');
        return $"legacy:{ownerType}:{normalizedPath}";
    }

    private ValueTask<bool> ReleaseCore(Guid releaseToken)
    {
        CancellationTokenSource? cancellationSource = null;
        lock (_gate)
        {
            if (_activeLease is null || _activeLease.Snapshot.ReleaseToken != releaseToken)
                return ValueTask.FromResult(false);

            cancellationSource = _activeLease.CancellationSource;
            RemoveLegacyLeaseReference(_activeLease.Lease);
            _activeLease = null;
        }

        cancellationSource.Dispose();
        PublishActiveLeaseChanged(null);
        return ValueTask.FromResult(true);
    }

    private void PublishActiveLeaseChanged(UsbUsageLeaseSnapshot? snapshot)
        => ActiveLeaseChanged?.Invoke(this, snapshot);

    private sealed record ActiveLeaseState(
        UsbUsageLeaseSnapshot Snapshot,
        CancellationTokenSource CancellationSource,
        IUsbUsageLease Lease);

    private sealed class UsbUsageLease : IUsbUsageLease
    {
        private readonly UsbUsageCoordinator _coordinator;
        private readonly CancellationToken _cancellationToken;
        private int _releaseState;

        public UsbUsageLease(UsbUsageCoordinator coordinator, UsbUsageLeaseSnapshot snapshot, CancellationToken cancellationToken)
        {
            _coordinator = coordinator;
            _cancellationToken = cancellationToken;
            OwnerId = snapshot.OwnerId;
            OwnerType = snapshot.OwnerType;
            Operation = snapshot.Operation;
            AcquiredAt = snapshot.AcquiredAt;
            ReleaseToken = snapshot.ReleaseToken;
        }

        public string OwnerId { get; }

        public UsbUsageOwnerType OwnerType { get; }

        public string Operation { get; }

        public DateTimeOffset AcquiredAt { get; }

        public Guid ReleaseToken { get; }

        public CancellationToken CancellationToken => _cancellationToken;

        public ValueTask<bool> ReleaseAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (Interlocked.Exchange(ref _releaseState, 1) != 0)
                return ValueTask.FromResult(false);

            return _coordinator.ReleaseAsync(ReleaseToken, ct);
        }

        public void Dispose()
            => _ = ReleaseAsync().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync()
            => _ = await ReleaseAsync();
    }
}
