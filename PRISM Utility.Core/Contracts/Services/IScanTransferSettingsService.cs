using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanTransferSettingsService
{
    event EventHandler? BulkInReadModeChanged;

    ScanBulkInReadMode BulkInReadMode { get; }
    ScanBulkInTransferOptions DefaultSettings { get; }
    ScanBulkInTransferOptions Settings { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetBulkInReadModeAsync(ScanBulkInReadMode mode, CancellationToken cancellationToken = default);
    Task SetSettingsAsync(ScanBulkInTransferOptions settings, CancellationToken cancellationToken = default);
}
