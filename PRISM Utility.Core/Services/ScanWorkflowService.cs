using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanWorkflowService : IScanWorkflowService
{
    private const int TotalPasses = ScanDebugConstants.IlluminationChannelCount;

    private readonly IScanParameterService _parameters;

    public ScanWorkflowService(IScanParameterService parameters)
    {
        _parameters = parameters;
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

        var originalIllumination = await session.GetIlluminationStateAsync(ct);
        var originalMotion = await session.GetMotionStateAsync(ct);
        var originalMotorState = originalMotion.FirstOrDefault(state => state.MotorId == request.ScanMotorId);

        var computedMotorSteps = 0u;
        var captures = new List<ScanPassCapture>(TotalPasses);
        var motionStarted = false;

        try
        {
            if (originalMotorState is null || !originalMotorState.Enabled)
            {
                onStatus?.Invoke($"Enabling Motor{request.ScanMotorId + 1} for scan transport...");
                await session.SetMotorEnabledAsync(request.ScanMotorId, true, ct);
            }

            for (var passIndex = 0; passIndex < TotalPasses; passIndex++)
            {
                ct.ThrowIfCancellationRequested();

                var ledIndex = (byte)passIndex;
                var directionPositive = GetDirectionForPass(request, passIndex);
                var passProfile = request.PassParameterProfiles[passIndex];
                var passRole = request.PassChannelRoles[passIndex];
                var expectedLineTimeUs = ComputeLineExposureUs(passProfile.ExposureTicks, passProfile.SysClockKhz);
                var passMotorSteps = ComputeMotorStepsPerPass(request.Rows, passProfile.ExposureTicks, passProfile.SysClockKhz, request.MotorIntervalUs);
                if (passIndex == 0)
                    computedMotorSteps = passMotorSteps;

                onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Preparing"));
                onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: applying CCD profile for {passRole} channel...");
                await _parameters.ApplyAsync(session, passProfile, ct);

                await ApplySingleLedIlluminationAsync(session, request, ledIndex, ct);

                if (passMotorSteps > 0)
                {
                    onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: preparing Motor{request.ScanMotorId + 1} {(directionPositive ? "forward" : "reverse")} for {passMotorSteps} step(s), waiting for EXPOSURE_SYNC...");
                    await session.PrepareMotorOnExposureSyncAsync(request.ScanMotorId, directionPositive, passMotorSteps, request.MotorIntervalUs, ct);
                    motionStarted = true;
                }

                onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Scanning"));
                onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: LED{ledIndex + 1} active, capturing {request.Rows} row(s)...");

                var scanResult = await RunScanAsync(session, request.Rows, request.WarmUpEnabled, expectedLineTimeUs, ct, onStatus, onDiagnostic, onByteProgress);
                if (!scanResult.Success || scanResult.ImageBytes is null)
                    throw new IOException($"Pass {passIndex + 1} failed: {scanResult.Message}");

                captures.Add(new ScanPassCapture(passIndex + 1, ledIndex, directionPositive, request.Rows, passMotorSteps, scanResult.ImageBytes));

                if (passMotorSteps > 0)
                {
                    onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Waiting for motor"));
                    await WaitForMotorIdleAsync(session, request.ScanMotorId, passMotorSteps, request.MotorIntervalUs, ct);
                    motionStarted = false;
                }

                if (!request.AlternateMotorDirection && passMotorSteps > 0 && passIndex < (TotalPasses - 1))
                {
                    onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, !directionPositive, "Returning"));
                    onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses}: returning Motor{request.ScanMotorId + 1} to start position before next channel scan...");
                    motionStarted = true;
                    await session.MoveMotorStepsAndWaitForCompletionAsync(request.ScanMotorId, !directionPositive, passMotorSteps, request.MotorIntervalUs, ct);
                    motionStarted = false;
                }

                onProgress?.Invoke(new ScanWorkflowProgress(passIndex + 1, TotalPasses, ledIndex, directionPositive, "Completed"));
                onStatus?.Invoke($"Pass {passIndex + 1}/{TotalPasses} complete.");
            }

            onStatus?.Invoke("Four-channel scan workflow completed.");
            return new ScanWorkflowResult(request.Rows, captures, computedMotorSteps, request.MotorIntervalUs, request.ExposureTicks, request.SysClockKhz);
        }
        finally
        {
            if (motionStarted)
            {
                try
                {
                    await session.StopMotorAsync(request.ScanMotorId, CancellationToken.None);
                    await WaitForMotorIdleAsync(session, request.ScanMotorId, computedMotorSteps, request.MotorIntervalUs, CancellationToken.None);
                }
                catch
                {
                }
            }

            await RestoreIlluminationAsync(session, originalIllumination);

            if (originalMotorState is not null && !originalMotorState.Enabled)
            {
                try
                {
                    await session.SetMotorEnabledAsync(request.ScanMotorId, false, CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }

    private static async Task<ScanStartResult> RunScanAsync(
        IScanSessionService session,
        int rows,
        bool warmUpEnabled,
        uint expectedLineTimeUs,
        CancellationToken ct,
        Action<string>? onStatus,
        Action<string>? onDiagnostic,
        Action<int, int>? onByteProgress)
    {
        if (warmUpEnabled && rows > session.SingleTransferMaxRows)
        {
            return await session.StartWarmUpSegmentedScanAsync(
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

    private static async Task ApplySingleLedIlluminationAsync(IScanSessionService session, ScanWorkflowRequest request, byte ledIndex, CancellationToken ct)
    {
        var acquisitionSettings = request.AcquisitionSettings?.Normalize() ?? BuildDefaultAcquisitionSettings(request.LedLevels, request.MotorIntervalUs);
        var levels = new ushort[ScanDebugConstants.IlluminationChannelCount];
        levels[ledIndex] = ledIndex switch
        {
            0 => acquisitionSettings.Led1Level,
            1 => acquisitionSettings.Led2Level,
            2 => acquisitionSettings.Led3Level,
            3 => acquisitionSettings.Led4Level,
            _ => 0
        };

        var ledMask = (byte)(1 << ledIndex);
        var syncMask = (byte)(acquisitionSettings.SyncMask & ledMask);
        var steadyMask = (byte)(acquisitionSettings.SteadyMask & ledMask);
        if (syncMask == 0 && steadyMask == 0)
            steadyMask = ledMask;

        await session.SetIlluminationLevelsAsync(levels[0], levels[1], levels[2], levels[3], ct);
        await session.SetSyncPulseClocksAsync(acquisitionSettings.Led1PulseClock, acquisitionSettings.Led2PulseClock, acquisitionSettings.Led3PulseClock, acquisitionSettings.Led4PulseClock, ct);
        await session.SetSteadyIlluminationAsync(steadyMask, ct);
        await session.ConfigureExposureLightingAsync(syncMask, ct);
    }

    private static ScanFilmAcquisitionSettings BuildDefaultAcquisitionSettings(ushort[] ledLevels, uint motorIntervalUs)
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
            motorIntervalUs).Normalize();
    }

    private static async Task RestoreIlluminationAsync(IScanSessionService session, ScanIlluminationState state)
    {
        try
        {
            await session.SetIlluminationLevelsAsync(state.Led1Level, state.Led2Level, state.Led3Level, state.Led4Level, CancellationToken.None);
            await session.SetSteadyIlluminationAsync(state.SteadyMask, CancellationToken.None);
            await session.ConfigureExposureLightingAsync(state.SyncMask, CancellationToken.None);
            await session.SetSyncPulseClocksAsync(state.Led1PulseClock, state.Led2PulseClock, state.Led3PulseClock, state.Led4PulseClock, CancellationToken.None);
        }
        catch
        {
        }
    }

    private static async Task WaitForMotorIdleAsync(IScanSessionService session, byte motorId, uint steps, uint intervalUs, CancellationToken ct)
    {
        await session.WaitForMotorMotionCompleteAsync(motorId, steps, intervalUs, ct);
    }

    private static uint ComputeMotorStepsPerPass(int rows, ushort exposureTicks, uint sysClockKhz, uint motorIntervalUs)
    {
        var scanDurationUs = (double)rows * ComputeLineExposureUs(exposureTicks, sysClockKhz);
        return (uint)Math.Max(1, (int)Math.Round(scanDurationUs / Math.Max(motorIntervalUs, 1u), MidpointRounding.AwayFromZero));
    }

    private static uint ComputeLineExposureUs(ushort exposureTicks, uint sysClockKhz)
    {
        var lineExposureNs = (45827.0 + (exposureTicks * 6.0)) * (1_000_000.0 / Math.Max(sysClockKhz, 1u));
        return (uint)Math.Max(1, (int)Math.Ceiling(lineExposureNs / 1000.0));
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

        if (request.ScanMotorId >= ScanDebugConstants.MotionMotorCount)
            throw new ArgumentOutOfRangeException(nameof(request), $"Scan motor id must be in [0, {ScanDebugConstants.MotionMotorCount - 1}].");

        if (request.MotorIntervalUs < ScanDebugConstants.MotionMinIntervalUs)
            throw new ArgumentOutOfRangeException(nameof(request), $"Motor interval must be at least {ScanDebugConstants.MotionMinIntervalUs} us.");

        if (request.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
            throw new ArgumentOutOfRangeException(nameof(request), $"System clock must be at least {ScanDebugConstants.MinSysClockKhz} kHz.");
    }
}
