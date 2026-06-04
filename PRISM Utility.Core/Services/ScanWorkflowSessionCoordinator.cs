using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanWorkflowSessionCoordinator : IScanWorkflowSessionCoordinator
{
    private const string OwnerId = "scan-workflow";
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IScannerDeviceSessionManager _sessionManager;

    public ScanWorkflowSessionCoordinator(IUsbUsageCoordinator usbUsageCoordinator, IScannerDeviceSessionManager sessionManager)
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

    public bool OwnsSnapshot(ScannerDeviceSessionSnapshot snapshot)
    {
        var activeOwner = snapshot.ActiveOwner;
        if (activeOwner is null)
            return false;

        return activeOwner.OwnerType == ScannerSessionOwnerType.ScanWorkflow
            && string.Equals(activeOwner.OwnerId, OwnerId, StringComparison.Ordinal);
    }

    public async Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
    {
        if (ConnectedSession is not null)
            return new ScanOperationResult(true, "Scanner session already connected.");

        if (IsConnectBlockedByUsbDebug())
            return new ScanOperationResult(false, "USB Debug currently owns scanner USB access.");

        var owner = CreateOwner(GetOperationForConnect());

        try
        {
            var result = owner.Operation == ScannerSessionOperation.Reconnect
                ? await _sessionManager.ReconnectAfterPromptAsync(owner, ct)
                : await _sessionManager.ConnectAsync(owner, ct);

            if (result.Success)
            {
                var session = _sessionManager.TryGetOwnedSession(owner.LeaseId);
                if (session is null)
                    return new ScanOperationResult(false, "Scanner connected but the shared workflow session was unavailable.");

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
        if (ConnectedSession is null)
            return new ScanOperationResult(true, "Scanner disconnected.");

        return await _sessionManager.DisconnectAsync(ct);
    }

    public Task<ScanStopResult> StopAsync(CancellationToken ct)
    {
        if (ConnectedSession is null)
            return Task.FromResult(new ScanStopResult(false, "Scanner not connected. Connect the scanner before stopping scan workflow."));

        return _sessionManager.StopAsync(CreateOwner(ScannerSessionOperation.Scan), ct);
    }

    public Task<TResult> UseConnectedSessionAsync<TResult>(Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ConnectedSession is null)
            throw new InvalidOperationException("Scanner not connected. Connect the scanner before issuing Scan workflow commands.");

        return _sessionManager.UseConnectedSessionAsync(
            CreateOwner(ScannerSessionOperation.Diagnostics),
            session => ExecuteWithSessionTokenAsync(session, action, ct),
            ct);
    }

    public Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionState state, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ConnectedSession is null)
            throw new InvalidOperationException("Scanner not connected. Connect the scanner before issuing Scan workflow commands.");

        return _sessionManager.RunConnectedSessionStateAsync(
            CreateOwner(state == ScannerSessionState.Running ? ScannerSessionOperation.Scan : ScannerSessionOperation.Diagnostics),
            state,
            session => ExecuteWithSessionTokenAsync(session, action, ct),
            ct);
    }

    private ScannerSessionOperation GetOperationForConnect()
        => _sessionManager.Snapshot.State == ScannerSessionState.ReconnectPrompt
           && _sessionManager.Snapshot.ReconnectPrompt.RequiresConfirmation
            ? ScannerSessionOperation.Reconnect
            : ScannerSessionOperation.Connect;

    private static ScannerSessionOwner CreateOwner(ScannerSessionOperation operation)
        => new(
            OwnerId,
            ScannerSessionOwnerType.ScanWorkflow,
            operation,
            DateTimeOffset.UtcNow,
            $"scan-workflow-{Guid.NewGuid():N}");

    private string BuildBusyMessage(string fallbackMessage)
    {
        var activeOwner = _sessionManager.Snapshot.ActiveOwner;
        if (activeOwner is null)
            return fallbackMessage;

        return activeOwner.OwnerType == ScannerSessionOwnerType.ScanWorkflow
            ? fallbackMessage
            : $"Scanner session is currently owned by '{activeOwner.OwnerId}' for '{activeOwner.Operation}'. Scan workflow cannot connect until that session disconnects.";
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
