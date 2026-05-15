using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanWorkflowServiceTests
{
    [Fact]
    public async Task ExecuteAsync_NonAlternatingDirection_TurnsOffIlluminationBeforeEveryReturn()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        var result = await service.ExecuteAsync(session, BuildRequest(alternateMotorDirection: false), CancellationToken.None);

        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, result.Passes.Count);
        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, log.Count(entry => entry == "IlluminationOff"));
        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, log.Count(entry => entry.StartsWith("Return:", StringComparison.Ordinal)));

        for (var passIndex = 0; passIndex < ScanDebugConstants.IlluminationChannelCount; passIndex++)
        {
            var scanIndex = log.IndexOf($"Scan:{passIndex}");
            var waitIndex = FindNthPrefixIndex(log, "Wait:", passIndex + 1);
            var offIndex = FindNthIndex(log, "IlluminationOff", passIndex + 1);
            var returnIndex = FindNthPrefixIndex(log, "Return:", passIndex + 1);

            Assert.True(scanIndex >= 0, $"Pass {passIndex + 1} should scan.");
            Assert.True(waitIndex > scanIndex, $"Pass {passIndex + 1} should wait for scan motion after scanning.");
            Assert.True(offIndex > waitIndex, $"Pass {passIndex + 1} should turn illumination off after scan motion stops.");
            Assert.True(returnIndex > offIndex, $"Pass {passIndex + 1} should return after illumination is off.");
        }
    }

    [Fact]
    public async Task ExecuteAsync_MotorTransportDisabled_ScansWithoutMotorMotion()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        var result = await service.ExecuteAsync(session, BuildRequest(alternateMotorDirection: true, enableMotorTransport: false), CancellationToken.None);

        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, result.Passes.Count);
        Assert.All(result.Passes, pass => Assert.Equal(0u, pass.MotorSteps));
        Assert.DoesNotContain(log, entry => entry.StartsWith("Prepare:", StringComparison.Ordinal));
        Assert.DoesNotContain(log, entry => entry.StartsWith("Wait:", StringComparison.Ordinal));
        Assert.DoesNotContain(log, entry => entry.StartsWith("Return:", StringComparison.Ordinal));
        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, log.Count(entry => entry.StartsWith("Scan:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ExecuteAsync_LedAutoControlDisabled_ScansWithoutIlluminationChanges()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        var result = await service.ExecuteAsync(session, BuildRequest(alternateMotorDirection: true, enableLedAutoControl: false), CancellationToken.None);

        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, result.Passes.Count);
        Assert.DoesNotContain(log, entry => entry.StartsWith("IlluminationOn:", StringComparison.Ordinal));
        Assert.DoesNotContain("IlluminationOff", log);
        Assert.DoesNotContain("RestoreIllumination", log);
        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, log.Count(entry => entry.StartsWith("Scan:", StringComparison.Ordinal)));
    }

    private static ScanWorkflowRequest BuildRequest(bool alternateMotorDirection, bool enableMotorTransport = true, bool enableLedAutoControl = true)
    {
        var profiles = Enumerable.Range(0, ScanDebugConstants.IlluminationChannelCount)
            .Select(_ => new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz))
            .ToArray();

        return new ScanWorkflowRequest(
            1,
            false,
            [100, 100, 100, 100],
            ["Blue", "Green", "Red", "IR"],
            profiles,
            0,
            ScanDebugConstants.MotionDefaultIntervalNs,
            true,
            alternateMotorDirection,
            0,
            ScanDebugConstants.MinSysClockKhz,
            EnableMotorTransport: enableMotorTransport,
            EnableLedAutoControl: enableLedAutoControl);
    }

    private static int FindNthIndex(IReadOnlyList<string> values, string value, int occurrence)
    {
        var seen = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] != value)
                continue;

            seen++;
            if (seen == occurrence)
                return i;
        }

        return -1;
    }

    private static int FindNthPrefixIndex(IReadOnlyList<string> values, string prefix, int occurrence)
    {
        var seen = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (!values[i].StartsWith(prefix, StringComparison.Ordinal))
                continue;

            seen++;
            if (seen == occurrence)
                return i;
        }

        return -1;
    }

    private sealed class RecordingParameterService(List<string> log) : IScanParameterService
    {
        public IReadOnlyList<ScanParameterDefinition> Definitions => Array.Empty<ScanParameterDefinition>();

        public bool TryParseInput(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz, out ScanParameterSnapshot snapshot, out string error)
        {
            snapshot = new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);
            error = string.Empty;
            return true;
        }

        public ScanParameterDisplays BuildDisplays(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz)
            => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        public string FormatOffsetForInput(int offset)
            => offset.ToString();

        public Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct)
            => Task.FromResult(new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz));

        public Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct)
        {
            log.Add("ApplyParameters");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingIlluminationService(List<string> log) : IScanIlluminationService
    {
        public Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct)
            => Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2));

        public Task ApplyStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplyStateWithSafeTransitionAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplySingleChannelAsync(IScanSessionService session, ScanFilmAcquisitionSettings settings, byte ledIndex, CancellationToken ct)
        {
            log.Add($"IlluminationOn:{ledIndex}");
            return Task.CompletedTask;
        }

        public Task TurnOffAsync(IScanSessionService session, CancellationToken ct)
        {
            log.Add("IlluminationOff");
            return Task.CompletedTask;
        }

        public Task RestoreStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
        {
            log.Add("RestoreIllumination");
            return Task.CompletedTask;
        }
    }

    private sealed class StubTransferSettingsService : IScanTransferSettingsService
    {
        public event EventHandler? BulkInReadModeChanged;

        public ScanBulkInReadMode BulkInReadMode => ScanBulkInReadMode.SingleRequest;

        public ScanBulkInTransferOptions DefaultSettings { get; } = new(ScanBulkInReadMode.SingleRequest, 16 * 1024, 1, ScanDebugConstants.ImageReadTimeoutMs, false);

        public ScanBulkInTransferOptions Settings => DefaultSettings;

        public Task InitializeAsync()
        {
            _ = BulkInReadModeChanged;
            return Task.CompletedTask;
        }

        public Task SetBulkInReadModeAsync(ScanBulkInReadMode mode)
            => Task.CompletedTask;

        public Task SetSettingsAsync(ScanBulkInTransferOptions settings)
            => Task.CompletedTask;
    }

    private sealed class RecordingScanSession(List<string> log) : IScanSessionService
    {
        private int _scanIndex;

        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets => new(true, null, null);
        public bool IsConnected => true;
        public int SingleTransferMaxRows => ScanDebugConstants.MaxRows;
        public CancellationToken ConnectionToken => CancellationToken.None;

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
            => Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2));

        public Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ScanMotorState>>([new ScanMotorState(0, true, false, false, 0, 0, 0)]);

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
            => Task.CompletedTask;

        public Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            log.Add($"Prepare:{direction}:{steps}");
            return Task.CompletedTask;
        }

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
        {
            log.Add($"Wait:{steps}");
            return Task.FromResult(new ScanMotorState(motorId, true, false, false, 0, intervalNs, 0));
        }

        public Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            log.Add($"Return:{direction}:{steps}");
            return Task.FromResult(new ScanMotorState(motorId, true, false, direction, 0, intervalNs, 0));
        }

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
            => Task.FromResult(new ScanOperationResult(true, string.Empty));

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
        {
            log.Add($"Scan:{_scanIndex++}");
            return Task.FromResult(new ScanStartResult(true, string.Empty, new byte[rows * ScanDebugConstants.BytesPerLine]));
        }

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
            => StartScanAsync(totalRows, ct, onStatus, onDiagnostic, onProgress, expectedLineTimeUs);

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromResult(new ScanStopResult(true, string.Empty));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromResult(new ScanControlFrame(expectedCommand, 0, Array.Empty<byte>()));

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
