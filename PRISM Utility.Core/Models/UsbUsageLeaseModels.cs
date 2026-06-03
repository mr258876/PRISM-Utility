using PRISM_Utility.Core.Contracts.Services;

namespace PRISM_Utility.Core.Models;

public enum UsbUsageOwnerType
{
    Scanner,
    RawUsb
}

public sealed record UsbUsageLeaseSnapshot(
    string OwnerId,
    UsbUsageOwnerType OwnerType,
    string Operation,
    DateTimeOffset AcquiredAt,
    Guid ReleaseToken);

public sealed record UsbUsageLeaseAcquireResult(
    bool Success,
    IUsbUsageLease? Lease,
    UsbUsageLeaseSnapshot? ActiveLease,
    string Message);
