using System.Runtime.CompilerServices;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IUsbUsageCoordinator
{
    event EventHandler<UsbUsageLeaseSnapshot?>? ActiveLeaseChanged;

    bool IsScanDebugInUse { get; }

    bool IsUsbDebugInUse { get; }

    UsbUsageLeaseSnapshot? ActiveLease { get; }

    ValueTask<UsbUsageLeaseAcquireResult> TryAcquireLeaseAsync(string ownerId, UsbUsageOwnerType ownerType, string operation, CancellationToken ct = default);

    ValueTask<bool> ReleaseAsync(Guid releaseToken, CancellationToken ct = default);

    ValueTask<bool> ForceReleaseAsync(string ownerId, UsbUsageOwnerType ownerType, CancellationToken ct = default);

    bool CanObserveReadOnly(string ownerId, UsbUsageOwnerType ownerType);

    void SetScanDebugInUse(bool inUse, [CallerFilePath] string callerFilePath = "");

    void SetUsbDebugInUse(bool inUse, [CallerFilePath] string callerFilePath = "");
}
