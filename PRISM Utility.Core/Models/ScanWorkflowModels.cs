namespace PRISM_Utility.Core.Models;

public sealed record ScanWorkflowLinePitchInput(
    double TargetLinePitchMicrometers,
    ScanMotorMechanicalSettings? MotorMechanics,
    uint MinimumMotorIntervalNanoseconds,
    uint?[] MotorIntervalOverrides)
{
    internal ScanWorkflowLinePitchInput CreateExecutionCopy()
        => this with { MotorIntervalOverrides = MotorIntervalOverrides?.ToArray() ?? [] };
}

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
    bool EnableLedAutoControl = true,
    ScanLinePitchPlanResult? LinePitchPlan = null,
    ScanLinePitchPlanInputSnapshot? LinePitchPlanInput = null,
    ScanWorkflowLinePitchInput? LinePitchInput = null)
{
    internal ScanWorkflowRequest CreateExecutionSnapshot()
        => this with
        {
            LedLevels = LedLevels?.ToArray() ?? [],
            PassChannelRoles = PassChannelRoles?.ToArray() ?? [],
            PassParameterProfiles = PassParameterProfiles?.ToArray() ?? [],
            LinePitchInput = LinePitchInput?.CreateExecutionCopy()
        };
}

public enum ScanWorkflowTransportValidationFailure
{
    MissingLinePitchPlan,
    MissingLinePitchInput,
    InvalidLinePitchInput,
    InvalidLinePitchPlan,
    PlanInputMismatch,
    AlternateDirectionNotAllowed,
    PlanPassMismatch
}

public sealed class ScanWorkflowTransportValidationException : InvalidOperationException
{
    public ScanWorkflowTransportValidationException(
        ScanWorkflowTransportValidationFailure failure,
        string message,
        ScanLinePitchPlanResult? linePitchPlan = null)
        : base(message)
    {
        Failure = failure;
        LinePitchPlan = linePitchPlan;
    }

    public ScanWorkflowTransportValidationFailure Failure { get; }

    public ScanLinePitchPlanResult? LinePitchPlan { get; }
}

public sealed record ScanPassCaptureProvenance(
    Guid CaptureId,
    string ChannelRole,
    byte? SubmittedLedChannelIndex,
    ushort? SubmittedLedLevel,
    ScanParameterSnapshot SubmittedParameters,
    int RequestedRows,
    int CompletedRows,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int CompletedResultVersion)
{
    public string? DeviceIdentity { get; init; }
    public long? SessionGeneration { get; init; }
    public string? ConfigurationIdentity { get; init; }
    public string? CalibrationIdentity { get; init; }
    public ScanParameterSnapshot? DeviceReadbackParameters { get; init; }
}

public sealed record ScanPassCapture(
    int PassIndex,
    byte LedChannelIndex,
    bool DirectionPositive,
    int Rows,
    uint MotorSteps,
    byte[] ImageBytes)
{
    public ScanPassCaptureProvenance? Provenance { get; init; }
}

public sealed record ScanWorkflowResult(
    int Rows,
    IReadOnlyList<ScanPassCapture> Passes,
    uint ComputedMotorStepsPerPass,
    uint MotorIntervalNs,
    ushort ExposureTicks,
    uint SysClockKhz)
{
    public Guid? CaptureId { get; init; }
    public int? CompletedResultVersion { get; init; }
}

public sealed record ScanWorkflowProgress(
    int CurrentPass,
    int TotalPasses,
    byte LedChannelIndex,
    bool DirectionPositive,
    string Stage);

public sealed record ScanWorkflowRowsAvailable(
    int CurrentPass,
    int TotalPasses,
    int PassIndex,
    byte LedChannelIndex,
    bool DirectionPositive,
    string ChannelRole,
    byte[] ImageBytes,
    int CompletedRows,
    uint MotorSteps = 0,
    uint MotorIntervalNanoseconds = 0)
{
    public Guid? CaptureId { get; init; }

    public int StartRow { get; init; }

    public int RowCount { get; init; } = -1;
}

public sealed record ScanRowAvailability(int StartRow, int RowCount)
{
    public int EndExclusive => StartRow + Math.Max(0, RowCount);

    public bool Contains(int row)
        => row >= StartRow && row < EndExclusive;
}

public delegate void ScanWorkflowRowsAvailableHandler(ScanWorkflowRowsAvailable snapshot);

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
    [Newtonsoft.Json.JsonIgnore]
    public IReadOnlyList<string> Roles => new[] { Channel1Role, Channel2Role, Channel3Role, Channel4Role };

    [Newtonsoft.Json.JsonIgnore]
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
