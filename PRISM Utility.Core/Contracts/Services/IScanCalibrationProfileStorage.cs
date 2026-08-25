using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanCalibrationProfileStorage
{
    Task<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>?> ReadDocumentAsync(CancellationToken ct);
    Task SaveDocumentAsync(VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload> document, CancellationToken ct);

    Task<Dictionary<string, ScanChannelCalibrationProfile>?> ReadCurrentProfilesAsync(CancellationToken ct);
    Task<Dictionary<string, ScanParameterSnapshot>?> ReadLegacyProfilesAsync(CancellationToken ct);
    Task<string?> ReadSelectedChannelAsync(CancellationToken ct);
    Task SaveCurrentProfilesAsync(Dictionary<string, ScanChannelCalibrationProfile> profiles, CancellationToken ct);
    Task SaveSelectedChannelAsync(string? selectedChannel, CancellationToken ct);
}
