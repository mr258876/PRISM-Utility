using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanAutoCalibrationService : IScanAutoCalibrationService
{
    private const int MaxBlackIterations = 32;
    private const int MaxWhiteIterations = 32;
    private const int MaxChannelBalanceIterations = 16;
    private const int MaxOffsetBalanceIterations = 16;
    private const int MaxAdjustmentHistory = 6;
    private const int MappingProbeOffsetStep = 32;
    private const double WhiteExposureSafetyFactor = 0.92;
    private const double BlackShieldWeight = 0.65;
    private const double WhiteShieldWeight = 0.35;
    private const int TargetBlackLevel = 512;
    private const int BlackTolerance = 48;
    private const int DeadBlackThreshold = 32;
    private const int WhiteTargetLevel = 60000;
    private const int WhiteTolerance = 1500;
    private const int BlackChannelMatchTolerance = 24;
    private const int WhiteChannelMatchTolerance = 192;
    private const int ShieldChannelMatchTolerance = 64;
    private const int FinalShieldChannelMatchTolerance = 24;
    private const double WhiteChannelRatioTolerance = 0.005;
    private const int SaturationThreshold = 65000;
    private const double SaturationRatioLimit = 0.0025;
    private const int DarkPixelThreshold = 64;

    private readonly IScanParameterService _parameters;
    private readonly IScanImageDecoder _decoder;
    private readonly IScanTransferSettingsService _transferSettings;

    public ScanAutoCalibrationService(IScanParameterService parameters, IScanImageDecoder decoder, IScanTransferSettingsService transferSettings)
    {
        _parameters = parameters;
        _decoder = decoder;
        _transferSettings = transferSettings;
    }

    public async Task<ScanParameterSnapshot> AutoCalibrateAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var blackAdjusted = await AutoBlackAdjustAsync(session, currentSnapshot, promptAsync, onStatus, onSnapshotApplied, onFrameCaptured, ct);
        return await AutoWhiteAdjustAsync(session, blackAdjusted, promptAsync, onStatus, onSnapshotApplied, onFrameCaptured, ct);
    }

    public async Task<ScanParameterSnapshot> AutoBlackAdjustAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var working = currentSnapshot with
        {
            Adc1Offset = 0,
            Adc2Offset = 0,
            Adc1Gain = 0,
            Adc2Gain = 0
        };

        onStatus?.Invoke("Auto black: resetting offset/gain to zero...");
        await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);

        var confirmed = await promptAsync(new ScanCalibrationPrompt(
            "Black Calibration",
            "Please cover the sensor completely. Warm-up will start, then the scanner will capture dark frames to auto-adjust black level and offset.",
            "Start",
            "Cancel"));
        if (!confirmed)
            throw new OperationCanceledException("Black calibration canceled by user.");

        var adc1State = new ChannelOffsetState();
        var adc2State = new ChannelOffsetState();
        var history = new List<CalibrationAttempt>(MaxAdjustmentHistory);
        ScanParameterSnapshot? bestSnapshot = null;
        var bestScore = double.MaxValue;

        try
        {
            var channelMapping = await DetectChannelMappingAsync(session, working, onStatus, onFrameCaptured, ct);
            await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            onStatus?.Invoke($"Auto black: detected mapping adc1={(channelMapping.IsAdc1Even ? "even" : "odd")}, adc2={(channelMapping.IsAdc1Even ? "odd" : "even")}.");

            for (var iteration = 1; iteration <= MaxBlackIterations; iteration++)
            {
                onStatus?.Invoke($"Auto black: iteration {iteration}/{MaxBlackIterations} sampling...");
                await session.SetWarmUpEnabledAsync(true, ct);
                await WaitForWarmUpSettlingAsync(working.ExposureTicks, working.SysClockKhz, onStatus, ct);

                var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.CalibrationSampleRows, $"Auto black iteration {iteration}", onStatus, onFrameCaptured, ct);
                await session.SetWarmUpEnabledAsync(false, ct);

                var adc1Mean = channelMapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
                var adc2Mean = channelMapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
                var adc1ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
                var adc2ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;

                onStatus?.Invoke($"Auto black: mean={stats.EffectiveMean:0.0}, adc1={adc1Mean:0.0}, adc2={adc2Mean:0.0}, shield-delta={Math.Abs(adc1ShieldMean - adc2ShieldMean):0.0}, min={stats.MinColumnMean:0.0}, dark-ratio={stats.DarkPixelRatio:P3}");

                var score = ComputeBlackScore(stats, channelMapping);
                RecordAttempt(history, working, score);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSnapshot = working;
                }

                if (bestSnapshot is not null && IsRepeatedBlackOscillation(history))
                {
                    onStatus?.Invoke($"Auto black: detected repeating oscillation, keeping best sampled offsets adc1={bestSnapshot.Adc1Offset}, adc2={bestSnapshot.Adc2Offset}.");
                    await ApplyParametersForCalibrationAsync(session, bestSnapshot, onSnapshotApplied, ct);
                    return bestSnapshot;
                }

                if (IsBlackValid(stats, channelMapping))
                {
                    onStatus?.Invoke($"Auto black: passed with offsets adc1={working.Adc1Offset}, adc2={working.Adc2Offset}.");
                    return working;
                }

                var (nextAdc1Offset, nextAdc2Offset) = ComputeNextBlackOffsets(
                    working,
                    adc1Mean,
                    adc2Mean,
                    adc1ShieldMean,
                    adc2ShieldMean,
                    adc1State,
                    adc2State,
                    history,
                    bestSnapshot);

                if (nextAdc1Offset == working.Adc1Offset && nextAdc2Offset == working.Adc2Offset)
                {
                    if (bestSnapshot is not null && !HasSameBlackOffsets(bestSnapshot, working))
                    {
                        onStatus?.Invoke($"Auto black: oscillation detected, reverting to best sampled offsets adc1={bestSnapshot.Adc1Offset}, adc2={bestSnapshot.Adc2Offset}.");
                        working = working with { Adc1Offset = bestSnapshot.Adc1Offset, Adc2Offset = bestSnapshot.Adc2Offset };
                        await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
                        return working;
                    }

                    throw new IOException("Auto black cannot improve offsets further.");
                }

                adc1State = adc1State.Remember(working.Adc1Offset, (adc1Mean * (1.0 - BlackShieldWeight)) + (adc1ShieldMean * BlackShieldWeight), nextAdc1Offset - working.Adc1Offset);
                adc2State = adc2State.Remember(working.Adc2Offset, (adc2Mean * (1.0 - BlackShieldWeight)) + (adc2ShieldMean * BlackShieldWeight), nextAdc2Offset - working.Adc2Offset);
                working = working with { Adc1Offset = nextAdc1Offset, Adc2Offset = nextAdc2Offset };

                onStatus?.Invoke($"Auto black: applying offsets adc1={working.Adc1Offset}, adc2={working.Adc2Offset}...");
                await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            }
        }
        finally
        {
            try { await session.SetWarmUpEnabledAsync(false, CancellationToken.None); } catch { }
        }

        if (bestSnapshot is not null)
        {
            onStatus?.Invoke($"Auto black: reached iteration limit, keeping best sampled offsets adc1={bestSnapshot.Adc1Offset}, adc2={bestSnapshot.Adc2Offset}.");
            await ApplyParametersForCalibrationAsync(session, bestSnapshot, onSnapshotApplied, ct);
            return bestSnapshot;
        }

        throw new IOException("Auto black failed to converge.");
    }

    public async Task<ScanParameterSnapshot> AutoWhiteAdjustAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var working = currentSnapshot with { Adc1Gain = 0, Adc2Gain = 0 };
        var adc1State = new ChannelGainState();
        var adc2State = new ChannelGainState();
        var history = new List<CalibrationAttempt>(MaxAdjustmentHistory);
        ScanParameterSnapshot? bestSnapshot = null;
        var bestScore = double.MaxValue;

        onStatus?.Invoke("Auto white: resetting gain to zero...");
        await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);

        var confirmed = await promptAsync(new ScanCalibrationPrompt(
            "White Calibration",
            "Place a uniform white paper target under fixed lighting. The scanner will probe overexposure range first, then adjust gain for a bright but non-clipped white level.",
            "Start",
            "Cancel"));
        if (!confirmed)
            throw new OperationCanceledException("White calibration canceled by user.");

        var originalExposure = (ushort)Math.Max(working.ExposureTicks, ScanDebugConstants.MinExposureTicks);
        if (working.ExposureTicks != originalExposure)
        {
            working = working with { ExposureTicks = originalExposure };
            await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
        }
        try
        {
            var channelMapping = await DetectChannelMappingAsync(session, working, onStatus, onFrameCaptured, ct);
            await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            onStatus?.Invoke($"Auto white: detected mapping adc1={(channelMapping.IsAdc1Even ? "even" : "odd")}, adc2={(channelMapping.IsAdc1Even ? "odd" : "even")}.");

            onStatus?.Invoke("Auto white: probing overexposure range...");
            var probeExposure = await ProbeSaturationExposureAsync(session, working, onStatus, onFrameCaptured, ct);
            onStatus?.Invoke($"Auto white: saturation starts around exposure ticks {probeExposure} ({FormatExposureTime(probeExposure, working.SysClockKhz)}).");

            var seededGain = SelectGainFromExposureRatio(originalExposure, probeExposure, working.SysClockKhz, WhiteExposureSafetyFactor);
            if (seededGain > 0)
            {
                working = working with { Adc1Gain = seededGain, Adc2Gain = seededGain };
                onStatus?.Invoke($"Auto white: seeding gain from exposure ratio with adc1={seededGain}, adc2={seededGain} (safety={WhiteExposureSafetyFactor:0.00}).");
                await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            }

            if (working.ExposureTicks != originalExposure)
            {
                working = working with { ExposureTicks = originalExposure };
                await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            }

            for (var iteration = 1; iteration <= MaxWhiteIterations; iteration++)
            {
                onStatus?.Invoke($"Auto white: iteration {iteration}/{MaxWhiteIterations} sampling...");
                await session.SetWarmUpEnabledAsync(true, ct);
                await WaitForWarmUpSettlingAsync(working.ExposureTicks, working.SysClockKhz, onStatus, ct);

                var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.CalibrationSampleRows, $"Auto white iteration {iteration}", onStatus, onFrameCaptured, ct);
                await session.SetWarmUpEnabledAsync(false, ct);

                var adc1Mean = channelMapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
                var adc2Mean = channelMapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
                var adc1ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
                var adc2ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;

                onStatus?.Invoke($"Auto white: mean={stats.EffectiveMean:0.0}, adc1={adc1Mean:0.0}, adc2={adc2Mean:0.0}, active-delta={Math.Abs(adc1Mean - adc2Mean):0.0}, shield-delta={Math.Abs(adc1ShieldMean - adc2ShieldMean):0.0}, sat-ratio={stats.SaturationRatio:P3}");

                var score = ComputeWhiteScore(stats, channelMapping);
                RecordAttempt(history, working, score);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSnapshot = working;
                }

                if (bestSnapshot is not null && IsRepeatedWhiteOscillation(history))
                {
                    onStatus?.Invoke($"Auto white: detected repeating oscillation, keeping best sampled gains adc1={bestSnapshot.Adc1Gain}, adc2={bestSnapshot.Adc2Gain}.");
                    await ApplyParametersForCalibrationAsync(session, bestSnapshot, onSnapshotApplied, ct);
                    return bestSnapshot;
                }

                if (IsWhiteValid(stats, channelMapping))
                {
                    working = await BalanceOffsetsAfterWhiteAdjustAsync(session, working, channelMapping, onStatus, onSnapshotApplied, onFrameCaptured, ct);
                    working = await BalanceChannelsAfterWhiteAdjustAsync(session, working, channelMapping, onStatus, onSnapshotApplied, onFrameCaptured, ct);
                    onStatus?.Invoke($"Auto white: passed with gain adc1={working.Adc1Gain}, adc2={working.Adc2Gain}.");
                    return working;
                }

                var (nextAdc1Gain, nextAdc2Gain) = SelectNextWhiteGains(
                    working,
                    adc1Mean,
                    adc2Mean,
                    adc1ShieldMean,
                    adc2ShieldMean,
                    stats.SaturationRatio,
                    adc1State,
                    adc2State,
                    history,
                    bestSnapshot);

                if (nextAdc1Gain == working.Adc1Gain && nextAdc2Gain == working.Adc2Gain)
                {
                    if (bestSnapshot is not null && !HasSameWhiteGains(bestSnapshot, working))
                    {
                        onStatus?.Invoke($"Auto white: oscillation detected, reverting to best sampled gains adc1={bestSnapshot.Adc1Gain}, adc2={bestSnapshot.Adc2Gain}.");
                        working = working with { Adc1Gain = bestSnapshot.Adc1Gain, Adc2Gain = bestSnapshot.Adc2Gain };
                        await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
                        return working;
                    }

                    throw new IOException("Auto white cannot improve gains further.");
                }

                adc1State = adc1State.Remember(working.Adc1Gain, adc1Mean, nextAdc1Gain - working.Adc1Gain);
                adc2State = adc2State.Remember(working.Adc2Gain, adc2Mean, nextAdc2Gain - working.Adc2Gain);
                working = working with { Adc1Gain = nextAdc1Gain, Adc2Gain = nextAdc2Gain };
                onStatus?.Invoke($"Auto white: applying gains adc1={working.Adc1Gain}, adc2={working.Adc2Gain}...");
                await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            }
        }
        finally
        {
            try { await session.SetWarmUpEnabledAsync(false, CancellationToken.None); } catch { }
        }

        if (bestSnapshot is not null)
        {
            onStatus?.Invoke($"Auto white: reached iteration limit, keeping best sampled gains adc1={bestSnapshot.Adc1Gain}, adc2={bestSnapshot.Adc2Gain}.");
            await ApplyParametersForCalibrationAsync(session, bestSnapshot, onSnapshotApplied, ct);
            return bestSnapshot;
        }

        throw new IOException("Auto white failed to converge.");
    }

    private async Task<ushort> ProbeSaturationExposureAsync(IScanSessionService session, ScanParameterSnapshot snapshot, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var exposure = (ushort)Math.Max(snapshot.ExposureTicks, ScanDebugConstants.MinExposureTicks);
        var lastExposure = exposure;
        var exposureNs = ExposureTicksToNanoseconds(exposure, snapshot.SysClockKhz);

        for (var step = 1; step <= 6; step++)
        {
            var probeSnapshot = snapshot with { ExposureTicks = exposure, Adc1Gain = 0, Adc2Gain = 0 };
            await ApplyParametersForCalibrationAsync(session, probeSnapshot, null, ct);
            await session.SetWarmUpEnabledAsync(true, ct);
            await WaitForWarmUpSettlingAsync(exposure, snapshot.SysClockKhz, onStatus, ct);
            var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.WhiteProbeSampleRows, $"White probe step {step}", onStatus, onFrameCaptured, ct);
            await session.SetWarmUpEnabledAsync(false, ct);

            onStatus?.Invoke($"Auto white: probe exposure={exposure} ({FormatExposureTime(exposure, snapshot.SysClockKhz)}), mean={stats.EffectiveMean:0.0}, sat-ratio={stats.SaturationRatio:P3}");
            lastExposure = exposure;

            if (stats.SaturationRatio >= SaturationRatioLimit)
                return exposure;

            if (exposure >= ushort.MaxValue / 2)
                break;

            exposureNs *= 2.0;
            exposure = NanosecondsToExposureTicks(exposureNs, snapshot.SysClockKhz);
        }

        return lastExposure;
    }

    private async Task<ScanChannelMapping> DetectChannelMappingAsync(IScanSessionService session, ScanParameterSnapshot snapshot, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        onStatus?.Invoke("Auto calibration: detecting ADC/pixel mapping...");

        await session.SetWarmUpEnabledAsync(true, ct);
        await WaitForWarmUpSettlingAsync(snapshot.ExposureTicks, snapshot.SysClockKhz, onStatus, ct);
        var baseline = await CaptureStatisticsAsync(session, session.SingleTransferMaxRows, "Channel mapping baseline", onStatus, onFrameCaptured, ct);
        await session.SetWarmUpEnabledAsync(false, ct);

        var probeOffset = Math.Min(snapshot.Adc1Offset + MappingProbeOffsetStep, 255);
        var probeSnapshot = snapshot with { Adc1Offset = probeOffset };
        await ApplyParametersForCalibrationAsync(session, probeSnapshot, null, ct);

        await session.SetWarmUpEnabledAsync(true, ct);
        await WaitForWarmUpSettlingAsync(snapshot.ExposureTicks, snapshot.SysClockKhz, onStatus, ct);
        var probe = await CaptureStatisticsAsync(session, session.SingleTransferMaxRows, "Channel mapping probe", onStatus, onFrameCaptured, ct);
        await session.SetWarmUpEnabledAsync(false, ct);
        await ApplyParametersForCalibrationAsync(session, snapshot, null, ct);

        var evenDelta = probe.EvenMean - baseline.EvenMean;
        var oddDelta = probe.OddMean - baseline.OddMean;
        var isAdc1Even = Math.Abs(evenDelta) >= Math.Abs(oddDelta);
        var adc1Delta = isAdc1Even ? evenDelta : oddDelta;
        var adc2Delta = isAdc1Even ? oddDelta : evenDelta;

        onStatus?.Invoke($"Auto calibration: mapping probe even-delta={evenDelta:0.0}, odd-delta={oddDelta:0.0}.");
        return new ScanChannelMapping(isAdc1Even, adc1Delta, adc2Delta);
    }

    private async Task<ScanParameterSnapshot> BalanceChannelsAfterWhiteAdjustAsync(IScanSessionService session, ScanParameterSnapshot snapshot, ScanChannelMapping mapping, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var working = snapshot;

        for (var iteration = 1; iteration <= MaxChannelBalanceIterations; iteration++)
        {
            await session.SetWarmUpEnabledAsync(true, ct);
            await WaitForWarmUpSettlingAsync(working.ExposureTicks, working.SysClockKhz, onStatus, ct);
            var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.CalibrationSampleRows, $"White channel balance {iteration}", onStatus, onFrameCaptured, ct);
            await session.SetWarmUpEnabledAsync(false, ct);

            var adc1Mean = mapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
            var adc2Mean = mapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
            var adc1ShieldMean = mapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
            var adc2ShieldMean = mapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;
            var activeDelta = adc1Mean - adc2Mean;
            var shieldDelta = adc1ShieldMean - adc2ShieldMean;
            var ratio = adc1Mean / Math.Max(adc2Mean, 1.0);

            onStatus?.Invoke($"White balance: iteration {iteration}/{MaxChannelBalanceIterations}, active-delta={activeDelta:0.0}, shield-delta={shieldDelta:0.0}, ratio={ratio:0.0000}");

            if (Math.Abs(activeDelta) <= WhiteChannelMatchTolerance
                && Math.Abs(shieldDelta) <= ShieldChannelMatchTolerance
                && Math.Abs(1.0 - ratio) <= WhiteChannelRatioTolerance)
            {
                return working;
            }

            var weightedDelta = activeDelta + (shieldDelta * 0.5);
            if (weightedDelta > 0)
            {
                working = working with
                {
                    Adc1Gain = (ushort)Math.Max(0, working.Adc1Gain - 1),
                    Adc2Gain = (ushort)Math.Min(63, working.Adc2Gain + 1)
                };
            }
            else
            {
                working = working with
                {
                    Adc1Gain = (ushort)Math.Min(63, working.Adc1Gain + 1),
                    Adc2Gain = (ushort)Math.Max(0, working.Adc2Gain - 1)
                };
            }

            await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
        }

        return working;
    }

    private async Task<ScanParameterSnapshot> BalanceOffsetsAfterWhiteAdjustAsync(IScanSessionService session, ScanParameterSnapshot snapshot, ScanChannelMapping mapping, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var working = snapshot;

        for (var iteration = 1; iteration <= MaxOffsetBalanceIterations; iteration++)
        {
            await session.SetWarmUpEnabledAsync(true, ct);
            await WaitForWarmUpSettlingAsync(working.ExposureTicks, working.SysClockKhz, onStatus, ct);
            var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.CalibrationSampleRows, $"White offset balance {iteration}", onStatus, onFrameCaptured, ct);
            await session.SetWarmUpEnabledAsync(false, ct);

            var adc1Mean = mapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
            var adc2Mean = mapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
            var adc1ShieldMean = mapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
            var adc2ShieldMean = mapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;
            var shieldDelta = adc1ShieldMean - adc2ShieldMean;
            var activeDelta = adc1Mean - adc2Mean;

            onStatus?.Invoke($"White offset balance: iteration {iteration}/{MaxOffsetBalanceIterations}, shield-delta={shieldDelta:0.0}, active-delta={activeDelta:0.0}, offsets=({working.Adc1Offset},{working.Adc2Offset})");

            if (Math.Abs(shieldDelta) <= FinalShieldChannelMatchTolerance)
                return working;

            var offsetStep = Math.Abs(shieldDelta) > 256 ? 2 : 1;
            if (shieldDelta > 0)
            {
                working = working with
                {
                    Adc1Offset = Math.Clamp(working.Adc1Offset - offsetStep, -255, 255),
                    Adc2Offset = Math.Clamp(working.Adc2Offset + offsetStep, -255, 255)
                };
            }
            else
            {
                working = working with
                {
                    Adc1Offset = Math.Clamp(working.Adc1Offset + offsetStep, -255, 255),
                    Adc2Offset = Math.Clamp(working.Adc2Offset - offsetStep, -255, 255)
                };
            }

            await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
        }

        return working;
    }

    private async Task ApplyParametersForCalibrationAsync(IScanSessionService session, ScanParameterSnapshot snapshot, Action<ScanParameterSnapshot>? onSnapshotApplied, CancellationToken ct)
    {
        await session.SetWarmUpEnabledAsync(false, ct);
        await _parameters.ApplyAsync(session, snapshot, ct);
        onSnapshotApplied?.Invoke(snapshot);
    }

    private async Task<ScanCalibrationStatistics> CaptureStatisticsAsync(IScanSessionService session, int rows, string phase, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        onStatus?.Invoke($"{phase}: capturing {rows} rows...");
        var useExtendedSingleRead = await ShouldUseFullStartReadPathAsync();
        var result = rows > session.SingleTransferMaxRows && !useExtendedSingleRead
            ? await session.StartWarmUpSegmentedScanAsync(rows, ct, status => onStatus?.Invoke($"{phase}: {status}"))
            : await session.StartScanAsync(rows, ct, status => onStatus?.Invoke($"{phase}: {status}"));
        if (!result.Success || result.ImageBytes is null)
            throw new IOException($"{phase} failed: {result.Message}");

        onFrameCaptured?.Invoke(result.ImageBytes, rows, phase);

        return BuildStatistics(result.ImageBytes, rows);
    }

    private ScanCalibrationStatistics BuildStatistics(byte[] lineBuffer, int rows)
    {
        var width = _decoder.GetDecodedPixelsPerLine();
        var (effectiveStart, effectiveEnd) = _decoder.GetEffectivePixelRange();
        var shieldStart = Math.Clamp(ScanDebugConstants.ShieldPixelStart, 0, width - 1);
        var shieldEnd = Math.Clamp(ScanDebugConstants.ShieldPixelEnd, shieldStart, effectiveStart - 1);
        var effectiveCount = effectiveEnd - effectiveStart + 1;
        var columnSums = new double[effectiveCount];
        long evenCount = 0;
        long oddCount = 0;
        double evenSum = 0;
        double oddSum = 0;
        long shieldEvenCount = 0;
        long shieldOddCount = 0;
        double shieldEvenSum = 0;
        double shieldOddSum = 0;
        long darkCount = 0;
        long saturationCount = 0;
        long totalCount = 0;

        for (var y = 0; y < rows; y++)
        {
            for (var x = effectiveStart; x <= effectiveEnd; x++)
            {
                if (!_decoder.TryGetSample16(lineBuffer, rows, x, y, out var sample))
                    continue;

                var sampleValue = (double)sample;
                columnSums[x - effectiveStart] += sampleValue;
                totalCount++;
                if (sample <= DarkPixelThreshold)
                    darkCount++;
                if (sample >= SaturationThreshold)
                    saturationCount++;

                if ((x & 1) == 0)
                {
                    evenSum += sampleValue;
                    evenCount++;
                }
                else
                {
                    oddSum += sampleValue;
                    oddCount++;
                }
            }

            for (var x = shieldStart; x <= shieldEnd; x++)
            {
                if (!_decoder.TryGetSample16(lineBuffer, rows, x, y, out var sample))
                    continue;

                if ((x & 1) == 0)
                {
                    shieldEvenSum += sample;
                    shieldEvenCount++;
                }
                else
                {
                    shieldOddSum += sample;
                    shieldOddCount++;
                }
            }
        }

        var columnMeans = new double[effectiveCount];
        var minColumnMean = double.MaxValue;
        var maxColumnMean = double.MinValue;
        var totalMean = 0.0;
        for (var i = 0; i < effectiveCount; i++)
        {
            var mean = columnSums[i] / Math.Max(rows, 1);
            columnMeans[i] = mean;
            totalMean += mean;
            if (mean < minColumnMean)
                minColumnMean = mean;
            if (mean > maxColumnMean)
                maxColumnMean = mean;
        }

        return new ScanCalibrationStatistics(
            rows,
            effectiveCount,
            columnMeans,
            shieldEvenSum / Math.Max(shieldEvenCount, 1),
            shieldOddSum / Math.Max(shieldOddCount, 1),
            evenSum / Math.Max(evenCount, 1),
            oddSum / Math.Max(oddCount, 1),
            totalMean / Math.Max(effectiveCount, 1),
            minColumnMean,
            maxColumnMean,
            saturationCount / (double)Math.Max(totalCount, 1),
            darkCount / (double)Math.Max(totalCount, 1));
    }

    private static bool IsBlackValid(ScanCalibrationStatistics stats, ScanChannelMapping mapping)
    {
        var adc1Mean = mapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
        var adc2Mean = mapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
        var adc1ShieldMean = mapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
        var adc2ShieldMean = mapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;
        return Math.Abs(adc1Mean - TargetBlackLevel) <= BlackTolerance
           && Math.Abs(adc2Mean - TargetBlackLevel) <= BlackTolerance
           && Math.Abs(adc1Mean - adc2Mean) <= BlackChannelMatchTolerance
           && Math.Abs(adc1ShieldMean - adc2ShieldMean) <= BlackChannelMatchTolerance
           && stats.MinColumnMean > DeadBlackThreshold
           && stats.DarkPixelRatio < 0.01;
    }

    private static bool IsWhiteValid(ScanCalibrationStatistics stats, ScanChannelMapping mapping)
    {
        var adc1Mean = mapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
        var adc2Mean = mapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
        var adc1ShieldMean = mapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
        var adc2ShieldMean = mapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;
        var ratio = adc1Mean / Math.Max(adc2Mean, 1.0);
        return Math.Abs(adc1Mean - WhiteTargetLevel) <= WhiteTolerance
           && Math.Abs(adc2Mean - WhiteTargetLevel) <= WhiteTolerance
           && Math.Abs(adc1Mean - adc2Mean) <= WhiteChannelMatchTolerance
           && Math.Abs(adc1ShieldMean - adc2ShieldMean) <= ShieldChannelMatchTolerance
           && Math.Abs(1.0 - ratio) <= WhiteChannelRatioTolerance
           && stats.SaturationRatio < SaturationRatioLimit;
    }

    private static double ComputeBlackScore(ScanCalibrationStatistics stats, ScanChannelMapping mapping)
    {
        var adc1Mean = mapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
        var adc2Mean = mapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
        var adc1ShieldMean = mapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
        var adc2ShieldMean = mapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;
        var score = Math.Abs(adc1Mean - TargetBlackLevel)
            + Math.Abs(adc2Mean - TargetBlackLevel)
            + (Math.Abs(adc1Mean - adc2Mean) * 2.0)
            + (Math.Abs(adc1ShieldMean - adc2ShieldMean) * 1.5);

        if (stats.MinColumnMean <= DeadBlackThreshold)
            score += (DeadBlackThreshold - stats.MinColumnMean) * 8.0;
        if (stats.DarkPixelRatio >= 0.01)
            score += (stats.DarkPixelRatio - 0.01) * 200_000.0;

        return score;
    }

    private static double ComputeWhiteScore(ScanCalibrationStatistics stats, ScanChannelMapping mapping)
    {
        var adc1Mean = mapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
        var adc2Mean = mapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
        var adc1ShieldMean = mapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
        var adc2ShieldMean = mapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;
        var ratio = adc1Mean / Math.Max(adc2Mean, 1.0);
        var score = Math.Abs(adc1Mean - WhiteTargetLevel)
            + Math.Abs(adc2Mean - WhiteTargetLevel)
            + (Math.Abs(adc1Mean - adc2Mean) * 2.0)
            + (Math.Abs(adc1ShieldMean - adc2ShieldMean) * 1.5)
            + (Math.Abs(1.0 - ratio) * 20_000.0);

        if (stats.SaturationRatio >= SaturationRatioLimit)
            score += (stats.SaturationRatio - SaturationRatioLimit) * 2_000_000.0;

        return score;
    }

    private static int ComputeNextOffset(int currentOffset, double currentMean, ChannelOffsetState state)
    {
        var error = TargetBlackLevel - currentMean;
        double slope = 24.0;
        if (state.PreviousMean is not null && state.PreviousOffset is not null)
        {
            var deltaOffset = currentOffset - state.PreviousOffset.Value;
            var deltaMean = currentMean - state.PreviousMean.Value;
            if (deltaOffset != 0 && Math.Abs(deltaMean) > 0.01)
                slope = deltaMean / deltaOffset;
        }

        if (Math.Abs(slope) < 0.5)
            slope = error >= 0 ? 24.0 : -24.0;

        var delta = (int)Math.Round(error / slope, MidpointRounding.AwayFromZero);
        if (delta == 0)
            delta = error > 0 ? 1 : -1;

        delta = LimitSignedStep(delta, SelectOffsetStepLimit(error));
        var candidate = Math.Clamp(currentOffset + delta, -255, 255);

        if (HasErrorSignFlip(state.PreviousError, error) && state.PreviousOffset is not null)
            candidate = SelectMidpointValue(currentOffset, state.PreviousOffset.Value, error, -255, 255);

        if (state.OlderOffset is not null && candidate == state.OlderOffset.Value)
            candidate = SelectMidpointValue(currentOffset, state.OlderOffset.Value, error, -255, 255);

        return candidate;
    }

    private static (int adc1Offset, int adc2Offset) ComputeNextBlackOffsets(
        ScanParameterSnapshot current,
        double adc1ActiveMean,
        double adc2ActiveMean,
        double adc1ShieldMean,
        double adc2ShieldMean,
        ChannelOffsetState adc1State,
        ChannelOffsetState adc2State,
        IReadOnlyList<CalibrationAttempt> history,
        ScanParameterSnapshot? bestSnapshot)
    {
        var adc1DarkMean = (adc1ActiveMean * (1.0 - BlackShieldWeight)) + (adc1ShieldMean * BlackShieldWeight);
        var adc2DarkMean = (adc2ActiveMean * (1.0 - BlackShieldWeight)) + (adc2ShieldMean * BlackShieldWeight);

        var nextAdc1Offset = ComputeNextOffset(current.Adc1Offset, adc1DarkMean, adc1State);
        var nextAdc2Offset = ComputeNextOffset(current.Adc2Offset, adc2DarkMean, adc2State);

        var channelDelta = adc1DarkMean - adc2DarkMean;
        if (Math.Abs(channelDelta) > BlackChannelMatchTolerance)
        {
            var balanceStep = Math.Abs(channelDelta) > 128 ? 2 : 1;
            if (channelDelta > 0)
            {
                nextAdc1Offset = Math.Clamp(nextAdc1Offset - balanceStep, -255, 255);
                nextAdc2Offset = Math.Clamp(nextAdc2Offset + balanceStep, -255, 255);
            }
            else
            {
                nextAdc1Offset = Math.Clamp(nextAdc1Offset + balanceStep, -255, 255);
                nextAdc2Offset = Math.Clamp(nextAdc2Offset - balanceStep, -255, 255);
            }
        }

        return StabilizeOffsetPair(current, nextAdc1Offset, nextAdc2Offset, history, bestSnapshot);
    }

    private static (int adc1Offset, int adc2Offset) StabilizeOffsetPair(
        ScanParameterSnapshot current,
        int nextAdc1Offset,
        int nextAdc2Offset,
        IReadOnlyList<CalibrationAttempt> history,
        ScanParameterSnapshot? bestSnapshot)
    {
        if (!WouldRevisitBlackOffsets(nextAdc1Offset, nextAdc2Offset, history))
            return (nextAdc1Offset, nextAdc2Offset);

        if (bestSnapshot is not null && !HasSameBlackOffsets(bestSnapshot, current))
        {
            nextAdc1Offset = SelectMidpointValue(current.Adc1Offset, bestSnapshot.Adc1Offset, bestSnapshot.Adc1Offset - current.Adc1Offset, -255, 255);
            nextAdc2Offset = SelectMidpointValue(current.Adc2Offset, bestSnapshot.Adc2Offset, bestSnapshot.Adc2Offset - current.Adc2Offset, -255, 255);
        }
        else if (history.Count >= 2)
        {
            var previous = history[^2].Snapshot;
            nextAdc1Offset = SelectMidpointValue(current.Adc1Offset, previous.Adc1Offset, previous.Adc1Offset - current.Adc1Offset, -255, 255);
            nextAdc2Offset = SelectMidpointValue(current.Adc2Offset, previous.Adc2Offset, previous.Adc2Offset - current.Adc2Offset, -255, 255);
        }

        return (nextAdc1Offset, nextAdc2Offset);
    }

    private static ushort SelectNextGain(double currentMean, ushort currentGain, double saturationRatio, ChannelGainState state)
    {
        if (saturationRatio >= SaturationRatioLimit)
            return (ushort)Math.Max(0, currentGain - 1);

        var currentScale = ComputeGainScale(currentGain);
        var bestGain = currentGain;
        var bestError = double.MaxValue;

        for (ushort candidate = 0; candidate <= 63; candidate++)
        {
            var candidateScale = ComputeGainScale(candidate);
            var predicted = currentMean * (candidateScale / currentScale);
            var error = Math.Abs(predicted - WhiteTargetLevel);
            if (predicted > 63000)
                error += 5000;

            if (error < bestError)
            {
                bestError = error;
                bestGain = candidate;
            }
        }

        if (HasErrorSignFlip(state.PreviousError, WhiteTargetLevel - currentMean) && state.PreviousValue is not null)
            bestGain = (ushort)SelectMidpointValue(currentGain, state.PreviousValue.Value, WhiteTargetLevel - currentMean, 0, 63);

        if (state.OlderValue is not null && bestGain == state.OlderValue.Value)
            bestGain = (ushort)SelectMidpointValue(currentGain, state.OlderValue.Value, WhiteTargetLevel - currentMean, 0, 63);

        var limitedDelta = LimitSignedStep(bestGain - currentGain, SelectGainStepLimit(WhiteTargetLevel - currentMean, saturationRatio));
        bestGain = (ushort)Math.Clamp(currentGain + limitedDelta, 0, 63);

        return bestGain;
    }

    private static (ushort adc1Gain, ushort adc2Gain) SelectNextWhiteGains(
        ScanParameterSnapshot current,
        double adc1ActiveMean,
        double adc2ActiveMean,
        double adc1ShieldMean,
        double adc2ShieldMean,
        double saturationRatio,
        ChannelGainState adc1State,
        ChannelGainState adc2State,
        IReadOnlyList<CalibrationAttempt> history,
        ScanParameterSnapshot? bestSnapshot)
    {
        var nextAdc1Gain = SelectNextGain(adc1ActiveMean, current.Adc1Gain, saturationRatio, adc1State);
        var nextAdc2Gain = SelectNextGain(adc2ActiveMean, current.Adc2Gain, saturationRatio, adc2State);

        var weightedDelta = (adc1ActiveMean - adc2ActiveMean) + ((adc1ShieldMean - adc2ShieldMean) * WhiteShieldWeight);
        if (Math.Abs(weightedDelta) > WhiteChannelMatchTolerance)
        {
            var balanceStep = Math.Abs(weightedDelta) > 512 ? 2 : 1;
            if (weightedDelta > 0)
            {
                nextAdc1Gain = (ushort)Math.Max(0, nextAdc1Gain - balanceStep);
                nextAdc2Gain = (ushort)Math.Min(63, nextAdc2Gain + balanceStep);
            }
            else
            {
                nextAdc1Gain = (ushort)Math.Min(63, nextAdc1Gain + balanceStep);
                nextAdc2Gain = (ushort)Math.Max(0, nextAdc2Gain - balanceStep);
            }
        }

        return StabilizeGainPair(current, nextAdc1Gain, nextAdc2Gain, history, bestSnapshot);
    }

    private static (ushort adc1Gain, ushort adc2Gain) StabilizeGainPair(
        ScanParameterSnapshot current,
        ushort nextAdc1Gain,
        ushort nextAdc2Gain,
        IReadOnlyList<CalibrationAttempt> history,
        ScanParameterSnapshot? bestSnapshot)
    {
        if (!WouldRevisitWhiteGains(nextAdc1Gain, nextAdc2Gain, history))
            return (nextAdc1Gain, nextAdc2Gain);

        if (bestSnapshot is not null && !HasSameWhiteGains(bestSnapshot, current))
        {
            nextAdc1Gain = (ushort)SelectMidpointValue(current.Adc1Gain, bestSnapshot.Adc1Gain, bestSnapshot.Adc1Gain - current.Adc1Gain, 0, 63);
            nextAdc2Gain = (ushort)SelectMidpointValue(current.Adc2Gain, bestSnapshot.Adc2Gain, bestSnapshot.Adc2Gain - current.Adc2Gain, 0, 63);
        }
        else if (history.Count >= 2)
        {
            var previous = history[^2].Snapshot;
            nextAdc1Gain = (ushort)SelectMidpointValue(current.Adc1Gain, previous.Adc1Gain, previous.Adc1Gain - current.Adc1Gain, 0, 63);
            nextAdc2Gain = (ushort)SelectMidpointValue(current.Adc2Gain, previous.Adc2Gain, previous.Adc2Gain - current.Adc2Gain, 0, 63);
        }

        return (nextAdc1Gain, nextAdc2Gain);
    }

    private static void RecordAttempt(List<CalibrationAttempt> history, ScanParameterSnapshot snapshot, double score)
    {
        history.Add(new CalibrationAttempt(snapshot, score));
        if (history.Count > MaxAdjustmentHistory)
            history.RemoveAt(0);
    }

    private async Task<bool> ShouldUseFullStartReadPathAsync()
    {
        await _transferSettings.InitializeAsync();
        var settings = _transferSettings.Settings;
        return settings.ReadMode == ScanBulkInReadMode.MultiBuffered && settings.RawIoEnabled;
    }

    private static bool IsRepeatedBlackOscillation(IReadOnlyList<CalibrationAttempt> history)
    {
        if (history.Count < 4)
            return false;

        var a = history[^1].Snapshot;
        var b = history[^2].Snapshot;
        var c = history[^3].Snapshot;
        var d = history[^4].Snapshot;
        return HasSameBlackOffsets(a, c) && HasSameBlackOffsets(b, d);
    }

    private static bool IsRepeatedWhiteOscillation(IReadOnlyList<CalibrationAttempt> history)
    {
        if (history.Count < 4)
            return false;

        var a = history[^1].Snapshot;
        var b = history[^2].Snapshot;
        var c = history[^3].Snapshot;
        var d = history[^4].Snapshot;
        return HasSameWhiteGains(a, c) && HasSameWhiteGains(b, d);
    }

    private static bool WouldRevisitBlackOffsets(int adc1Offset, int adc2Offset, IReadOnlyList<CalibrationAttempt> history)
        => history.Any(attempt => attempt.Snapshot.Adc1Offset == adc1Offset && attempt.Snapshot.Adc2Offset == adc2Offset);

    private static bool WouldRevisitWhiteGains(ushort adc1Gain, ushort adc2Gain, IReadOnlyList<CalibrationAttempt> history)
        => history.Any(attempt => attempt.Snapshot.Adc1Gain == adc1Gain && attempt.Snapshot.Adc2Gain == adc2Gain);

    private static bool HasSameBlackOffsets(ScanParameterSnapshot left, ScanParameterSnapshot right)
        => left.Adc1Offset == right.Adc1Offset && left.Adc2Offset == right.Adc2Offset;

    private static bool HasSameWhiteGains(ScanParameterSnapshot left, ScanParameterSnapshot right)
        => left.Adc1Gain == right.Adc1Gain && left.Adc2Gain == right.Adc2Gain;

    private static bool HasErrorSignFlip(double? previousError, double currentError)
        => previousError is not null
            && Math.Abs(previousError.Value) > 0.01
            && Math.Abs(currentError) > 0.01
            && Math.Sign(previousError.Value) != Math.Sign(currentError);

    private static int SelectMidpointValue(int currentValue, int targetValue, double error, int minValue, int maxValue)
    {
        var midpoint = (int)Math.Round((currentValue + targetValue) / 2.0, MidpointRounding.AwayFromZero);
        if (midpoint == currentValue)
            midpoint = currentValue + (error >= 0 ? 1 : -1);

        return Math.Clamp(midpoint, minValue, maxValue);
    }

    private static int SelectOffsetStepLimit(double error)
    {
        var magnitude = Math.Abs(error);
        if (magnitude > 768.0)
            return 8;
        if (magnitude > 256.0)
            return 4;
        if (magnitude > 96.0)
            return 2;

        return 1;
    }

    private static int SelectGainStepLimit(double error, double saturationRatio)
    {
        if (saturationRatio >= SaturationRatioLimit)
            return 1;

        var magnitude = Math.Abs(error);
        if (magnitude > 12000.0)
            return 6;
        if (magnitude > 5000.0)
            return 3;
        if (magnitude > 2000.0)
            return 2;

        return 1;
    }

    private static int LimitSignedStep(int desiredStep, int maxMagnitude)
    {
        if (desiredStep == 0)
            return 0;

        var sign = Math.Sign(desiredStep);
        var magnitude = Math.Min(Math.Abs(desiredStep), Math.Max(1, maxMagnitude));
        return sign * magnitude;
    }

    private static ushort SelectGainFromExposureRatio(ushort currentExposureTicks, ushort saturationExposureTicks, uint sysClockKhz, double safetyFactor)
    {
        var currentExposureNs = ExposureTicksToNanoseconds(currentExposureTicks, sysClockKhz);
        var saturationExposureNs = ExposureTicksToNanoseconds(saturationExposureTicks, sysClockKhz);
        if (currentExposureNs <= 0 || saturationExposureNs <= 0)
            return 0;

        var targetScale = Math.Max(1.0, (saturationExposureNs / currentExposureNs) * safetyFactor);
        var bestGain = (ushort)0;
        var bestError = double.MaxValue;

        for (ushort candidate = 0; candidate <= 63; candidate++)
        {
            var candidateScale = ComputeGainScale(candidate);
            var error = Math.Abs(candidateScale - targetScale);
            if (error < bestError)
            {
                bestError = error;
                bestGain = candidate;
            }
        }

        return bestGain;
    }

    private static double ComputeGainScale(ushort gain)
        => 6.0 / (1.0 + 5.0 * ((63.0 - gain) / 63.0));

    private static double ExposureTicksToNanoseconds(ushort ticks, uint sysClockKhz)
        => (45827.0 + (ticks * 6.0)) * (1_000_000.0 / Math.Max(sysClockKhz, 1u));

    private static ushort NanosecondsToExposureTicks(double nanoseconds, uint sysClockKhz)
    {
        var ticks = ((nanoseconds * Math.Max(sysClockKhz, 1u)) / 1_000_000.0 - 45827.0) / 6.0;
        var rounded = (int)Math.Round(ticks, MidpointRounding.AwayFromZero);
        return (ushort)Math.Clamp(rounded, ScanDebugConstants.MinExposureTicks, ushort.MaxValue);
    }

    private static string FormatExposureTime(ushort ticks, uint sysClockKhz)
    {
        var exposureNs = ExposureTicksToNanoseconds(ticks, sysClockKhz);
        var exposureUs = exposureNs / 1000.0;
        return $"{exposureNs:0.##} ns / {exposureUs:0.###} us";
    }

    private static async Task WaitForWarmUpSettlingAsync(ushort exposureTicks, uint sysClockKhz, Action<string>? onStatus, CancellationToken ct)
    {
        var clampedTicks = (ushort)Math.Max(exposureTicks, ScanDebugConstants.MinExposureTicks);
        var settleMs = Math.Max(1, (int)Math.Ceiling((ExposureTicksToNanoseconds(clampedTicks, sysClockKhz) * 64.0) / 1_000_000.0));
        onStatus?.Invoke($"Warm-up settling: wait {settleMs} ms (64 lines at {FormatExposureTime(clampedTicks, sysClockKhz)})...");
        await Task.Delay(settleMs, ct);
    }

    private sealed record ChannelOffsetState(
        int? PreviousOffset = null,
        double? PreviousMean = null,
        double? PreviousError = null,
        int? OlderOffset = null,
        double? OlderError = null,
        int LastStep = 0)
    {
        public ChannelOffsetState Remember(int currentOffset, double currentMean, int appliedStep)
        {
            var error = TargetBlackLevel - currentMean;
            return this with
            {
                OlderOffset = PreviousOffset,
                OlderError = PreviousError,
                PreviousOffset = currentOffset,
                PreviousMean = currentMean,
                PreviousError = error,
                LastStep = appliedStep
            };
        }
    }

    private sealed record ChannelGainState(
        int? PreviousValue = null,
        double? PreviousMean = null,
        double? PreviousError = null,
        int? OlderValue = null,
        double? OlderError = null,
        int LastStep = 0)
    {
        public ChannelGainState Remember(int currentValue, double currentMean, int appliedStep)
        {
            var error = WhiteTargetLevel - currentMean;
            return this with
            {
                OlderValue = PreviousValue,
                OlderError = PreviousError,
                PreviousValue = currentValue,
                PreviousMean = currentMean,
                PreviousError = error,
                LastStep = appliedStep
            };
        }
    }

    private sealed record CalibrationAttempt(ScanParameterSnapshot Snapshot, double Score);
}
