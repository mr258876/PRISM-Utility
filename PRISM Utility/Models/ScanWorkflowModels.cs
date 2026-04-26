using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Models;

public sealed record ScanWorkflowRequest(
    int Rows,
    bool WarmUpEnabled,
    ushort[] LedLevels,
    string[] PassChannelRoles,
    ScanParameterSnapshot[] PassParameterProfiles,
    byte ScanMotorId,
    uint MotorIntervalUs,
    bool StartingDirectionPositive,
    bool AlternateMotorDirection,
    ushort ExposureTicks,
    uint SysClockKhz);

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
    uint MotorIntervalUs,
    ushort ExposureTicks,
    uint SysClockKhz);

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

public sealed record ScanColorManagementOptions(
    bool IsEnabled,
    double RedWavelengthNm,
    double GreenWavelengthNm,
    double BlueWavelengthNm,
    double OutputGamma)
{
    public static ScanColorManagementOptions CreateDefault()
        => new(true, 630.0, 530.0, 470.0, 2.2);
}

public sealed record ScanCompositeFrame(byte[] Pixels, int Width, int Height, WriteableBitmap Bitmap);
