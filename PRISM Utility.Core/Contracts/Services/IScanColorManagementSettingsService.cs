using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanColorManagementSettingsService
{
    ScanColorManagementOptions DefaultSettings { get; }
    ScanColorManagementOptions Settings { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SetSettingsAsync(ScanColorManagementOptions settings, CancellationToken cancellationToken = default);
}
