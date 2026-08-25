using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Services;

public sealed class LocalSettingsScanCalibrationProfileStorage : IScanCalibrationProfileStorage
{
    private const string DocumentKey = "ScanCalibrationProfileSettingsDocument";
    private const string ProfilesKey = "ScanChannelParameterProfiles";
    private const string SelectedChannelKey = "ScanCalibrationSelectedChannel";

    private readonly ILocalSettingsService _localSettings;

    public LocalSettingsScanCalibrationProfileStorage(ILocalSettingsService localSettings)
    {
        _localSettings = localSettings;
    }

    public Task<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>?> ReadDocumentAsync(CancellationToken ct)
        => _localSettings.ReadSettingAsync<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>>(DocumentKey);

    public Task SaveDocumentAsync(VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload> document, CancellationToken ct)
        => _localSettings.SaveSettingAsync(DocumentKey, document);

    // The versioned document is authoritative; legacy keys remain inert because ILocalSettingsService has no removal operation.
    public Task<Dictionary<string, ScanChannelCalibrationProfile>?> ReadCurrentProfilesAsync(CancellationToken ct)
        => _localSettings.ReadSettingAsync<Dictionary<string, ScanChannelCalibrationProfile>>(ProfilesKey);

    public Task<Dictionary<string, ScanParameterSnapshot>?> ReadLegacyProfilesAsync(CancellationToken ct)
        => _localSettings.ReadSettingAsync<Dictionary<string, ScanParameterSnapshot>>(ProfilesKey);

    public Task<string?> ReadSelectedChannelAsync(CancellationToken ct)
        => _localSettings.ReadSettingAsync<string>(SelectedChannelKey);

    public Task SaveCurrentProfilesAsync(Dictionary<string, ScanChannelCalibrationProfile> profiles, CancellationToken ct)
        => _localSettings.SaveSettingAsync(ProfilesKey, profiles);

    public Task SaveSelectedChannelAsync(string? selectedChannel, CancellationToken ct)
        => _localSettings.SaveSettingAsync(SelectedChannelKey, selectedChannel);
}
