using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScannerDeviceSessionManager
{
    event EventHandler<ScannerDeviceSessionSnapshot>? SnapshotChanged;

    event EventHandler? TargetsChanged;

    ScannerDeviceSessionSnapshot Snapshot { get; }

    ScanTargetState Targets { get; }

    IScanSessionService? TryGetConnectedSession();

    IScanSessionService? TryGetOwnedSession(string leaseId);

    ValueTask<IAsyncDeviceLease> AcquireLeaseAsync(ScannerSessionOwner owner, CancellationToken ct);

    Task<ScanOperationResult> ConnectAsync(ScannerSessionOwner owner, CancellationToken ct);

    Task<ScanOperationResult> ReconnectAfterPromptAsync(ScannerSessionOwner owner, CancellationToken ct);

    Task<ScanOperationResult> DisconnectAsync(CancellationToken ct);

    Task<ScanOperationResult> DisconnectAsync(string leaseId, CancellationToken ct);

    Task<ScanOperationResult> ShutdownAsync(CancellationToken ct);

    Task<ScanStopResult> StopAsync(string leaseId, CancellationToken ct);

    Task<ScanStopResult> StopAsync(ScannerSessionOwner owner, CancellationToken ct);

    Task<ScanOperationResult> SetWarmUpEnabledAsync(string leaseId, bool enabled, CancellationToken ct);

    Task<ScanOperationResult> SetWarmUpEnabledAsync(ScannerSessionOwner owner, bool enabled, CancellationToken ct);

    Task<TResult> UseSessionAsync<TResult>(string leaseId, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct);

    Task<TResult> UseConnectedSessionAsync<TResult>(ScannerSessionOwner owner, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct);

    Task<TResult> RunWithSessionStateAsync<TResult>(string leaseId, ScannerSessionState state, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct);

    Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionOwner owner, ScannerSessionState state, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct);

    ScannerSessionObserverPermission GrantObserverPermission(
        string observerId,
        ScannerSessionObserverScope requestedScope,
        DateTimeOffset grantedAtUtc);
}
