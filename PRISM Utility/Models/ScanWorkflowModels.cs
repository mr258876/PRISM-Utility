using Microsoft.UI.Xaml.Media.Imaging;

namespace PRISM_Utility.Models;

public sealed record ScanWorkflowRequest(
    int Rows,
    bool WarmUpEnabled,
    ushort[] LedLevels,
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

public sealed record ScanCompositeFrame(byte[] Pixels, int Width, int Height, WriteableBitmap Bitmap);
