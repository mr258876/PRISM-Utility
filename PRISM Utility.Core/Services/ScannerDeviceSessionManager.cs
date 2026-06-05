using System.Threading;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScannerDeviceSessionManager : IScannerDeviceSessionManager, IAsyncDisposable
{
    private static readonly TimeSpan DefaultShutdownCleanupTimeout = TimeSpan.FromSeconds(10);

    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private readonly IScanSessionServiceFactory _sessionFactory;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly TimeSpan _shutdownCleanupTimeout;

    private ScannerDeviceSessionSnapshot _snapshot = ScannerDeviceSessionSnapshot.Disconnected(DateTimeOffset.UtcNow);
    private OwnershipContext? _ownership;
    private PendingReconnectContext? _pendingReconnect;
    private IScanSessionService? _session;
    private bool _disposed;

    public ScannerDeviceSessionManager(
        IScanSessionServiceFactory sessionFactory,
        IUsbUsageCoordinator usbUsageCoordinator,
        TimeSpan? shutdownCleanupTimeout = null)
    {
        _sessionFactory = sessionFactory;
        _usbUsageCoordinator = usbUsageCoordinator;
        _shutdownCleanupTimeout = shutdownCleanupTimeout ?? DefaultShutdownCleanupTimeout;
    }

    public event EventHandler<ScannerDeviceSessionSnapshot>? SnapshotChanged;

    public event EventHandler? TargetsChanged;

    public ScannerDeviceSessionSnapshot Snapshot
    {
        get
        {
            lock (_stateGate)
                return _snapshot;
        }
    }

    public ScanTargetState Targets => EnsureSession().Targets;

    public IScanSessionService? TryGetConnectedSession()
    {
        ThrowIfDisposed();

        lock (_stateGate)
        {
            return _session?.IsConnected == true ? _session : null;
        }
    }

    public IScanSessionService? TryGetOwnedSession(string leaseId)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("Lease id is required.", nameof(leaseId));

        lock (_stateGate)
        {
            return _ownership?.Owner.LeaseId == leaseId && _session?.IsConnected == true
                ? _session
                : null;
        }
    }

    public async ValueTask<IAsyncDeviceLease> AcquireLeaseAsync(ScannerSessionOwner owner, CancellationToken ct)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(owner.OwnerId))
            throw new ArgumentException("Owner id is required.", nameof(owner));

        if (string.IsNullOrWhiteSpace(owner.LeaseId))
            throw new ArgumentException("Lease id is required.", nameof(owner));

        OwnershipContext? existingOwnership;
        lock (_stateGate)
        {
            existingOwnership = _ownership;
            if (existingOwnership is not null)
            {
                if (existingOwnership.Owner.LeaseId == owner.LeaseId)
                    return new AsyncDeviceLease(this, existingOwnership);

                throw new InvalidOperationException($"Scanner session is already owned by '{existingOwnership.Owner.OwnerId}' for '{existingOwnership.Owner.Operation}'.");
            }
        }

        var acquireResult = await _usbUsageCoordinator.TryAcquireLeaseAsync(
            owner.OwnerId,
            MapUsbOwnerType(owner.OwnerType),
            DescribeOperation(owner.Operation),
            ct);

        if (!acquireResult.Success || acquireResult.Lease is null)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(acquireResult.Message) ? "Scanner ownership could not be acquired." : acquireResult.Message);

        OwnershipContext? competingOwnership = null;
        var ownership = new OwnershipContext(owner, acquireResult.Lease);
        ScannerDeviceSessionSnapshot? snapshot = null;

        lock (_stateGate)
        {
            if (_ownership is not null)
            {
                competingOwnership = _ownership;
            }
            else
            {
                _ownership = ownership;
                _snapshot = UpdateSnapshotCore(_snapshot, _snapshot.State, DateTimeOffset.UtcNow, _snapshot.DeviceId, owner, _snapshot.Fault, _snapshot.ReconnectPrompt);
                snapshot = _snapshot;
            }
        }

        if (competingOwnership is not null)
        {
            await acquireResult.Lease.ReleaseAsync(CancellationToken.None);
            throw new InvalidOperationException($"Scanner session is already owned by '{competingOwnership.Owner.OwnerId}' for '{competingOwnership.Owner.Operation}'.");
        }

        PublishSnapshot(snapshot!);
        return new AsyncDeviceLease(this, ownership);
    }

    public Task<ScanOperationResult> ConnectAsync(ScannerSessionOwner owner, CancellationToken ct)
        => ConnectCoreAsync(owner, ct, requiresReconnectConfirmation: false);

    public Task<ScanOperationResult> ReconnectAfterPromptAsync(ScannerSessionOwner owner, CancellationToken ct)
        => ConnectCoreAsync(owner, ct, requiresReconnectConfirmation: true);

    private async Task<ScanOperationResult> ConnectCoreAsync(ScannerSessionOwner owner, CancellationToken ct, bool requiresReconnectConfirmation)
    {
        var mutation = await TryBeginMutationAsync(ct);
        if (mutation is null)
            return BusyOperationResult(requiresReconnectConfirmation ? "Reconnect" : "Connect");

        await using (mutation)
        {
            ThrowIfDisposed();

            if (!requiresReconnectConfirmation && Snapshot.State == ScannerSessionState.ReconnectPrompt && Snapshot.ReconnectPrompt.RequiresConfirmation)
                return new ScanOperationResult(false, "Reconnect confirmation is required before reconnecting the scanner.");

            if (requiresReconnectConfirmation)
            {
                var reconnectPrompt = Snapshot.ReconnectPrompt;
                if (Snapshot.State != ScannerSessionState.ReconnectPrompt || !reconnectPrompt.RequiresConfirmation)
                    return new ScanOperationResult(false, "Reconnect confirmation is not currently required.");

                owner = owner with
                {
                    Operation = ScannerSessionOperation.Reconnect,
                    AcquiredAtUtc = DateTimeOffset.UtcNow
                };
            }

            try
            {
                var session = EnsureSession();

                if (session.IsConnected)
                    return new ScanOperationResult(true, "Scanner session already connected.");

                _ = await EnsureOwnershipAsync(owner, ct);

                PublishSnapshot(UpdateSnapshot(ScannerSessionState.Connecting, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));

                var result = await session.ConnectAsync(ct);
                if (!result.Success)
                    return await HandleFaultResultAsync(
                        ClassifyConnectFailure(result.Message),
                        result.Message,
                        owner,
                        ScannerSessionState.Connecting,
                        ResolveDeviceId(session.Targets),
                        allowReconnectPromptOnRedetection: false,
                        result.Message,
                        CancellationToken.None);

                ClearPendingReconnect();
                PublishSnapshot(UpdateSnapshot(ScannerSessionState.Connected, ResolveDeviceId(session.Targets), null, null, ScannerReconnectPromptState.None));
                return result;
            }
            catch (InvalidOperationException ex)
            {
                return new ScanOperationResult(false, ex.Message);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                var session = GetSession();
                var deviceId = ResolveDeviceId(session);
                var ownerSnapshot = GetActiveOwner() ?? owner;
                return await HandleFaultResultAsync(
                    ClassifyOperationCancellation(session),
                    string.IsNullOrWhiteSpace(ex.Message) ? "Scanner connection was canceled unexpectedly." : ex.Message,
                    ownerSnapshot,
                    Snapshot.State,
                    deviceId,
                    allowReconnectPromptOnRedetection: true,
                    $"Connect failed: {ex.Message}",
                    CancellationToken.None);
            }
            catch (TimeoutException ex)
            {
                var session = GetSession();
                var deviceId = ResolveDeviceId(session);
                var ownerSnapshot = GetActiveOwner() ?? owner;
                return await HandleFaultResultAsync(
                    ScannerSessionFaultCode.TransferFailed,
                    ex.Message,
                    ownerSnapshot,
                    Snapshot.State,
                    deviceId,
                    allowReconnectPromptOnRedetection: false,
                    $"Connect failed: {ex.Message}",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                var ownerSnapshot = GetActiveOwner() ?? owner;
                var session = GetSession();
                var previousDeviceId = ResolveDeviceId(session);
                return await HandleFaultResultAsync(
                    ScannerSessionFaultCode.UnexpectedError,
                    ex.Message,
                    ownerSnapshot,
                    Snapshot.State,
                    previousDeviceId,
                    allowReconnectPromptOnRedetection: false,
                    $"Connect failed: {ex.Message}",
                    CancellationToken.None);
            }
        }
    }

    public async Task<ScanOperationResult> DisconnectAsync(string leaseId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("Lease id is required.", nameof(leaseId));

        var mutation = await TryBeginMutationAsync(ct);
        if (mutation is null)
            return BusyOperationResult("Disconnect");

        await using (mutation)
        {
            ThrowIfDisposed();
            if (!TryEnsureOwnedLease(leaseId, out _))
                return new ScanOperationResult(false, "Scanner session is not owned by the supplied lease.");

            await DisconnectAndReleaseAsync(releaseOwnership: true, ct);
            ClearPendingReconnect();
            PublishSnapshot(UpdateSnapshot(ScannerSessionState.Disconnected, Snapshot.DeviceId, null, Snapshot.Fault, ScannerReconnectPromptState.None));
            return new ScanOperationResult(true, "Scanner disconnected.");
        }
    }

    public async Task<ScanOperationResult> DisconnectAsync(CancellationToken ct)
    {
        var mutation = await TryBeginMutationAsync(ct);
        if (mutation is null)
            return BusyOperationResult("Disconnect");

        await using (mutation)
        {
            ThrowIfDisposed();
            if (Snapshot.State != ScannerSessionState.Connected || TryGetConnectedSession() is null)
                return new ScanOperationResult(false, "Scanner can disconnect only when the scanner is connected and idle.");

            await DisconnectAndReleaseAsync(releaseOwnership: true, ct);
            ClearPendingReconnect();
            PublishSnapshot(UpdateSnapshot(ScannerSessionState.Disconnected, Snapshot.DeviceId, null, Snapshot.Fault, ScannerReconnectPromptState.None));
            return new ScanOperationResult(true, "Scanner disconnected.");
        }
    }

    public async Task<ScanOperationResult> ShutdownAsync(CancellationToken ct)
    {
        ThrowIfDisposed();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_shutdownCleanupTimeout);

        try
        {
            var mutation = await TryBeginMutationAsync(timeoutCts.Token, waitForAvailability: true);
            if (mutation is null)
                return new ScanOperationResult(false, "Scanner shutdown cleanup could not start.");

            await using (mutation)
            {
                await DisconnectAndReleaseAsync(releaseOwnership: true, timeoutCts.Token).WaitAsync(timeoutCts.Token);
                ClearPendingReconnect();
                PublishSnapshot(UpdateSnapshot(ScannerSessionState.Disconnected, Snapshot.DeviceId, null, Snapshot.Fault, ScannerReconnectPromptState.None));
                return new ScanOperationResult(true, "Scanner shutdown cleanup completed.");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ScanOperationResult(false, $"Scanner shutdown cleanup timed out after {_shutdownCleanupTimeout.TotalSeconds:0} seconds.");
        }
    }

    public async Task<ScanStopResult> StopAsync(string leaseId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("Lease id is required.", nameof(leaseId));

        var mutation = await TryBeginMutationAsync(ct);
        if (mutation is null)
            return new ScanStopResult(false, "Scanner manager is busy running another mutating command.");

        await using (mutation)
        {
            ThrowIfDisposed();

            if (!TryEnsureOwnedConnectedSession(leaseId, out var session))
                return new ScanStopResult(false, "Scanner session is not owned by the supplied lease.");

            ScanStopResult result;
            var owner = CreateOperationOwner(ScannerSessionOperation.Scan);
            try
            {
                PublishSnapshot(UpdateSnapshot(Snapshot.State, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));
                result = await session.StopScanAsync(ct);
            }
            catch (TimeoutException ex)
            {
                await HandleFaultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, GetActiveOwner(), Snapshot.State, ResolveDeviceId(session.Targets), false, CancellationToken.None);
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await HandleFaultAsync(ClassifyOperationCancellation(session), "Scanner stop was canceled because the session ended unexpectedly.", GetActiveOwner(), Snapshot.State, ResolveDeviceId(session.Targets), true, CancellationToken.None);
                throw;
            }
            finally
            {
                if (IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }

            if (result.Success)
                PublishSnapshot(UpdateSnapshot(ScannerSessionState.Connected, ResolveDeviceId(session.Targets), null, null, ScannerReconnectPromptState.None));

            return result;
        }
    }

    public async Task<ScanStopResult> StopAsync(ScannerSessionOwner owner, CancellationToken ct)
    {
        var mutation = await TryBeginMutationAsync(ct);
        if (mutation is null)
            return new ScanStopResult(false, "Scanner manager is busy running another mutating command.");

        await using (mutation)
        {
            ThrowIfDisposed();

            var session = EnsureConnectedSession();
            ScanStopResult result;
            try
            {
                PublishSnapshot(UpdateSnapshot(Snapshot.State, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));
                result = await session.StopScanAsync(ct);
            }
            catch (TimeoutException ex)
            {
                await HandleFaultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, owner, Snapshot.State, ResolveDeviceId(session.Targets), false, CancellationToken.None);
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await HandleFaultAsync(ClassifyOperationCancellation(session), "Scanner stop was canceled because the session ended unexpectedly.", owner, Snapshot.State, ResolveDeviceId(session.Targets), true, CancellationToken.None);
                throw;
            }
            finally
            {
                if (IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }

            return result;
        }
    }

    public async Task<ScanOperationResult> SetWarmUpEnabledAsync(string leaseId, bool enabled, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("Lease id is required.", nameof(leaseId));

        var mutation = await TryBeginMutationAsync(ct);
        if (mutation is null)
            return BusyOperationResult("Warm-up");

        await using (mutation)
        {
            ThrowIfDisposed();

            if (!TryEnsureOwnedConnectedSession(leaseId, out var session))
                return new ScanOperationResult(false, "Scanner session is not owned by the supplied lease.");

            var owner = CreateOperationOwner(ScannerSessionOperation.WarmUp);
            PublishSnapshot(UpdateSnapshot(Snapshot.State, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));
            try
            {
                return await session.SetWarmUpEnabledAsync(enabled, ct);
            }
            catch (TimeoutException ex)
            {
                return await HandleFaultResultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, GetActiveOwner(), Snapshot.State, ResolveDeviceId(session.Targets), false, ex.Message, CancellationToken.None);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return await HandleFaultResultAsync(ClassifyOperationCancellation(session), "Scanner warm-up change was canceled because the session ended unexpectedly.", GetActiveOwner(), Snapshot.State, ResolveDeviceId(session.Targets), true, "Scanner warm-up change was canceled because the session ended unexpectedly.", CancellationToken.None);
            }
            finally
            {
                if (IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }
        }
    }

    public async Task<ScanOperationResult> SetWarmUpEnabledAsync(ScannerSessionOwner owner, bool enabled, CancellationToken ct)
    {
        var mutation = await TryBeginMutationAsync(ct);
        if (mutation is null)
            return BusyOperationResult("Warm-up");

        await using (mutation)
        {
            ThrowIfDisposed();

            var session = EnsureConnectedSession();
            PublishSnapshot(UpdateSnapshot(Snapshot.State, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));
            try
            {
                return await session.SetWarmUpEnabledAsync(enabled, ct);
            }
            catch (TimeoutException ex)
            {
                return await HandleFaultResultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, owner, Snapshot.State, ResolveDeviceId(session.Targets), false, ex.Message, CancellationToken.None);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return await HandleFaultResultAsync(ClassifyOperationCancellation(session), "Scanner warm-up change was canceled because the session ended unexpectedly.", owner, Snapshot.State, ResolveDeviceId(session.Targets), true, "Scanner warm-up change was canceled because the session ended unexpectedly.", CancellationToken.None);
            }
            finally
            {
                if (IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }
        }
    }

    public async Task<TResult> UseSessionAsync<TResult>(string leaseId, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("Lease id is required.", nameof(leaseId));
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();

        var mutation = await TryBeginMutationAsync(ct, waitForAvailability: true);
        if (mutation is null)
            throw new InvalidOperationException("Scanner session action could not acquire the mutation gate.");

        await using (mutation)
        {
            var session = EnsureOwnedConnectedSession(leaseId);
            var owner = CreateOperationOwner(ScannerSessionOperation.Diagnostics);
            PublishSnapshot(UpdateSnapshot(Snapshot.State, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));

            try
            {
                return await action(session);
            }
            catch (TimeoutException ex)
            {
                await HandleFaultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, GetActiveOwner(), Snapshot.State, ResolveDeviceId(session.Targets), false, CancellationToken.None);
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await HandleFaultAsync(ClassifyOperationCancellation(session), "Scanner session action was canceled because the device session ended unexpectedly.", GetActiveOwner(), Snapshot.State, ResolveDeviceId(session.Targets), true, CancellationToken.None);
                throw;
            }
            finally
            {
                if (IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }
        }
    }

    public async Task<TResult> UseConnectedSessionAsync<TResult>(ScannerSessionOwner owner, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();

        var mutation = await TryBeginMutationAsync(ct, waitForAvailability: true);
        if (mutation is null)
            throw new InvalidOperationException("Scanner session action could not acquire the mutation gate.");

        await using (mutation)
        {
            var session = EnsureConnectedSession();
            PublishSnapshot(UpdateSnapshot(Snapshot.State, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));

            try
            {
                return await action(session);
            }
            catch (TimeoutException ex)
            {
                await HandleFaultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, owner, Snapshot.State, ResolveDeviceId(session.Targets), false, CancellationToken.None);
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await HandleFaultAsync(ClassifyOperationCancellation(session), "Scanner session action was canceled because the device session ended unexpectedly.", owner, Snapshot.State, ResolveDeviceId(session.Targets), true, CancellationToken.None);
                throw;
            }
            finally
            {
                if (IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }
        }
    }

    public async Task<TResult> RunWithSessionStateAsync<TResult>(string leaseId, ScannerSessionState state, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("Lease id is required.", nameof(leaseId));
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();

        var mutation = await TryBeginMutationAsync(ct, waitForAvailability: true);
        if (mutation is null)
            throw new InvalidOperationException("Scanner session action could not acquire the mutation gate.");

        await using (mutation)
        {
            var session = EnsureOwnedConnectedSession(leaseId);
            var owner = CreateOperationOwner(state == ScannerSessionState.Running ? ScannerSessionOperation.Scan : ScannerSessionOperation.Diagnostics);
            if (session.IsConnected)
                PublishSnapshot(UpdateSnapshot(state, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));

            try
            {
                return await action(session);
            }
            catch (TimeoutException ex)
            {
                await HandleFaultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, owner, state, ResolveDeviceId(session.Targets), false, CancellationToken.None);
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await HandleFaultAsync(ClassifyOperationCancellation(session), "Scanner operation was canceled because the device session ended unexpectedly.", owner, state, ResolveDeviceId(session.Targets), true, CancellationToken.None);
                throw;
            }
            finally
            {
                if (owner is not null && IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }
        }
    }

    public async Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionOwner owner, ScannerSessionState state, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct, bool waitForAvailability = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();

        var mutation = await TryBeginMutationAsync(ct, waitForAvailability: waitForAvailability);
        if (mutation is null)
            throw new InvalidOperationException("Scanner session is busy. Wait for the current scanner operation to finish before starting another scan.");

        await using (mutation)
        {
            var session = EnsureConnectedSession();
            PublishSnapshot(UpdateSnapshot(state, ResolveDeviceId(session.Targets), owner, null, ScannerReconnectPromptState.None));

            try
            {
                return await action(session);
            }
            catch (TimeoutException ex)
            {
                await HandleFaultAsync(ScannerSessionFaultCode.TransferFailed, ex.Message, owner, state, ResolveDeviceId(session.Targets), false, CancellationToken.None);
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await HandleFaultAsync(ClassifyOperationCancellation(session), "Scanner session action was canceled because the device session ended unexpectedly.", owner, state, ResolveDeviceId(session.Targets), true, CancellationToken.None);
                throw;
            }
            finally
            {
                if (IsActiveOwner(owner))
                    PublishSnapshot(UpdateSnapshot(session.IsConnected ? ScannerSessionState.Connected : ScannerSessionState.Disconnected, ResolveDeviceId(session.Targets), null, Snapshot.Fault, Snapshot.ReconnectPrompt));
            }
        }
    }

    public ScannerSessionObserverPermission GrantObserverPermission(string observerId, ScannerSessionObserverScope requestedScope, DateTimeOffset grantedAtUtc)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(observerId))
            throw new ArgumentException("Observer id is required.", nameof(observerId));

        var grantedScope = _usbUsageCoordinator.CanObserveReadOnly(observerId, UsbUsageOwnerType.RawUsb)
            ? requestedScope
            : ScannerSessionObserverScope.None;

        return new ScannerSessionObserverPermission(observerId, grantedScope, grantedAtUtc);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        var mutation = await TryBeginMutationAsync(CancellationToken.None, allowDisposed: true);
        if (mutation is not null)
        {
            await using (mutation)
            {
                await DisconnectAndReleaseAsync(releaseOwnership: true, CancellationToken.None);
                PublishSnapshot(UpdateSnapshot(ScannerSessionState.Disconnected, Snapshot.DeviceId, null, null, ScannerReconnectPromptState.None));
            }
        }

        _mutationGate.Dispose();
    }

    private async Task HandleTargetsChangedAsync(IScanSessionService session)
    {
        var mutation = await TryBeginMutationAsync(CancellationToken.None, waitForAvailability: true);
        if (mutation is null)
            return;

        await using (mutation)
        {
            if (!ReferenceEquals(session, GetSession()))
                return;

            var deviceId = ResolveDeviceId(session.Targets);
            if (session.IsConnected && !session.Targets.IsDevicesPresent)
            {
                await HandleFaultAsync(
                    ScannerSessionFaultCode.DeviceDisconnected,
                    "Scanner device connection was lost.",
                    GetActiveOwner(),
                    Snapshot.State,
                    deviceId,
                    allowReconnectPromptOnRedetection: true,
                    CancellationToken.None);
                return;
            }

            if (!session.IsConnected
                && Snapshot.State == ScannerSessionState.Faulted
                && _pendingReconnect is not null
                && session.Targets.IsDevicesPresent)
            {
                var reconnect = _pendingReconnect;
                PublishSnapshot(UpdateSnapshot(
                    ScannerSessionState.ReconnectPrompt,
                    ResolveReconnectDeviceId(session.Targets, reconnect),
                    null,
                    reconnect.Fault,
                    new ScannerReconnectPromptState(true, ResolveReconnectDeviceId(session.Targets, reconnect), reconnect.Fault, reconnect.PreviousOwner, reconnect.PreviousOperation, DateTimeOffset.UtcNow)));
                return;
            }

            PublishSnapshot(UpdateSnapshot(Snapshot.State, deviceId, Snapshot.ActiveOwner, Snapshot.Fault, Snapshot.ReconnectPrompt));
        }
    }

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
    {
        TargetsChanged?.Invoke(this, EventArgs.Empty);

        if (sender is IScanSessionService session)
            _ = HandleTargetsChangedAsync(session);
    }

    private IScanSessionService EnsureSession()
    {
        lock (_stateGate)
        {
            if (_session is not null)
                return _session;
        }

        var session = _sessionFactory.CreateSession();
        session.TargetsChanged += OnSessionTargetsChanged;

        lock (_stateGate)
        {
            if (_session is null)
            {
                _session = session;
                return session;
            }
        }

        session.TargetsChanged -= OnSessionTargetsChanged;
        session.Dispose();
        return GetSession()!;
    }

    private IScanSessionService? GetSession()
    {
        lock (_stateGate)
            return _session;
    }

    private IScanSessionService EnsureOwnedConnectedSession(string leaseId)
    {
        if (!TryEnsureOwnedConnectedSession(leaseId, out var session))
            throw new InvalidOperationException("Scanner session is not owned by the supplied lease.");

        return session;
    }

    private IScanSessionService EnsureConnectedSession()
        => TryGetConnectedSession() ?? throw new InvalidOperationException("Scanner not connected. Connect the scanner before issuing commands.");

    private bool TryEnsureOwnedLease(string leaseId, out OwnershipContext? ownership)
    {
        lock (_stateGate)
        {
            ownership = _ownership;
            return ownership is not null && string.Equals(ownership.Owner.LeaseId, leaseId, StringComparison.Ordinal);
        }
    }

    private bool TryEnsureOwnedConnectedSession(string leaseId, out IScanSessionService session)
    {
        lock (_stateGate)
        {
            if (_ownership is not null
                && string.Equals(_ownership.Owner.LeaseId, leaseId, StringComparison.Ordinal)
                && _session is not null
                && _session.IsConnected)
            {
                session = _session;
                return true;
            }
        }

        session = null!;
        return false;
    }

    private ScannerSessionOwner? GetActiveOwner()
    {
        lock (_stateGate)
            return _ownership?.Owner;
    }

    private bool IsActiveOwner(ScannerSessionOwner? owner)
    {
        if (owner is null)
            return false;

        var activeOwner = Snapshot.ActiveOwner;
        return activeOwner is not null
            && string.Equals(activeOwner.LeaseId, owner.LeaseId, StringComparison.Ordinal)
            && activeOwner.Operation == owner.Operation;
    }

    private ScannerSessionOwner? CreateOperationOwner(ScannerSessionOperation operation)
    {
        var owner = GetActiveOwner();
        return owner is null
            ? null
            : owner with
            {
                Operation = operation,
                AcquiredAtUtc = DateTimeOffset.UtcNow
            };
    }

    private async Task<OwnershipResolution> EnsureOwnershipAsync(ScannerSessionOwner owner, CancellationToken ct)
    {
        lock (_stateGate)
        {
            if (_ownership is not null)
            {
                if (_ownership.Owner.LeaseId == owner.LeaseId)
                    return new OwnershipResolution(_ownership, false);

                throw new InvalidOperationException($"Scanner session is already owned by '{_ownership.Owner.OwnerId}' for '{_ownership.Owner.Operation}'.");
            }
        }

        var lease = await AcquireLeaseAsync(owner, ct);
        return new OwnershipResolution(((AsyncDeviceLease)lease).Context, true);
    }

    private async Task DisconnectAndReleaseAsync(bool releaseOwnership, CancellationToken ct)
    {
        IScanSessionService? session;
        OwnershipContext? ownership;
        Exception? cleanupException = null;

        lock (_stateGate)
        {
            session = _session;
            _session = null;
            ownership = releaseOwnership ? _ownership : null;
            if (releaseOwnership)
                _ownership = null;
        }

        if (session is not null)
        {
            session.TargetsChanged -= OnSessionTargetsChanged;

            try
            {
                await session.DisconnectAsync();
            }
            catch (Exception ex)
            {
                cleanupException = ex;
            }

            try
            {
                await session.DisposeAsync();
            }
            catch (Exception ex) when (cleanupException is null)
            {
                cleanupException = ex;
            }
        }

        if (ownership is not null && ownership.TryBeginRelease())
        {
            await ownership.UsbLease.ReleaseAsync(CancellationToken.None);
            ownership.MarkReleased();
        }

        if (cleanupException is not null)
            throw cleanupException;
    }

    private async ValueTask<bool> ReleaseLeaseAsync(OwnershipContext context, CancellationToken ct)
    {
        var mutation = await TryBeginMutationAsync(ct, waitForAvailability: true);
        if (mutation is null)
            return false;

        await using (mutation)
        {
            if (!context.TryBeginRelease())
                return false;

            var shouldDisconnect = false;
            lock (_stateGate)
            {
                if (ReferenceEquals(_ownership, context))
                {
                    shouldDisconnect = true;
                    _ownership = null;
                }
            }

            try
            {
                if (shouldDisconnect)
                {
                    Exception? cleanupException = null;
                    try
                    {
                        await DisconnectAndReleaseAsync(releaseOwnership: false, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        cleanupException = ex;
                    }

                    await context.UsbLease.ReleaseAsync(CancellationToken.None);
                    PublishSnapshot(UpdateSnapshot(ScannerSessionState.Disconnected, Snapshot.DeviceId, null, null, ScannerReconnectPromptState.None));

                    if (cleanupException is not null)
                        throw cleanupException;

                    return true;
                }

                await context.UsbLease.ReleaseAsync(CancellationToken.None);
                return true;
            }
            finally
            {
                context.MarkReleased();
            }
        }
    }

    private async ValueTask<MutationLease?> TryBeginMutationAsync(CancellationToken ct, bool waitForAvailability = false, bool allowDisposed = false)
    {
        if (!allowDisposed)
            ThrowIfDisposed();

        if (waitForAvailability)
        {
            await _mutationGate.WaitAsync(ct);
            return new MutationLease(_mutationGate);
        }

        return await _mutationGate.WaitAsync(0, ct)
            ? new MutationLease(_mutationGate)
            : null;
    }

    private ScannerDeviceSessionSnapshot UpdateSnapshot(
        ScannerSessionState state,
        string? deviceId,
        ScannerSessionOwner? activeOwner,
        ScannerSessionFault? fault,
        ScannerReconnectPromptState reconnectPrompt)
    {
        lock (_stateGate)
        {
            _snapshot = UpdateSnapshotCore(_snapshot, state, DateTimeOffset.UtcNow, deviceId, activeOwner, fault, reconnectPrompt);
            return _snapshot;
        }
    }

    private static ScannerDeviceSessionSnapshot UpdateSnapshotCore(
        ScannerDeviceSessionSnapshot current,
        ScannerSessionState state,
        DateTimeOffset updatedAtUtc,
        string? deviceId,
        ScannerSessionOwner? activeOwner,
        ScannerSessionFault? fault,
        ScannerReconnectPromptState reconnectPrompt)
    {
        if (current.State == state)
        {
            return current with
            {
                DeviceId = deviceId ?? current.DeviceId,
                ActiveOwner = activeOwner,
                Fault = fault,
                ReconnectPrompt = reconnectPrompt,
                UpdatedAtUtc = updatedAtUtc
            };
        }

        return current.TransitionTo(state, updatedAtUtc, deviceId, activeOwner, fault, reconnectPrompt);
    }

    private static void PublishSnapshot(ScannerDeviceSessionSnapshot snapshot, EventHandler<ScannerDeviceSessionSnapshot>? handler)
        => handler?.Invoke(null, snapshot);

    private void PublishSnapshot(ScannerDeviceSessionSnapshot snapshot)
        => SnapshotChanged?.Invoke(this, snapshot);

    private static ScanOperationResult BusyOperationResult(string operation)
        => new(false, $"Scanner manager is busy running another mutating command. {operation} was rejected.");

    private async Task<ScanOperationResult> HandleFaultResultAsync(
        ScannerSessionFaultCode code,
        string message,
        ScannerSessionOwner? owner,
        ScannerSessionState previousState,
        string? deviceId,
        bool allowReconnectPromptOnRedetection,
        string resultMessage,
        CancellationToken cleanupToken)
    {
        await HandleFaultAsync(code, message, owner, previousState, deviceId, allowReconnectPromptOnRedetection, cleanupToken);
        return new ScanOperationResult(false, resultMessage);
    }

    private async Task HandleFaultAsync(
        ScannerSessionFaultCode code,
        string message,
        ScannerSessionOwner? owner,
        ScannerSessionState previousState,
        string? deviceId,
        bool allowReconnectPromptOnRedetection,
        CancellationToken cleanupToken)
    {
        var fault = BuildFault(code, message, owner, previousState, deviceId);
        PublishSnapshot(UpdateSnapshot(ScannerSessionState.Faulted, deviceId, null, fault, ScannerReconnectPromptState.None));

        try
        {
            await DisconnectAndReleaseAsync(releaseOwnership: true, cleanupToken);
        }
        finally
        {
            if (allowReconnectPromptOnRedetection)
            {
                _pendingReconnect = new PendingReconnectContext(fault, owner, owner?.Operation ?? ScannerSessionOperation.None);
                EnsureSession();
            }
            else
            {
                ClearPendingReconnect();
                PublishSnapshot(UpdateSnapshot(ScannerSessionState.Disconnected, deviceId, null, fault, ScannerReconnectPromptState.None));
            }
        }
    }

    private void ClearPendingReconnect()
        => _pendingReconnect = null;

    private static UsbUsageOwnerType MapUsbOwnerType(ScannerSessionOwnerType ownerType)
        => ownerType == ScannerSessionOwnerType.UsbDebug ? UsbUsageOwnerType.RawUsb : UsbUsageOwnerType.Scanner;

    private static string DescribeOperation(ScannerSessionOperation operation)
        => operation switch
        {
            ScannerSessionOperation.Connect => "Connect scanner",
            ScannerSessionOperation.WarmUp => "Warm-up scanner",
            ScannerSessionOperation.Scan => "Scan film",
            ScannerSessionOperation.Calibration => "Run calibration",
            ScannerSessionOperation.AutoFocus => "Run autofocus",
            ScannerSessionOperation.Diagnostics => "Run diagnostics",
            ScannerSessionOperation.Disconnect => "Disconnect scanner",
            ScannerSessionOperation.Reconnect => "Reconnect scanner",
            ScannerSessionOperation.Shutdown => "Shutdown scanner session",
            _ => "Manage scanner session"
        };

    private static string? ResolveDeviceId(ScanTargetState targets)
        => targets.BulkOutDeviceId ?? targets.BulkInDeviceId;

    private static string? ResolveDeviceId(IScanSessionService? session)
        => session is null ? null : ResolveDeviceId(session.Targets);

    private static string? ResolveReconnectDeviceId(ScanTargetState targets, PendingReconnectContext reconnect)
        => ResolveDeviceId(targets) ?? reconnect.Fault.DeviceId;

    private static ScannerSessionFaultCode ClassifyConnectFailure(string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("disappeared") || normalized.Contains("not detected"))
            return ScannerSessionFaultCode.DeviceDisconnected;

        if (normalized.Contains("access") || normalized.Contains("busy"))
            return ScannerSessionFaultCode.DeviceAccessLost;

        return ScannerSessionFaultCode.CommandFailed;
    }

    private static ScannerSessionFaultCode ClassifyOperationCancellation(IScanSessionService? session)
        => session?.ConnectionToken.IsCancellationRequested == true
            ? ScannerSessionFaultCode.DeviceAccessLost
            : ScannerSessionFaultCode.TransferFailed;

    private static ScannerSessionFault BuildFault(
        ScannerSessionFaultCode code,
        string message,
        ScannerSessionOwner? owner,
        ScannerSessionState previousState,
        string? deviceId)
        => new(code, message, DateTimeOffset.UtcNow, deviceId, previousState, owner);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ScannerDeviceSessionManager));
    }

    private sealed record OwnershipResolution(OwnershipContext Context, bool Created);

    private sealed record PendingReconnectContext(
        ScannerSessionFault Fault,
        ScannerSessionOwner? PreviousOwner,
        ScannerSessionOperation PreviousOperation);

    private sealed class OwnershipContext
    {
        private int _releaseState;

        public OwnershipContext(ScannerSessionOwner owner, IUsbUsageLease usbLease)
        {
            Owner = owner;
            UsbLease = usbLease;
        }

        public ScannerSessionOwner Owner { get; }

        public IUsbUsageLease UsbLease { get; }

        public bool TryBeginRelease()
            => Interlocked.CompareExchange(ref _releaseState, 1, 0) == 0;

        public void MarkReleased()
            => Interlocked.Exchange(ref _releaseState, 2);
    }

    private sealed class AsyncDeviceLease : IAsyncDeviceLease
    {
        private readonly ScannerDeviceSessionManager _manager;

        public AsyncDeviceLease(ScannerDeviceSessionManager manager, OwnershipContext context)
        {
            _manager = manager;
            Context = context;
        }

        internal OwnershipContext Context { get; }

        public string LeaseId => Context.Owner.LeaseId;

        public ScannerSessionOwner Owner => Context.Owner;

        public CancellationToken ReleaseToken => Context.UsbLease.CancellationToken;

        public ValueTask ReleaseAsync(CancellationToken ct = default)
            => new(_manager.ReleaseLeaseAsync(Context, ct).AsTask());

        public ValueTask DisposeAsync()
            => ReleaseAsync();
    }

    private sealed class MutationLease : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate;
        private bool _released;

        public MutationLease(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
