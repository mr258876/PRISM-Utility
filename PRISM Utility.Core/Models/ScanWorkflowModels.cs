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
    ScanFilmAcquisitionSettings? AcquisitionSettings = null)
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

public sealed record ScanColorManagementOptions(
    bool IsEnabled,
    double RedWavelengthNm,
    double GreenWavelengthNm,
    double BlueWavelengthNm,
    double OutputGamma)
{
    public static ScanColorManagementOptions CreateDefault()
        => new(true, 680.0, 525.0, 450.0, 2.2);
}
