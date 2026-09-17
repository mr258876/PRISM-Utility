namespace PRISM_Utility.Core.Models;

public enum ScanLinePitchIssueSeverity
{
    Warning,
    Blocker,
    Fatal
}

public enum ScanLinePitchIssueScope
{
    Plan,
    AlternateDirection
}

public enum ScanLinePitchIssueCode
{
    InvalidRequest,
    InvalidRows,
    InvalidTargetLinePitch,
    InvalidMotorMechanics,
    InvalidMotorStepsPerRevolution,
    InvalidMotorMicrosteps,
    InvalidMotorLeadLength,
    InvalidMinimumMotorInterval,
    NoActiveChannels,
    InvalidPassIdentity,
    DuplicatePassIdentity,
    InvalidSystemClock,
    MotorIntervalOverrideBelowMinimum,
    MotorIntervalOverflow,
    MotorStepCountUnrepresentable,
    ArithmeticOverflow,
    MinimumMotorIntervalApplied,
    PitchErrorExceedsTolerance,
    AlternateDriftExceedsOneStep
}

public sealed record ScanLinePitchPassInput(
    int PassIndex,
    string ChannelRole,
    bool IsActive,
    ushort ExposureTicks,
    uint SysClockKhz,
    uint? MotorIntervalOverrideNanoseconds = null,
    ScanParameterSnapshot? ParameterProfile = null);

public sealed record ScanLinePitchPlanRequest(
    int Rows,
    double TargetLinePitchMicrometers,
    ScanMotorMechanicalSettings? MotorMechanics,
    uint MinimumMotorIntervalNanoseconds,
    IReadOnlyList<ScanLinePitchPassInput?>? Passes);

public sealed record ScanLinePitchPassInputSnapshot(
    int PassIndex,
    string ChannelRole,
    bool IsActive,
    ushort ExposureTicks,
    uint SysClockKhz,
    uint? MotorIntervalOverrideNanoseconds,
    ScanParameterSnapshot? ParameterProfile);

public sealed class ScanLinePitchPlanInputSnapshot
{
    private ScanLinePitchPlanInputSnapshot(
        int rows,
        double targetLinePitchMicrometers,
        ScanMotorMechanicalSettings? motorMechanics,
        uint minimumMotorIntervalNanoseconds,
        IEnumerable<ScanLinePitchPassInputSnapshot?>? passes)
    {
        Rows = rows;
        TargetLinePitchMicrometers = targetLinePitchMicrometers;
        MotorMechanics = motorMechanics;
        MinimumMotorIntervalNanoseconds = minimumMotorIntervalNanoseconds;
        Passes = Array.AsReadOnly((passes ?? []).ToArray());
    }

    public int Rows { get; }

    public double TargetLinePitchMicrometers { get; }

    public ScanMotorMechanicalSettings? MotorMechanics { get; }

    public uint MinimumMotorIntervalNanoseconds { get; }

    public IReadOnlyList<ScanLinePitchPassInputSnapshot?> Passes { get; }

    public static ScanLinePitchPlanInputSnapshot Create(ScanLinePitchPlanRequest request)
        => new(
            request.Rows,
            request.TargetLinePitchMicrometers,
            request.MotorMechanics,
            request.MinimumMotorIntervalNanoseconds,
            request.Passes?.Select(pass => pass is null
                ? null
                : new ScanLinePitchPassInputSnapshot(
                    pass.PassIndex,
                    pass.ChannelRole,
                    pass.IsActive,
                    pass.ExposureTicks,
                    pass.SysClockKhz,
                    pass.MotorIntervalOverrideNanoseconds,
                    pass.ParameterProfile)));

    public bool Matches(ScanLinePitchPlanInputSnapshot? other)
    {
        if (other is null
            || Rows != other.Rows
            || BitConverter.DoubleToInt64Bits(TargetLinePitchMicrometers) != BitConverter.DoubleToInt64Bits(other.TargetLinePitchMicrometers)
            || !MatchesMechanics(MotorMechanics, other.MotorMechanics)
            || MinimumMotorIntervalNanoseconds != other.MinimumMotorIntervalNanoseconds
            || Passes.Count != other.Passes.Count)
        {
            return false;
        }

        for (var index = 0; index < Passes.Count; index++)
        {
            if (Passes[index] != other.Passes[index])
                return false;
        }

        return true;
    }

