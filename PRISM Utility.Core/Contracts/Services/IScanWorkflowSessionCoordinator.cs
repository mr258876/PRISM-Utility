using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanWorkflowSessionCoordinator
{
    bool IsConnectBlockedByUsbDebug();

    bool HasConnectedSession { get; }

    IScanSessionService? ConnectedSession { get; }

    bool OwnsSnapshot(ScannerDeviceSessionSnapshot snapshot);

    Task<ScanOperationResult> ConnectAsync(CancellationToken ct);

    Task<ScanOperationResult> DisconnectAsync(CancellationToken ct);

    Task<ScanStopResult> StopAsync(CancellationToken ct);

    Task<TResult> UseConnectedSessionAsync<TResult>(Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct);

    Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionState state, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct);
}
