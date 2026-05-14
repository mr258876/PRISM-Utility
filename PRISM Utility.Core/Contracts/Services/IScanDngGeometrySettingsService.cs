using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanDngGeometrySettingsService
{
    ScanDngGeometrySettings DefaultSettings { get; }

    ScanDngGeometrySettings Settings { get; }

    Task InitializeAsync();

    Task SetSettingsAsync(ScanDngGeometrySettings settings);
}
