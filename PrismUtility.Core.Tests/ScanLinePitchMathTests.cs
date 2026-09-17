using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanLinePitchMathTests
{
    private static readonly ScanMotorMechanicalSettings MotorSettings = new(200, 16, 8.0);

    [Fact]
    public void BuildLinePitchPlan_KnownVectorPreservesPhysicalUnits()
    {
        var result = BuildPlan(
            rows: 128,
            targetMicrometers: 5.0,
            passes: [ActivePass(0, "Blue", exposureTicks: 1000, sysClockKhz: 100_000)]);

        var pass = Assert.Single(result.Passes);
        Assert.True(result.CanScan);
        Assert.Equal(400m, result.StepsPerMillimeter);
        Assert.Equal(0.0025m, result.OneMotorStepMillimeters);
        Assert.Equal(518_270m, pass.EffectiveLinePeriodNanoseconds);
        Assert.Equal(0.005m, pass.TargetLinePitchMillimeters);
        Assert.Equal(259_135m, pass.RequiredMotorIntervalNanoseconds);
        Assert.Equal(259_135u, pass.SelectedMotorIntervalNanoseconds);
        Assert.Equal(0.005m, pass.PredictedLinePitchMillimeters);
        Assert.Equal(5_080m, pass.Dpi);
        Assert.Equal(256m, pass.ExactMotorStepsPerPass);
        Assert.Equal(256u, pass.MotorStepsPerPass);
        Assert.Equal(0.64m, pass.PassDisplacementMillimeters);
        Assert.Equal(0m, pass.RelativePitchError);
        Assert.Equal(0m, pass.ReturnDriftMillimeters);
        Assert.Equal(0m, result.ReturnCumulativeDriftMillimeters);
    }

    [Fact]
    public void BuildLinePitchPlan_FractionalRequiredIntervalUsesCeiling()
    {
        var result = BuildPlan(
            rows: 100,
            targetMicrometers: 7.0,
            passes: [ActivePass(0, "Green", exposureTicks: 1000, sysClockKhz: 100_000)]);

        var pass = Assert.Single(result.Passes);
        Assert.Equal(518_270m / 2.8m, pass.RequiredMotorIntervalNanoseconds);
        Assert.Equal(185_097u, pass.SelectedMotorIntervalNanoseconds);
        Assert.True(pass.PredictedLinePitchMillimeters <= pass.TargetLinePitchMillimeters);
        Assert.True(pass.RelativePitchError < 0.01m);
    }

    [Fact]
    public void BuildLinePitchPlan_IntegralRequiredIntervalIsNotIncremented()
    {
        var result = BuildPlan(
            rows: 128,
            targetMicrometers: 5.0,
            passes: [ActivePass(0, "Blue", exposureTicks: 1000, sysClockKhz: 100_000)]);

        var pass = Assert.Single(result.Passes);
        Assert.Equal(pass.RequiredMotorIntervalNanoseconds, pass.SelectedMotorIntervalNanoseconds);
    }

    [Fact]
    public void BuildLinePitchPlan_DifferentChannelTimingsConvergeOnTargetPitch()
    {
        var result = BuildPlan(
            rows: 128,
            targetMicrometers: 5.0,
            passes:
            [
                ActivePass(3, "Red", exposureTicks: 0, sysClockKhz: 50_000),
                ActivePass(0, "Blue", exposureTicks: 1000, sysClockKhz: 100_000)
            ]);

        Assert.Equal(new[] { 0, 3 }, result.Passes.Select(pass => pass.PassIndex));
        Assert.Equal(new[] { "Blue", "Red" }, result.Passes.Select(pass => pass.ChannelRole));
        Assert.Equal(259_135u, result.Passes[0].SelectedMotorIntervalNanoseconds);
        Assert.Equal(458_270u, result.Passes[1].SelectedMotorIntervalNanoseconds);
        Assert.All(result.Passes, pass => Assert.Equal(0.005m, pass.PredictedLinePitchMillimeters));
        Assert.All(result.Passes, pass => Assert.Equal(5_080m, pass.Dpi));
    }

    [Fact]
    public void BuildLinePitchPlan_InactiveChannelsDoNotAffectCalculationsOrValidation()
    {
        var result = BuildPlan(
            rows: 128,
            targetMicrometers: 5.0,
            passes:
            [
                new ScanLinePitchPassInput(2, "IR", false, 0, 0),
                ActivePass(1, "Green", exposureTicks: 1000, sysClockKhz: 100_000)
            ]);

        var pass = Assert.Single(result.Passes);
        Assert.Equal(1, pass.PassIndex);
        Assert.DoesNotContain(result.Issues, issue => issue.PassIndex == 2);
    }

    [Fact]
    public void BuildLinePitchPlan_UsesMidpointAwayFromZeroForRepresentablePassSteps()
    {
        var result = BuildPlan(
            rows: 1,
            targetMicrometers: 2500.0,
            mechanics: new ScanMotorMechanicalSettings(1, 1, 1.0),
            passes: [ActivePass(0, "Blue", exposureTicks: 0, sysClockKhz: 100_000, intervalOverrideNs: 916_540)]);

        var pass = Assert.Single(result.Passes);
        Assert.Equal(0.5m, pass.ExactMotorStepsPerPass);
        Assert.Equal(1u, pass.MotorStepsPerPass);
        Assert.Equal(1m, pass.PassDisplacementMillimeters);
    }

    [Fact]
    public void BuildLinePitchPlan_ExactlyOnePercentPitchErrorIsAllowed()
    {
        var result = BuildPlan(
            rows: 1,
            targetMicrometers: 100_000.0,
            mechanics: new ScanMotorMechanicalSettings(1, 1, 1.0),
            passes: [ActivePass(0, "Blue", exposureTicks: 55, sysClockKhz: 100_000, intervalOverrideNs: 4_570)]);

        var pass = Assert.Single(result.Passes);
        Assert.Equal(101m, pass.PredictedLinePitchMillimeters);
        Assert.Equal(0.01m, pass.RelativePitchError);
        Assert.True(result.CanScan);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == ScanLinePitchIssueCode.PitchErrorExceedsTolerance);
    }

    [Fact]
    public void BuildLinePitchPlan_JustOverOnePercentPitchErrorBlocksScan()
    {
        var result = BuildPlan(
            rows: 1,
            targetMicrometers: 99_999.999,
            mechanics: new ScanMotorMechanicalSettings(1, 1, 1.0),
            passes: [ActivePass(0, "Blue", exposureTicks: 55, sysClockKhz: 100_000, intervalOverrideNs: 4_570)]);

        var issue = Assert.Single(result.Issues, issue => issue.Code == ScanLinePitchIssueCode.PitchErrorExceedsTolerance);
        Assert.Equal(ScanLinePitchIssueSeverity.Blocker, issue.Severity);
        Assert.Equal(ScanLinePitchIssueScope.Plan, issue.Scope);
        Assert.False(result.CanScan);
    }

    [Fact]
    public void BuildLinePitchPlan_ReturnModeDriftIsZeroForPerPassRoundTrips()
    {
        var result = BuildBoundaryDriftPlan(passCount: 4);

        Assert.All(result.Passes, pass => Assert.Equal(0m, pass.ReturnDriftMillimeters));
        Assert.Equal(0m, result.ReturnCumulativeDriftMillimeters);
    }

    [Fact]
    public void BuildLinePitchPlan_ExactlyOneStepAlternateBoundaryDriftIsAllowed()
    {
        var result = BuildBoundaryDriftPlan(passCount: 2);

        Assert.Equal(result.OneMotorStepMillimeters, result.AlternateCumulativeDriftMillimeters);
        Assert.True(result.CanScan);
        Assert.True(result.CanUseAlternateDirection);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == ScanLinePitchIssueCode.AlternateDriftExceedsOneStep);
    }

    [Fact]
    public void BuildLinePitchPlan_AdjacentStepDifferencesAccumulateAndBlockAlternateDirection()
    {
        var result = BuildBoundaryDriftPlan(passCount: 4);

        var issue = Assert.Single(result.Issues, issue => issue.Code == ScanLinePitchIssueCode.AlternateDriftExceedsOneStep);
        Assert.Equal(2m, result.AlternateCumulativeDriftMillimeters);
        Assert.Equal(ScanLinePitchIssueSeverity.Blocker, issue.Severity);
        Assert.Equal(ScanLinePitchIssueScope.AlternateDirection, issue.Scope);
        Assert.True(result.CanScan);
        Assert.False(result.CanUseAlternateDirection);
    }

    [Fact]
    public void BuildLinePitchPlan_BoundedPhysicalDomainsPreserveDimensionalInvariants()
    {
        var caseCount = 0;
        foreach (var exposureTicks in new ushort[] { 0, 1000, ushort.MaxValue })
        foreach (var sysClockKhz in new uint[] { 30_000, 100_000, 250_000 })
        foreach (var stepsPerRevolution in new uint[] { 200, 400 })
        foreach (var microsteps in new uint[] { 8, 16 })
        foreach (var leadLengthMm in new[] { 4.0, 8.0 })
        foreach (var targetMicrometers in new[] { 5.0, 10.0 })
        {
            var mechanics = new ScanMotorMechanicalSettings(stepsPerRevolution, microsteps, leadLengthMm);
            var result = BuildPlan(
                rows: 137,
                targetMicrometers,
                mechanics: mechanics,
                passes: [ActivePass(0, "Blue", exposureTicks, sysClockKhz)]);

            var pass = Assert.Single(result.Passes);
            var expectedCycles = 45_827m + (6m * exposureTicks);
            var expectedClockPeriodNs = 1_000_000m / sysClockKhz;
            var expectedLinePeriodNs = expectedCycles * expectedClockPeriodNs;
            var expectedStepsPerMm = ((decimal)stepsPerRevolution * microsteps) / (decimal)leadLengthMm;
            var expectedPitchMm = (decimal)targetMicrometers / 1000m;
            var expectedRequiredInterval = expectedLinePeriodNs / (expectedPitchMm * expectedStepsPerMm);

            Assert.True(result.CanScan);
            Assert.Equal(expectedLinePeriodNs, pass.EffectiveLinePeriodNanoseconds);
            Assert.Equal(expectedRequiredInterval, pass.RequiredMotorIntervalNanoseconds);
            Assert.True(pass.SelectedMotorIntervalNanoseconds >= decimal.Ceiling(expectedRequiredInterval));
            Assert.Equal(
                expectedLinePeriodNs / (pass.SelectedMotorIntervalNanoseconds * expectedStepsPerMm),
                pass.PredictedLinePitchMillimeters);
            Assert.Equal(pass.MotorStepsPerPass / expectedStepsPerMm, pass.PassDisplacementMillimeters);
            caseCount++;
        }

        Assert.Equal(144, caseCount);
    }

    private static ScanLinePitchPlanResult BuildBoundaryDriftPlan(int passCount)
    {
        var passes = Enumerable.Range(0, passCount)
            .Select(index => ActivePass(
                index,
                $"Channel{index}",
                exposureTicks: 55,
                sysClockKhz: 100_000,
                intervalOverrideNs: index % 2 == 0 ? 4_570u : 4_620u))
            .ToArray();

        return BuildPlan(
            rows: 1,
            targetMicrometers: 100_000.0,
            mechanics: new ScanMotorMechanicalSettings(1, 1, 1.0),
            passes: passes);
    }

    private static ScanLinePitchPlanResult BuildPlan(
        int rows,
        double targetMicrometers,
        IReadOnlyList<ScanLinePitchPassInput?> passes,
        ScanMotorMechanicalSettings? mechanics = null,
        uint minimumIntervalNs = ScanDebugConstants.MotionMinIntervalNs)
        => ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            rows,
            targetMicrometers,
            mechanics ?? MotorSettings,
            minimumIntervalNs,
            passes));

    private static ScanLinePitchPassInput ActivePass(
        int passIndex,
        string channelRole,
        ushort exposureTicks,
        uint sysClockKhz,
        uint? intervalOverrideNs = null)
        => new(passIndex, channelRole, true, exposureTicks, sysClockKhz, intervalOverrideNs);
}
