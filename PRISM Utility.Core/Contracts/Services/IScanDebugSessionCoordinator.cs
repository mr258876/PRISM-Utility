using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanDebugSessionCoordinator
{
    event EventHandler<ScannerDeviceSessionSnapshot>? SnapshotChanged
    {
        add { }
        remove { }
    }

    bool IsConnectBlockedByUsbDebug();

    ScannerDeviceSessionSnapshot Snapshot { get; }

    bool HasConnectedSession { get; }

    IScanSessionService? ConnectedSession { get; }

    Task<ScanOperationResult> ConnectAsync(CancellationToken ct);

    Task<ScanOperationResult> DisconnectAsync(CancellationToken ct);

    Task<ScanOperationResult> SetWarmUpAsync(bool enabled, CancellationToken ct);

    Task<ScanOperationResult> StopAllMotionAsync(CancellationToken ct)
        => Task.FromResult(new ScanOperationResult(false, "Global motor stop is unavailable from this coordinator."));

    Task<TResult> UseConnectedSessionAsync<TResult>(Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct);

    Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionState state, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct, bool waitForAvailability = true);
}
