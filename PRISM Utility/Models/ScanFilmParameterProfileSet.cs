using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Models;

public sealed record ScanFilmParameterProfileSet(
    int SchemaVersion,
    string ProfileName,
    DateTimeOffset SavedAtUtc,
    Dictionary<string, ScanChannelCalibrationProfile> ChannelProfiles,
    string? SelectedCalibrationChannel);
