using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanAutoCalibrationService : IScanAutoCalibrationService
{
    private const int MaxBlackIterations = 32;
    private const int MaxWhiteIterations = 32;
    private const int MaxChannelBalanceIterations = 16;
    private const int MaxOffsetBalanceIterations = 16;
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

    public ScanAutoCalibrationService(IScanParameterService parameters, IScanImageDecoder decoder)
    {
        _parameters = parameters;
        _decoder = decoder;
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

        try
        {
            var channelMapping = await DetectChannelMappingAsync(session, working, onStatus, onFrameCaptured, ct);
            await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            onStatus?.Invoke($"Auto black: detected mapping adc1={(channelMapping.IsAdc1Even ? "even" : "odd")}, adc2={(channelMapping.IsAdc1Even ? "odd" : "even")}.");

            for (var iteration = 1; iteration <= MaxBlackIterations; iteration++)
            {
                onStatus?.Invoke($"Auto black: iteration {iteration}/{MaxBlackIterations} sampling...");
                await session.SetWarmUpEnabledAsync(true, ct);
                await WaitForWarmUpSettlingAsync(working.ExposureTicks, onStatus, ct);

                var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.CalibrationSampleRows, $"Auto black iteration {iteration}", onStatus, onFrameCaptured, ct);
                await session.SetWarmUpEnabledAsync(false, ct);

                var adc1Mean = channelMapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
                var adc2Mean = channelMapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
                var adc1ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
                var adc2ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;

                onStatus?.Invoke($"Auto black: mean={stats.EffectiveMean:0.0}, adc1={adc1Mean:0.0}, adc2={adc2Mean:0.0}, shield-delta={Math.Abs(adc1ShieldMean - adc2ShieldMean):0.0}, min={stats.MinColumnMean:0.0}, dark-ratio={stats.DarkPixelRatio:P3}");

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
                    adc2State);

                if (nextAdc1Offset == working.Adc1Offset && nextAdc2Offset == working.Adc2Offset)
                    throw new IOException("Auto black cannot improve offsets further.");

                adc1State = adc1State with { PreviousOffset = working.Adc1Offset, PreviousMean = adc1Mean };
                adc2State = adc2State with { PreviousOffset = working.Adc2Offset, PreviousMean = adc2Mean };
                working = working with { Adc1Offset = nextAdc1Offset, Adc2Offset = nextAdc2Offset };

                onStatus?.Invoke($"Auto black: applying offsets adc1={working.Adc1Offset}, adc2={working.Adc2Offset}...");
                await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            }
        }
        finally
        {
            try { await session.SetWarmUpEnabledAsync(false, CancellationToken.None); } catch { }
        }

        throw new IOException("Auto black failed to converge.");
    }

    public async Task<ScanParameterSnapshot> AutoWhiteAdjustAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var working = currentSnapshot with { Adc1Gain = 0, Adc2Gain = 0 };

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
            onStatus?.Invoke($"Auto white: saturation starts around exposure ticks {probeExposure} ({FormatExposureTime(probeExposure)}).");

            var seededGain = SelectGainFromExposureRatio(originalExposure, probeExposure, WhiteExposureSafetyFactor);
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
                await WaitForWarmUpSettlingAsync(working.ExposureTicks, onStatus, ct);

                var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.CalibrationSampleRows, $"Auto white iteration {iteration}", onStatus, onFrameCaptured, ct);
                await session.SetWarmUpEnabledAsync(false, ct);

                var adc1Mean = channelMapping.IsAdc1Even ? stats.EvenMean : stats.OddMean;
                var adc2Mean = channelMapping.IsAdc1Even ? stats.OddMean : stats.EvenMean;
                var adc1ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldEvenMean : stats.ShieldOddMean;
                var adc2ShieldMean = channelMapping.IsAdc1Even ? stats.ShieldOddMean : stats.ShieldEvenMean;

                onStatus?.Invoke($"Auto white: mean={stats.EffectiveMean:0.0}, adc1={adc1Mean:0.0}, adc2={adc2Mean:0.0}, active-delta={Math.Abs(adc1Mean - adc2Mean):0.0}, shield-delta={Math.Abs(adc1ShieldMean - adc2ShieldMean):0.0}, sat-ratio={stats.SaturationRatio:P3}");

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
                    stats.SaturationRatio);

                if (nextAdc1Gain == working.Adc1Gain && nextAdc2Gain == working.Adc2Gain)
                    throw new IOException("Auto white cannot improve gains further.");

                working = working with { Adc1Gain = nextAdc1Gain, Adc2Gain = nextAdc2Gain };
                onStatus?.Invoke($"Auto white: applying gains adc1={working.Adc1Gain}, adc2={working.Adc2Gain}...");
                await ApplyParametersForCalibrationAsync(session, working, onSnapshotApplied, ct);
            }
        }
        finally
        {
            try { await session.SetWarmUpEnabledAsync(false, CancellationToken.None); } catch { }
        }

        throw new IOException("Auto white failed to converge.");
    }

    private async Task<ushort> ProbeSaturationExposureAsync(IScanSessionService session, ScanParameterSnapshot snapshot, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var exposure = (ushort)Math.Max(snapshot.ExposureTicks, ScanDebugConstants.MinExposureTicks);
        var lastExposure = exposure;
        var exposureNs = ExposureTicksToNanoseconds(exposure);

        for (var step = 1; step <= 6; step++)
        {
            var probeSnapshot = snapshot with { ExposureTicks = exposure, Adc1Gain = 0, Adc2Gain = 0 };
            await ApplyParametersForCalibrationAsync(session, probeSnapshot, null, ct);
            await session.SetWarmUpEnabledAsync(true, ct);
            await WaitForWarmUpSettlingAsync(exposure, onStatus, ct);
            var stats = await CaptureStatisticsAsync(session, ScanDebugConstants.WhiteProbeSampleRows, $"White probe step {step}", onStatus, onFrameCaptured, ct);
            await session.SetWarmUpEnabledAsync(false, ct);

            onStatus?.Invoke($"Auto white: probe exposure={exposure} ({FormatExposureTime(exposure)}), mean={stats.EffectiveMean:0.0}, sat-ratio={stats.SaturationRatio:P3}");
            lastExposure = exposure;

            if (stats.SaturationRatio >= SaturationRatioLimit)
                return exposure;

            if (exposure >= ushort.MaxValue / 2)
                break;

            exposureNs *= 2.0;
            exposure = NanosecondsToExposureTicks(exposureNs);
        }

        return lastExposure;
    }

    private async Task<ScanChannelMapping> DetectChannelMappingAsync(IScanSessionService session, ScanParameterSnapshot snapshot, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        onStatus?.Invoke("Auto calibration: detecting ADC/pixel mapping...");

        await session.SetWarmUpEnabledAsync(true, ct);
        await WaitForWarmUpSettlingAsync(snapshot.ExposureTicks, onStatus, ct);
        var baseline = await CaptureStatisticsAsync(session, session.SingleTransferMaxRows, "Channel mapping baseline", onStatus, onFrameCaptured, ct);
        await session.SetWarmUpEnabledAsync(false, ct);

        var probeOffset = Math.Min(snapshot.Adc1Offset + MappingProbeOffsetStep, 255);
        var probeSnapshot = snapshot with { Adc1Offset = probeOffset };
        await ApplyParametersForCalibrationAsync(session, probeSnapshot, null, ct);

        await session.SetWarmUpEnabledAsync(true, ct);
        await WaitForWarmUpSettlingAsync(snapshot.ExposureTicks, onStatus, ct);
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
            await WaitForWarmUpSettlingAsync(working.ExposureTicks, onStatus, ct);
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
            await WaitForWarmUpSettlingAsync(working.ExposureTicks, onStatus, ct);
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
        var result = rows > session.SingleTransferMaxRows
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
        var effectiveStart = Math.Clamp(ScanDebugConstants.EffectivePixelStart, 0, width - 1);
        var effectiveEnd = Math.Clamp(ScanDebugConstants.EffectivePixelEnd, effectiveStart, width - 1);
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

        delta = Math.Clamp(delta, -32, 32);
        return Math.Clamp(currentOffset + delta, -255, 255);
    }

    private static (int adc1Offset, int adc2Offset) ComputeNextBlackOffsets(
        ScanParameterSnapshot current,
        double adc1ActiveMean,
        double adc2ActiveMean,
        double adc1ShieldMean,
        double adc2ShieldMean,
        ChannelOffsetState adc1State,
        ChannelOffsetState adc2State)
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

        return (nextAdc1Offset, nextAdc2Offset);
    }

    private static ushort SelectNextGain(double currentMean, ushort currentGain, double saturationRatio)
    {
        if (saturationRatio >= SaturationRatioLimit)
            return (ushort)Math.Max(0, currentGain - 2);

        var currentScale = ComputeGainScale(currentGain);
        var desiredScale = currentScale * (WhiteTargetLevel / Math.Max(currentMean, 1.0));
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

        return bestGain;
    }

    private static (ushort adc1Gain, ushort adc2Gain) SelectNextWhiteGains(
        ScanParameterSnapshot current,
        double adc1ActiveMean,
        double adc2ActiveMean,
        double adc1ShieldMean,
        double adc2ShieldMean,
        double saturationRatio)
    {
        var nextAdc1Gain = SelectNextGain(adc1ActiveMean, current.Adc1Gain, saturationRatio);
        var nextAdc2Gain = SelectNextGain(adc2ActiveMean, current.Adc2Gain, saturationRatio);

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

        return (nextAdc1Gain, nextAdc2Gain);
    }

    private static ushort SelectGainFromExposureRatio(ushort currentExposureTicks, ushort saturationExposureTicks, double safetyFactor)
    {
        var currentExposureNs = ExposureTicksToNanoseconds(currentExposureTicks);
        var saturationExposureNs = ExposureTicksToNanoseconds(saturationExposureTicks);
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

    private static double ExposureTicksToNanoseconds(ushort ticks)
        => (((ticks + 1.0) * 12.0) + 45636.0) * 8.0;

    private static ushort NanosecondsToExposureTicks(double nanoseconds)
    {
        var ticks = ((nanoseconds / 8.0) - 45636.0) / 12.0 - 1.0;
        var rounded = (int)Math.Round(ticks, MidpointRounding.AwayFromZero);
        return (ushort)Math.Clamp(rounded, ScanDebugConstants.MinExposureTicks, ushort.MaxValue);
    }

    private static string FormatExposureTime(ushort ticks)
    {
        var exposureNs = ExposureTicksToNanoseconds(ticks);
        var exposureUs = exposureNs / 1000.0;
        return $"{exposureNs:0.##} ns / {exposureUs:0.###} us";
    }

    private static async Task WaitForWarmUpSettlingAsync(ushort exposureTicks, Action<string>? onStatus, CancellationToken ct)
    {
        var clampedTicks = (ushort)Math.Max(exposureTicks, ScanDebugConstants.MinExposureTicks);
        var settleMs = Math.Max(1, (int)Math.Ceiling((ExposureTicksToNanoseconds(clampedTicks) * 64.0) / 1_000_000.0));
        onStatus?.Invoke($"Warm-up settling: wait {settleMs} ms (64 lines at {FormatExposureTime(clampedTicks)})...");
        await Task.Delay(settleMs, ct);
    }

    private sealed record ChannelOffsetState(int? PreviousOffset = null, double? PreviousMean = null);
}
