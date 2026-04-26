using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;

namespace PRISM_Utility.Contracts.Services;

public interface IScanChannelParameterProfileService
{
    Task InitializeAsync();

    IReadOnlyDictionary<string, ScanChannelCalibrationProfile> Profiles { get; }

    bool TryGetProfile(string channelRole, out ScanChannelCalibrationProfile profile);

    Task SaveProfileAsync(string channelRole, ScanChannelCalibrationProfile profile);

    Task<bool> ClearProfileAsync(string channelRole);

    Task ExportProfilesAsync(ScanFilmParameterProfileSet profileSet);

    Task<ScanFilmParameterProfileSet?> ImportProfilesAsync();

    Task ReplaceProfilesAsync(ScanFilmParameterProfileSet profileSet);

    Task<string?> GetSelectedCalibrationChannelAsync();

    Task SetSelectedCalibrationChannelAsync(string channelRole);
}
