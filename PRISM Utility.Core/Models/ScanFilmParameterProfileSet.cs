namespace PRISM_Utility.Core.Models;

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
    uint MotorIntervalNs)
{
    public uint MotorIntervalUs => MotorIntervalNs;

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
            ScanDebugConstants.MotionDefaultIntervalNs);

    public ScanFilmAcquisitionSettings Normalize()
    {
        var steadyMask = (byte)(SteadyMask & ScanDebugConstants.IlluminationValidMask);
        var syncMask = (byte)(SyncMask & ScanDebugConstants.IlluminationValidMask);
        syncMask = (byte)(syncMask & ~steadyMask);

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
            Math.Max(MotorIntervalNs, ScanDebugConstants.MotionMinIntervalNs));
    }
}

public sealed record ScanFilmParameterProfileSet(
    int SchemaVersion,
    string ProfileName,
    DateTimeOffset SavedAtUtc,
    Dictionary<string, ScanChannelCalibrationProfile> ChannelProfiles,
    string? SelectedCalibrationChannel,
    ScanFilmAcquisitionSettings? AcquisitionSettings = null);
