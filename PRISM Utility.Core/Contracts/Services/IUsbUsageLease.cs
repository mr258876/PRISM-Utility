using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IUsbUsageLease : IAsyncDisposable, IDisposable
{
    string OwnerId { get; }

    UsbUsageOwnerType OwnerType { get; }

    string Operation { get; }

    DateTimeOffset AcquiredAt { get; }

    Guid ReleaseToken { get; }

    CancellationToken CancellationToken { get; }

    ValueTask<bool> ReleaseAsync(CancellationToken ct = default);
}