    public bool MatchesWorkflowInputs(
        int rows,
        IReadOnlyList<string> passChannelRoles,
        IReadOnlyList<ScanParameterSnapshot> passParameterProfiles)
    {
        if (Rows != rows
            || Passes.Count != passChannelRoles.Count
            || passChannelRoles.Count != passParameterProfiles.Count)
        {
            return false;
        }

        var resolvedPassIndexes = new HashSet<int>();
        foreach (var pass in Passes)
        {
            if (pass is null
                || pass.PassIndex < 0
                || pass.PassIndex >= passChannelRoles.Count
                || !resolvedPassIndexes.Add(pass.PassIndex))
            {
                return false;
            }

            var currentRole = passChannelRoles[pass.PassIndex];
            var currentProfile = passParameterProfiles[pass.PassIndex];
            if (!string.Equals(pass.ChannelRole, currentRole, StringComparison.Ordinal)
                || pass.IsActive == string.Equals(currentRole, "Unused", StringComparison.OrdinalIgnoreCase)
                || pass.ExposureTicks != currentProfile.ExposureTicks
                || pass.SysClockKhz != currentProfile.SysClockKhz
                || pass.ParameterProfile != currentProfile)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesMechanics(
        ScanMotorMechanicalSettings? left,
        ScanMotorMechanicalSettings? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return left.StepsPerRevolution == right.StepsPerRevolution
            && left.Microsteps == right.Microsteps
            && BitConverter.DoubleToInt64Bits(left.LeadLengthMm) == BitConverter.DoubleToInt64Bits(right.LeadLengthMm);
    }
}

public sealed record ScanLinePitchIssue(
    ScanLinePitchIssueCode Code,
    ScanLinePitchIssueSeverity Severity,
    ScanLinePitchIssueScope Scope,
    string FieldPath,
    string Message,
    int? PassIndex = null,
    string? ChannelRole = null);

public sealed record ScanLinePitchPassPlan(
    int PassIndex,
    string ChannelRole,
    decimal EffectiveLinePeriodNanoseconds,
    decimal TargetLinePitchMillimeters,
    decimal RequiredMotorIntervalNanoseconds,
    uint SelectedMotorIntervalNanoseconds,
    decimal PredictedLinePitchMillimeters,
    decimal Dpi,
    decimal ExactMotorStepsPerPass,
    uint MotorStepsPerPass,
    decimal PassDisplacementMillimeters,
    decimal RelativePitchError,
    decimal ReturnDriftMillimeters);

public sealed record ScanLinePitchPlanResult
{
    public ScanLinePitchPlanResult(
        IEnumerable<ScanLinePitchPassPlan>? passes,
        IEnumerable<ScanLinePitchIssue>? issues,
        decimal stepsPerMillimeter = 0m,
        decimal oneMotorStepMillimeters = 0m,
        decimal returnCumulativeDriftMillimeters = 0m,
        decimal alternateCumulativeDriftMillimeters = 0m,
        ScanLinePitchPlanInputSnapshot? inputSnapshot = null)
    {
        Passes = Array.AsReadOnly((passes ?? [])
            .OrderBy(pass => pass.PassIndex)
            .ToArray());
        Issues = Array.AsReadOnly((issues ?? [])
            .OrderBy(issue => issue.FieldPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.Severity)
            .ToArray());
        StepsPerMillimeter = stepsPerMillimeter;
        OneMotorStepMillimeters = oneMotorStepMillimeters;
        ReturnCumulativeDriftMillimeters = returnCumulativeDriftMillimeters;
        AlternateCumulativeDriftMillimeters = alternateCumulativeDriftMillimeters;
        InputSnapshot = inputSnapshot;
    }

    public IReadOnlyList<ScanLinePitchPassPlan> Passes { get; }

    public IReadOnlyList<ScanLinePitchIssue> Issues { get; }

    public decimal StepsPerMillimeter { get; }

    public decimal OneMotorStepMillimeters { get; }

    public decimal ReturnCumulativeDriftMillimeters { get; }

    public decimal AlternateCumulativeDriftMillimeters { get; }

    public ScanLinePitchPlanInputSnapshot? InputSnapshot { get; }

    public bool CanScan
        => Issues.All(issue => issue.Scope != ScanLinePitchIssueScope.Plan
            || issue.Severity == ScanLinePitchIssueSeverity.Warning);

    public bool CanUseAlternateDirection
        => CanScan && Issues.All(issue => issue.Scope != ScanLinePitchIssueScope.AlternateDirection
            || issue.Severity == ScanLinePitchIssueSeverity.Warning);
}
