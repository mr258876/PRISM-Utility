namespace PRISM_Utility.Core.Models;

public sealed record ScanWorkflowRequest(
    int Rows,
    bool WarmUpEnabled,
    ushort[] LedLevels,
    string[] PassChannelRoles,
    ScanParameterSnapshot[] PassParameterProfiles,
    byte ScanMotorId,
    uint MotorIntervalNs,
    bool StartingDirectionPositive,
    bool AlternateMotorDirection,
    ushort ExposureTicks,
    uint SysClockKhz,
    ScanFilmAcquisitionSettings? AcquisitionSettings = null,
    bool EnableMotorTransport = true,
    bool EnableLedAutoControl = true)
{
    public uint MotorIntervalUs => MotorIntervalNs;
}

public sealed record ScanPassCapture(
    int PassIndex,
    byte LedChannelIndex,
    bool DirectionPositive,
    int Rows,
    uint MotorSteps,
    byte[] ImageBytes);

public sealed record ScanWorkflowResult(
    int Rows,
    IReadOnlyList<ScanPassCapture> Passes,
    uint ComputedMotorStepsPerPass,
    uint MotorIntervalNs,
    ushort ExposureTicks,
    uint SysClockKhz)
{
    public uint MotorIntervalUs => MotorIntervalNs;
}

public sealed record ScanWorkflowProgress(
    int CurrentPass,
    int TotalPasses,
    byte LedChannelIndex,
    bool DirectionPositive,
    string Stage);

public sealed record ScanChannelAssignment(
    string Channel1Role,
    string Channel2Role,
    string Channel3Role,
    string Channel4Role,
    bool Channel1Reversed,
    bool Channel2Reversed,
    bool Channel3Reversed,
    bool Channel4Reversed)
{
    public IReadOnlyList<string> Roles => new[] { Channel1Role, Channel2Role, Channel3Role, Channel4Role };
    public IReadOnlyList<bool> ReversedFlags => new[] { Channel1Reversed, Channel2Reversed, Channel3Reversed, Channel4Reversed };
}

public enum ScanChannelAlignmentMode
{
    Ecc = 0,
    MutualInformation = 1,
    EccThenMutualInformation = 2
}

public enum ScanTargetWhitePointMode
{
    D65 = 0,
    D50 = 1,
    ManualColorTemperature = 2
}

public sealed record ScanColorManagementOptions(
    bool IsEnabled,
    double RedWavelengthNm,
    double GreenWavelengthNm,
    double BlueWavelengthNm,
    double OutputGamma,
    ScanTargetWhitePointMode TargetWhitePointMode = ScanTargetWhitePointMode.D65,
    double ManualWhitePointColorTemperatureK = 6504.0)
{
    public static ScanColorManagementOptions CreateDefault()
        => new(true, 680.0, 525.0, 450.0, 2.2, ScanTargetWhitePointMode.D65, 6504.0);
}
