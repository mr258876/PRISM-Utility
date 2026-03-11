using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanTransferSettingsService
{
    event EventHandler? BulkInReadModeChanged;

    ScanBulkInReadMode BulkInReadMode { get; }
    ScanBulkInTransferOptions DefaultSettings { get; }
    ScanBulkInTransferOptions Settings { get; }

    Task InitializeAsync();

    Task SetBulkInReadModeAsync(ScanBulkInReadMode mode);
    Task SetSettingsAsync(ScanBulkInTransferOptions settings);
}
