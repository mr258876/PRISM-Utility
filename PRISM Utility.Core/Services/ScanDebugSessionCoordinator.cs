using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanDebugSessionCoordinator : IScanDebugSessionCoordinator
{
    private const string OwnerId = "scan-debug";
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IScannerDeviceSessionManager _sessionManager;

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
        => _sessionManager.TryGetConnectedSession();

    public async Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
    {
        if (ConnectedSession is not null)
            return new ScanOperationResult(true, "Scanner session already connected.");

        var owner = CreateOwner(ScannerSessionOperation.Connect);

        try
        {
            var result = await _sessionManager.ConnectAsync(owner, ct);
            if (result.Success)
            {
                var session = _sessionManager.TryGetOwnedSession(owner.LeaseId);
                if (session is null)
                    return new ScanOperationResult(false, "Scanner connected but the shared session was unavailable.");

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
        if (_sessionManager.Snapshot.State != ScannerSessionState.Connected)
            return new ScanOperationResult(false, "Scan Debug can disconnect only when the scanner is connected and idle.");

        return await _sessionManager.DisconnectAsync(ct);
    }

    public Task<ScanOperationResult> SetWarmUpAsync(bool enabled, CancellationToken ct)
    {
        if (ConnectedSession is null)
            return Task.FromResult(new ScanOperationResult(false, "Scanner not connected. Connect the scanner before changing warm-up state."));

        return _sessionManager.SetWarmUpEnabledAsync(CreateOwner(ScannerSessionOperation.WarmUp), enabled, ct);
    }

    public Task<TResult> UseConnectedSessionAsync<TResult>(Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ConnectedSession is null)
            throw new InvalidOperationException("Scanner not connected. Connect the scanner before issuing Scan Debug commands.");

        return _sessionManager.UseConnectedSessionAsync(
            CreateOwner(ScannerSessionOperation.Diagnostics),
            session => ExecuteWithSessionTokenAsync(session, action, ct),
            ct);
    }

    public Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionState state, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct, bool waitForAvailability = true)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ConnectedSession is null)
            throw new InvalidOperationException("Scanner not connected. Connect the scanner before issuing Scan Debug commands.");

        return _sessionManager.RunConnectedSessionStateAsync(
            CreateOwner(ScannerSessionOperation.Diagnostics),
            state,
            session => ExecuteWithSessionTokenAsync(session, action, ct),
            ct,
            waitForAvailability);
    }

    private static ScannerSessionOwner CreateOwner(ScannerSessionOperation operation)
        => new(
            OwnerId,
            ScannerSessionOwnerType.ScanDebug,
            operation,
            DateTimeOffset.UtcNow,
            $"scan-debug-{Guid.NewGuid():N}");

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
