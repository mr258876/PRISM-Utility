using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanIlluminationServiceTests
{
    public enum FailurePoint
    {
        None,
        Levels,
        Pulses,
        FinalSteady,
        FinalSync
    }

    public enum CancellationPoint
    {
        None,
        Levels,
        Pulses
    }

    private readonly ScanIlluminationService _service = new();

    [Fact]
    public async Task GetStateAsync_DelegatesToSession()
    {
        var expected = CreateValidState();
        var session = new RecordingScanSessionService { StateToReturn = expected };

        var actual = await _service.GetStateAsync(session, CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.Equal(["get"], session.Calls);
    }

    [Fact]
    public async Task ApplyStateAsync_UsesEstablishedCommandOrder()
    {
        var state = CreateValidState(steadyMask: 0b0001, syncMask: 0b0010);
        var session = new RecordingScanSessionService();

        await _service.ApplyStateAsync(session, state, CancellationToken.None);

        Assert.Equal(
            ["levels:11,22,33,44", "pulses:2,3,4,5", "steady:1", "sync:2"],
            session.Calls);
    }

    [Fact]
    public async Task ApplyStateWithSafeTransitionAsync_DisablesActiveModesBeforeApplying()
    {
        var session = new RecordingScanSessionService
        {
            StateToReturn = CreateValidState(steadyMask: 0b0100, syncMask: 0b0010)
        };
        var target = CreateValidState(steadyMask: 0b0001, syncMask: 0b1000);

        await _service.ApplyStateWithSafeTransitionAsync(session, target, CancellationToken.None);

        Assert.Equal(
            ["get", "sync:0", "steady:0", "levels:11,22,33,44", "pulses:2,3,4,5", "steady:1", "sync:8"],
            session.Calls);
    }

    [Fact]
    public async Task ApplySingleChannelAsync_DefaultsSelectedChannelToSteady()
    {
        var settings = new ScanFilmAcquisitionSettings(
            101,
            202,
            303,
            404,
            0,
            0,
            2,
            3,
            4,
            5,
            ScanDebugConstants.MotionDefaultIntervalNs);
        var session = new RecordingScanSessionService();

        await _service.ApplySingleChannelAsync(session, settings, 2, CancellationToken.None);

        Assert.Equal(
            ["levels:0,0,303,0", "pulses:2,3,4,5", "steady:4", "sync:0"],
            session.Calls);
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData(byte.MaxValue)]
    public async Task ApplySingleChannelAsync_WhenLedIndexIsOutOfRange_ThrowsBeforeSessionCalls(byte ledIndex)
    {
        var session = new RecordingScanSessionService();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ApplySingleChannelAsync(session, ScanFilmAcquisitionSettings.CreateDefault(), ledIndex, CancellationToken.None));

        Assert.Equal(nameof(ledIndex), exception.ParamName);
        Assert.Empty(session.Calls);
    }

    [Fact]
    public async Task TurnOffAsync_DisablesSyncBeforeSteady()
    {
        var session = new RecordingScanSessionService();

        await _service.TurnOffAsync(session, CancellationToken.None);

        Assert.Equal(["sync:0", "steady:0"], session.Calls);
    }

    [Fact]
    public async Task RestoreStateAsync_UsesSafeRestoreOrder()
    {
        var state = CreateValidState(steadyMask: 0b0001, syncMask: 0b0010);
        var session = new RecordingScanSessionService();

        await _service.RestoreStateAsync(session, state, CancellationToken.None);

        Assert.Equal(
            ["sync:0", "steady:0", "levels:11,22,33,44", "pulses:2,3,4,5", "steady:1", "sync:2"],
            session.Calls);
    }

    [Fact]
    public async Task ApplyStateAsync_WhenSteadyAndSyncOverlap_ThrowsFieldSpecificIssuesBeforeWrites()
    {
        var state = CreateValidState(steadyMask: 0b0101, syncMask: 0b0101);
        var session = new RecordingScanSessionService();

        var exception = await Assert.ThrowsAsync<ScanIlluminationValidationException>(
            () => _service.ApplyStateAsync(session, state, CancellationToken.None));

        Assert.Equal(
            [
                ("rawIllumination.led1.mode", ScanIlluminationValidationCode.SteadySyncOverlap),
                ("rawIllumination.led3.mode", ScanIlluminationValidationCode.SteadySyncOverlap)
            ],
            exception.Validation.Issues.Select(issue => (issue.FieldPath, issue.Code)).ToArray());
        Assert.Empty(session.Calls);
    }

    [Fact]
    public async Task ApplyStateWithSafeTransitionAsync_WhenMasksContainOutOfRangeBits_RejectsBeforeSessionRead()
    {
        var state = CreateValidState(steadyMask: 0b0001_0000, syncMask: 0b0010_0000);
        var session = new RecordingScanSessionService
        {
            StateToReturn = CreateValidState(steadyMask: 0b0001, syncMask: 0b0010)
        };

        var exception = await Assert.ThrowsAsync<ScanIlluminationValidationException>(
            () => _service.ApplyStateWithSafeTransitionAsync(session, state, CancellationToken.None));

        Assert.Equal(
            [
                ("rawIllumination.steadyMask", ScanIlluminationValidationCode.InvalidMaskBits),
                ("rawIllumination.syncMask", ScanIlluminationValidationCode.InvalidMaskBits)
            ],
            exception.Validation.Issues.Select(issue => (issue.FieldPath, issue.Code)).ToArray());
        Assert.Empty(session.Calls);
    }

    [Fact]
    public async Task RestoreStateAsync_WhenSyncPulseClockIsBelowMinimum_ThrowsExactLedPulseIssueBeforeWrites()
    {
        var state = CreateValidState(steadyMask: 0, syncMask: 0b0010) with { Led2PulseClock = 1 };
        var session = new RecordingScanSessionService();

        var exception = await Assert.ThrowsAsync<ScanIlluminationValidationException>(
            () => _service.RestoreStateAsync(session, state, CancellationToken.None));

        Assert.Equal(
            [("rawIllumination.led2.pulseClock", ScanIlluminationValidationCode.SyncPulseClockBelowMinimum)],
            exception.Validation.Issues.Select(issue => (issue.FieldPath, issue.Code)).ToArray());
        Assert.Empty(session.Calls);
    }

    [Fact]
    public void ValidateState_DoesNotMutateInputAndReturnsStableIssues()
    {
        var state = CreateValidState(steadyMask: 0b0101, syncMask: 0b0111) with { Led2PulseClock = 1 };

        var first = ScanIlluminationValidator.ValidateState(state, "rawIllumination");
        var second = ScanIlluminationValidator.ValidateState(state, "rawIllumination");

        Assert.Equal(CreateValidState(steadyMask: 0b0101, syncMask: 0b0111) with { Led2PulseClock = 1 }, state);
        Assert.Equal(first.Issues.ToArray(), second.Issues.ToArray());
        Assert.Equal(
            [
                ("rawIllumination.led1.mode", ScanIlluminationValidationCode.SteadySyncOverlap),
                ("rawIllumination.led3.mode", ScanIlluminationValidationCode.SteadySyncOverlap),
                ("rawIllumination.led2.pulseClock", ScanIlluminationValidationCode.SyncPulseClockBelowMinimum)
            ],
            first.Issues.Select(issue => (issue.FieldPath, issue.Code)).ToArray());
    }

    [Fact]
    public async Task ApplyStateAsync_WhenPreCanceledAndSessionHonorsCancellation_RecordsNoWrites()
    {
        var session = new RecordingScanSessionService { ThrowWhenCanceled = true };
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ApplyStateAsync(session, CreateValidState(), cancellationSource.Token));

        Assert.Empty(session.Calls);
    }

    [Fact]
    public async Task RestoreStateAsync_InvalidStateMakesZeroCalls()
    {
        var session = new RecordingScanSessionService();

        await Assert.ThrowsAsync<ScanIlluminationValidationException>(
            () => _service.RestoreStateAsync(session, CreateValidState(1, 1), CancellationToken.None));

        Assert.Empty(session.AttemptedCalls);
        Assert.Empty(session.CompletedCalls);
    }

    [Fact]
    public async Task RestoreStateAsync_NeverReadsAndDisablesFirst()
    {
        var session = new RecordingScanSessionService { ThrowOnGet = true };

        await _service.RestoreStateAsync(session, CreateValidState(1, 2), CancellationToken.None);

        Assert.Equal(["sync:0", "steady:0"], session.CompletedCalls.Take(2));
        Assert.DoesNotContain("get", session.AttemptedCalls);
    }

    [Theory]
    [InlineData(FailurePoint.Levels)]
    [InlineData(FailurePoint.Pulses)]
    [InlineData(FailurePoint.FinalSync)]
    public async Task RestoreStateAsync_FailurePropagatesWithoutFinalEnable(FailurePoint failure)
    {
        var session = new RecordingScanSessionService { Failure = failure };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RestoreStateAsync(session, CreateValidState(1, 2), CancellationToken.None));

        string[] expectedAttempted = failure switch
        {
            FailurePoint.Levels => ["sync:0", "steady:0", "levels:11,22,33,44"],
            FailurePoint.Pulses => ["sync:0", "steady:0", "levels:11,22,33,44", "pulses:2,3,4,5"],
            FailurePoint.FinalSync => ["sync:0", "steady:0", "levels:11,22,33,44", "pulses:2,3,4,5", "steady:1", "sync:2"],
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        Assert.Equal(expectedAttempted, session.AttemptedCalls);

        string[] expectedCompleted = failure switch
        {
            FailurePoint.Levels => ["sync:0", "steady:0"],
            FailurePoint.Pulses => ["sync:0", "steady:0", "levels:11,22,33,44"],
            FailurePoint.FinalSync => ["sync:0", "steady:0", "levels:11,22,33,44", "pulses:2,3,4,5", "steady:1"],
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        Assert.Equal(expectedCompleted, session.CompletedCalls);
        Assert.DoesNotContain("steady:1", session.AttemptedCalls.SkipWhile(call => call != "sync:2"));
    }

    [Theory]
    [InlineData(CancellationPoint.Levels)]
    [InlineData(CancellationPoint.Pulses)]
    public async Task RestoreStateAsync_InjectedCancellationPropagatesWithoutTargetEnable(CancellationPoint cancellationPoint)
    {
        var session = new RecordingScanSessionService { Cancellation = cancellationPoint };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.RestoreStateAsync(session, CreateValidState(1, 2), CancellationToken.None));

        string[] expectedAttempted = cancellationPoint == CancellationPoint.Levels
            ? ["sync:0", "steady:0", "levels:11,22,33,44"]
            : ["sync:0", "steady:0", "levels:11,22,33,44", "pulses:2,3,4,5"];
        Assert.Equal(expectedAttempted, session.AttemptedCalls);
        Assert.DoesNotContain("steady:1", session.AttemptedCalls);
        Assert.DoesNotContain("sync:2", session.AttemptedCalls);
    }

    [Fact]
    public async Task RestoreStateAsync_PreCanceledHasZeroCalls()
    {
        using var c = new CancellationTokenSource();
        c.Cancel();
        var session = new RecordingScanSessionService { ThrowWhenCanceled = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.RestoreStateAsync(session, CreateValidState(1, 2), c.Token));

        Assert.Empty(session.AttemptedCalls);
        Assert.Empty(session.CompletedCalls);
    }
    private static ScanIlluminationState CreateValidState(byte steadyMask = 0, byte syncMask = 0)
        => new(11, 22, 33, 44, steadyMask, syncMask, 0, 2, 3, 4, 5);

    private sealed class RecordingScanSessionService : IScanSessionService
    {
        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public List<string> Calls { get; } = [];
        public List<string> AttemptedCalls { get; } = [];
        public List<string> CompletedCalls { get; } = [];
        public FailurePoint Failure { get; set; }
        public CancellationPoint Cancellation { get; set; }
        public bool ThrowOnGet { get; set; }
        public ScanIlluminationState StateToReturn { get; set; } = CreateValidState();
        public bool ThrowWhenCanceled { get; set; }
        public ScanTargetState Targets => new(true, "bulk-in", "bulk-out");
        public bool IsConnected => true;
        public int SingleTransferMaxRows => 1;
        public CancellationToken ConnectionToken => CancellationToken.None;

        public void RefreshTargets()
            => TargetsChanged?.Invoke(this, EventArgs.Empty);

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
            => Task.FromResult(new ScanOperationResult(true, "Connected."));

        public Task DisconnectAsync()
            => Task.CompletedTask;

        public Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
        {
            ThrowIfCanceled(ct);
            if (ThrowOnGet) throw new InvalidOperationException("get called");
            Calls.Add("get");
            return Task.FromResult(StateToReturn);
        }

        public Task SetIlluminationLevelsAsync(ushort a, ushort b, ushort c, ushort d, CancellationToken ct) => RecordAsync($"levels:{a},{b},{c},{d}", FailurePoint.Levels, ct);

        public Task SetSteadyIlluminationAsync(byte mask, CancellationToken ct) => RecordAsync($"steady:{mask}", mask == 0 ? FailurePoint.None : FailurePoint.FinalSteady, ct);

        public Task ConfigureExposureLightingAsync(byte mask, CancellationToken ct) => RecordAsync($"sync:{mask}", mask == 0 ? FailurePoint.None : FailurePoint.FinalSync, ct);

        public Task SetSyncPulseClocksAsync(uint a, uint b, uint c, uint d, CancellationToken ct) => RecordAsync($"pulses:{a},{b},{c},{d}", FailurePoint.Pulses, ct);

        public Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ScanMotorState>>([]);

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
            => Task.CompletedTask;

        public Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, false, false, false, 0, intervalNs, 0));

        public Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, false, false, direction, 0, intervalNs, 0));

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
            => Task.FromResult(new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled."));

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromResult(new ScanStopResult(true, "Stopped."));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromException<ScanControlFrame>(new NotSupportedException());

        public void Dispose()
        {
            _ = MotionEventReceived;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private Task RecordAsync(string call, FailurePoint failure, CancellationToken ct)
        {
            ThrowIfCanceled(ct);
            AttemptedCalls.Add(call); Calls.Add(call);
            if (Failure != FailurePoint.None && Failure == failure)
                return Task.FromException(new InvalidOperationException("injected"));

            var cancellationPoint = failure switch
            {
                FailurePoint.Levels => CancellationPoint.Levels,
                FailurePoint.Pulses => CancellationPoint.Pulses,
                _ => CancellationPoint.None
            };
            if (Cancellation != CancellationPoint.None && Cancellation == cancellationPoint)
            {
                using var cancellationSource = new CancellationTokenSource();
                cancellationSource.Cancel();
                return Task.FromException(new OperationCanceledException(cancellationSource.Token));
            }

            CompletedCalls.Add(call);
            return Task.CompletedTask;
        }

        private void ThrowIfCanceled(CancellationToken ct)
        {
            if (ThrowWhenCanceled)
                ct.ThrowIfCancellationRequested();
        }
    }
}
