using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScannerAccessCoordinator
{
    event EventHandler<ScannerAccessSnapshot>? SnapshotChanged;

    ScannerAccessSnapshot Snapshot { get; }

    bool CanActivate(ScannerAccessMode mode);

    bool CanDeactivate(ScannerAccessMode mode);

    Task<ScanOperationResult> ActivateAsync(ScannerAccessMode mode, CancellationToken ct);

    Task<ScanOperationResult> DeactivateAsync(ScannerAccessMode mode, CancellationToken ct);
}
