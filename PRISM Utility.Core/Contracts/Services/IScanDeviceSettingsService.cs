using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanDeviceSettingsService
{
    ScanDeviceSettings DefaultSettings { get; }

    ScanDeviceSettings Settings { get; }

    Task InitializeAsync();

    Task SetSettingsAsync(ScanDeviceSettings settings);
}
