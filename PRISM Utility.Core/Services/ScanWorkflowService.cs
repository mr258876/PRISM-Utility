using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanWorkflowService : IScanWorkflowService
{
    private const int TotalPasses = ScanDebugConstants.IlluminationChannelCount;

    private readonly IScanParameterService _parameters;
    private readonly IScanIlluminationService _illumination;
    private readonly IScanTransferSettingsService _transferSettings;

    public ScanWorkflowService(IScanParameterService parameters, IScanIlluminationService illumination, IScanTransferSettingsService transferSettings)
    {
        _parameters = parameters;
        _illumination = illumination;
        _transferSettings = transferSettings;
    }

    public async Task<ScanWorkflowResult> ExecuteAsync(
        IScanSessionService session,
        ScanWorkflowRequest request,
        CancellationToken ct,
        Action<ScanWorkflowProgress>? onProgress = null,
        Action<string>? onStatus = null,
        Action<string>? onDiagnostic = null,
        Action<int, int>? onByteProgress = null)
    {
        ValidateRequest(session, request);

        var originalIllumination = request.EnableLedAutoControl ? await _illumination.GetStateAsync(session, ct) : null;
        var originalMotorState = request.EnableMotorTransport
            ? (await session.GetMotionStateAsync(ct)).FirstOrDefault(state => state.MotorId == request.ScanMotorId)
            : null;

        var computedMotorSteps = 0u;
        var captures = new List<ScanPassCapture>(TotalPasses);
        var motionStarted = false;
        var enabledMotorForWorkflow = false;

        try
        {
            if (request.EnableMotorTransport && (originalMotorState is null || !originalMotorState.Enabled))
            {
                onStatus?.Invoke($"Enabling Motor{request.ScanMotorId + 1} for scan transport...");
                await session.SetMotorEnabledAsync(request.ScanMotorId, true, ct);
                enabledMotorForWorkflow = true;
            }

            for (var passIndex = 0; passIndex < TotalPasses; passIndex++)
            {
                ct.ThrowIfCancellationRequested();

                var ledIndex = (byte)passIndex;
                var directionPositive = GetDirectionForPass(request, passIndex);
                var passProfile = request.PassParameterProfiles[passIndex];
                var passRole = request.PassChannelRoles[passIndex];
                var expectedLineTimeUs = ScanTimingMath.ExposureTicksToMicrosecondsCeil(passProfile.ExposureTicks, passProfile.SysClockKhz);
                var passMotorSteps = request.EnableMotorTransport
                    ? ScanTimingMath.ComputeMotorStepsPerPass(request.Rows, passProfile.ExposureTicks, passProfile.SysClockKhz, request.MotorIntervalNs)
                    : 0u;
                if (passIndex == 0)
                    computedMotorSteps = passMotorSteps;

                onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Preparing"));
                onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: applying CCD profile for {passRole} channel...");
                await _parameters.ApplyAsync(session, passProfile, ct);

                if (request.EnableLedAutoControl)
                    await _illumination.ApplySingleChannelAsync(session, BuildAcquisitionSettings(request), ledIndex, ct);

                if (passMotorSteps > 0)
                {
                    onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: preparing Motor{request.ScanMotorId + 1} {(directionPositive ? "forward" : "reverse")} for {passMotorSteps} step(s), waiting for EXPOSURE_SYNC...");
                    motionStarted = true;
                    await session.PrepareMotorOnExposureSyncAsync(request.ScanMotorId, directionPositive, passMotorSteps, request.MotorIntervalNs, ct);
                }

                onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Scanning"));
                onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: LED{ledIndex + 1} active, capturing {request.Rows} row(s)...");

                var scanResult = await RunScanAsync(
                    session,
                    request.Rows,
                    expectedLineTimeUs,
                    ct,
                    onStatus,
                    onDiagnostic,
                    onByteProgress is null
                        ? null
                        : (transferredBytes, totalBytes) =>
                        {
                            var workflowTotalBytes = Math.Min(int.MaxValue, Math.Max(1L, (long)totalBytes * TotalPasses));
                            var workflowTransferredBytes = Math.Min(workflowTotalBytes, Math.Max(0L, ((long)passIndex * totalBytes) + transferredBytes));
                            onByteProgress((int)workflowTransferredBytes, (int)workflowTotalBytes);
                        });
                if (!scanResult.Success || scanResult.ImageBytes is null)
                    throw new IOException($"Pass {passIndex + 1} failed: {scanResult.Message}");

                captures.Add(new ScanPassCapture(passIndex + 1, ledIndex, directionPositive, request.Rows, passMotorSteps, scanResult.ImageBytes));

                if (passMotorSteps > 0)
                {
                    onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Waiting for motor"));
                    await WaitForMotorIdleAsync(session, request.ScanMotorId, passMotorSteps, request.MotorIntervalNs, ct);
                    motionStarted = false;
                }

                if (request.EnableLedAutoControl)
                    await _illumination.TurnOffAsync(session, ct);

                if (!request.AlternateMotorDirection && passMotorSteps > 0)
                {
                    var returnReason = passIndex < (TotalPasses - 1) ? "before next channel scan" : "after final channel scan";
                    onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, !directionPositive, "Returning"));
                    onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: returning Motor{request.ScanMotorId + 1} to start position {returnReason}...");
                    motionStarted = true;
                    await session.MoveMotorStepsAndWaitForCompletionAsync(request.ScanMotorId, !directionPositive, passMotorSteps, request.MotorIntervalNs, ct);
                    motionStarted = false;
                }

                onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Completed"));
                onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses} complete.");
            }

            onStatus?.Invoke("Four-channel scan workflow completed.");
            return new ScanWorkflowResult(request.Rows, captures, computedMotorSteps, request.MotorIntervalNs, request.ExposureTicks, request.SysClockKhz);
        }
        finally
        {
            if (motionStarted)
            {
                try
                {
                    await session.StopMotorAsync(request.ScanMotorId, CancellationToken.None);
                    await WaitForMotorIdleAsync(session, request.ScanMotorId, computedMotorSteps, request.MotorIntervalNs, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    onDiagnostic?.Invoke($"Scan workflow motor cleanup failed: {ex.Message}");
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
                    onDiagnostic?.Invoke($"Scan workflow illumination cleanup failed: {ex.Message}");
                }
            }

            if (enabledMotorForWorkflow)
            {
                try
                {
                    await session.SetMotorEnabledAsync(request.ScanMotorId, false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    onDiagnostic?.Invoke($"Scan workflow motor restore failed: {ex.Message}");
                }
            }
        }
    }

    private async Task<ScanStartResult> RunScanAsync(
        IScanSessionService session,
        int rows,
        uint expectedLineTimeUs,
        CancellationToken ct,
        Action<string>? onStatus,
        Action<string>? onDiagnostic,
        Action<int, int>? onByteProgress)
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
                expectedLineTimeUs);
        }

        return await session.StartScanAsync(
            rows,
            ct,
            onStatus,
            onDiagnostic,
            onByteProgress,
            expectedLineTimeUs);
    }

    private async Task<bool> ShouldUseFullStartReadPathAsync()
    {
        await _transferSettings.InitializeAsync();
        var settings = _transferSettings.Settings;
        return settings.ReadMode == ScanBulkInReadMode.MultiBuffered && settings.RawIoEnabled;
    }

    private static ScanFilmAcquisitionSettings BuildAcquisitionSettings(ScanWorkflowRequest request)
        => request.AcquisitionSettings?.Normalize() ?? BuildDefaultAcquisitionSettings(request.LedLevels, request.MotorIntervalNs);

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

    private static bool GetDirectionForPass(ScanWorkflowRequest request, int passIndex)
    {
        if (!request.AlternateMotorDirection)
            return request.StartingDirectionPositive;

        return (passIndex % 2) == 0 ? request.StartingDirectionPositive : !request.StartingDirectionPositive;
    }

    private static void ValidateRequest(IScanSessionService session, ScanWorkflowRequest request)
    {
        if (request.Rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Rows must be greater than zero.");

        if (!session.IsConnected)
            throw new InvalidOperationException("Scanner not connected.");

        if (request.LedLevels.Length != ScanDebugConstants.IlluminationChannelCount)
            throw new ArgumentException($"LED level count must be {ScanDebugConstants.IlluminationChannelCount}.", nameof(request));

        if (request.PassChannelRoles.Length != ScanDebugConstants.IlluminationChannelCount)
            throw new ArgumentException($"Pass channel role count must be {ScanDebugConstants.IlluminationChannelCount}.", nameof(request));

        if (request.PassParameterProfiles.Length != ScanDebugConstants.IlluminationChannelCount)
            throw new ArgumentException($"Pass parameter profile count must be {ScanDebugConstants.IlluminationChannelCount}.", nameof(request));

        if (request.EnableMotorTransport && request.ScanMotorId >= ScanDebugConstants.MotionMotorCount)
            throw new ArgumentOutOfRangeException(nameof(request), $"Scan motor id must be in [0, {ScanDebugConstants.MotionMotorCount - 1}].");

        if (request.EnableMotorTransport && request.MotorIntervalNs < ScanDebugConstants.MotionMinIntervalNs)
            throw new ArgumentOutOfRangeException(nameof(request), $"Motor interval must be at least {ScanDebugConstants.MotionMinIntervalNs} ns.");

        if (request.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
            throw new ArgumentOutOfRangeException(nameof(request), $"System clock must be at least {ScanDebugConstants.MinSysClockKhz} kHz.");
    }
}
