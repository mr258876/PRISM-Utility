namespace PRISM_Utility.Core.Models;

public enum ScanFilmTransportStrategy
{
    ReturnToStart = 0,
    AlternateDirection = 1
}

public sealed record ScanFilmAcquisitionSettings(
    ushort Led1Level,
    ushort Led2Level,
    ushort Led3Level,
    ushort Led4Level,
    byte SteadyMask,
    byte SyncMask,
    uint Led1PulseClock,
    uint Led2PulseClock,
    uint Led3PulseClock,
    uint Led4PulseClock,
    uint MotorIntervalNs,
    string Led1ChannelColor = "Blue",
    string Led2ChannelColor = "White",
    string Led3ChannelColor = "Red",
    string Led4ChannelColor = "Green",
    int Rows = 128,
    byte ScanMotorId = 1,
    double? TargetLinePitchMicrometers = null,
    bool StartingDirectionPositive = true,
    bool WarmUpEnabled = false,
    ScanFilmTransportStrategy TransportStrategy = ScanFilmTransportStrategy.AlternateDirection,
    ScanChannelAssignment? AcquisitionChannelAssignment = null)
{
    public static ScanFilmAcquisitionSettings CreateDefault()
        => new(
            0,
            0,
            0,
            0,
            ScanDebugConstants.IlluminationValidMask,
            0,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            ScanDebugConstants.MotionDefaultIntervalNs,
            "Blue",
            "White",
            "Red",
            "Green",
            128,
            1,
            null,
            true,
            false,
            ScanFilmTransportStrategy.AlternateDirection,
            new ScanChannelAssignment("Blue", "White", "Red", "Green", false, false, false, false));

    public ScanFilmAcquisitionSettings Normalize()
    {
        var steadyMask = (byte)(SteadyMask & ScanDebugConstants.IlluminationValidMask);
        var syncMask = (byte)(SyncMask & ScanDebugConstants.IlluminationValidMask);
        syncMask = (byte)(syncMask & ~steadyMask);
        var led1ChannelColor = NormalizeChannelColor(Led1ChannelColor, "Blue");
        var led2ChannelColor = NormalizeChannelColor(Led2ChannelColor, "White");
        var led3ChannelColor = NormalizeChannelColor(Led3ChannelColor, "Red");
        var led4ChannelColor = NormalizeChannelColor(Led4ChannelColor, "Green");
        var assignment = AcquisitionChannelAssignment;

        return new ScanFilmAcquisitionSettings(
            Led1Level,
            Led2Level,
            Led3Level,
            Led4Level,
            steadyMask,
            syncMask,
            Math.Max(Led1PulseClock, ScanDebugConstants.IlluminationMinSyncPulseClock),
            Math.Max(Led2PulseClock, ScanDebugConstants.IlluminationMinSyncPulseClock),
            Math.Max(Led3PulseClock, ScanDebugConstants.IlluminationMinSyncPulseClock),
            Math.Max(Led4PulseClock, ScanDebugConstants.IlluminationMinSyncPulseClock),
            Math.Max(MotorIntervalNs, ScanDebugConstants.MotionMinIntervalNs),
            led1ChannelColor,
            led2ChannelColor,
            led3ChannelColor,
            led4ChannelColor,
            Rows,
            ScanMotorId,
            TargetLinePitchMicrometers,
            StartingDirectionPositive,
            WarmUpEnabled,
            TransportStrategy,
            assignment is null
                ? new ScanChannelAssignment(led1ChannelColor, led2ChannelColor, led3ChannelColor, led4ChannelColor, false, false, false, false)
                : new ScanChannelAssignment(
                    NormalizeChannelColor(assignment.Channel1Role, led1ChannelColor),
                    NormalizeChannelColor(assignment.Channel2Role, led2ChannelColor),
                    NormalizeChannelColor(assignment.Channel3Role, led3ChannelColor),
                    NormalizeChannelColor(assignment.Channel4Role, led4ChannelColor),
                    assignment.Channel1Reversed,
                    assignment.Channel2Reversed,
                    assignment.Channel3Reversed,
                    assignment.Channel4Reversed));
    }

    private static string NormalizeChannelColor(string? channelColor, string fallback)
        => string.IsNullOrWhiteSpace(channelColor) ? fallback : channelColor.Trim();
}

public sealed record ScanFilmScanRecipeSettings(
    ScanChannelAssignment? ChannelAssignment = null,
    ScanColorManagementOptions? ColorManagement = null,
    ScanChannelAlignmentMode? AlignmentMode = null,
    ScanDngExportMode? DngExportMode = null);

public sealed record ScanFilmParameterProfileSet(
    int SchemaVersion,
    string ProfileName,
    DateTimeOffset SavedAtUtc,
    Dictionary<string, ScanChannelCalibrationProfile> ChannelProfiles,
    string? SelectedCalibrationChannel,
    ScanFilmAcquisitionSettings? AcquisitionSettings = null,
    ScanFilmScanRecipeSettings? ScanRecipeSettings = null);
