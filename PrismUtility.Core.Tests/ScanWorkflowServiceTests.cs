using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanWorkflowServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WarmUpDisabled_PreservesExistingPassOrderWithoutWarmUpCalls()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        var result = await service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: false),
            CancellationToken.None);

        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, result.Passes.Count);
        Assert.DoesNotContain(log, entry => entry.StartsWith("WarmUp:", StringComparison.Ordinal));
        Assert.Equal(
            ["Scan:0", "Scan:1", "Scan:2", "Scan:3"],
            log.Where(entry => entry.StartsWith("Scan:", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpEnabled_EnablesBeforeFirstCapture()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        await service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None);

        var enableIndex = log.IndexOf("WarmUp:True");
        var firstCaptureIndex = log.IndexOf("Scan:0");
        Assert.True(enableIndex >= 0, "Warm-up should be enabled.");
        Assert.True(firstCaptureIndex > enableIndex, "Warm-up should be enabled before the first capture.");
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpEnabled_DisablesAfterSuccessfulWorkflow()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        var result = await service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None);

        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, result.Passes.Count);
        Assert.Equal([true, false], session.WarmUpCalls.Select(call => call.Enabled).ToArray());
        Assert.False(session.WarmUpCalls.Single(call => !call.Enabled).TokenCanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpEnabled_DisablesAfterCancellationWithCleanupSafeToken()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        using var cts = new CancellationTokenSource();
        var session = new RecordingScanSession(log) { CancellationSourceToCancelOnScan = cts };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            cts.Token));

        Assert.Equal([true, false], session.WarmUpCalls.Select(call => call.Enabled).ToArray());
        Assert.False(session.WarmUpCalls.Single(call => !call.Enabled).TokenCanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpEnabled_DisablesAfterCaptureFailureWithCleanupSafeToken()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log) { ScanFailureMessage = "Capture command failed." };

        var exception = await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None));

        Assert.Contains("Pass 1 failed", exception.Message, StringComparison.Ordinal);
        Assert.Equal([true, false], session.WarmUpCalls.Select(call => call.Enabled).ToArray());
        Assert.False(session.WarmUpCalls.Single(call => !call.Enabled).TokenCanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpEnableFailure_AbortsBeforeCaptureWithClearFailure()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log) { WarmUpEnableFailureMessage = "Controller rejected warm-up." };

        var exception = await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None));

        Assert.Equal("Scan workflow warm-up enable failed: Controller rejected warm-up.", exception.Message);
        Assert.Equal([true], session.WarmUpCalls.Select(call => call.Enabled).ToArray());
        Assert.DoesNotContain(log, entry => entry.StartsWith("Scan:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpDisableFailure_ReportsDiagnosticWithoutConvertingSuccessfulWorkflow()
    {
        var log = new List<string>();
        var diagnostics = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log) { WarmUpDisableFailureMessage = "Stop command rejected." };

        var result = await service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None,
            onDiagnostic: diagnostics.Add);

        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, result.Passes.Count);
        Assert.Equal([true, false], session.WarmUpCalls.Select(call => call.Enabled).ToArray());
        Assert.Contains("Scan workflow warm-up cleanup failed: Stop command rejected.", diagnostics);
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpDisableFailure_DiagnosticCallbackFailureDoesNotConvertSuccessfulWorkflow()
    {
        var log = new List<string>();
        var diagnosticCalls = 0;
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log) { WarmUpDisableFailureMessage = "Stop command rejected." };

        var result = await service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None,
            onDiagnostic: _ =>
            {
                diagnosticCalls++;
                throw new InvalidOperationException("Diagnostic observer failed.");
            });

        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, result.Passes.Count);
        Assert.Equal(1, diagnosticCalls);
        Assert.Equal([true, false], session.WarmUpCalls.Select(call => call.Enabled).ToArray());
    }

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
    public async Task ExecuteAsync_PropagatesWorkflowRowAvailabilityMetadata()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);
        var snapshots = new List<ScanWorkflowRowsAvailable>();

        await service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true),
            CancellationToken.None,
            onRowsAvailable: snapshot =>
            {
                lock (snapshots)
                    snapshots.Add(snapshot);
            });

        await WaitForAsync(() =>
        {
            lock (snapshots)
                return snapshots.Count == ScanDebugConstants.IlluminationChannelCount;
        });

        lock (snapshots)
        {
            Assert.Collection(
                snapshots.OrderBy(snapshot => snapshot.CurrentPass),
                snapshot => AssertWorkflowSnapshot(snapshot, 1, 0, 0, true, "Blue"),
                snapshot => AssertWorkflowSnapshot(snapshot, 2, 1, 1, false, "Green"),
                snapshot => AssertWorkflowSnapshot(snapshot, 3, 2, 2, true, "Red"),
                snapshot => AssertWorkflowSnapshot(snapshot, 4, 3, 3, false, "IR"));
        }
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

    [Fact]
    public async Task ExecuteAsync_SparseSelectedPass_AppliesMatchingPhysicalLedSettings()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        var request = BuildRequest(alternateMotorDirection: true) with
        {
            LedLevels = [0, 0, 345, 0],
            PassChannelRoles = ["Unused", "Unused", "Red", "Unused"],
            AcquisitionSettings = new ScanFilmAcquisitionSettings(
                0,
                0,
                345,
                0,
                0x04,
                0,
                ScanDebugConstants.IlluminationMinSyncPulseClock,
                ScanDebugConstants.IlluminationMinSyncPulseClock,
                ScanDebugConstants.IlluminationMinSyncPulseClock,
                ScanDebugConstants.IlluminationMinSyncPulseClock,
                ScanDebugConstants.MotionDefaultIntervalNs)
        };

        var result = await service.ExecuteAsync(session, request, CancellationToken.None);

        var pass = Assert.Single(result.Passes);
        Assert.Equal(2, pass.LedChannelIndex);
        Assert.Contains("IlluminationOn:2:0,0,345,0:4:0", log);
        Assert.Equal(1, log.Count(entry => entry == "IlluminationOff"));
    }

    private static ScanWorkflowRequest BuildRequest(bool alternateMotorDirection, bool enableMotorTransport = true, bool enableLedAutoControl = true, bool warmUpEnabled = false)
    {
        var profiles = Enumerable.Range(0, ScanDebugConstants.IlluminationChannelCount)
            .Select(_ => new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz))
            .ToArray();

        return new ScanWorkflowRequest(
            1,
            warmUpEnabled,
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(20);
        }

        Assert.True(condition(), "Timed out waiting for workflow row callback propagation.");
    }

    private static void AssertWorkflowSnapshot(ScanWorkflowRowsAvailable snapshot, int currentPass, int passIndex, byte ledChannelIndex, bool directionPositive, string channelRole)
    {
        Assert.Equal(currentPass, snapshot.CurrentPass);
        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, snapshot.TotalPasses);
        Assert.Equal(passIndex, snapshot.PassIndex);
        Assert.Equal(ledChannelIndex, snapshot.LedChannelIndex);
        Assert.Equal(directionPositive, snapshot.DirectionPositive);
        Assert.Equal(channelRole, snapshot.ChannelRole);
        Assert.Equal(1, snapshot.CompletedRows);
        Assert.Equal(ScanDebugConstants.BytesPerLine, snapshot.ImageBytes.Length);
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
            log.Add($"IlluminationOn:{ledIndex}:{settings.Led1Level},{settings.Led2Level},{settings.Led3Level},{settings.Led4Level}:{settings.SteadyMask}:{settings.SyncMask}");
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

    private sealed record WarmUpCall(bool Enabled, bool TokenCanBeCanceled);

    private sealed class RecordingScanSession(List<string> log) : IScanSessionService
    {
        private int _scanIndex;

        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets => new(true, null, null);
        public bool IsConnected => true;
        public int SingleTransferMaxRows => ScanDebugConstants.MaxRows;
        public CancellationToken ConnectionToken => CancellationToken.None;
        public List<WarmUpCall> WarmUpCalls { get; } = [];
        public CancellationTokenSource? CancellationSourceToCancelOnScan { get; init; }
        public string? ScanFailureMessage { get; init; }
        public string? WarmUpEnableFailureMessage { get; init; }
        public string? WarmUpDisableFailureMessage { get; init; }

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
        {
            log.Add($"WarmUp:{enabled}");
            WarmUpCalls.Add(new WarmUpCall(enabled, ct.CanBeCanceled));
            if (enabled && WarmUpEnableFailureMessage is not null)
                return Task.FromResult(new ScanOperationResult(false, WarmUpEnableFailureMessage));

            if (!enabled && WarmUpDisableFailureMessage is not null)
                return Task.FromResult(new ScanOperationResult(false, WarmUpDisableFailureMessage));

            return Task.FromResult(new ScanOperationResult(true, string.Empty));
        }

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
        {
            log.Add($"Scan:{_scanIndex++}");
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
            => Task.FromResult(new ScanControlFrame(expectedCommand, 0, Array.Empty<byte>()));

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
