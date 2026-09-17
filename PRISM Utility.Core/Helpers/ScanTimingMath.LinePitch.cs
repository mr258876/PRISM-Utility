using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Helpers;

public static partial class ScanTimingMath
{
    private const decimal LinePitchExposureBaseCycles = 45_827m;
    private const decimal LinePitchExposureTickScale = 6m;
    private const decimal LinePitchNanosecondsPerKilohertz = 1_000_000m;
    private const decimal LinePitchMicrometersPerMillimeter = 1_000m;
    private const decimal MillimetersPerInch = 25.4m;
    private const decimal MaximumRelativePitchError = 0.01m;

    public static ScanLinePitchPlanResult BuildLinePitchPlan(ScanLinePitchPlanRequest? request)
    {
        if (request is null)
        {
            return new ScanLinePitchPlanResult(
                [],
                [CreateIssue(
                    ScanLinePitchIssueCode.InvalidRequest,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    "request",
                    "A line-pitch plan request is required.")]);
        }

        var inputSnapshot = ScanLinePitchPlanInputSnapshot.Create(request);
        var issues = new List<ScanLinePitchIssue>();
        ValidateRequest(request, issues, out var targetPitchMillimeters, out var stepsPerMillimeter);
        var indexedPasses = ValidatePassIdentities(request.Passes, issues);
        var activePasses = indexedPasses
            .Where(entry => entry.Pass.IsActive)
            .OrderBy(entry => entry.Pass.PassIndex)
            .ToArray();

        if (activePasses.Length == 0)
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.NoActiveChannels,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "passes",
                "At least one active channel pass is required."));
        }

        var oneMotorStepMillimeters = stepsPerMillimeter > 0m
            ? 1m / stepsPerMillimeter
            : 0m;
        if (HasFatalPlanIssue(issues))
        {
            return new ScanLinePitchPlanResult(
                [],
                issues,
                stepsPerMillimeter,
                oneMotorStepMillimeters,
                inputSnapshot: inputSnapshot);
        }

        var plans = new List<ScanLinePitchPassPlan>(activePasses.Length);
        foreach (var entry in activePasses)
        {
            BuildPassPlan(
                request,
                entry,
                targetPitchMillimeters,
                stepsPerMillimeter,
                plans,
                issues);
        }

        var alternateDriftMillimeters = CalculateAlternateDrift(
            stepsPerMillimeter,
            plans,
            indexedPasses,
            issues);

        return new ScanLinePitchPlanResult(
            plans,
            issues,
            stepsPerMillimeter,
            oneMotorStepMillimeters,
            returnCumulativeDriftMillimeters: 0m,
            alternateCumulativeDriftMillimeters: alternateDriftMillimeters,
            inputSnapshot: inputSnapshot);
    }

    private static void ValidateRequest(
        ScanLinePitchPlanRequest request,
        List<ScanLinePitchIssue> issues,
        out decimal targetPitchMillimeters,
        out decimal stepsPerMillimeter)
    {
        targetPitchMillimeters = 0m;
        stepsPerMillimeter = 0m;

        if (request.Rows <= 0)
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.InvalidRows,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "rows",
                "Rows must be greater than zero."));
        }

        if (!TryConvertPositiveFinite(request.TargetLinePitchMicrometers, out var targetPitchMicrometers))
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.InvalidTargetLinePitch,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "targetLinePitchMicrometers",
                "Target line pitch must be a finite positive decimal value."));
        }
        else
        {
            targetPitchMillimeters = targetPitchMicrometers / LinePitchMicrometersPerMillimeter;
            if (targetPitchMillimeters == 0m)
            {
                issues.Add(CreateIssue(
                    ScanLinePitchIssueCode.InvalidTargetLinePitch,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    "targetLinePitchMicrometers",
                    "Target line pitch is below the supported decimal calculation range."));
            }
        }

        if (request.MinimumMotorIntervalNanoseconds < ScanDebugConstants.MotionMinIntervalNs)
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.InvalidMinimumMotorInterval,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "minimumMotorIntervalNanoseconds",
                $"Minimum motor interval must be at least {ScanDebugConstants.MotionMinIntervalNs} nanoseconds."));
        }

        if (request.MotorMechanics is null)
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.InvalidMotorMechanics,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "motorMechanics",
                "Motor mechanical settings are required."));
            return;
        }

        var mechanics = request.MotorMechanics;
        if (mechanics.StepsPerRevolution == 0)
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.InvalidMotorStepsPerRevolution,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "motorMechanics.stepsPerRevolution",
                "Motor steps per revolution must be greater than zero."));
        }

        if (mechanics.Microsteps == 0)
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.InvalidMotorMicrosteps,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "motorMechanics.microsteps",
                "Motor microsteps must be greater than zero."));
        }

        if (!TryConvertPositiveFinite(mechanics.LeadLengthMm, out var leadLengthMillimeters))
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.InvalidMotorLeadLength,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "motorMechanics.leadLengthMm",
                "Motor lead length must be a finite positive decimal value."));
        }
        else if (mechanics.StepsPerRevolution > 0 && mechanics.Microsteps > 0)
        {
            try
            {
                stepsPerMillimeter = checked(
                    ((decimal)mechanics.StepsPerRevolution * mechanics.Microsteps)
                    / leadLengthMillimeters);
                if (stepsPerMillimeter == 0m)
                {
                    issues.Add(CreateIssue(
                        ScanLinePitchIssueCode.ArithmeticOverflow,
                        ScanLinePitchIssueSeverity.Fatal,
                        ScanLinePitchIssueScope.Plan,
                        "motorMechanics",
                        "Motor mechanics are below the supported decimal calculation range."));
                }
            }
            catch (OverflowException)
            {
                issues.Add(CreateIssue(
                    ScanLinePitchIssueCode.ArithmeticOverflow,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    "motorMechanics",
                    "Motor mechanics exceed the supported decimal calculation range."));
            }
        }
    }

    private static IReadOnlyList<IndexedLinePitchPass> ValidatePassIdentities(
        IReadOnlyList<ScanLinePitchPassInput?>? passes,
        List<ScanLinePitchIssue> issues)
    {
        if (passes is null)
            return [];

        var indexedPasses = new List<IndexedLinePitchPass>(passes.Count);
        var passIndexes = new HashSet<int>();
        for (var sourceIndex = 0; sourceIndex < passes.Count; sourceIndex++)
        {
            var pass = passes[sourceIndex];
            if (pass is null)
            {
                issues.Add(CreateIssue(
                    ScanLinePitchIssueCode.InvalidPassIdentity,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    $"passes[{sourceIndex}]",
                    "Channel pass cannot be null."));
                continue;
            }

            indexedPasses.Add(new IndexedLinePitchPass(sourceIndex, pass));
            if (pass.PassIndex < 0)
            {
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.InvalidPassIdentity,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    sourceIndex,
                    "passIndex",
                    "Pass index must be zero or greater.",
                    pass));
            }
            else if (!passIndexes.Add(pass.PassIndex))
            {
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.DuplicatePassIdentity,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    sourceIndex,
                    "passIndex",
                    "Pass indexes must be unique.",
                    pass));
            }

            if (string.IsNullOrWhiteSpace(pass.ChannelRole))
            {
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.InvalidPassIdentity,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    sourceIndex,
                    "channelRole",
                    "Channel role cannot be empty.",
                    pass));
            }
        }

        return indexedPasses;
    }

    private static void BuildPassPlan(
        ScanLinePitchPlanRequest request,
        IndexedLinePitchPass entry,
        decimal targetPitchMillimeters,
        decimal stepsPerMillimeter,
        List<ScanLinePitchPassPlan> plans,
        List<ScanLinePitchIssue> issues)
    {
        var pass = entry.Pass;
        if (pass.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            issues.Add(CreatePassIssue(
                ScanLinePitchIssueCode.InvalidSystemClock,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                entry.SourceIndex,
                "sysClockKhz",
                $"System clock must be at least {ScanDebugConstants.MinSysClockKhz} kHz.",
                pass));
            return;
        }

        if (pass.MotorIntervalOverrideNanoseconds is { } intervalOverride
            && intervalOverride < request.MinimumMotorIntervalNanoseconds)
        {
            issues.Add(CreatePassIssue(
                ScanLinePitchIssueCode.MotorIntervalOverrideBelowMinimum,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                entry.SourceIndex,
                "motorIntervalOverrideNanoseconds",
                "Motor interval override cannot be below the configured minimum.",
                pass));
            return;
        }

        try
        {
            var exposureCycles = checked(LinePitchExposureBaseCycles
                + (LinePitchExposureTickScale * pass.ExposureTicks));
            var clockPeriodNanoseconds = LinePitchNanosecondsPerKilohertz / pass.SysClockKhz;
            var effectiveLinePeriodNanoseconds = checked(exposureCycles * clockPeriodNanoseconds);
            var requiredIntervalNanoseconds = effectiveLinePeriodNanoseconds
                / checked(targetPitchMillimeters * stepsPerMillimeter);
            if (requiredIntervalNanoseconds <= 0m)
            {
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.ArithmeticOverflow,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    entry.SourceIndex,
                    "calculation",
                    "Required motor interval is below the supported decimal calculation range.",
                    pass));
                return;
            }

            var derivedIntervalNanoseconds = decimal.Ceiling(requiredIntervalNanoseconds);
            if (derivedIntervalNanoseconds > uint.MaxValue)
            {
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.MotorIntervalOverflow,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    entry.SourceIndex,
                    "motorIntervalNanoseconds",
                    "Required motor interval exceeds the supported unsigned 32-bit range.",
                    pass));
                return;
            }

            uint selectedIntervalNanoseconds;
            if (pass.MotorIntervalOverrideNanoseconds is { } selectedOverride)
            {
                selectedIntervalNanoseconds = selectedOverride;
            }
            else
            {
                selectedIntervalNanoseconds = (uint)Math.Max(
                    derivedIntervalNanoseconds,
                    request.MinimumMotorIntervalNanoseconds);
                if (derivedIntervalNanoseconds < request.MinimumMotorIntervalNanoseconds)
                {
                    issues.Add(CreatePassIssue(
                        ScanLinePitchIssueCode.MinimumMotorIntervalApplied,
                        ScanLinePitchIssueSeverity.Warning,
                        ScanLinePitchIssueScope.Plan,
                        entry.SourceIndex,
                        "motorIntervalNanoseconds",
                        "The configured minimum motor interval was applied.",
                        pass));
                }
            }

            var predictedPitchMillimeters = effectiveLinePeriodNanoseconds
                / checked(selectedIntervalNanoseconds * stepsPerMillimeter);
            var dpi = MillimetersPerInch / predictedPitchMillimeters;
            var exactMotorSteps = checked((decimal)request.Rows * effectiveLinePeriodNanoseconds)
                / selectedIntervalNanoseconds;
            var roundedMotorSteps = decimal.Round(exactMotorSteps, 0, MidpointRounding.AwayFromZero);
            if (roundedMotorSteps < 1m || roundedMotorSteps > uint.MaxValue)
            {
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.MotorStepCountUnrepresentable,
                    ScanLinePitchIssueSeverity.Fatal,
                    ScanLinePitchIssueScope.Plan,
                    entry.SourceIndex,
                    "motorStepsPerPass",
                    "Per-pass motor steps must round to an unsigned 32-bit value of at least one.",
                    pass));
                return;
            }

            var motorSteps = (uint)roundedMotorSteps;
            var passDisplacementMillimeters = motorSteps / stepsPerMillimeter;
            var relativePitchError = decimal.Abs(predictedPitchMillimeters - targetPitchMillimeters)
                / targetPitchMillimeters;
            plans.Add(new ScanLinePitchPassPlan(
                pass.PassIndex,
                pass.ChannelRole.Trim(),
                effectiveLinePeriodNanoseconds,
                targetPitchMillimeters,
                requiredIntervalNanoseconds,
                selectedIntervalNanoseconds,
                predictedPitchMillimeters,
                dpi,
                exactMotorSteps,
                motorSteps,
                passDisplacementMillimeters,
                relativePitchError,
                ReturnDriftMillimeters: 0m));

            if (relativePitchError > MaximumRelativePitchError)
            {
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.PitchErrorExceedsTolerance,
                    ScanLinePitchIssueSeverity.Blocker,
                    ScanLinePitchIssueScope.Plan,
                    entry.SourceIndex,
                    "predictedLinePitchMillimeters",
                    "Predicted line-pitch error exceeds one percent.",
                    pass));
            }
        }
        catch (OverflowException)
        {
            issues.Add(CreatePassIssue(
                ScanLinePitchIssueCode.ArithmeticOverflow,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                entry.SourceIndex,
                "calculation",
                "Line-pitch arithmetic exceeds the supported decimal range.",
                pass));
        }
        catch (DivideByZeroException)
        {
            issues.Add(CreatePassIssue(
                ScanLinePitchIssueCode.ArithmeticOverflow,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                entry.SourceIndex,
                "calculation",
                "Line-pitch arithmetic is below the supported decimal range.",
                pass));
        }
    }

    private static decimal CalculateAlternateDrift(
        decimal stepsPerMillimeter,
        IReadOnlyList<ScanLinePitchPassPlan> plans,
        IReadOnlyList<IndexedLinePitchPass> indexedPasses,
        List<ScanLinePitchIssue> issues)
    {
        if (plans.Count == 0 || HasFatalPlanIssue(issues))
            return 0m;

        try
        {
            var sharedBoundarySteps = (decimal)plans[0].MotorStepsPerPass;
            var positionSteps = 0m;
            var maximumDriftSteps = 0m;
            ScanLinePitchPassPlan? maximumDriftPass = null;
            for (var index = 0; index < plans.Count; index++)
            {
                var pass = plans[index];
                positionSteps += index % 2 == 0 ? pass.MotorStepsPerPass : -pass.MotorStepsPerPass;
                var expectedBoundarySteps = index % 2 == 0 ? sharedBoundarySteps : 0m;
                var driftSteps = decimal.Abs(positionSteps - expectedBoundarySteps);
                if (driftSteps > maximumDriftSteps)
                {
                    maximumDriftSteps = driftSteps;
                    maximumDriftPass = pass;
                }
            }

            var driftMillimeters = maximumDriftSteps / stepsPerMillimeter;
            if (maximumDriftSteps > 1m && maximumDriftPass is not null)
            {
                var source = indexedPasses.Single(entry => entry.Pass.PassIndex == maximumDriftPass.PassIndex);
                issues.Add(CreatePassIssue(
                    ScanLinePitchIssueCode.AlternateDriftExceedsOneStep,
                    ScanLinePitchIssueSeverity.Blocker,
                    ScanLinePitchIssueScope.AlternateDirection,
                    source.SourceIndex,
                    "passDisplacementMillimeters",
                    "Alternate-direction cumulative boundary drift exceeds one motor step.",
                    source.Pass));
            }

            return driftMillimeters;
        }
        catch (OverflowException)
        {
            issues.Add(CreateIssue(
                ScanLinePitchIssueCode.ArithmeticOverflow,
                ScanLinePitchIssueSeverity.Fatal,
                ScanLinePitchIssueScope.Plan,
                "alternateCumulativeDriftMillimeters",
                "Alternate-direction drift arithmetic exceeds the supported decimal range."));
            return 0m;
        }
    }

    private static bool TryConvertPositiveFinite(double value, out decimal converted)
    {
        converted = 0m;
        if (!double.IsFinite(value) || value <= 0.0)
            return false;

        try
        {
            converted = (decimal)value;
            return converted > 0m;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool HasFatalPlanIssue(IEnumerable<ScanLinePitchIssue> issues)
        => issues.Any(issue => issue.Scope == ScanLinePitchIssueScope.Plan
            && issue.Severity == ScanLinePitchIssueSeverity.Fatal);

    private static ScanLinePitchIssue CreatePassIssue(
        ScanLinePitchIssueCode code,
        ScanLinePitchIssueSeverity severity,
        ScanLinePitchIssueScope scope,
        int sourceIndex,
        string fieldName,
        string message,
        ScanLinePitchPassInput pass)
        => CreateIssue(
            code,
            severity,
            scope,
            $"passes[{sourceIndex}].{fieldName}",
            message,
            pass.PassIndex,
            string.IsNullOrWhiteSpace(pass.ChannelRole) ? null : pass.ChannelRole.Trim());

    private static ScanLinePitchIssue CreateIssue(
        ScanLinePitchIssueCode code,
        ScanLinePitchIssueSeverity severity,
        ScanLinePitchIssueScope scope,
        string fieldPath,
        string message,
        int? passIndex = null,
        string? channelRole = null)
        => new(code, severity, scope, fieldPath, message, passIndex, channelRole);

    private sealed record IndexedLinePitchPass(int SourceIndex, ScanLinePitchPassInput Pass);
}
