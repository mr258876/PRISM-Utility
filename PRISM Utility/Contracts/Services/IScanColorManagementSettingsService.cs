using PRISM_Utility.Models;

namespace PRISM_Utility.Contracts.Services;

public interface IScanColorManagementSettingsService
{
    ScanColorManagementOptions DefaultSettings { get; }
    ScanColorManagementOptions Settings { get; }

    Task InitializeAsync();
    Task SetSettingsAsync(ScanColorManagementOptions settings);
}
