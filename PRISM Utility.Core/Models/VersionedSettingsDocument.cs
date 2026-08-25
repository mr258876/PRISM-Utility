namespace PRISM_Utility.Core.Models;

public sealed record VersionedSettingsDocument<TPayload>(int SchemaVersion, TPayload? Payload);

public sealed record ScanCalibrationProfileSettingsPayload(
    Dictionary<string, ScanChannelCalibrationProfile>? Profiles,
    string? SelectedChannel);
