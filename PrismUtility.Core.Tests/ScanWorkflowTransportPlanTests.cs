using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanWorkflowTransportPlanTests
{
    [Fact]
    public async Task ExecuteAsync_UsesFreshRawRequestDerivedIntervalAndStepsForPrepareWaitAndReturn()
    {
        var profiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 50_000)
        };
        var roles = new[] { "Blue", "Unused", "Unused", "IR" };
        var plan = BuildPlan(rows: 128, targetPitchMicrometers: 5.0, roles, profiles);
        var session = new RecordingScanSession();

        await CreateService().ExecuteAsync(session, BuildRawRequest(128, roles, profiles, 5.0), CancellationToken.None);

        var expected = plan.Passes
            .SelectMany(pass => new[]
            {
                new MotorCommand("Prepare", true, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Wait", false, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Return", false, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false)
            })
            .ToArray();

        Assert.Equal(expected, session.TransportCommands);
        Assert.Equal(0L, session.MotorPositionSteps);
        Assert.Equal(2, session.ScanStartCount);
        Assert.NotEqual(999_999u, session.TransportCommands[0].IntervalNanoseconds);
    }

    [Fact]
    public async Task ExecuteAsync_UsesDerivedCommandsInsteadOfForgedCallerPlan()
    {
        var profiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var expectedPlan = BuildPlan(128, 5.0, roles, profiles);
        var forgedPlan = new ScanLinePitchPlanResult(
            [Assert.Single(expectedPlan.Passes) with
            {
                SelectedMotorIntervalNanoseconds = 999_999,
                MotorStepsPerPass = 4_294_967_295
            }],
            expectedPlan.Issues,
            expectedPlan.StepsPerMillimeter,
            expectedPlan.OneMotorStepMillimeters,
            expectedPlan.ReturnCumulativeDriftMillimeters,
            expectedPlan.AlternateCumulativeDriftMillimeters,
            expectedPlan.InputSnapshot);
        var session = new RecordingScanSession();
        var request = BuildRawRequest(128, roles, profiles, 5.0) with
        {
            LinePitchPlan = forgedPlan,
            LinePitchPlanInput = forgedPlan.InputSnapshot
        };

        await CreateService().ExecuteAsync(session, request, CancellationToken.None);

        var expectedPass = Assert.Single(expectedPlan.Passes);
        Assert.Equal(
            new[]
            {
                new MotorCommand("Prepare", true, expectedPass.MotorStepsPerPass, expectedPass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Wait", false, expectedPass.MotorStepsPerPass, expectedPass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Return", false, expectedPass.MotorStepsPerPass, expectedPass.SelectedMotorIntervalNanoseconds, false)
            },
            session.TransportCommands);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("mechanics")]
    public async Task ExecuteAsync_UsesCurrentRawInputInsteadOfSelfConsistentStaleCallerPlan(string staleField)
    {
        var profiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var currentMechanics = new ScanMotorMechanicalSettings(200, 16, 8.0);
        var staleTargetPitchMicrometers = staleField == "target" ? 5.5 : 5.0;
        var staleMechanics = staleField == "mechanics"
            ? new ScanMotorMechanicalSettings(200, 8, 8.0)
            : currentMechanics;
        var stalePlan = BuildPlan(128, staleTargetPitchMicrometers, roles, profiles, staleMechanics);
        var expectedPlan = BuildPlan(128, 5.0, roles, profiles, currentMechanics);
        var session = new RecordingScanSession();
        var request = BuildRawRequest(128, roles, profiles, 5.0, currentMechanics) with
        {
            LinePitchPlan = stalePlan,
            LinePitchPlanInput = stalePlan.InputSnapshot
        };

        await CreateService().ExecuteAsync(session, request, CancellationToken.None);

        var expectedPass = Assert.Single(expectedPlan.Passes);
        Assert.Equal(expectedPass.SelectedMotorIntervalNanoseconds, session.TransportCommands[0].IntervalNanoseconds);
        Assert.Equal(expectedPass.MotorStepsPerPass, session.TransportCommands[0].Steps);
    }

    [Theory]
    [InlineData("rows")]
    [InlineData("target")]
    [InlineData("mechanics")]
    [InlineData("minimum")]
    [InlineData("exposure")]
    [InlineData("clock")]
    [InlineData("profile")]
    [InlineData("role-order")]
    [InlineData("active-membership")]
    [InlineData("interval-override")]
    public async Task ExecuteAsync_UsesRawCurrentInputForEveryPriorStalePlanVector(string staleField)
    {
        var currentRows = 128;
        var currentTargetPitchMicrometers = 5.0;
        var currentMechanics = new ScanMotorMechanicalSettings(200, 16, 8.0);
        var currentMinimumIntervalNanoseconds = staleField == "minimum" ? 260_000u : ScanDebugConstants.MotionMinIntervalNs;
        var currentRoles = new[] { "Blue", "Green", "Unused", "Unused" };
        var currentProfiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var currentOverrides = new uint?[] { null, null, null, null };
        var staleRows = currentRows;
        var staleTargetPitchMicrometers = currentTargetPitchMicrometers;
        var staleMechanics = currentMechanics;
        var staleMinimumIntervalNanoseconds = ScanDebugConstants.MotionMinIntervalNs;
        var staleRoles = currentRoles.ToArray();
        var staleProfiles = currentProfiles.ToArray();
        var staleOverrides = currentOverrides.ToArray();
        switch (staleField)
        {
            case "rows":
                staleRows++;
                break;
            case "target":
                staleTargetPitchMicrometers = 5.5;
                break;
            case "mechanics":
                staleMechanics = new ScanMotorMechanicalSettings(200, 8, 8.0);
                break;
            case "minimum":
                break;
            case "exposure":
                staleProfiles[0] = Profile(0, 100_000);
                break;
            case "clock":
                staleProfiles[0] = Profile(1_000, 110_000);
                break;
            case "profile":
                staleProfiles[0] = new ScanParameterSnapshot(1_000, 1, 0, 0, 0, 100_000);
                break;
            case "role-order":
                staleRoles = ["Green", "Blue", "Unused", "Unused"];
                break;
            case "active-membership":
                staleRoles = ["Blue", "Unused", "Green", "Unused"];
                break;
            case "interval-override":
                staleOverrides[0] = 260_000;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(staleField));
        }

        var stalePlan = BuildPlan(
            staleRows,
            staleTargetPitchMicrometers,
            staleRoles,
            staleProfiles,
            staleMechanics,
            staleOverrides,
            minimumIntervalNanoseconds: staleMinimumIntervalNanoseconds);
        var expectedPlan = BuildPlan(
            currentRows,
            currentTargetPitchMicrometers,
            currentRoles,
            currentProfiles,
            currentMechanics,
            currentOverrides,
            minimumIntervalNanoseconds: currentMinimumIntervalNanoseconds);
        var request = BuildRawRequest(
            currentRows,
            currentRoles,
            currentProfiles,
            currentTargetPitchMicrometers,
            currentMechanics,
            currentMinimumIntervalNanoseconds,
            currentOverrides) with
        {
            LinePitchPlan = stalePlan,
            LinePitchPlanInput = stalePlan.InputSnapshot
        };
        var session = new RecordingScanSession();

        Assert.True(expectedPlan.CanScan);
        await CreateService().ExecuteAsync(session, request, CancellationToken.None);

        Assert.Equal(
            expectedPlan.Passes.SelectMany(pass => new[]
            {
                new MotorCommand("Prepare", true, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Wait", false, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Return", false, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false)
            }),
            session.TransportCommands);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("mechanics")]
    [InlineData("minimum")]
    [InlineData("override")]
    public async Task ExecuteAsync_RejectsInvalidRawPlanningInputBeforeAnyWorkflowSideEffect(string invalidField)
    {
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var profiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var targetPitchMicrometers = invalidField == "target" ? 0.0 : 5.0;
        var mechanics = new ScanMotorMechanicalSettings(200, 16, 8.0);
        var minimumIntervalNanoseconds = invalidField == "minimum"
            ? ScanDebugConstants.MotionMinIntervalNs - 1
            : ScanDebugConstants.MotionMinIntervalNs;
        var intervalOverrides = invalidField == "override"
            ? new uint?[] { ScanDebugConstants.MotionMinIntervalNs - 1, null, null, null }
            : new uint?[] { null, null, null, null };
        var parameters = new StubParameterService();
        var illumination = new StubIlluminationService();
        var session = new RecordingScanSession();

        var request = BuildRawRequest(128, roles, profiles, targetPitchMicrometers, mechanics, minimumIntervalNanoseconds, intervalOverrides);
        if (invalidField == "mechanics")
        {
            request = request with
            {
                LinePitchInput = new ScanWorkflowLinePitchInput(
                    targetPitchMicrometers,
                    null,
                    minimumIntervalNanoseconds,
                    intervalOverrides)
            };
        }

        var exception = await Assert.ThrowsAsync<ScanWorkflowTransportValidationException>(() => CreateService(parameters, illumination).ExecuteAsync(
            session,
            request,
            CancellationToken.None));

        Assert.Equal(ScanWorkflowTransportValidationFailure.InvalidLinePitchPlan, exception.Failure);
        AssertNoWorkflowSideEffects(session, parameters, illumination);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCurrentRawValuesWhenTheyChangeBeforeExecution()
    {
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var profiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var intervalOverrides = new uint?[] { null, null, null, null };
        var request = BuildRawRequest(128, roles, profiles, 5.0, intervalOverrides: intervalOverrides) with
        {
            SysClockKhz = 100_000
        };

        roles[0] = "Unused";
        roles[1] = "Green";
        profiles[1] = Profile(0, 100_000);
        intervalOverrides[1] = 229_135;
        var expectedPlan = BuildPlan(128, 5.0, roles, profiles, intervalOverrides: intervalOverrides, sysClockKhz: 100_000);

        var session = new RecordingScanSession();
        await CreateService().ExecuteAsync(session, request, CancellationToken.None);

        var expectedPass = Assert.Single(expectedPlan.Passes);
        Assert.Equal(1, expectedPass.PassIndex);
        Assert.Equal(expectedPass.SelectedMotorIntervalNanoseconds, session.TransportCommands[0].IntervalNanoseconds);
        Assert.Equal(expectedPass.MotorStepsPerPass, session.TransportCommands[0].Steps);
    }

    [Fact]
    public async Task ExecuteAsync_UsesExecutionSnapshotWhenRawValuesChangeDuringExecution()
    {
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var profiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var intervalOverrides = new uint?[] { null, null, null, null };
        var expectedPlan = BuildPlan(128, 5.0, roles, profiles, intervalOverrides: intervalOverrides);
        var stateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var illumination = new StubIlluminationService { StateGate = stateGate };
        var session = new RecordingScanSession();
        var request = BuildRawRequest(128, roles, profiles, 5.0, intervalOverrides: intervalOverrides, enableLedAutoControl: true);

        var execution = CreateService(illumination: illumination).ExecuteAsync(session, request, CancellationToken.None);
        await illumination.StateReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        roles[0] = "Unused";
        roles[1] = "Green";
        profiles[0] = Profile(0, 100_000);
        intervalOverrides[0] = 260_000;
        stateGate.SetResult();
        await execution;

        var expectedPass = Assert.Single(expectedPlan.Passes);
        Assert.Equal(0, expectedPass.PassIndex);
        Assert.Equal(expectedPass.SelectedMotorIntervalNanoseconds, session.TransportCommands[0].IntervalNanoseconds);
        Assert.Equal(expectedPass.MotorStepsPerPass, session.TransportCommands[0].Steps);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCurrentRawProfileInsteadOfStaleCallerPlan()
    {
        var staleProfiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var currentProfiles = new[]
        {
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var stalePlan = BuildPlan(rows: 128, targetPitchMicrometers: 5.0, roles, staleProfiles, sysClockKhz: 100_000);
        var currentPlan = BuildPlan(rows: 128, targetPitchMicrometers: 5.0, roles, currentProfiles, sysClockKhz: 100_000);
        var session = new RecordingScanSession();

        Assert.Equal(259_135u, Assert.Single(stalePlan.Passes).SelectedMotorIntervalNanoseconds);
        Assert.Equal(229_135u, Assert.Single(currentPlan.Passes).SelectedMotorIntervalNanoseconds);

        await CreateService().ExecuteAsync(
            session,
            BuildRawRequest(128, roles, currentProfiles, 5.0) with
            {
                SysClockKhz = 100_000,
                LinePitchPlan = stalePlan,
                LinePitchPlanInput = stalePlan.InputSnapshot
            },
            CancellationToken.None);

        var currentPass = Assert.Single(currentPlan.Passes);
        Assert.Equal(currentPass.SelectedMotorIntervalNanoseconds, session.TransportCommands[0].IntervalNanoseconds);
        Assert.Equal(currentPass.MotorStepsPerPass, session.TransportCommands[0].Steps);
    }

    [Fact]
    public void BuildLinePitchPlan_CopiesMutablePassInputsIntoImmutableProvenance()
    {
        var profiles = new[]
        {
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000),
            Profile(0, 100_000)
        };
        var passInputs = new ScanLinePitchPassInput?[]
        {
            new(0, "Blue", true, profiles[0].ExposureTicks, profiles[0].SysClockKhz, ParameterProfile: profiles[0]),
            new(1, "Unused", false, profiles[1].ExposureTicks, profiles[1].SysClockKhz, ParameterProfile: profiles[1]),
            new(2, "Unused", false, profiles[2].ExposureTicks, profiles[2].SysClockKhz, ParameterProfile: profiles[2]),
            new(3, "Unused", false, profiles[3].ExposureTicks, profiles[3].SysClockKhz, ParameterProfile: profiles[3])
        };

        var plan = ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            128,
            5.0,
            new ScanMotorMechanicalSettings(200, 16, 8.0),
            ScanDebugConstants.MotionMinIntervalNs,
            passInputs));
        passInputs[0] = new ScanLinePitchPassInput(0, "Blue", true, 0, 100_000, ParameterProfile: Profile(0, 100_000));

        Assert.NotNull(plan.InputSnapshot);
        Assert.Equal(1_000, plan.InputSnapshot!.Passes[0]!.ExposureTicks);
        Assert.Equal(profiles[0], plan.InputSnapshot.Passes[0]!.ParameterProfile);
    }

    [Fact]
    public async Task ExecuteAsync_AlternatesBySortedActiveExecutionOrderWhenPhysicalPassesAreGapped()
    {
        var profiles = new[]
        {
            Profile(0, 100_000),
            Profile(1_000, 100_000),
            Profile(0, 100_000),
            Profile(0, 50_000)
        };
        var roles = new[] { "Unused", "Blue", "Unused", "IR" };
        var plan = BuildPlan(
            rows: 128,
            targetPitchMicrometers: 5.0,
            roles,
            profiles,
            passInputOrder: new[] { 3, 1 });
        var session = new RecordingScanSession();

        await CreateService().ExecuteAsync(
            session,
            BuildRequest(128, roles, profiles, plan, alternateMotorDirection: true),
            CancellationToken.None);

        Assert.Equal([1, 3], plan.Passes.Select(pass => pass.PassIndex).ToArray());
        Assert.Equal([true, false], session.TransportCommands
            .Where(command => command.Kind == "Prepare")
            .Select(command => command.DirectionPositive)
            .ToArray());
        Assert.DoesNotContain(session.TransportCommands, command => command.Kind == "Return");
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAlternateDirectionWhenPlanDisallowsItBeforeScanOrMotorStart()
    {
        var profiles = Enumerable.Range(0, 4).Select(_ => Profile(55, 100_000)).ToArray();
        var roles = new[] { "Channel0", "Channel1", "Channel2", "Channel3" };
        var plan = BuildAlternateDriftBlockerPlan(roles, profiles);
        var session = new RecordingScanSession();

        var exception = await Assert.ThrowsAsync<ScanWorkflowTransportValidationException>(() => CreateService().ExecuteAsync(
            session,
            BuildRequest(1, roles, profiles, plan, alternateMotorDirection: true) with { SysClockKhz = 100_000 },
            CancellationToken.None));

        Assert.Equal(ScanWorkflowTransportValidationFailure.AlternateDirectionNotAllowed, exception.Failure);
        Assert.False(plan.CanUseAlternateDirection);
        Assert.Equal(0, session.ScanStartCount);
        Assert.Empty(session.TransportCommands);
        Assert.Equal(0, session.MotorEnableCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsSafeReturnToStartWhenOnlyAlternateDriftIsBlocked()
    {
        var profiles = Enumerable.Range(0, 4).Select(_ => Profile(55, 100_000)).ToArray();
        var roles = new[] { "Channel0", "Channel1", "Channel2", "Channel3" };
        var plan = BuildAlternateDriftBlockerPlan(roles, profiles);
        var session = new RecordingScanSession();

        await CreateService().ExecuteAsync(
            session,
            BuildRequest(1, roles, profiles, plan, alternateMotorDirection: false) with { SysClockKhz = 100_000 },
            CancellationToken.None);

        Assert.True(plan.CanScan);
        Assert.False(plan.CanUseAlternateDirection);
        Assert.Equal(4, session.ScanStartCount);
        Assert.Equal(4, session.TransportCommands.Count(command => command.Kind == "Return"));
        Assert.Equal(0L, session.MotorPositionSteps);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsPitchErrorBlockerBeforeScanOrMotorStart()
    {
        var profiles = new[] { Profile(55, 100_000), Profile(0, 100_000), Profile(0, 100_000), Profile(0, 100_000) };
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var plan = BuildPlan(
            rows: 1,
            targetPitchMicrometers: 90_000.0,
            roles,
            profiles,
            mechanics: new ScanMotorMechanicalSettings(1, 1, 1.0),
            intervalOverrides: new uint?[] { 4_570, null, null, null });
        var session = new RecordingScanSession();

        var exception = await Assert.ThrowsAsync<ScanWorkflowTransportValidationException>(() => CreateService().ExecuteAsync(
            session,
            BuildRequest(1, roles, profiles, plan),
            CancellationToken.None));

        Assert.Equal(ScanWorkflowTransportValidationFailure.InvalidLinePitchPlan, exception.Failure);
        Assert.Contains(plan.Issues, issue => issue.Code == ScanLinePitchIssueCode.PitchErrorExceedsTolerance);
        Assert.Equal(0, session.ScanStartCount);
        Assert.Empty(session.TransportCommands);
        Assert.Equal(0, session.MotorEnableCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoresSavedProfileClockWhenRequestGlobalClockIsValid()
    {
        var profiles = new[] { Profile(0, 0), Profile(0, 100_000), Profile(0, 100_000), Profile(0, 100_000) };
        var roles = new[] { "Blue", "Unused", "Unused", "Unused" };
        var session = new RecordingScanSession();

        await CreateService().ExecuteAsync(
            session,
            BuildRawRequest(1, roles, profiles, 5.0),
            CancellationToken.None);

        Assert.Equal(1, session.ScanStartCount);
        Assert.NotEmpty(session.TransportCommands);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringScanStopsAndWaitsUsingCurrentPassPlan()
    {
        var profiles = new[] { Profile(1_000, 100_000), Profile(0, 100_000), Profile(0, 100_000), Profile(0, 50_000) };
        var roles = new[] { "Blue", "Unused", "Unused", "IR" };
        var plan = BuildPlan(rows: 128, targetPitchMicrometers: 5.0, roles, profiles);
        using var cancellation = new CancellationTokenSource();
        var session = new RecordingScanSession { CancellationSourceToCancelOnScan = cancellation };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().ExecuteAsync(
            session,
            BuildRequest(128, roles, profiles, plan),
            cancellation.Token));

        var firstPass = plan.Passes[0];
        Assert.Equal(
            new[]
            {
                new MotorCommand("Prepare", true, firstPass.MotorStepsPerPass, firstPass.SelectedMotorIntervalNanoseconds, true),
                new MotorCommand("Stop", false, 0, 0, false),
                new MotorCommand("Wait", false, firstPass.MotorStepsPerPass, firstPass.SelectedMotorIntervalNanoseconds, false)
            },
            session.TransportCommands);
    }

    [Fact]
    public async Task ExecuteAsync_CaptureFailureStopsAndWaitsUsingCurrentPassPlan()
    {
        var profiles = new[] { Profile(1_000, 100_000), Profile(0, 100_000), Profile(0, 100_000), Profile(0, 50_000) };
        var roles = new[] { "Blue", "Unused", "Unused", "IR" };
        var plan = BuildPlan(rows: 128, targetPitchMicrometers: 5.0, roles, profiles);
        var session = new RecordingScanSession { ScanFailureMessage = "Capture command failed." };

        await Assert.ThrowsAsync<IOException>(() => CreateService().ExecuteAsync(
            session,
            BuildRequest(128, roles, profiles, plan),
            CancellationToken.None));

        var firstPass = plan.Passes[0];
        Assert.Equal(
            new[]
            {
                new MotorCommand("Prepare", true, firstPass.MotorStepsPerPass, firstPass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Stop", false, 0, 0, false),
                new MotorCommand("Wait", false, firstPass.MotorStepsPerPass, firstPass.SelectedMotorIntervalNanoseconds, false)
            },
            session.TransportCommands);
    }

    [Fact]
    public async Task DeviceGlobalClock_GivenConflictingLegacyProfileClocks_WhenWorkflowRuns_ThenEveryChannelApplyUsesTheRequestClock()
    {
        var parameters = new StubParameterService();
        var profiles = new[]
        {
            Profile(1_000, 30_000),
            Profile(1_000, 200_000),
            Profile(0, 30_000),
            Profile(0, 30_000)
        };
        var request = new ScanWorkflowRequest(
            1,
            false,
            [100, 100, 0, 0],
            ["Blue", "Green", "Unused", "Unused"],
            profiles,
            0,
            0,
            true,
            false,
            1_000,
            125_000,
            EnableMotorTransport: false,
            EnableLedAutoControl: false);

        await CreateService(parameters).ExecuteAsync(new RecordingScanSession(), request, CancellationToken.None);

        Assert.Equal([125_000u, 125_000u], parameters.AppliedSnapshots.Select(snapshot => snapshot.SysClockKhz));
    }

    [Fact]
    public async Task ExecuteAsync_MotorTransportUsesRequestClockForConflictingProfileClocks()
    {
        const uint requestClockKhz = 125_000;
        var profiles = new[]
        {
            Profile(1_000, 30_000),
            Profile(1_000, 200_000),
            Profile(0, 30_000),
            Profile(0, 200_000)
        };
        var roles = new[] { "Blue", "Green", "Unused", "Unused" };
        var effectiveProfiles = profiles
            .Select(profile => profile with { SysClockKhz = requestClockKhz })
            .ToArray();
        var expectedPlan = BuildPlan(128, 5.0, roles, effectiveProfiles, sysClockKhz: requestClockKhz);
        var session = new RecordingScanSession();
        var request = BuildRawRequest(128, roles, profiles, 5.0) with
        {
            SysClockKhz = requestClockKhz
        };

        await CreateService().ExecuteAsync(session, request, CancellationToken.None);

        Assert.Equal(
            expectedPlan.Passes.SelectMany(pass => new[]
            {
                new MotorCommand("Prepare", true, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Wait", false, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false),
                new MotorCommand("Return", false, pass.MotorStepsPerPass, pass.SelectedMotorIntervalNanoseconds, false)
            }),
            session.TransportCommands);
    }

    private static ScanWorkflowService CreateService(
        StubParameterService? parameters = null,
        StubIlluminationService? illumination = null)
        => new(parameters ?? new StubParameterService(), illumination ?? new StubIlluminationService(), new StubTransferSettingsService());

    private static ScanWorkflowRequest BuildRequest(
        int rows,
        string[] roles,
        ScanParameterSnapshot[] profiles,
        ScanLinePitchPlanResult plan,
        bool alternateMotorDirection = false)
        => new(
            rows,
            false,
            [100, 100, 100, 100],
            roles,
            profiles,
            0,
            999_999,
            true,
            alternateMotorDirection,
            0,
            ScanDebugConstants.MinSysClockKhz,
            EnableLedAutoControl: false,
            LinePitchPlan: plan,
            LinePitchPlanInput: plan.InputSnapshot,
            LinePitchInput: CreateRawLinePitchInput(plan));

    private static ScanWorkflowRequest BuildRawRequest(
        int rows,
        string[] roles,
        ScanParameterSnapshot[] profiles,
        double targetPitchMicrometers,
        ScanMotorMechanicalSettings? mechanics = null,
        uint minimumIntervalNanoseconds = ScanDebugConstants.MotionMinIntervalNs,
        uint?[]? intervalOverrides = null,
        bool alternateMotorDirection = false,
        bool enableLedAutoControl = false)
        => new(
            rows,
            false,
            [100, 100, 100, 100],
            roles,
            profiles,
            0,
            999_999,
            true,
            alternateMotorDirection,
            0,
            ScanDebugConstants.MinSysClockKhz,
            EnableLedAutoControl: enableLedAutoControl,
            LinePitchInput: new ScanWorkflowLinePitchInput(
                targetPitchMicrometers,
                mechanics ?? new ScanMotorMechanicalSettings(200, 16, 8.0),
                minimumIntervalNanoseconds,
                intervalOverrides ?? new uint?[roles.Length]));

    private static ScanLinePitchPlanResult BuildPlan(
        int rows,
        double targetPitchMicrometers,
        string[] roles,
        ScanParameterSnapshot[] profiles,
        ScanMotorMechanicalSettings? mechanics = null,
        uint?[]? intervalOverrides = null,
        int[]? passInputOrder = null,
        uint minimumIntervalNanoseconds = ScanDebugConstants.MotionMinIntervalNs,
        uint sysClockKhz = ScanDebugConstants.MinSysClockKhz)
    {
        var passIndexes = (passInputOrder ?? [])
            .Concat(Enumerable.Range(0, roles.Length).Except(passInputOrder ?? []))
            .ToArray();
        var passes = passIndexes.Select(passIndex =>
        {
            var effectiveProfile = profiles[passIndex] with { SysClockKhz = sysClockKhz };
            return new ScanLinePitchPassInput(
                passIndex,
                roles[passIndex],
                !string.Equals(roles[passIndex], "Unused", StringComparison.OrdinalIgnoreCase),
                effectiveProfile.ExposureTicks,
                effectiveProfile.SysClockKhz,
                intervalOverrides?[passIndex],
                effectiveProfile);
        });

        return ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            rows,
            targetPitchMicrometers,
            mechanics ?? new ScanMotorMechanicalSettings(200, 16, 8.0),
            minimumIntervalNanoseconds,
            passes.ToArray()));
    }

    private static ScanWorkflowLinePitchInput CreateRawLinePitchInput(ScanLinePitchPlanResult plan)
    {
        var snapshot = Assert.IsType<ScanLinePitchPlanInputSnapshot>(plan.InputSnapshot);
        return new ScanWorkflowLinePitchInput(
            snapshot.TargetLinePitchMicrometers,
            snapshot.MotorMechanics,
            snapshot.MinimumMotorIntervalNanoseconds,
            snapshot.Passes
                .OrderBy(pass => pass!.PassIndex)
                .Select(pass => pass!.MotorIntervalOverrideNanoseconds)
                .ToArray());
    }

    private static ScanLinePitchPlanResult BuildAlternateDriftBlockerPlan(string[] roles, ScanParameterSnapshot[] profiles)
        => BuildPlan(
            rows: 1,
            targetPitchMicrometers: 100_000.0,
            roles,
            profiles,
            new ScanMotorMechanicalSettings(1, 1, 1.0),
            [4_570, 4_620, 4_570, 4_620],
            sysClockKhz: 100_000);

    private static ScanParameterSnapshot Profile(ushort exposureTicks, uint sysClockKhz)
        => new(exposureTicks, 0, 0, 0, 0, sysClockKhz);

    private static void AssertNoWorkflowSideEffects(
        RecordingScanSession session,
        StubParameterService parameters,
        StubIlluminationService illumination)
    {
        Assert.Equal(0, session.MotionReadCount);
        Assert.Equal(0, illumination.ReadCount);
        Assert.Equal(0, session.WarmUpCallCount);
        Assert.Equal(0, session.MotorEnableCallCount);
        Assert.Equal(0, parameters.ApplyCount);
        Assert.Equal(0, illumination.ApplyCount);
        Assert.Equal(0, session.ScanStartCount);
        Assert.Empty(session.TransportCommands);
    }

    private sealed class StubParameterService : IScanParameterService
    {
        public int ApplyCount { get; private set; }

        public List<ScanParameterSnapshot> AppliedSnapshots { get; } = [];

        public IReadOnlyList<ScanParameterDefinition> Definitions => [];

        public bool TryParseInput(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz, out ScanParameterSnapshot snapshot, out string error)
        {
            snapshot = Profile(0, ScanDebugConstants.MinSysClockKhz);
            error = string.Empty;
            return true;
        }

        public ScanParameterDisplays BuildDisplays(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz)
            => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        public string FormatOffsetForInput(int offset) => offset.ToString();

        public Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct)
            => Task.FromResult(Profile(0, ScanDebugConstants.MinSysClockKhz));

        public Task ApplyGlobalClockAsync(IScanSessionService session, uint sysClockKhz, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct)
        {
            ApplyCount++;
            AppliedSnapshots.Add(snapshot);
            return Task.CompletedTask;
        }
    }

    private sealed class StubIlluminationService : IScanIlluminationService
    {
        public int ReadCount { get; private set; }

        public int ApplyCount { get; private set; }

        public TaskCompletionSource? StateGate { get; init; }

        public TaskCompletionSource StateReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct)
        {
            ReadCount++;
            StateReadStarted.TrySetResult();
            if (StateGate is not null)
                await StateGate.Task;

            return new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        public Task ApplyStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplyStateWithSafeTransitionAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplySingleChannelAsync(IScanSessionService session, ScanFilmAcquisitionSettings settings, byte ledIndex, CancellationToken ct)
        {
            ApplyCount++;
            return Task.CompletedTask;
        }

        public Task TurnOffAsync(IScanSessionService session, CancellationToken ct)
            => Task.CompletedTask;

        public Task RestoreStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class StubTransferSettingsService : IScanTransferSettingsService
    {
        public event EventHandler? BulkInReadModeChanged;

        public ScanBulkInReadMode BulkInReadMode => ScanBulkInReadMode.SingleRequest;
        public ScanBulkInTransferOptions DefaultSettings { get; } = new(ScanBulkInReadMode.SingleRequest, 16_384, 1, ScanDebugConstants.ImageReadTimeoutMs, false);
        public ScanBulkInTransferOptions Settings => DefaultSettings;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _ = BulkInReadModeChanged;
            return Task.CompletedTask;
        }

        public Task SetBulkInReadModeAsync(ScanBulkInReadMode mode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetSettingsAsync(ScanBulkInTransferOptions settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record MotorCommand(
        string Kind,
        bool DirectionPositive,
        uint Steps,
        uint IntervalNanoseconds,
        bool TokenCanBeCanceled);

    private sealed class RecordingScanSession : IScanSessionService
    {
        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets => new(true, null, null);
        public bool IsConnected => true;
        public int SingleTransferMaxRows => ScanDebugConstants.MaxRows;
        public CancellationToken ConnectionToken => CancellationToken.None;
        public List<MotorCommand> TransportCommands { get; } = [];
        public long MotorPositionSteps { get; private set; }
        public int MotorEnableCallCount { get; private set; }

        public int MotionReadCount { get; private set; }

        public int WarmUpCallCount { get; private set; }
        public int ScanStartCount { get; private set; }
        public CancellationTokenSource? CancellationSourceToCancelOnScan { get; init; }
        public string? ScanFailureMessage { get; init; }

        public void RefreshTargets()
        {
            _ = TargetsChanged;
            _ = MotionEventReceived;
        }

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
            => Task.FromResult(new ScanOperationResult(true, string.Empty));

        public Task DisconnectAsync()
            => Task.CompletedTask;

        public Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
            => Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
        {
            MotionReadCount++;
            return Task.FromResult<IReadOnlyList<ScanMotorState>>([new ScanMotorState(0, true, false, false, 0, 0, 0)]);
        }

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
        {
            MotorEnableCallCount++;
            return Task.CompletedTask;
        }

        public Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            TransportCommands.Add(new MotorCommand("Prepare", direction, steps, intervalNs, ct.CanBeCanceled));
            MotorPositionSteps += direction ? steps : -(long)steps;
            return Task.CompletedTask;
        }

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
        {
            TransportCommands.Add(new MotorCommand("Wait", false, steps, intervalNs, ct.CanBeCanceled));
            return Task.FromResult(new ScanMotorState(motorId, true, false, false, 0, intervalNs, 0));
        }

        public Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            TransportCommands.Add(new MotorCommand("Return", direction, steps, intervalNs, ct.CanBeCanceled));
            MotorPositionSteps += direction ? steps : -(long)steps;
            return Task.FromResult(new ScanMotorState(motorId, true, false, direction, 0, intervalNs, 0));
        }

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
        {
            TransportCommands.Add(new MotorCommand("Stop", false, 0, 0, ct.CanBeCanceled));
            return Task.CompletedTask;
        }

        public Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
        {
            WarmUpCallCount++;
            return Task.FromResult(new ScanOperationResult(true, string.Empty));
        }

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
        {
            ScanStartCount++;
            CancellationSourceToCancelOnScan?.Cancel();
            ct.ThrowIfCancellationRequested();
            if (ScanFailureMessage is not null)
                return Task.FromResult(new ScanStartResult(false, ScanFailureMessage, null));

            var imageBytes = new byte[rows * ScanDebugConstants.BytesPerLine];
            onRowsAvailable?.Invoke(imageBytes, rows);
            return Task.FromResult(new ScanStartResult(true, string.Empty, imageBytes));
        }

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => StartScanAsync(totalRows, ct, onStatus, onDiagnostic, onProgress, onRowsAvailable, expectedLineTimeUs);

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromResult(new ScanStopResult(true, string.Empty));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromResult(new ScanControlFrame(expectedCommand, 0, []));

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
