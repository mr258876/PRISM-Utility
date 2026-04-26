using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Contracts.Services;

public interface IScanDebugSessionCoordinator
{
    bool IsConnectBlockedByUsbDebug();

    Task<ScanOperationResult> ConnectAsync(IScanSessionService session, CancellationToken ct);

    Task<ScanOperationResult> DisconnectAsync(IScanSessionService session, CancellationToken ct);

    Task<ScanOperationResult> SetWarmUpAsync(IScanSessionService session, bool enabled, CancellationToken ct);
}
