using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanCalibrationProfileRepository
{
    ScanCalibrationProfileRepositorySnapshot Snapshot { get; }
    Task InitializeAsync(CancellationToken ct);
    Task<ScanCalibrationProfileRepositorySnapshot> ReadAsync(CancellationToken ct);
    bool TryGetProfile(string channelRole, out ScanChannelCalibrationProfile profile);
    Task SaveProfileAsync(string channelRole, ScanChannelCalibrationProfile profile, CancellationToken ct);
    Task<bool> ClearProfileAsync(string channelRole, CancellationToken ct);
    Task ReplaceAsync(ScanCalibrationProfileRepositorySnapshot snapshot, CancellationToken ct);
    Task<string?> GetSelectedChannelAsync(CancellationToken ct);
    Task SetSelectedChannelAsync(string? channelRole, CancellationToken ct);

    async Task SetWhiteChannelSettingsAsync(WhiteChannelSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var current = await ReadAsync(ct);
        var profiles = current.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (settings.ActiveSettings?.Profile is { } profile)
            profiles["White"] = profile;
        else
            profiles.Remove("White");
        await ReplaceAsync(new(profiles, current.SelectedChannel, settings), ct);
    }
}
