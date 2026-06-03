using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanDebugSessionCoordinator : IScanDebugSessionCoordinator
{
    private const string OwnerId = "scan-debug";
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IScannerDeviceSessionManager _sessionManager;
    private ScannerSessionOwner? _owner;

    public ScanDebugSessionCoordinator(IUsbUsageCoordinator usbUsageCoordinator, IScannerDeviceSessionManager sessionManager)
    {
        _usbUsageCoordinator = usbUsageCoordinator;
        _sessionManager = sessionManager;
    }

    public bool IsConnectBlockedByUsbDebug()
        => _usbUsageCoordinator.IsUsbDebugInUse;

    public bool HasConnectedSession
        => ConnectedSession is not null;

    public IScanSessionService? ConnectedSession
        => _owner is null ? null : _sessionManager.TryGetOwnedSession(_owner.LeaseId);

    public async Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
    {
        if (ConnectedSession is not null)
            return new ScanOperationResult(true, "Scanner session already connected.");

        var activeOwner = _sessionManager.Snapshot.ActiveOwner;
        if (activeOwner is not null && activeOwner.OwnerType != ScannerSessionOwnerType.ScanDebug)
            return new ScanOperationResult(false, BuildBusyMessage("Scanner session is already owned by another workflow."));

        var owner = new ScannerSessionOwner(
            OwnerId,
            ScannerSessionOwnerType.ScanDebug,
            ScannerSessionOperation.Connect,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));

        try
        {
            var result = await _sessionManager.ConnectAsync(owner, ct);
            if (result.Success)
            {
                var session = _sessionManager.TryGetOwnedSession(owner.LeaseId);
                if (session is null)
                    return new ScanOperationResult(false, "Scanner connected but the shared session was unavailable.");

                _owner = owner;
            }

            if (!result.Success && IsOwnershipConflict(result.Message))
                return new ScanOperationResult(false, BuildBusyMessage(result.Message));

            return result;
        }
        catch (InvalidOperationException ex)
        {
            return new ScanOperationResult(false, BuildBusyMessage(ex.Message));
        }
    }

    public async Task<ScanOperationResult> DisconnectAsync(CancellationToken ct)
    {
        if (_owner is null || ConnectedSession is null)
        {
            _owner = null;
            return new ScanOperationResult(true, "Scanner disconnected.");
        }

        var result = await _sessionManager.DisconnectAsync(_owner.LeaseId, ct);
        if (result.Success)
            _owner = null;

        return result;
    }

    public Task<ScanOperationResult> SetWarmUpAsync(bool enabled, CancellationToken ct)
    {
        if (_owner is null || ConnectedSession is null)
            return Task.FromResult(new ScanOperationResult(false, "Scanner not connected. Connect the scanner before changing warm-up state."));

        return _sessionManager.SetWarmUpEnabledAsync(_owner.LeaseId, enabled, ct);
    }

    public Task<TResult> UseConnectedSessionAsync<TResult>(Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_owner is null || ConnectedSession is null)
            throw new InvalidOperationException("Scanner not connected. Connect the scanner before issuing Scan Debug commands.");

        return _sessionManager.UseSessionAsync(
            _owner.LeaseId,
            session => ExecuteWithSessionTokenAsync(session, action, ct),
            ct);
    }

    public Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionState state, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_owner is null || ConnectedSession is null)
            throw new InvalidOperationException("Scanner not connected. Connect the scanner before issuing Scan Debug commands.");

        return _sessionManager.RunWithSessionStateAsync(
            _owner.LeaseId,
            state,
            session => ExecuteWithSessionTokenAsync(session, action, ct),
            ct);
    }

    private string BuildBusyMessage(string fallbackMessage)
    {
        var activeOwner = _sessionManager.Snapshot.ActiveOwner;
        if (activeOwner is null)
            return $"{fallbackMessage} Scan Debug is read-only until the active scanner owner disconnects.";

        return activeOwner.OwnerType == ScannerSessionOwnerType.ScanDebug
            ? fallbackMessage
            : $"Scanner session is currently owned by '{activeOwner.OwnerId}' for '{activeOwner.Operation}'. Scan Debug is read-only until that session disconnects.";
    }

    private static bool IsOwnershipConflict(string message)
        => !string.IsNullOrWhiteSpace(message)
           && message.Contains("already owned", StringComparison.OrdinalIgnoreCase);

    private static async Task<TResult> ExecuteWithSessionTokenAsync<TResult>(IScanSessionService session, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, session.ConnectionToken);
        return await action(session, linkedCts.Token);
    }
}
