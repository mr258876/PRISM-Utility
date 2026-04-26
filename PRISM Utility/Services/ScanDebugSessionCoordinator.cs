using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Services;

public sealed class ScanDebugSessionCoordinator : IScanDebugSessionCoordinator
{
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;

    public ScanDebugSessionCoordinator(IUsbUsageCoordinator usbUsageCoordinator)
    {
        _usbUsageCoordinator = usbUsageCoordinator;
    }

    public bool IsConnectBlockedByUsbDebug()
        => _usbUsageCoordinator.IsUsbDebugInUse;

    public async Task<ScanOperationResult> ConnectAsync(IScanSessionService session, CancellationToken ct)
    {
        var result = await session.ConnectAsync(ct);
        if (result.Success)
            _usbUsageCoordinator.SetScanDebugInUse(true);

        return result;
    }

    public async Task<ScanOperationResult> DisconnectAsync(IScanSessionService session, CancellationToken ct)
    {
        await session.DisconnectAsync();
        _usbUsageCoordinator.SetScanDebugInUse(false);
        return new ScanOperationResult(true, "Scanner disconnected.");
    }

    public Task<ScanOperationResult> SetWarmUpAsync(IScanSessionService session, bool enabled, CancellationToken ct)
        => session.SetWarmUpEnabledAsync(enabled, ct);
}
