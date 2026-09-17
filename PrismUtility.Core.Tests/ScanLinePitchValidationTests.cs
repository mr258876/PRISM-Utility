using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanLinePitchValidationTests
{
    private static readonly ScanMotorMechanicalSettings ValidMechanics = new(200, 16, 8.0);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildLinePitchPlan_InvalidRowsReturnFatalIssue(int rows)
    {
        var result = BuildPlan(rows: rows);

        AssertFatal(result, ScanLinePitchIssueCode.InvalidRows, "rows");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e100)]
    public void BuildLinePitchPlan_InvalidTargetPitchReturnsFatalIssue(double targetMicrometers)
    {
        var result = BuildPlan(targetMicrometers: targetMicrometers);

        AssertFatal(result, ScanLinePitchIssueCode.InvalidTargetLinePitch, "targetLinePitchMicrometers");
    }

    [Fact]
    public void BuildLinePitchPlan_MissingMotorMechanicsReturnsFatalIssue()
    {
        var result = ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            128,
            5.0,
            null,
            ScanDebugConstants.MotionMinIntervalNs,
            [ValidPass(0, "Blue")]));

        AssertFatal(result, ScanLinePitchIssueCode.InvalidMotorMechanics, "motorMechanics");
    }

    [Fact]
    public void BuildLinePitchPlan_ZeroStepsPerRevolutionReturnsFatalIssue()
    {
        var result = BuildPlan(mechanics: new ScanMotorMechanicalSettings(0, 16, 8.0));

        AssertFatal(result, ScanLinePitchIssueCode.InvalidMotorStepsPerRevolution, "motorMechanics.stepsPerRevolution");
    }

    [Fact]
    public void BuildLinePitchPlan_ZeroMicrostepsReturnsFatalIssue()
    {
        var result = BuildPlan(mechanics: new ScanMotorMechanicalSettings(200, 0, 8.0));

        AssertFatal(result, ScanLinePitchIssueCode.InvalidMotorMicrosteps, "motorMechanics.microsteps");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e100)]
    public void BuildLinePitchPlan_InvalidLeadLengthReturnsFatalIssue(double leadLengthMm)
    {
        var result = BuildPlan(mechanics: new ScanMotorMechanicalSettings(200, 16, leadLengthMm));

        AssertFatal(result, ScanLinePitchIssueCode.InvalidMotorLeadLength, "motorMechanics.leadLengthMm");
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(749u)]
    public void BuildLinePitchPlan_InvalidMinimumIntervalReturnsFatalIssue(uint minimumIntervalNs)
    {
        var result = BuildPlan(minimumIntervalNs: minimumIntervalNs);

        AssertFatal(result, ScanLinePitchIssueCode.InvalidMinimumMotorInterval, "minimumMotorIntervalNanoseconds");
    }

    [Fact]
    public void BuildLinePitchPlan_MinimumIntervalBoundaryIsAccepted()
    {
        var result = BuildPlan(minimumIntervalNs: ScanDebugConstants.MotionMinIntervalNs);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == ScanLinePitchIssueCode.InvalidMinimumMotorInterval);
    }

    [Fact]
    public void BuildLinePitchPlan_NoPassCollectionReturnsFatalIssue()
    {
        var result = ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            128,
            5.0,
            ValidMechanics,
            ScanDebugConstants.MotionMinIntervalNs,
            null));

        AssertFatal(result, ScanLinePitchIssueCode.NoActiveChannels, "passes");
    }

    [Fact]
    public void BuildLinePitchPlan_NoActiveChannelsReturnsFatalIssue()
    {
        var result = BuildPlan(passes: [new ScanLinePitchPassInput(0, "Blue", false, 0, 0)]);

        AssertFatal(result, ScanLinePitchIssueCode.NoActiveChannels, "passes");
    }

    [Fact]
    public void BuildLinePitchPlan_NullPassReturnsFatalIdentityIssue()
    {
        var result = BuildPlan(passes: [null, ValidPass(1, "Green")]);

        AssertFatal(result, ScanLinePitchIssueCode.InvalidPassIdentity, "passes[0]");
    }

    [Theory]
    [InlineData(-1, "Blue", "passes[0].passIndex")]
    [InlineData(0, "", "passes[0].channelRole")]
    [InlineData(0, "   ", "passes[0].channelRole")]
    public void BuildLinePitchPlan_InvalidPassIdentityReturnsFatalIssue(int passIndex, string channelRole, string fieldPath)
    {
        var result = BuildPlan(passes: [ValidPass(passIndex, channelRole)]);

        AssertFatal(result, ScanLinePitchIssueCode.InvalidPassIdentity, fieldPath);
    }

    [Fact]
    public void BuildLinePitchPlan_DuplicatePassIndexReturnsAssociatedFatalIssue()
    {
        var result = BuildPlan(passes: [ValidPass(2, "Blue"), ValidPass(2, "Green")]);

        var issue = AssertFatal(result, ScanLinePitchIssueCode.DuplicatePassIdentity, "passes[1].passIndex");
        Assert.Equal(2, issue.PassIndex);
        Assert.Equal("Green", issue.ChannelRole);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(29_999u)]
    public void BuildLinePitchPlan_InvalidActiveChannelClockReturnsAssociatedFatalIssue(uint sysClockKhz)
    {
        var result = BuildPlan(passes: [ValidPass(3, "IR", sysClockKhz: sysClockKhz)]);

        var issue = AssertFatal(result, ScanLinePitchIssueCode.InvalidSystemClock, "passes[0].sysClockKhz");
        Assert.Equal(3, issue.PassIndex);
        Assert.Equal("IR", issue.ChannelRole);
    }

    [Fact]
    public void BuildLinePitchPlan_OverrideBelowMinimumReturnsAssociatedFatalIssue()
    {
        var result = BuildPlan(passes: [ValidPass(1, "Red", intervalOverrideNs: 749)]);

        var issue = AssertFatal(result, ScanLinePitchIssueCode.MotorIntervalOverrideBelowMinimum, "passes[0].motorIntervalOverrideNanoseconds");
        Assert.Equal(1, issue.PassIndex);
        Assert.Equal("Red", issue.ChannelRole);
    }

    [Fact]
    public void BuildLinePitchPlan_DerivedIntervalOverflowReturnsTypedFatalIssueWithoutThrowing()
    {
        var result = BuildPlan(targetMicrometers: 1e-17);

        AssertFatal(result, ScanLinePitchIssueCode.MotorIntervalOverflow, "passes[0].motorIntervalNanoseconds");
    }

    [Fact]
    public void BuildLinePitchPlan_DerivedIntervalOverflowCannotBeBypassedByOverride()
    {
        var result = BuildPlan(
            targetMicrometers: 1e-17,
            passes: [ValidPass(0, "Blue", intervalOverrideNs: uint.MaxValue)]);

        AssertFatal(result, ScanLinePitchIssueCode.MotorIntervalOverflow, "passes[0].motorIntervalNanoseconds");
    }

    [Fact]
    public void BuildLinePitchPlan_TargetThatUnderflowsMillimeterConversionReturnsTypedFatalIssue()
    {
        var result = BuildPlan(targetMicrometers: 1e-27);

        AssertFatal(result, ScanLinePitchIssueCode.InvalidTargetLinePitch, "targetLinePitchMicrometers");
    }

    [Fact]
    public void BuildLinePitchPlan_MechanicsThatUnderflowStepsPerMillimeterReturnTypedFatalIssue()
    {
        var result = BuildPlan(mechanics: new ScanMotorMechanicalSettings(1, 1, 7.9e28));

        AssertFatal(result, ScanLinePitchIssueCode.ArithmeticOverflow, "motorMechanics");
    }

    [Fact]
    public void BuildLinePitchPlan_DenominatorUnderflowReturnsTypedFatalIssueWithoutThrowing()
    {
        var result = BuildPlan(
            targetMicrometers: 1e-24,
            mechanics: new ScanMotorMechanicalSettings(1, 1, 1e20));

        AssertFatal(result, ScanLinePitchIssueCode.ArithmeticOverflow, "passes[0].calculation");
    }

    [Fact]
    public void BuildLinePitchPlan_MechanicalArithmeticOverflowReturnsTypedFatalIssueWithoutThrowing()
    {
        var result = BuildPlan(mechanics: new ScanMotorMechanicalSettings(uint.MaxValue, uint.MaxValue, 1e-20));

        AssertFatal(result, ScanLinePitchIssueCode.ArithmeticOverflow, "motorMechanics");
    }

    [Fact]
    public void BuildLinePitchPlan_StepCountOverflowReturnsTypedFatalIssueWithoutWrapping()
    {
        var targetMicrometers = ((45_827m + (6m * ushort.MaxValue)) * (1_000_000m / 30_000m))
            / (750m * 400m)
            * 1000m;
        var result = BuildPlan(
            rows: int.MaxValue,
            targetMicrometers: (double)targetMicrometers,
            passes: [ValidPass(0, "Blue", exposureTicks: ushort.MaxValue, sysClockKhz: 30_000, intervalOverrideNs: 750)]);

        AssertFatal(result, ScanLinePitchIssueCode.MotorStepCountUnrepresentable, "passes[0].motorStepsPerPass");
    }

    [Fact]
    public void BuildLinePitchPlan_LessThanHalfStepReturnsTypedFatalIssue()
    {
        var result = BuildPlan(
            rows: 1,
            targetMicrometers: 0.1,
            passes: [ValidPass(0, "Blue", intervalOverrideNs: uint.MaxValue)]);

        AssertFatal(result, ScanLinePitchIssueCode.MotorStepCountUnrepresentable, "passes[0].motorStepsPerPass");
    }

    [Fact]
    public void BuildLinePitchPlan_MinimumIntervalWarningAloneDoesNotBlockScan()
    {
        var result = BuildPlan(targetMicrometers: 1530.0);

        var warning = Assert.Single(result.Issues, issue => issue.Code == ScanLinePitchIssueCode.MinimumMotorIntervalApplied);
        Assert.Equal(ScanLinePitchIssueSeverity.Warning, warning.Severity);
        Assert.Equal(750u, Assert.Single(result.Passes).SelectedMotorIntervalNanoseconds);
        Assert.True(result.CanScan);
        Assert.True(result.CanUseAlternateDirection);
    }

    [Fact]
    public void BuildLinePitchPlan_IssuesAndPassesHaveDeterministicOrdering()
    {
        var request = new ScanLinePitchPlanRequest(
            1,
            100_000.0,
            new ScanMotorMechanicalSettings(1, 1, 1.0),
            ScanDebugConstants.MotionMinIntervalNs,
            [
                new ScanLinePitchPassInput(7, "Red", true, 55, 100_000, 4_570),
                new ScanLinePitchPassInput(2, "Blue", true, 55, 100_000, 4_570)
            ]);

        var first = ScanTimingMath.BuildLinePitchPlan(request);
        var second = ScanTimingMath.BuildLinePitchPlan(request);

        Assert.Equal(new[] { 2, 7 }, first.Passes.Select(pass => pass.PassIndex));
        Assert.Equal(first.Passes, second.Passes);
        Assert.Equal(first.Issues, second.Issues);
    }

    [Fact]
    public void BuildLinePitchPlan_NullRequestReturnsTypedFatalIssue()
    {
        var result = ScanTimingMath.BuildLinePitchPlan(null);

        AssertFatal(result, ScanLinePitchIssueCode.InvalidRequest, "request");
    }

    private static ScanLinePitchIssue AssertFatal(
        ScanLinePitchPlanResult result,
        ScanLinePitchIssueCode code,
        string fieldPath)
    {
        var issue = Assert.Single(result.Issues, candidate => candidate.Code == code && candidate.FieldPath == fieldPath);
        Assert.Equal(ScanLinePitchIssueSeverity.Fatal, issue.Severity);
        Assert.Equal(ScanLinePitchIssueScope.Plan, issue.Scope);
        Assert.False(result.CanScan);
        Assert.False(result.CanUseAlternateDirection);
        return issue;
    }

    private static ScanLinePitchPlanResult BuildPlan(
        int rows = 128,
        double targetMicrometers = 5.0,
        ScanMotorMechanicalSettings? mechanics = null,
        uint minimumIntervalNs = ScanDebugConstants.MotionMinIntervalNs,
        IReadOnlyList<ScanLinePitchPassInput?>? passes = default)
        => ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            rows,
            targetMicrometers,
            mechanics ?? ValidMechanics,
            minimumIntervalNs,
            passes ?? [ValidPass(0, "Blue")]));

    private static ScanLinePitchPassInput ValidPass(
        int passIndex,
        string channelRole,
        ushort exposureTicks = 0,
        uint sysClockKhz = 100_000,
        uint? intervalOverrideNs = null)
        => new(passIndex, channelRole, true, exposureTicks, sysClockKhz, intervalOverrideNs);
}
