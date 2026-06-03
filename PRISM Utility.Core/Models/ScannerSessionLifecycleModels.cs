namespace PRISM_Utility.Core.Models;

[Flags]
public enum ScannerSessionObserverScope
{
    None = 0,
    SessionState = 1,
    DeviceCatalog = 2,
    Diagnostics = 4
}

public enum ScannerSessionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Running = 3,
    Faulted = 4,
    ReconnectPrompt = 5
}

public enum ScannerSessionOwnerType
{
    None = 0,
    ScanWorkflow = 1,
    ScanDebug = 2,
    UsbDebug = 3,
    Shutdown = 4,
    FaultRecovery = 5
}

public enum ScannerSessionOperation
{
    None = 0,
    Connect = 1,
    WarmUp = 2,
    Scan = 3,
    Calibration = 4,
    AutoFocus = 5,
    Diagnostics = 6,
    Disconnect = 7,
    Reconnect = 8,
    Shutdown = 9
}

public enum ScannerSessionFaultCode
{
    None = 0,
    DeviceDisconnected = 1,
    DeviceAccessLost = 2,
    CommandFailed = 3,
    TransferFailed = 4,
    UnexpectedError = 5
}

public sealed record ScannerSessionOwner(
    string OwnerId,
    ScannerSessionOwnerType OwnerType,
    ScannerSessionOperation Operation,
    DateTimeOffset AcquiredAtUtc,
    string LeaseId);

public sealed record ScannerSessionFault(
    ScannerSessionFaultCode Code,
    string Message,
    DateTimeOffset OccurredAtUtc,
    string? DeviceId = null,
    ScannerSessionState? PreviousState = null,
    ScannerSessionOwner? Owner = null);

public sealed record ScannerReconnectPromptState(
    bool RequiresConfirmation,
    string? DeviceId,
    ScannerSessionFault? Fault,
    ScannerSessionOwner? PreviousOwner,
    ScannerSessionOperation PreviousOperation,
    DateTimeOffset? PromptedAtUtc)
{
    public static ScannerReconnectPromptState None { get; } = new(
        RequiresConfirmation: false,
        DeviceId: null,
        Fault: null,
        PreviousOwner: null,
        PreviousOperation: ScannerSessionOperation.None,
        PromptedAtUtc: null);
}

public sealed record ScannerSessionObserverPermission(
    string ObserverId,
    ScannerSessionObserverScope Scope,
    DateTimeOffset GrantedAtUtc)
{
    public bool Allows(ScannerSessionObserverScope requestedScope)
        => requestedScope != ScannerSessionObserverScope.None
           && (Scope & requestedScope) == requestedScope;
}

public sealed record ScannerDeviceSessionSnapshot(
    ScannerSessionState State,
    string? DeviceId,
    ScannerSessionOwner? ActiveOwner,
    ScannerSessionFault? Fault,
    ScannerReconnectPromptState ReconnectPrompt,
    DateTimeOffset UpdatedAtUtc)
{
    public static ScannerDeviceSessionSnapshot Disconnected(DateTimeOffset updatedAtUtc, string? deviceId = null)
        => new(
            State: ScannerSessionState.Disconnected,
            DeviceId: deviceId,
            ActiveOwner: null,
            Fault: null,
            ReconnectPrompt: ScannerReconnectPromptState.None,
            UpdatedAtUtc: updatedAtUtc);

    public ScannerDeviceSessionSnapshot TransitionTo(
        ScannerSessionState nextState,
        DateTimeOffset updatedAtUtc,
        string? deviceId = null,
        ScannerSessionOwner? activeOwner = null,
        ScannerSessionFault? fault = null,
        ScannerReconnectPromptState? reconnectPrompt = null)
    {
        if (!CanTransition(State, nextState))
            throw new InvalidOperationException($"Cannot transition scanner session from {State} to {nextState}.");

        return new ScannerDeviceSessionSnapshot(
            State: nextState,
            DeviceId: deviceId ?? DeviceId,
            ActiveOwner: activeOwner,
            Fault: fault,
            ReconnectPrompt: reconnectPrompt ?? ScannerReconnectPromptState.None,
            UpdatedAtUtc: updatedAtUtc);
    }

    public static bool CanTransition(ScannerSessionState current, ScannerSessionState next)
    {
        if (current == next)
            return true;

        return current switch
        {
            ScannerSessionState.Disconnected => next == ScannerSessionState.Connecting,
            ScannerSessionState.Connecting => next is ScannerSessionState.Connected or ScannerSessionState.Disconnected or ScannerSessionState.Faulted,
            ScannerSessionState.Connected => next is ScannerSessionState.Running or ScannerSessionState.Disconnected or ScannerSessionState.Faulted,
            ScannerSessionState.Running => next is ScannerSessionState.Connected or ScannerSessionState.Disconnected or ScannerSessionState.Faulted,
            ScannerSessionState.Faulted => next is ScannerSessionState.ReconnectPrompt or ScannerSessionState.Disconnected,
            ScannerSessionState.ReconnectPrompt => next is ScannerSessionState.Connecting or ScannerSessionState.Connected or ScannerSessionState.Disconnected,
            _ => false
        };
    }
}
