using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanDebugSessionCoordinator
{
    bool IsConnectBlockedByUsbDebug();

    bool HasConnectedSession { get; }

    IScanSessionService? ConnectedSession { get; }

    Task<ScanOperationResult> ConnectAsync(CancellationToken ct);

    Task<ScanOperationResult> DisconnectAsync(CancellationToken ct);

    Task<ScanOperationResult> SetWarmUpAsync(bool enabled, CancellationToken ct);

    Task<TResult> UseConnectedSessionAsync<TResult>(Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct);

    Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionState state, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct);
}
