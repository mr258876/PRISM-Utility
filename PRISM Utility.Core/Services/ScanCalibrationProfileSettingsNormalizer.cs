using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal static class ScanCalibrationProfileSettingsNormalizer
{
    public static Dictionary<string, ScanChannelCalibrationProfile> NormalizeLoadedProfiles(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? source)
    {
        var normalizedProfiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
            return normalizedProfiles;

        foreach (var pair in source)
        {
            var role = NormalizeRole(pair.Key);
            if (!string.IsNullOrEmpty(role) && TryNormalizeProfile(pair.Value, out var profile))
                normalizedProfiles[role] = profile;
        }

        return normalizedProfiles;
    }

    public static Dictionary<string, ScanChannelCalibrationProfile> MigrateLegacyProfiles(
        IReadOnlyDictionary<string, ScanParameterSnapshot>? legacyProfiles)
    {
        var migratedProfiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        if (legacyProfiles is null)
            return migratedProfiles;

        foreach (var pair in legacyProfiles)
        {
            var role = NormalizeRole(pair.Key);
            if (!string.IsNullOrEmpty(role)
                && pair.Value is not null
                && ScanDebugValidation.TryNormalizeSnapshot(pair.Value, out var parameters))
            {
                migratedProfiles[role] = new ScanChannelCalibrationProfile(
                    parameters,
                    ScanCalibrationRoiSettings.CreateDefault().Normalize());
            }
        }

        return migratedProfiles;
    }

    public static bool TryNormalizeDocumentPayload(
        ScanCalibrationProfileSettingsPayload? payload,
        out Dictionary<string, ScanChannelCalibrationProfile> profiles,
        out string? selectedChannel)
    {
        profiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        selectedChannel = null;
        if (payload?.Profiles is null)
            return false;

        foreach (var pair in payload.Profiles)
        {
            var role = NormalizeRole(pair.Key);
            if (string.IsNullOrEmpty(role)
                || !TryNormalizeProfile(pair.Value, out var profile)
                || !profiles.TryAdd(role, profile))
                return false;
        }

        selectedChannel = NormalizeSelectedChannel(payload.SelectedChannel);
        return true;
    }

    public static Dictionary<string, ScanChannelCalibrationProfile> NormalizeReplacementProfiles(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var normalizedProfiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            var role = RequireRole(pair.Key);
            if (!TryValidateProfile(pair.Value, out var profile))
                throw new ArgumentOutOfRangeException(nameof(source), "Calibration profile contains unsupported scan parameters.");
            if (!normalizedProfiles.TryAdd(role, profile))
                throw new ArgumentException("Calibration profile roles must be unique ignoring case.", nameof(source));
        }

        return normalizedProfiles;
    }

    public static bool TryNormalizeProfile(ScanChannelCalibrationProfile? profile, out ScanChannelCalibrationProfile normalized)
    {
        if (TryValidateProfile(profile, out normalized))
            return true;

        if (profile is null
            || profile.Parameters is null
            || !ScanDebugValidation.TryNormalizeSnapshot(profile.Parameters, out var parameters))
        {
            normalized = CreateDefaultProfile();
            return false;
        }

        var whiteLevel = profile.WhiteLevel is > 0 ? profile.WhiteLevel : null;
        var blackLevel = profile.BlackLevel;
        if (blackLevel is not null && whiteLevel is not null && blackLevel >= whiteLevel)
            blackLevel = (ushort)(whiteLevel.Value - 1);

        normalized = new ScanChannelCalibrationProfile(
            parameters,
            (profile.RoiSettings ?? ScanCalibrationRoiSettings.CreateDefault()).Normalize(),
            blackLevel,
            whiteLevel);
        return true;
    }

    public static bool TryValidateProfile(ScanChannelCalibrationProfile? profile, out ScanChannelCalibrationProfile validated)
    {
        if (profile is null
            || profile.Parameters is null
            || !ScanDebugValidation.TryNormalizeSnapshot(profile.Parameters, out _)
            || !ScanAdcCalibrationRoi.TryCreate(profile.RoiSettings, ScanDebugConstants.DecodedPixelsPerLine).IsValid
            || !ScanFocusRoi.TryCreate(profile.RoiSettings, ScanDebugConstants.DecodedPixelsPerLine).IsValid
            || profile.WhiteLevel is 0
            || (profile.BlackLevel is not null && profile.WhiteLevel is not null && profile.BlackLevel >= profile.WhiteLevel))
        {
            validated = CreateDefaultProfile();
            return false;
        }

        validated = profile;
        return true;
    }

    public static bool LooksLikeLegacySnapshots(IReadOnlyDictionary<string, ScanChannelCalibrationProfile> profiles)
        => profiles.Count > 0 && profiles.Values.All(profile => profile?.Parameters is null);

    public static Dictionary<string, ScanChannelCalibrationProfile> CopyProfiles(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile> source)
        => source.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    public static ScanChannelCalibrationProfile CreateDefaultProfile()
        => new(
            new ScanParameterSnapshot(ScanDebugConstants.MinExposureTicks, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            ScanCalibrationRoiSettings.CreateDefault().Normalize());

    public static string RequireRole(string? channelRole)
    {
        var role = NormalizeRole(channelRole);
        return !string.IsNullOrEmpty(role)
            ? role
            : throw new ArgumentException("Channel role cannot be empty.", nameof(channelRole));
    }

    public static string? NormalizeSelectedChannel(string? channelRole)
    {
        var role = NormalizeRole(channelRole);
        return string.IsNullOrEmpty(role) ? null : role;
    }

    public static string NormalizeRole(string? channelRole)
        => string.IsNullOrWhiteSpace(channelRole) ? string.Empty : channelRole.Trim();
}
