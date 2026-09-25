using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanWorkflowService : IScanWorkflowService
{
    private readonly IScanParameterService _parameters;
    private readonly IScanIlluminationService _illumination;
    private readonly IScanTransferSettingsService _transferSettings;
    private readonly Action<Action> _queueWorkflowRowsAvailable;

    public ScanWorkflowService(IScanParameterService parameters, IScanIlluminationService illumination, IScanTransferSettingsService transferSettings)
        : this(
            parameters,
            illumination,
            transferSettings,
            static workItem => ThreadPool.UnsafeQueueUserWorkItem(static state => ((Action)state!).Invoke(), workItem))
    {
    }

    internal ScanWorkflowService(
        IScanParameterService parameters,
        IScanIlluminationService illumination,
        IScanTransferSettingsService transferSettings,
        Action<Action> queueWorkflowRowsAvailable)
    {
        _parameters = parameters;
        _illumination = illumination;
        _transferSettings = transferSettings;
        _queueWorkflowRowsAvailable = queueWorkflowRowsAvailable;
    }

    public async Task<ScanWorkflowResult> ExecuteAsync(
        IScanSessionService session,
        ScanWorkflowRequest request,
        CancellationToken ct,
        Action<ScanWorkflowProgress>? onProgress = null,
        Action<string>? onStatus = null,
        Action<string>? onDiagnostic = null,
        Action<int, int>? onByteProgress = null,
        ScanWorkflowRowsAvailableHandler? onRowsAvailable = null)
    {
        var executionRequest = request.CreateExecutionSnapshot();
        ValidateRequest(session, executionRequest);

        var workflowPasses = ScanWorkflowTransportPlan.Resolve(executionRequest);
        var originalIllumination = executionRequest.EnableLedAutoControl ? await _illumination.GetStateAsync(session, ct) : null;
        var originalMotorState = executionRequest.EnableMotorTransport
            ? (await session.GetMotionStateAsync(ct)).FirstOrDefault(state => state.MotorId == executionRequest.ScanMotorId)
            : null;

        var totalPasses = workflowPasses.Count;
        var captureId = Guid.NewGuid();
        var computedMotorSteps = 0u;
        var computedMotorIntervalNanoseconds = 0u;
        var captures = new List<ScanPassCapture>(totalPasses);
        var rowsDelivery = onRowsAvailable is null ? null : new WorkflowRowsDelivery(_queueWorkflowRowsAvailable, onRowsAvailable);
        ScanWorkflowTransportPass? activeMotion = null;
        var enabledMotorForWorkflow = false;
        var warmUpEnabledForWorkflow = false;
        Exception? primaryFailure = null;

        try
        {
            if (executionRequest.WarmUpEnabled)
            {
                var enableWarmUpResult = await session.SetWarmUpEnabledAsync(true, ct);
                if (!enableWarmUpResult.Success)
                    throw new IOException($"Scan workflow warm-up enable failed: {enableWarmUpResult.Message}");

                warmUpEnabledForWorkflow = true;
            }

            if (executionRequest.EnableMotorTransport && (originalMotorState is null || !originalMotorState.Enabled))
            {
                onStatus?.Invoke($"Enabling Motor{executionRequest.ScanMotorId + 1} for scan transport...");
                await session.SetMotorEnabledAsync(executionRequest.ScanMotorId, true, ct);
                enabledMotorForWorkflow = true;
            }

            await _parameters.ApplyGlobalClockAsync(session, executionRequest.SysClockKhz, ct);

            for (var activePassIndex = 0; activePassIndex < totalPasses; activePassIndex++)
            {
                ct.ThrowIfCancellationRequested();

                var workflowPass = workflowPasses[activePassIndex];
                var passIndex = workflowPass.PassIndex;
                var ledIndex = (byte)passIndex;
                var directionPositive = GetDirectionForPass(executionRequest, activePassIndex);
                var passProfile = workflowPass.ParameterProfile;
                var effectivePassProfile = passProfile with { SysClockKhz = executionRequest.SysClockKhz };
                var passRole = workflowPass.ChannelRole;
                var expectedLineTimeUs = ScanTimingMath.ExposureTicksToMicrosecondsCeil(effectivePassProfile.ExposureTicks, effectivePassProfile.SysClockKhz);
                var passMotorSteps = workflowPass.MotorSteps;
                var passMotorIntervalNanoseconds = workflowPass.MotorIntervalNanoseconds;
                var passAcquisitionSettings = executionRequest.EnableLedAutoControl
                    ? BuildAcquisitionSettings(executionRequest, passMotorIntervalNanoseconds)
                    : null;
                if (activePassIndex == 0)
                {
                    computedMotorSteps = passMotorSteps;
                    computedMotorIntervalNanoseconds = passMotorIntervalNanoseconds;
                }

                onProgress?.Invoke(new ScanWorkflowProgress(activePassIndex + 1, totalPasses, ledIndex, directionPositive, "Preparing"));
                onStatus?.Invoke($"Pass {activePassIndex + 1}/{totalPasses}: applying CCD profile for {passRole} channel...");
                await _parameters.ApplyAsync(session, effectivePassProfile, ct);

                if (passAcquisitionSettings is not null)
                    await _illumination.ApplySingleChannelAsync(session, passAcquisitionSettings, ledIndex, ct);

                if (passMotorSteps > 0)
                {
                    onStatus?.Invoke($"Pass {activePassIndex + 1}/{totalPasses}: preparing Motor{executionRequest.ScanMotorId + 1} {(directionPositive ? "forward" : "reverse")} for {passMotorSteps} step(s), waiting for EXPOSURE_SYNC...");
                    activeMotion = workflowPass;
                    await session.PrepareMotorOnExposureSyncAsync(executionRequest.ScanMotorId, directionPositive, passMotorSteps, passMotorIntervalNanoseconds, ct);
                }

                onProgress?.Invoke(new ScanWorkflowProgress(activePassIndex + 1, totalPasses, ledIndex, directionPositive, "Scanning"));
                onStatus?.Invoke($"Pass {activePassIndex + 1}/{totalPasses}: LED{ledIndex + 1} active, capturing {executionRequest.Rows} row(s)...");

                var rowsCallbackGate = new object();
                var rowsCallbackOpen = true;
                var lastReportedRows = 0;
                ScanStartResult scanResult;
                var requestedAtUtc = DateTimeOffset.UtcNow;
                try
                {
                    scanResult = await RunScanAsync(
                    session,
                    executionRequest.Rows,
                    expectedLineTimeUs,
                    ct,
                    onStatus,
                    onDiagnostic,
                    onByteProgress is null
                        ? null
                        : (transferredBytes, totalBytes) =>
                        {
                            var workflowTotalBytes = Math.Min(int.MaxValue, Math.Max(1L, (long)totalBytes * totalPasses));
                            var workflowTransferredBytes = Math.Min(workflowTotalBytes, Math.Max(0L, ((long)activePassIndex * totalBytes) + transferredBytes));
                            onByteProgress((int)workflowTransferredBytes, (int)workflowTotalBytes);
                        },
                    rowsDelivery is null
                        ? null
                        : (imageBytes, completedRows) =>
                        {
                            ScanWorkflowRowsAvailable? snapshot = null;
                            lock (rowsCallbackGate)
                            {
                                if (!rowsCallbackOpen || completedRows <= lastReportedRows || completedRows > executionRequest.Rows)
                                    return;

                                var startRow = lastReportedRows;
                                var rowCount = completedRows - startRow;
                                int byteOffset;
                                int byteCount;
                                int endOffset;
                                try
                                {
                                    byteOffset = checked(startRow * ScanDebugConstants.BytesPerLine);
                                    byteCount = checked(rowCount * ScanDebugConstants.BytesPerLine);
                                    endOffset = checked(byteOffset + byteCount);
                                }
                                catch (OverflowException)
                                {
                                    return;
                                }

                                if (imageBytes.Length < endOffset)
                                    return;

                                snapshot = new ScanWorkflowRowsAvailable(
                                    activePassIndex + 1,
                                    totalPasses,
                                    passIndex,
                                    ledIndex,
                                    directionPositive,
                                    passRole,
                                    imageBytes.AsSpan(byteOffset, byteCount).ToArray(),
                                    completedRows,
                                    passMotorSteps,
                                    passMotorIntervalNanoseconds)
                                {
                                    CaptureId = captureId,
                                    StartRow = startRow,
                                    RowCount = rowCount
                                };
                                lastReportedRows = completedRows;
                            }

                            rowsDelivery.Enqueue(snapshot);
                        });
                }
                finally
                {
                    lock (rowsCallbackGate)
                        rowsCallbackOpen = false;
                }
                if (!scanResult.Success || scanResult.ImageBytes is null)
                    throw new IOException($"Pass {activePassIndex + 1} failed: {scanResult.Message}");

                var submittedLedLevel = passAcquisitionSettings is null ? (ushort?)null : ledIndex switch
                {
                    0 => passAcquisitionSettings.Led1Level,
                    1 => passAcquisitionSettings.Led2Level,
                    2 => passAcquisitionSettings.Led3Level,
                    _ => passAcquisitionSettings.Led4Level
                };
                captures.Add(new ScanPassCapture(activePassIndex + 1, ledIndex, directionPositive, executionRequest.Rows, passMotorSteps, scanResult.ImageBytes)
                {
                    Provenance = new ScanPassCaptureProvenance(
                        captureId,
                        passRole,
                        passAcquisitionSettings is null ? null : ledIndex,
                        submittedLedLevel,
                        effectivePassProfile,
                        executionRequest.Rows,
                        executionRequest.Rows,
                        requestedAtUtc,
                        DateTimeOffset.UtcNow,
                        1)
                });

                if (passMotorSteps > 0)
                {
                    onProgress?.Invoke(new ScanWorkflowProgress(activePassIndex + 1, totalPasses, ledIndex, directionPositive, "Waiting for motor"));
                    await WaitForMotorIdleAsync(session, executionRequest.ScanMotorId, passMotorSteps, passMotorIntervalNanoseconds, ct);
                    activeMotion = null;
                }

                if (executionRequest.EnableLedAutoControl)
                    await _illumination.TurnOffAsync(session, ct);

                if (!executionRequest.AlternateMotorDirection && passMotorSteps > 0)
                {
                    onProgress?.Invoke(new ScanWorkflowProgress(activePassIndex + 1, totalPasses, ledIndex, !directionPositive, "Returning"));
                    onStatus?.Invoke($"Pass {activePassIndex + 1}/{totalPasses}: returning Motor{executionRequest.ScanMotorId + 1} to start position...");
                    activeMotion = workflowPass;
                    await session.MoveMotorStepsAndWaitForCompletionAsync(executionRequest.ScanMotorId, !directionPositive, passMotorSteps, passMotorIntervalNanoseconds, ct);
                    activeMotion = null;
                }

                onProgress?.Invoke(new ScanWorkflowProgress(activePassIndex + 1, totalPasses, ledIndex, directionPositive, "Completed"));
                onStatus?.Invoke($"Pass {activePassIndex + 1}/{totalPasses} complete.");
            }

            onStatus?.Invoke($"Scan workflow completed with {totalPasses} pass(es).");
            return new ScanWorkflowResult(executionRequest.Rows, captures, computedMotorSteps, computedMotorIntervalNanoseconds, executionRequest.ExposureTicks, executionRequest.SysClockKhz)
            {
                CaptureId = captureId,
                CompletedResultVersion = 1
            };
        }
        catch (Exception ex)
        {
            primaryFailure = ex;
            throw;
        }
        finally
        {
            var cleanupFailures = new List<Exception>();
            if (warmUpEnabledForWorkflow)
            {
                try
                {
                    var disableWarmUpResult = await session.SetWarmUpEnabledAsync(false, CancellationToken.None);
                    if (!disableWarmUpResult.Success)
                        throw new IOException(disableWarmUpResult.Message);
                }
                catch (Exception ex)
                {
                    cleanupFailures.Add(ex);
                    ReportCleanupDiagnostic(onDiagnostic, $"Scan workflow warm-up cleanup failed: {ex.Message}");
                }
            }

            if (activeMotion is not null)
            {
                try
                {
                    await session.StopMotorAsync(executionRequest.ScanMotorId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    cleanupFailures.Add(ex);
                    ReportCleanupDiagnostic(onDiagnostic, $"Scan workflow motor stop cleanup failed: {ex.Message}");
                }

                try
                {
                    await WaitForMotorIdleAsync(
                        session,
                        executionRequest.ScanMotorId,
                        activeMotion.MotorSteps,
                        activeMotion.MotorIntervalNanoseconds,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    cleanupFailures.Add(ex);
                    ReportCleanupDiagnostic(onDiagnostic, $"Scan workflow motor idle cleanup failed: {ex.Message}");
                }
            }

            if (originalIllumination is not null)
            {
                try
                {
                    await _illumination.RestoreStateAsync(session, originalIllumination, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    cleanupFailures.Add(ex);
                    ReportCleanupDiagnostic(onDiagnostic, $"Scan workflow illumination cleanup failed: {ex.Message}");
                }
            }

            if (enabledMotorForWorkflow)
            {
                try
                {
                await session.SetMotorEnabledAsync(executionRequest.ScanMotorId, false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    cleanupFailures.Add(ex);
                    ReportCleanupDiagnostic(onDiagnostic, $"Scan workflow motor restore failed: {ex.Message}");
                }
            }

            if (cleanupFailures.Count > 0)
            {
                var failures = primaryFailure is null
                    ? cleanupFailures
                    : new[] { primaryFailure }.Concat(cleanupFailures);
                throw new AggregateException("Scan workflow cleanup failed.", failures);
            }
        }
    }

    private static void ReportCleanupDiagnostic(Action<string>? onDiagnostic, string message)
    {
        try
        {
            onDiagnostic?.Invoke(message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Scan workflow warm-up cleanup diagnostic delivery failed: {ex}");
        }
    }

    private async Task<ScanStartResult> RunScanAsync(
        IScanSessionService session,
        int rows,
        uint expectedLineTimeUs,
        CancellationToken ct,
        Action<string>? onStatus,
        Action<string>? onDiagnostic,
        Action<int, int>? onByteProgress,
        ScanRowsAvailableHandler? onRowsAvailable)
    {
        var useExtendedSingleRead = await ShouldUseFullStartReadPathAsync();
        if (rows > session.SingleTransferMaxRows && !useExtendedSingleRead)
        {
            return await session.StartSegmentedScanAsync(
                rows,
                ct,
                onStatus,
                onDiagnostic,
                onByteProgress,
                onRowsAvailable,
                expectedLineTimeUs);
        }

        return await session.StartScanAsync(
            rows,
            ct,
            onStatus,
            onDiagnostic,
            onByteProgress,
            onRowsAvailable,
            expectedLineTimeUs);
    }

    private sealed class WorkflowRowsDelivery(Action<Action> queue, ScanWorkflowRowsAvailableHandler callback)
    {
        private readonly object _gate = new();
        private readonly Queue<ScanWorkflowRowsAvailable> _pending = new();
        private bool _drainScheduled;

        public void Enqueue(ScanWorkflowRowsAvailable snapshot)
        {
            var scheduleDrain = false;
            lock (_gate)
            {
                _pending.Enqueue(snapshot);
                if (!_drainScheduled)
                {
                    _drainScheduled = true;
                    scheduleDrain = true;
                }
            }

            if (scheduleDrain)
                queue(Drain);
        }

        private void Drain()
        {
            while (true)
            {
                ScanWorkflowRowsAvailable snapshot;
                lock (_gate)
                {
                    if (_pending.Count == 0)
                    {
                        _drainScheduled = false;
                        return;
                    }

                    snapshot = _pending.Dequeue();
                }

                try
                {
                    callback(snapshot);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }
    }

    private async Task<bool> ShouldUseFullStartReadPathAsync()
    {
        await _transferSettings.InitializeAsync();
        var settings = _transferSettings.Settings;
        return settings.ReadMode == ScanBulkInReadMode.MultiBuffered && settings.RawIoEnabled;
    }

    private static ScanFilmAcquisitionSettings BuildAcquisitionSettings(ScanWorkflowRequest request, uint motorIntervalNanoseconds)
        => request.AcquisitionSettings?.Normalize() ?? BuildDefaultAcquisitionSettings(request.LedLevels, motorIntervalNanoseconds);

    private static ScanFilmAcquisitionSettings BuildDefaultAcquisitionSettings(ushort[] ledLevels, uint motorIntervalNs)
    {
        var level1 = ledLevels.Length > 0 ? ledLevels[0] : (ushort)0;
        var level2 = ledLevels.Length > 1 ? ledLevels[1] : (ushort)0;
        var level3 = ledLevels.Length > 2 ? ledLevels[2] : (ushort)0;
        var level4 = ledLevels.Length > 3 ? ledLevels[3] : (ushort)0;

        return new ScanFilmAcquisitionSettings(
            level1,
            level2,
            level3,
            level4,
            ScanDebugConstants.IlluminationValidMask,
            0,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            ScanDebugConstants.IlluminationMinSyncPulseClock,
            motorIntervalNs).Normalize();
    }

    private static async Task WaitForMotorIdleAsync(IScanSessionService session, byte motorId, uint steps, uint intervalNs, CancellationToken ct)
    {
        await session.WaitForMotorMotionCompleteAsync(motorId, steps, intervalNs, ct);
    }

    private static bool GetDirectionForPass(ScanWorkflowRequest request, int activePassIndex)
    {
        if (!request.AlternateMotorDirection)
            return request.StartingDirectionPositive;

        return (activePassIndex % 2) == 0 ? request.StartingDirectionPositive : !request.StartingDirectionPositive;
    }

    private static void ValidateRequest(IScanSessionService session, ScanWorkflowRequest request)
    {
        ScanRowCountValidation.EnsureValidForHostBuffer(request.Rows, nameof(request.Rows));

        if (!session.IsConnected)
            throw new InvalidOperationException("Scanner not connected.");

        if (request.LedLevels.Length != ScanDebugConstants.IlluminationChannelCount)
            throw new ArgumentException($"LED level count must be {ScanDebugConstants.IlluminationChannelCount}.", nameof(request));

        if (request.PassChannelRoles.Length != ScanDebugConstants.IlluminationChannelCount)
            throw new ArgumentException($"Pass channel role count must be {ScanDebugConstants.IlluminationChannelCount}.", nameof(request));

        if (request.PassParameterProfiles.Length != ScanDebugConstants.IlluminationChannelCount)
            throw new ArgumentException($"Pass parameter profile count must be {ScanDebugConstants.IlluminationChannelCount}.", nameof(request));

        if (!request.PassChannelRoles.Any(role => !string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("At least one active scan channel is required.", nameof(request));

        if (request.EnableMotorTransport && request.ScanMotorId >= ScanDebugConstants.MotionMotorCount)
            throw new ArgumentOutOfRangeException(nameof(request), $"Scan motor id must be in [0, {ScanDebugConstants.MotionMotorCount - 1}].");

        if (request.SysClockKhz is < ScanDebugConstants.MinSysClockKhz or > ScanDebugConstants.MaxSysClockKhz)
            throw new ArgumentOutOfRangeException(nameof(request), $"System clock must be in [{ScanDebugConstants.MinSysClockKhz}, {ScanDebugConstants.MaxSysClockKhz}] kHz.");
    }

}
