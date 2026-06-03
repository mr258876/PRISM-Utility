using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IAsyncDeviceLease : IAsyncDisposable
{
    string LeaseId { get; }

    ScannerSessionOwner Owner { get; }

    CancellationToken ReleaseToken { get; }

    ValueTask ReleaseAsync(CancellationToken ct = default);
}
