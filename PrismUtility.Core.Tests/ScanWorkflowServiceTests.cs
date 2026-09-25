using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
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

        var result = await service.ExecuteAsync(
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
    public async Task ExecuteAsync_PrimaryFailureAndMotorCleanupFailures_AttemptsStopWaitRestoreAndDisable()
    {
        var log = new List<string>();
        var illumination = new RecordingIlluminationService(log) { RestoreFailure = new IOException("restore failed") };
        var service = new ScanWorkflowService(new RecordingParameterService(log), illumination, new StubTransferSettingsService());
        var session = new RecordingScanSession(log)
        {
            ScanFailureMessage = "capture failed",
            StopMotorFailure = new IOException("stop failed"),
            MotorDisableFailure = new IOException("disable failed"),
            InitialMotorEnabled = false
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true),
            CancellationToken.None));

        var primary = Assert.IsType<IOException>(exception.InnerExceptions[0]);
        Assert.Contains("Pass 1 failed: capture failed", primary.Message, StringComparison.Ordinal);
        Assert.Contains("Stop:0", log);
        Assert.Contains("Wait:", string.Join('|', log), StringComparison.Ordinal);
        Assert.Contains("RestoreIllumination", log);
        Assert.Contains((byte)0, session.DisabledMotorIds);
        Assert.Equal(4, exception.InnerExceptions.Count);
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
    public async Task ExecuteAsync_WarmUpDisableFailure_ReportsDiagnosticAndSurfacesCleanupFault()
    {
        var log = new List<string>();
        var diagnostics = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log) { WarmUpDisableFailureMessage = "Stop command rejected." };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None,
            onDiagnostic: diagnostics.Add));

        Assert.IsType<IOException>(Assert.Single(exception.InnerExceptions));
        Assert.Equal([true, false], session.WarmUpCalls.Select(call => call.Enabled).ToArray());
        Assert.Contains("Scan workflow warm-up cleanup failed: Stop command rejected.", diagnostics);
    }

    [Fact]
    public async Task ExecuteAsync_WarmUpDisableFailure_DiagnosticCallbackFailureDoesNotMaskCleanupFault()
    {
        var log = new List<string>();
        var diagnosticCalls = 0;
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log) { WarmUpDisableFailureMessage = "Stop command rejected." };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, warmUpEnabled: true),
            CancellationToken.None,
            onDiagnostic: _ =>
            {
                diagnosticCalls++;
                throw new InvalidOperationException("Diagnostic observer failed.");
            }));

        Assert.IsType<IOException>(Assert.Single(exception.InnerExceptions));
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

        var result = await service.ExecuteAsync(
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
                snapshot => AssertWorkflowSnapshot(snapshot, result, 1, 0, 0, true, "Blue"),
                snapshot => AssertWorkflowSnapshot(snapshot, result, 2, 1, 1, false, "Green"),
                snapshot => AssertWorkflowSnapshot(snapshot, result, 3, 2, 2, true, "Red"),
                snapshot => AssertWorkflowSnapshot(snapshot, result, 4, 3, 3, false, "IR"));
        }
    }

    [Fact]
    public async Task ExecuteAsync_RowsAvailableDetachesBytesBeforeAsyncQueue()
    {
        var log = new List<string>();
        var backingBuffer = Enumerable.Repeat((byte)0x11, ScanDebugConstants.BytesPerLine).ToArray();
        var producerCallbackReturned = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action? queuedWorkItem = null;
        var service = new ScanWorkflowService(
            new RecordingParameterService(log),
            new RecordingIlluminationService(log),
            new StubTransferSettingsService(),
            workItem =>
            {
                Assert.Null(queuedWorkItem);
                queuedWorkItem = workItem;
            });
        var session = new RecordingScanSession(log)
        {
            ScanImageBytes = backingBuffer,
            OnRowsAvailableReturned = () => producerCallbackReturned.TrySetResult(null)
        };
        ScanWorkflowRowsAvailable? deliveredSnapshot = null;
        var request = BuildRequest(alternateMotorDirection: false, enableMotorTransport: false, enableLedAutoControl: false) with
        {
            PassChannelRoles = ["Blue", "Unused", "Unused", "Unused"]
        };

        var workflowTask = service.ExecuteAsync(
            session,
            request,
            CancellationToken.None,
            onRowsAvailable: snapshot => deliveredSnapshot = snapshot);
        await producerCallbackReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(queuedWorkItem);
        Assert.Null(deliveredSnapshot);
        Array.Fill(backingBuffer, (byte)0x22);
        queuedWorkItem();

        var snapshot = Assert.IsType<ScanWorkflowRowsAvailable>(deliveredSnapshot);
        Assert.Equal(1, snapshot.CurrentPass);
        Assert.Equal(1, snapshot.TotalPasses);
        Assert.Equal(0, snapshot.PassIndex);
        Assert.Equal((byte)0, snapshot.LedChannelIndex);
        Assert.True(snapshot.DirectionPositive);
        Assert.Equal("Blue", snapshot.ChannelRole);
        Assert.Equal(1, snapshot.CompletedRows);
        Assert.Equal(0u, snapshot.MotorSteps);
        Assert.Equal(0u, snapshot.MotorIntervalNanoseconds);
        Assert.All(snapshot.ImageBytes, value => Assert.Equal((byte)0x11, value));

        await workflowTask;
    }

    [Fact]
    public async Task ExecuteAsync_RowsAvailableEmitsDetachedContiguousDeltasInFifoOrder()
    {
        var log = new List<string>();
        var backingBuffer = new byte[ScanDebugConstants.BytesPerLine * 2];
        Array.Fill(backingBuffer, (byte)0x11, 0, ScanDebugConstants.BytesPerLine);
        Array.Fill(backingBuffer, (byte)0x22, ScanDebugConstants.BytesPerLine, ScanDebugConstants.BytesPerLine);
        var queuedWorkItems = new List<Action>();
        var delivered = new List<ScanWorkflowRowsAvailable>();
        var service = new ScanWorkflowService(
            new RecordingParameterService(log),
            new RecordingIlluminationService(log),
            new StubTransferSettingsService(),
            queuedWorkItems.Add);
        var session = new RecordingScanSession(log)
        {
            ScanImageBytes = backingBuffer,
            RowsAvailableReports = [1, 2]
        };
        var request = BuildRequest(alternateMotorDirection: false, enableMotorTransport: false, enableLedAutoControl: false) with
        {
            Rows = 2,
            PassChannelRoles = ["Blue", "Unused", "Unused", "Unused"]
        };

        var result = await service.ExecuteAsync(session, request, CancellationToken.None, onRowsAvailable: delivered.Add);

        Assert.Single(queuedWorkItems);
        queuedWorkItems.Single()();

        var pass = Assert.Single(result.Passes);
        Assert.NotEqual(Guid.Empty, result.CaptureId);
        Assert.Collection(
            delivered,
            first =>
            {
                Assert.Equal(result.CaptureId, first.CaptureId);
                Assert.Equal(pass.Provenance!.CaptureId, first.CaptureId);
                Assert.Equal((0, 1, 1), (first.StartRow, first.RowCount, first.CompletedRows));
                Assert.All(first.ImageBytes, value => Assert.Equal((byte)0x11, value));
            },
            second =>
            {
                Assert.Equal(result.CaptureId, second.CaptureId);
                Assert.Equal(pass.Provenance!.CaptureId, second.CaptureId);
                Assert.Equal((1, 1, 2), (second.StartRow, second.RowCount, second.CompletedRows));
                Assert.All(second.ImageBytes, value => Assert.Equal((byte)0x22, value));
            });
    }

    [Fact]
    public async Task ExecuteAsync_RowsAvailableEmitsExactDetachedDeltaByteVolume()
    {
        const int rows = 4;
        var log = new List<string>();
        var backingBuffer = new byte[rows * ScanDebugConstants.BytesPerLine];
        Array.Fill(backingBuffer, (byte)0x11, 0, ScanDebugConstants.BytesPerLine);
        Array.Fill(backingBuffer, (byte)0x22, ScanDebugConstants.BytesPerLine, ScanDebugConstants.BytesPerLine);
        Array.Fill(backingBuffer, (byte)0x33, ScanDebugConstants.BytesPerLine * 2, ScanDebugConstants.BytesPerLine);
        Array.Fill(backingBuffer, (byte)0x44, ScanDebugConstants.BytesPerLine * 3, ScanDebugConstants.BytesPerLine);
        var scheduler = new ReversingScheduler();
        var delivered = new List<ScanWorkflowRowsAvailable>();
        var service = new ScanWorkflowService(
            new RecordingParameterService(log),
            new RecordingIlluminationService(log),
            new StubTransferSettingsService(),
            scheduler.Queue);
        var session = new RecordingScanSession(log)
        {
            ScanImageBytes = backingBuffer,
            RowsAvailableReports = [1, 3, 4]
        };
        var request = BuildRequest(alternateMotorDirection: false, enableMotorTransport: false, enableLedAutoControl: false, rows: rows) with
        {
            PassChannelRoles = ["Blue", "Unused", "Unused", "Unused"]
        };

        await service.ExecuteAsync(session, request, CancellationToken.None, onRowsAvailable: delivered.Add);

        Assert.Single(scheduler.Actions);
        Array.Fill(backingBuffer, (byte)0xFF);
        scheduler.RunInReverse();

        Assert.Equal([0, 1, 3], delivered.Select(snapshot => snapshot.StartRow).ToArray());
        Assert.Equal([1, 2, 1], delivered.Select(snapshot => snapshot.RowCount).ToArray());
        Assert.Equal([1, 3, 4], delivered.Select(snapshot => snapshot.CompletedRows).ToArray());
        Assert.Equal(4 * ScanDebugConstants.BytesPerLine, delivered.Sum(snapshot => snapshot.ImageBytes.Length));
        Assert.All(delivered[0].ImageBytes, value => Assert.Equal((byte)0x11, value));
        Assert.All(delivered[1].ImageBytes[..ScanDebugConstants.BytesPerLine], value => Assert.Equal((byte)0x22, value));
        Assert.All(delivered[1].ImageBytes[ScanDebugConstants.BytesPerLine..], value => Assert.Equal((byte)0x33, value));
        Assert.All(delivered[2].ImageBytes, value => Assert.Equal((byte)0x44, value));
    }

    [Fact]
    public async Task ExecuteAsync_RowsAvailablePreservesFifoOrderWhenSchedulerReversesWorkItems()
    {
        var log = new List<string>();
        var scheduler = new ReversingScheduler();
        var delivered = new List<int>();
        var service = new ScanWorkflowService(
            new RecordingParameterService(log),
            new RecordingIlluminationService(log),
            new StubTransferSettingsService(),
            scheduler.Queue);
        var session = new RecordingScanSession(log)
        {
            ScanImageBytes = new byte[ScanDebugConstants.BytesPerLine * 3],
            RowsAvailableReports = [1, 2, 3]
        };
        var request = BuildRequest(alternateMotorDirection: false, enableMotorTransport: false, enableLedAutoControl: false, rows: 3) with
        {
            PassChannelRoles = ["Blue", "Unused", "Unused", "Unused"]
        };

        await service.ExecuteAsync(
            session,
            request,
            CancellationToken.None,
            onRowsAvailable: snapshot => delivered.Add(snapshot.CompletedRows));

        Assert.Single(scheduler.Actions);
        scheduler.RunInReverse();

        Assert.Equal([1, 2, 3], delivered);
    }

    [Fact]
    public async Task ExecuteAsync_RowsAvailableIsolatesConsumerExceptions()
    {
        var log = new List<string>();
        var scheduler = new ReversingScheduler();
        var deliveryAttempts = new List<int>();
        var deliveredAfterException = new List<int>();
        var service = new ScanWorkflowService(
            new RecordingParameterService(log),
            new RecordingIlluminationService(log),
            new StubTransferSettingsService(),
            scheduler.Queue);
        var session = new RecordingScanSession(log)
        {
            ScanImageBytes = new byte[ScanDebugConstants.BytesPerLine * 3],
            RowsAvailableReports = [1, 2, 3]
        };
        var request = BuildRequest(alternateMotorDirection: false, enableMotorTransport: false, enableLedAutoControl: false, rows: 3) with
        {
            PassChannelRoles = ["Blue", "Unused", "Unused", "Unused"]
        };

        var result = await service.ExecuteAsync(
            session,
            request,
            CancellationToken.None,
            onRowsAvailable: snapshot =>
            {
                deliveryAttempts.Add(snapshot.CompletedRows);
                if (snapshot.CompletedRows == 1)
                    throw new InvalidOperationException("Consumer failed.");

                deliveredAfterException.Add(snapshot.CompletedRows);
            });

        scheduler.RunInReverse();

        Assert.Single(result.Passes);
        Assert.Equal([1, 2, 3], deliveryAttempts);
        Assert.Equal([2, 3], deliveredAfterException);
    }

    [Fact]
    public async Task ExecuteAsync_RowsAvailableRejectsLateProducerCallbacks()
    {
        var log = new List<string>();
        var scheduler = new ReversingScheduler();
        var delivered = new List<ScanWorkflowRowsAvailable>();
        var imageBytes = new byte[ScanDebugConstants.BytesPerLine];
        var service = new ScanWorkflowService(
            new RecordingParameterService(log),
            new RecordingIlluminationService(log),
            new StubTransferSettingsService(),
            scheduler.Queue);
        var session = new RecordingScanSession(log)
        {
            ScanImageBytes = imageBytes,
            RowsAvailableReports = []
        };
        var request = BuildRequest(alternateMotorDirection: false, enableMotorTransport: false, enableLedAutoControl: false) with
        {
            PassChannelRoles = ["Blue", "Unused", "Unused", "Unused"]
        };

        var result = await service.ExecuteAsync(session, request, CancellationToken.None, onRowsAvailable: delivered.Add);
        Assert.Single(result.Passes);
        Assert.NotNull(session.CapturedRowsAvailable);

        session.InvokeCapturedRowsAvailable(imageBytes, 1);

        Assert.Empty(scheduler.Actions);
        Assert.Empty(delivered);
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

        var request = BuildRequest(alternateMotorDirection: true);
        var sparseRoles = new[] { "Unused", "Unused", "Red", "Unused" };
        var sparseLinePitchInput = BuildLinePitchInput(sparseRoles, request.PassParameterProfiles);
        request = request with
        {
            LedLevels = [0, 0, 345, 0],
            PassChannelRoles = sparseRoles,
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
                ScanDebugConstants.MotionDefaultIntervalNs),
            LinePitchInput = sparseLinePitchInput
        };

        var result = await service.ExecuteAsync(session, request, CancellationToken.None);

        var pass = Assert.Single(result.Passes);
        Assert.Equal(2, pass.LedChannelIndex);
        Assert.Contains("IlluminationOn:2:0,0,345,0:4:0", log);
        Assert.Equal(1, log.Count(entry => entry == "IlluminationOff"));
        Assert.Equal("Red", pass.Provenance!.ChannelRole);
        Assert.Equal((byte)2, pass.Provenance.SubmittedLedChannelIndex);
        Assert.Equal((ushort)345, pass.Provenance.SubmittedLedLevel);
    }

    [Fact]
    public async Task ExecuteAsync_CaptureProvenancePreservesExecutionSnapshotAfterRequestChanges()
    {
        var log = new List<string>();
        var parameters = new RecordingParameterService(log);
        var service = new ScanWorkflowService(parameters, new RecordingIlluminationService(log), new StubTransferSettingsService());
        var request = BuildRequest(alternateMotorDirection: true, enableMotorTransport: false, rows: 2) with
        {
            AcquisitionSettings = new ScanFilmAcquisitionSettings(12, 34, 56, 78, 0x0F, 0, 2, 2, 2, 2, 1_000)
        };
        var originalProfile = request.PassParameterProfiles[1];
        var session = new RecordingScanSession(log)
        {
            OnScanStarted = () =>
            {
                request.PassChannelRoles[1] = "Changed";
                request.LedLevels[1] = 900;
                request.PassParameterProfiles[1] = originalProfile with { ExposureTicks = 999 };
            }
        };

        var result = await service.ExecuteAsync(session, request, CancellationToken.None);

        Assert.NotNull(result.CaptureId);
        Assert.Equal(1, result.CompletedResultVersion);
        Assert.Equal(4, result.Passes.Count);
        foreach (var pass in result.Passes)
        {
            var provenance = Assert.IsType<ScanPassCaptureProvenance>(pass.Provenance);
            Assert.Equal(result.CaptureId, provenance.CaptureId);
            Assert.Equal(result.CompletedResultVersion, provenance.CompletedResultVersion);
            Assert.Equal((2, 2), (provenance.RequestedRows, provenance.CompletedRows));
            Assert.True(provenance.RequestedAtUtc <= provenance.CompletedAtUtc);
            Assert.Null(provenance.DeviceIdentity);
            Assert.Null(provenance.SessionGeneration);
            Assert.Null(provenance.ConfigurationIdentity);
            Assert.Null(provenance.CalibrationIdentity);
            Assert.Null(provenance.DeviceReadbackParameters);
        }

        var second = result.Passes[1].Provenance!;
        Assert.Equal("Green", second.ChannelRole);
        Assert.Equal((byte)1, second.SubmittedLedChannelIndex);
        Assert.Equal((ushort)34, second.SubmittedLedLevel);
        Assert.Equal(originalProfile, second.SubmittedParameters);
        Assert.Equal(parameters.AppliedSnapshots[1], second.SubmittedParameters);
    }

    [Fact]
    public async Task ExecuteAsync_NoLedControl_LeavesSubmittedLedUnknown()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var result = await service.ExecuteAsync(
            new RecordingScanSession(log),
            BuildRequest(alternateMotorDirection: true, enableMotorTransport: false, enableLedAutoControl: false),
            CancellationToken.None);

        Assert.All(result.Passes, pass =>
        {
            Assert.Null(pass.Provenance!.SubmittedLedChannelIndex);
            Assert.Null(pass.Provenance.SubmittedLedLevel);
        });
    }

    public static IEnumerable<object[]> InvalidHostRows()
    {
        yield return [0];
        yield return [-1];
        yield return [282_459];
        yield return [checked(ScanRowCountValidation.MaxHostRows + 1)];
        yield return [int.MaxValue];
    }

    [Theory]
    [MemberData(nameof(InvalidHostRows))]
    public async Task ExecuteAsync_InvalidRowsRejectBeforeIlluminationMotorOrScanIo(int rows)
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync(
            session,
            BuildRequest(alternateMotorDirection: true, rows: rows),
            CancellationToken.None));

        Assert.Equal("Rows", exception.ParamName);
        Assert.Empty(log);
    }

    [Fact]
    public async Task ExecuteAsync_UsesRequestClockForExpectedLineTimeAndAppliedProfile()
    {
        const uint requestClockKhz = 125_000;
        var log = new List<string>();
        var parameters = new RecordingParameterService(log);
        var service = new ScanWorkflowService(parameters, new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);
        var profiles = new[]
        {
            new ScanParameterSnapshot(1_000, 0, 0, 0, 0, 30_000),
            new ScanParameterSnapshot(0, 0, 0, 0, 0, 200_000),
            new ScanParameterSnapshot(0, 0, 0, 0, 0, 30_000),
            new ScanParameterSnapshot(0, 0, 0, 0, 0, 200_000)
        };
        var request = BuildRequest(alternateMotorDirection: true, enableMotorTransport: false, enableLedAutoControl: false) with
        {
            PassChannelRoles = ["Blue", "Unused", "Unused", "Unused"],
            PassParameterProfiles = profiles,
            SysClockKhz = requestClockKhz
        };

        await service.ExecuteAsync(session, request, CancellationToken.None);

        Assert.Equal([415u], session.ExpectedLineTimes);
        Assert.Equal([requestClockKhz], parameters.AppliedSnapshots.Select(snapshot => snapshot.SysClockKhz));
    }

    [Fact]
    public void TryNormalizeSnapshot_ClockAboveMaximumRejectsAndClamps()
    {
        var valid = ScanDebugValidation.TryNormalizeSnapshot(
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MaxSysClockKhz + 1),
            out var normalized);

        Assert.False(valid);
        Assert.Equal(ScanDebugConstants.MaxSysClockKhz, normalized.SysClockKhz);
    }

    [Fact]
    public async Task ExecuteAsync_ClockAboveMaximumRejectsBeforeAnyWorkflowSideEffect()
    {
        var log = new List<string>();
        var service = new ScanWorkflowService(new RecordingParameterService(log), new RecordingIlluminationService(log), new StubTransferSettingsService());
        var session = new RecordingScanSession(log);
        var request = BuildRequest(alternateMotorDirection: true, enableMotorTransport: false, enableLedAutoControl: false) with
        {
            SysClockKhz = ScanDebugConstants.MaxSysClockKhz + 1
        };

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync(session, request, CancellationToken.None));

        Assert.Equal("request", exception.ParamName);
        Assert.Empty(log);
        Assert.Empty(session.ExpectedLineTimes);
    }

    [Theory]
    [InlineData(30_000u)]
    [InlineData(200_000u)]
    public async Task ApplyGlobalClockAsync_ClockAtInclusiveBoundsTransfersParameter(uint sysClockKhz)
    {
        var session = new RecordingScanSession([]);
        var parameters = new ScanParameterService(new ScanProtocolService());

        await parameters.ApplyGlobalClockAsync(session, sysClockKhz, CancellationToken.None);

        var command = Assert.Single(session.ControlCommands);
        Assert.Equal(sysClockKhz, BitConverter.ToUInt32(command, 10));
    }

    [Fact]
    public async Task ApplyGlobalClockAsync_ClockAboveMaximumRejectsBeforeControlTransfer()
    {
        var session = new RecordingScanSession([]);
        var parameters = new ScanParameterService(new ScanProtocolService());

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => parameters.ApplyGlobalClockAsync(
            session,
            ScanDebugConstants.MaxSysClockKhz + 1,
            CancellationToken.None));

        Assert.Equal("sysClockKhz", exception.ParamName);
        Assert.Empty(session.ControlCommands);
    }

    private static ScanWorkflowRequest BuildRequest(bool alternateMotorDirection, bool enableMotorTransport = true, bool enableLedAutoControl = true, bool warmUpEnabled = false, int rows = 1)
    {
        var profiles = Enumerable.Range(0, ScanDebugConstants.IlluminationChannelCount)
            .Select(_ => new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz))
            .ToArray();

        var roles = new[] { "Blue", "Green", "Red", "IR" };
        return new ScanWorkflowRequest(
            rows,
            warmUpEnabled,
            [100, 100, 100, 100],
            roles,
            profiles,
            0,
            ScanDebugConstants.MotionDefaultIntervalNs,
            true,
            alternateMotorDirection,
            0,
            ScanDebugConstants.MinSysClockKhz,
            EnableMotorTransport: enableMotorTransport,
            EnableLedAutoControl: enableLedAutoControl,
            LinePitchInput: enableMotorTransport ? BuildLinePitchInput(roles, profiles) : null);
    }

    private static ScanWorkflowLinePitchInput BuildLinePitchInput(string[] roles, ScanParameterSnapshot[] profiles)
    {
        const uint intervalNanoseconds = ScanDebugConstants.MotionDefaultIntervalNs;
        var targetPitchMicrometers = ScanTimingMath.ExposureTicksToNanoseconds(
            profiles[0].ExposureTicks,
            profiles[0].SysClockKhz)
            / (intervalNanoseconds * 400.0)
            * 1_000.0;
        return new ScanWorkflowLinePitchInput(
            targetPitchMicrometers,
            new ScanMotorMechanicalSettings(200, 16, 8.0),
            ScanDebugConstants.MotionMinIntervalNs,
            Enumerable.Repeat<uint?>(intervalNanoseconds, roles.Length).ToArray());
    }

    private static ScanLinePitchPlanResult BuildLinePitchPlan(int rows, string[] roles, ScanParameterSnapshot[] profiles)
    {
        const uint intervalNanoseconds = ScanDebugConstants.MotionDefaultIntervalNs;
        var targetPitchMicrometers = ScanTimingMath.ExposureTicksToNanoseconds(
            profiles[0].ExposureTicks,
            profiles[0].SysClockKhz)
            / (intervalNanoseconds * 400.0)
            * 1_000.0;
        var passes = roles.Select((role, passIndex) => new ScanLinePitchPassInput(
            passIndex,
            role,
            !string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase),
            profiles[passIndex].ExposureTicks,
            profiles[passIndex].SysClockKhz,
            intervalNanoseconds,
            profiles[passIndex]));

        return ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            rows,
            targetPitchMicrometers,
            new ScanMotorMechanicalSettings(200, 16, 8.0),
            ScanDebugConstants.MotionMinIntervalNs,
            passes.ToArray()));
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

    private static void AssertWorkflowSnapshot(ScanWorkflowRowsAvailable snapshot, ScanWorkflowResult result, int currentPass, int passIndex, byte ledChannelIndex, bool directionPositive, string channelRole)
    {
        Assert.NotEqual(Guid.Empty, snapshot.CaptureId);
        Assert.Equal(result.CaptureId, snapshot.CaptureId);
        Assert.Equal(result.Passes[passIndex].Provenance!.CaptureId, snapshot.CaptureId);
        Assert.Equal(currentPass, snapshot.CurrentPass);
        Assert.Equal(ScanDebugConstants.IlluminationChannelCount, snapshot.TotalPasses);
        Assert.Equal(passIndex, snapshot.PassIndex);
        Assert.Equal(ledChannelIndex, snapshot.LedChannelIndex);
        Assert.Equal(directionPositive, snapshot.DirectionPositive);
        Assert.Equal(channelRole, snapshot.ChannelRole);
        Assert.Equal(1, snapshot.CompletedRows);
        Assert.Equal(ScanDebugConstants.BytesPerLine, snapshot.ImageBytes.Length);
        Assert.Equal(result.Passes[passIndex].MotorSteps, snapshot.MotorSteps);
        Assert.Equal(result.MotorIntervalNs, snapshot.MotorIntervalNanoseconds);
    }

    private sealed class RecordingParameterService(List<string> log) : IScanParameterService
    {
        public List<ScanParameterSnapshot> AppliedSnapshots { get; } = [];

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

        public Task ApplyGlobalClockAsync(IScanSessionService session, uint sysClockKhz, CancellationToken ct)
        {
            log.Add($"ApplyGlobalClock:{sysClockKhz}");
            return Task.CompletedTask;
        }

        public Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct)
        {
            AppliedSnapshots.Add(snapshot);
            log.Add("ApplyParameters");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingIlluminationService(List<string> log) : IScanIlluminationService
    {
        public Exception? RestoreFailure { get; init; }
        public Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct)
        {
            log.Add("IlluminationState");
            return Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2));
        }

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
            return RestoreFailure is null ? Task.CompletedTask : Task.FromException(RestoreFailure);
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

    private sealed class ReversingScheduler
    {
        private readonly List<Action> _actions = [];

        public IReadOnlyList<Action> Actions => _actions;

        public void Queue(Action action)
            => _actions.Add(action);

        public void RunInReverse()
        {
            for (var index = _actions.Count - 1; index >= 0; index--)
                _actions[index]();
        }
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
        public List<WarmUpCall> WarmUpCalls { get; } = [];
        public CancellationTokenSource? CancellationSourceToCancelOnScan { get; init; }
        public string? ScanFailureMessage { get; init; }
        public string? WarmUpEnableFailureMessage { get; init; }
        public string? WarmUpDisableFailureMessage { get; init; }
        public Exception? StopMotorFailure { get; init; }
        public Exception? MotorDisableFailure { get; init; }
        public bool InitialMotorEnabled { get; init; } = true;
        public byte[]? ScanImageBytes { get; init; }
        public IReadOnlyList<int>? RowsAvailableReports { get; init; }
        public Action? OnRowsAvailableReturned { get; init; }
        public Action? OnScanStarted { get; init; }
        public ScanRowsAvailableHandler? CapturedRowsAvailable { get; private set; }
        public List<uint?> ExpectedLineTimes { get; } = [];
        public List<byte[]> ControlCommands { get; } = [];
        public List<byte> DisabledMotorIds { get; } = [];

        public void InvokeCapturedRowsAvailable(byte[] imageBytes, int completedRows)
            => CapturedRowsAvailable?.Invoke(imageBytes, completedRows);

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
        {
            log.Add("MotionState");
            return Task.FromResult<IReadOnlyList<ScanMotorState>>([new ScanMotorState(0, InitialMotorEnabled, false, false, 0, 0, 0)]);
        }

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
        {
            log.Add($"MotorEnable:{motorId}:{enabled}");
            if (!enabled)
            {
                DisabledMotorIds.Add(motorId);
                if (MotorDisableFailure is not null)
                    return Task.FromException(MotorDisableFailure);
            }

            return Task.CompletedTask;
        }

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
        {
            log.Add($"Stop:{motorId}");
            return StopMotorFailure is null ? Task.CompletedTask : Task.FromException(StopMotorFailure);
        }

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
            OnScanStarted?.Invoke();
            ExpectedLineTimes.Add(expectedLineTimeUs);
            CancellationSourceToCancelOnScan?.Cancel();
            ct.ThrowIfCancellationRequested();
            if (ScanFailureMessage is not null)
                return Task.FromResult(new ScanStartResult(false, ScanFailureMessage, null));

            CapturedRowsAvailable = onRowsAvailable;
            var imageBytes = ScanImageBytes ?? new byte[rows * ScanDebugConstants.BytesPerLine];
            foreach (var completedRows in RowsAvailableReports ?? [rows])
                onRowsAvailable?.Invoke(imageBytes, completedRows);
            OnRowsAvailableReturned?.Invoke();
            return Task.FromResult(new ScanStartResult(true, string.Empty, imageBytes));
        }

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => StartScanAsync(totalRows, ct, onStatus, onDiagnostic, onProgress, onRowsAvailable, expectedLineTimeUs);

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromResult(new ScanStopResult(true, string.Empty));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
        {
            ControlCommands.Add(command.ToArray());
            if (expectedCommand == ScanDebugConstants.UsbCmdSetParamByHash && command.Length == 14)
            {
                var payload = new byte[10];
                Buffer.BlockCopy(command, 4, payload, 0, 4);
                payload[4] = command[8];
                payload[5] = command[9];
                Buffer.BlockCopy(command, 10, payload, 6, 4);
                return Task.FromResult(new ScanControlFrame(expectedCommand, 0, payload));
            }

            return Task.FromResult(new ScanControlFrame(expectedCommand, 0, Array.Empty<byte>()));
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
