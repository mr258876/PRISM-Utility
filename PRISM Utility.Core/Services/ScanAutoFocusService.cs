using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanAutoFocusService : IScanAutoFocusService
{
    private const int IgnoredLeadingProbeRows = 64;
    private const int MotionPollDelayMs = 75;
    private const int MotionTimeoutPaddingMs = 10000;
    private const double MotionTimeoutMultiplier = 2.0;
    private const double TiltBalanceTolerance = 0.035;
    private const double TiltImprovementEpsilon = 0.0025;
    private const uint FineZProbeDivisor = 4;

    private readonly IScanImageDecoder _decoder;

    public ScanAutoFocusService(IScanImageDecoder decoder)
    {
        _decoder = decoder;
    }

    public async Task<ScanAutofocusResult> AutoFocusAsync(IScanSessionService session, ScanAutofocusRequest request, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var focusRoi = ValidateRequest(session, request);
        var mapping = request.FocusMotorMapping;
        var motorIoStarted = false;
        Exception? primaryFailure = null;

        try
        {
            onStatus?.Invoke($"Autofocus: using {request.SampleRows} fresh rows per probe.");
            ct.ThrowIfCancellationRequested();
            motorIoStarted = true;
            await session.SetMotorEnabledAsync(mapping.LeftMotorId, true, ct);
            await session.SetMotorEnabledAsync(mapping.RightMotorId, true, ct);
            await WaitForFocusMotorsIdleAsync(session, mapping, 0, request.MotorIntervalNs, ct);

            var current = await CaptureFocusProbeAsync(session, request.SampleRows, 0, 0, focusRoi, "Autofocus baseline", onStatus, onFrameCaptured, ct);
            current = await OptimizeZAsync(session, request, focusRoi, current, onStatus, onFrameCaptured, ct);
            current = await BalanceTiltAsync(session, request, focusRoi, current, onStatus, onFrameCaptured, ct);
            current = await OptimizeZAsync(session, request, focusRoi, current, onStatus, onFrameCaptured, ct);

            onStatus?.Invoke($"Autofocus: complete. tilt={current.TiltOffsetSteps:+#;-#;0} steps, z={current.ZOffsetSteps:+#;-#;0} steps, sharpness={current.Metrics.OverallSharpness:0.0000}, imbalance={current.Metrics.TiltImbalance:+0.0000;-0.0000;0.0000}.");
            return new ScanAutofocusResult(
                request.SampleRows,
                current.TiltOffsetSteps,
                current.ZOffsetSteps,
                current.Metrics.OverallSharpness,
                current.Metrics.LeftSharpness,
                current.Metrics.RightSharpness,
                current.Metrics.TiltImbalance);
        }
        catch (Exception ex)
        {
            primaryFailure = ex;
            throw;
        }
        finally
        {
            if (motorIoStarted)
            {
                var cleanupFailures = await TryStopFocusMotorsAsync(session, mapping);
                if (cleanupFailures.Count > 0)
                {
                    var failures = primaryFailure is null
                        ? cleanupFailures
                        : new[] { primaryFailure }.Concat(cleanupFailures);
                    throw new AggregateException("Autofocus motor cleanup failed.", failures);
                }
            }
        }
    }

    private async Task<FocusProbe> BalanceTiltAsync(IScanSessionService session, ScanAutofocusRequest request, ScanFocusRoi focusRoi, FocusProbe baseline, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var current = baseline;
        LogProbe(onStatus, "Autofocus tilt baseline", current.Metrics);
        if (Math.Abs(current.Metrics.TiltImbalance) <= TiltBalanceTolerance)
        {
            onStatus?.Invoke("Autofocus tilt: baseline already balanced.");
            return current;
        }

        var positive = await ProbeTiltFromCurrentAsync(session, request, focusRoi, current, true, "Autofocus tilt +probe", onStatus, onFrameCaptured, ct);
        var negative = await ProbeTiltFromCurrentAsync(session, request, focusRoi, current, false, "Autofocus tilt -probe", onStatus, onFrameCaptured, ct);
        var best = SelectBestTiltProbe(current, positive, negative);
        if (ReferenceEquals(best, current))
        {
            onStatus?.Invoke("Autofocus tilt: no single-step improvement found; keeping current tilt.");
            return current;
        }

        var stepPositive = best.TiltOffsetSteps > current.TiltOffsetSteps;
        await MoveTiltAsync(session, request, stepPositive, request.TiltProbeSteps, ct);
        current = best;
        LogProbe(onStatus, "Autofocus tilt step", current.Metrics);

        for (var iteration = 2; iteration <= request.MaxTiltIterations; iteration++)
        {
            if (Math.Abs(current.Metrics.TiltImbalance) <= TiltBalanceTolerance)
            {
                onStatus?.Invoke($"Autofocus tilt: balanced after {iteration - 1} step(s).");
                return current;
            }

            var next = await ProbeTiltForwardAsync(session, request, focusRoi, current, stepPositive, $"Autofocus tilt iteration {iteration}", onStatus, onFrameCaptured, ct);
            if (!IsTiltMeaningfullyBetter(next.Metrics.TiltImbalance, current.Metrics.TiltImbalance))
            {
                await MoveTiltAsync(session, request, !stepPositive, request.TiltProbeSteps, ct);
                onStatus?.Invoke("Autofocus tilt: further movement stopped improving left/right balance.");
                return current;
            }

            current = next;
            LogProbe(onStatus, $"Autofocus tilt iteration {iteration}", current.Metrics);
        }

        return current;
    }

    private async Task<FocusProbe> OptimizeZAsync(IScanSessionService session, ScanAutofocusRequest request, ScanFocusRoi focusRoi, FocusProbe baseline, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var current = baseline;
        current = await OptimizeZAtStepAsync(session, request, focusRoi, current, request.ZProbeSteps, "coarse", onStatus, onFrameCaptured, ct);

        var fineZProbeSteps = Math.Max(1u, request.ZProbeSteps / FineZProbeDivisor);
        if (fineZProbeSteps < request.ZProbeSteps)
        {
            current = await OptimizeZAtStepAsync(session, request, focusRoi, current, fineZProbeSteps, "fine", onStatus, onFrameCaptured, ct);
        }

        return current;
    }

    private async Task<FocusProbe> OptimizeZAtStepAsync(IScanSessionService session, ScanAutofocusRequest request, ScanFocusRoi focusRoi, FocusProbe baseline, uint zProbeSteps, string phase, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        LogProbe(onStatus, $"Autofocus z {phase} baseline", baseline.Metrics);

        var step = CheckedToInt(zProbeSteps, nameof(zProbeSteps));
        var radius = CheckedMultiplyToInt(zProbeSteps, request.MaxZIterations, nameof(request.MaxZIterations));
        var firstOffset = CheckedOffset(baseline.ZOffsetSteps, -radius, $"Autofocus z {phase} sweep start");
        var lastOffset = CheckedOffset(baseline.ZOffsetSteps, radius, $"Autofocus z {phase} sweep end");
        var preloadOffset = CheckedOffset(firstOffset, -step, $"Autofocus z {phase} preload");
        var sampleCount = checked((request.MaxZIterations * 2) + 1);

        onStatus?.Invoke($"Autofocus z {phase}: one-way sweep {sampleCount} point(s), offsets {firstOffset:+#;-#;0}..{lastOffset:+#;-#;0} steps.");

        var physicalOffset = baseline.ZOffsetSteps;
        await MoveZToOffsetAsync(session, request, physicalOffset, preloadOffset, ct);
        physicalOffset = preloadOffset;

        await MoveZToOffsetAsync(session, request, physicalOffset, firstOffset, ct);
        physicalOffset = firstOffset;

        var best = await CaptureFocusProbeAsync(
            session,
            request.SampleRows,
            baseline.TiltOffsetSteps,
            physicalOffset,
            focusRoi,
            $"Autofocus z {phase} sweep 1/{sampleCount}",
            onStatus,
            onFrameCaptured,
            ct);
        LogProbe(onStatus, $"Autofocus z {phase} sweep 1/{sampleCount}", best.Metrics);

        for (var sampleIndex = 1; sampleIndex < sampleCount; sampleIndex++)
        {
            var nextOffset = CheckedOffset(firstOffset, (long)step * sampleIndex, $"Autofocus z {phase} sweep offset");
            await MoveZToOffsetAsync(session, request, physicalOffset, nextOffset, ct);
            physicalOffset = nextOffset;

            var probe = await CaptureFocusProbeAsync(
                session,
                request.SampleRows,
                baseline.TiltOffsetSteps,
                physicalOffset,
                focusRoi,
                $"Autofocus z {phase} sweep {sampleIndex + 1}/{sampleCount}",
                onStatus,
                onFrameCaptured,
                ct);
            LogProbe(onStatus, $"Autofocus z {phase} sweep {sampleIndex + 1}/{sampleCount}", probe.Metrics);

            if (probe.Metrics.OverallSharpness > best.Metrics.OverallSharpness)
                best = probe;
        }

        if (best.ZOffsetSteps == firstOffset || best.ZOffsetSteps == lastOffset)
            onStatus?.Invoke($"Autofocus z {phase}: best sharpness was at the sweep edge; the real focus may be outside this Z window.");

        var bestPreloadOffset = CheckedOffset(best.ZOffsetSteps, -step, $"Autofocus z {phase} final preload");
        await MoveZToOffsetAsync(session, request, physicalOffset, bestPreloadOffset, ct);
        physicalOffset = bestPreloadOffset;

        await MoveZToOffsetAsync(session, request, physicalOffset, best.ZOffsetSteps, ct);
        LogProbe(onStatus, $"Autofocus z {phase} selected", best.Metrics);

        return best;
    }

    private async Task<FocusProbe> ProbeTiltFromCurrentAsync(IScanSessionService session, ScanAutofocusRequest request, ScanFocusRoi focusRoi, FocusProbe current, bool positive, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        await MoveTiltAsync(session, request, positive, request.TiltProbeSteps, ct);
        var probe = await CaptureFocusProbeAsync(
            session,
            request.SampleRows,
            current.TiltOffsetSteps + (positive ? (int)request.TiltProbeSteps : -(int)request.TiltProbeSteps),
            current.ZOffsetSteps,
            focusRoi,
            label,
            onStatus,
            onFrameCaptured,
            ct);
        await MoveTiltAsync(session, request, !positive, request.TiltProbeSteps, ct);
        return probe;
    }

    private async Task<FocusProbe> ProbeTiltForwardAsync(IScanSessionService session, ScanAutofocusRequest request, ScanFocusRoi focusRoi, FocusProbe current, bool positive, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        await MoveTiltAsync(session, request, positive, request.TiltProbeSteps, ct);
        return await CaptureFocusProbeAsync(
            session,
            request.SampleRows,
            current.TiltOffsetSteps + (positive ? (int)request.TiltProbeSteps : -(int)request.TiltProbeSteps),
            current.ZOffsetSteps,
            focusRoi,
            label,
            onStatus,
            onFrameCaptured,
            ct);
    }

    private async Task MoveTiltAsync(IScanSessionService session, ScanAutofocusRequest request, bool positive, uint steps, CancellationToken ct)
    {
        if (steps == 0)
            return;

        var mapping = request.FocusMotorMapping;
        var leftDirection = positive ? mapping.TiltPositiveDirection : !mapping.TiltPositiveDirection;
        var rightDirection = !leftDirection;

        await session.MoveMotorStepsAsync(mapping.LeftMotorId, leftDirection, steps, request.MotorIntervalNs, ct);
        await session.MoveMotorStepsAsync(mapping.RightMotorId, rightDirection, steps, request.MotorIntervalNs, ct);
        await WaitForFocusMotorMotionCompleteEventsAsync(session, mapping, steps, request.MotorIntervalNs, ct);
    }

    private async Task MoveZAsync(IScanSessionService session, ScanAutofocusRequest request, bool positive, uint steps, CancellationToken ct)
    {
        if (steps == 0)
            return;

        var mapping = request.FocusMotorMapping;
        var direction = positive ? mapping.ZPositiveDirection : !mapping.ZPositiveDirection;
        await session.MoveMotorStepsAsync(mapping.LeftMotorId, direction, steps, request.MotorIntervalNs, ct);
        await session.MoveMotorStepsAsync(mapping.RightMotorId, direction, steps, request.MotorIntervalNs, ct);
        await WaitForFocusMotorMotionCompleteEventsAsync(session, mapping, steps, request.MotorIntervalNs, ct);
    }

    private async Task MoveZToOffsetAsync(IScanSessionService session, ScanAutofocusRequest request, int currentOffsetSteps, int targetOffsetSteps, CancellationToken ct)
    {
        var delta = (long)targetOffsetSteps - currentOffsetSteps;
        if (delta == 0)
            return;

        var steps = Math.Abs(delta);
        if (steps > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(targetOffsetSteps), "Autofocus Z move is too large.");

        await MoveZAsync(session, request, delta > 0, (uint)steps, ct);
    }

    private static async Task WaitForFocusMotorMotionCompleteEventsAsync(IScanSessionService session, ScanFocusMotorMapping mapping, uint steps, uint intervalNs, CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                session.WaitForMotorMotionCompleteAsync(mapping.LeftMotorId, steps, intervalNs, ct),
                session.WaitForMotorMotionCompleteAsync(mapping.RightMotorId, steps, intervalNs, ct));
        }
        catch (IOException)
        {
            await WaitForFocusMotorsIdleAsync(session, mapping, steps, intervalNs, ct);
        }
    }

    private static async Task WaitForFocusMotorsIdleAsync(IScanSessionService session, ScanFocusMotorMapping mapping, uint steps, uint intervalNs, CancellationToken ct)
    {
        var expectedTravelMs = Math.Ceiling((double)steps * intervalNs / 1000000.0);
        var timeoutMs = Math.Max(ScanDebugConstants.AckTimeoutMs, (expectedTravelMs * MotionTimeoutMultiplier) + MotionTimeoutPaddingMs);
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow <= timeoutAt)
        {
            ct.ThrowIfCancellationRequested();
            var states = await session.GetMotionStateAsync(ct);
            var focusStates = states.Where(state => state.MotorId == mapping.LeftMotorId || state.MotorId == mapping.RightMotorId).ToArray();
            if (focusStates.Length >= 2 && focusStates.All(state => !state.Running && state.RemainingSteps == 0))
                return;

            await Task.Delay(MotionPollDelayMs, ct);
        }

        throw new IOException("Autofocus motion did not settle before timeout.");
    }

    private async Task<FocusProbe> CaptureFocusProbeAsync(IScanSessionService session, int rows, int tiltOffsetSteps, int zOffsetSteps, ScanFocusRoi focusRoi, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        onStatus?.Invoke($"{label}: capturing {rows} rows...");
        var result = await session.StartScanAsync(rows, ct);
        if (!result.Success || result.ImageBytes is null)
            throw new IOException($"{label} failed: {result.Message}");

        onFrameCaptured?.Invoke(result.ImageBytes, rows, label);
        var metrics = BuildMetrics(result.ImageBytes, rows, focusRoi);
        return new FocusProbe(tiltOffsetSteps, zOffsetSteps, metrics);
    }

    private FocusMetrics BuildMetrics(byte[] lineBuffer, int rows, ScanFocusRoi focusRoi)
    {
        if (rows < 3)
            throw new IOException("Autofocus requires at least 3 rows.");

        var analysisStartRow = Math.Min(IgnoredLeadingProbeRows, Math.Max(rows - 3, 0));
        var leftRange = focusRoi.LeftRange;
        var rightRange = focusRoi.RightRange;
        var overallRange = focusRoi.OverallRange;

        var left = ComputeNormalizedSharpness(lineBuffer, rows, analysisStartRow, leftRange);
        var right = ComputeNormalizedSharpness(lineBuffer, rows, analysisStartRow, rightRange);
        var overall = ComputeNormalizedSharpness(lineBuffer, rows, analysisStartRow, overallRange);
        var imbalance = (left - right) / Math.Max(left + right, 1e-9);

        return new FocusMetrics(left, right, overall, imbalance);
    }

    private static async Task<IReadOnlyList<Exception>> TryStopFocusMotorsAsync(IScanSessionService session, ScanFocusMotorMapping mapping)
    {
        var failures = new List<Exception>();
        try { await session.StopMotorAsync(mapping.LeftMotorId, CancellationToken.None); } catch (Exception ex) { failures.Add(ex); }
        try { await session.StopMotorAsync(mapping.RightMotorId, CancellationToken.None); } catch (Exception ex) { failures.Add(ex); }
        return failures;
    }

    private double ComputeNormalizedSharpness(byte[] lineBuffer, int rows, int startRow, ScanColumnRange range)
    {
        var averagedLine = BuildAveragedLineProfile(lineBuffer, rows, startRow, range);
        if (averagedLine.Length < 3)
            return 0;

        var pixelCount = 0L;
        var sum = 0.0;
        var sumSquares = 0.0;

        for (var index = 0; index < averagedLine.Length; index++)
        {
            var value = averagedLine[index];
            sum += value;
            sumSquares += value * value;
            pixelCount++;
        }

        if (pixelCount <= 0)
            return 0;

        var mean = sum / pixelCount;
        var variance = Math.Max((sumSquares / pixelCount) - (mean * mean), 1.0);

        //var secondDerivativeEnergy = 0.0;
        //var derivativeCount = 0L;
        //for (var index = 2; index < averagedLine.Length - 2; index++)
        //{
        //    var secondDerivative = (2.0 * averagedLine[index]) - averagedLine[index - 2] - averagedLine[index + 2];
        //    secondDerivativeEnergy += secondDerivative * secondDerivative;
        //    derivativeCount++;
        //}

        //if (derivativeCount <= 0)
        //    return 0;

        //return (secondDerivativeEnergy / derivativeCount) / variance;
        var brennerEnergy = 0.0;
        var count = 0;

        for (var i = 0; i < averagedLine.Length - 2; i++)
        {
            var d = averagedLine[i + 2] - averagedLine[i];
            brennerEnergy += d * d;
            count++;
        }

        if (count <= 0)
            return 0;

        //return (brennerEnergy / count) / variance; // ��һ���� Brenner
        return brennerEnergy; // Brenner
    }

    private double[] BuildAveragedLineProfile(byte[] lineBuffer, int rows, int startRow, ScanColumnRange range)
    {
        var width = range.Width;
        if (width <= 0 || startRow >= rows)
            return Array.Empty<double>();

        var sums = new double[width];
        var counts = new int[width];

        for (var y = startRow; y < rows; y++)
        {
            for (var x = range.Start; x <= range.EndInclusive; x++)
            {
                if (!_decoder.TryGetSample16(lineBuffer, rows, x, y, out var sample))
                    continue;

                var index = x - range.Start;
                sums[index] += sample;
                counts[index]++;
            }
        }

        var averaged = new double[width];
        for (var index = 0; index < width; index++)
            averaged[index] = counts[index] > 0 ? sums[index] / counts[index] : 0.0;

        return averaged;
    }

    private static FocusProbe SelectBestTiltProbe(FocusProbe current, FocusProbe positive, FocusProbe negative)
    {
        var candidates = new[] { current, positive, negative };
        return candidates.OrderBy(candidate => Math.Abs(candidate.Metrics.TiltImbalance)).First();
    }

    private static bool IsTiltMeaningfullyBetter(double candidateImbalance, double currentImbalance)
        => Math.Abs(candidateImbalance) + TiltImprovementEpsilon < Math.Abs(currentImbalance);

    private static void LogProbe(Action<string>? onStatus, string label, FocusMetrics metrics)
        => onStatus?.Invoke($"{label}: overall={metrics.OverallSharpness:0.0000}, left={metrics.LeftSharpness:0.0000}, right={metrics.RightSharpness:0.0000}, imbalance={metrics.TiltImbalance:+0.0000;-0.0000;0.0000}");

    private static int CheckedToInt(uint steps, string parameterName)
    {
        if (steps > int.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "Autofocus step count is too large.");

        return (int)steps;
    }

    private static int CheckedMultiplyToInt(uint steps, int multiplier, string parameterName)
    {
        var result = (ulong)steps * (ulong)multiplier;
        if (result > int.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "Autofocus sweep window is too large.");

        return (int)result;
    }

    private static int CheckedOffset(int origin, long delta, string label)
    {
        var result = origin + delta;
        if (result < int.MinValue || result > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(delta), $"{label} exceeds the autofocus offset range.");

        return (int)result;
    }

    private ScanFocusRoi ValidateRequest(IScanSessionService session, ScanAutofocusRequest request)
    {
        if (request.SampleRows <= 0 || request.SampleRows > session.SingleTransferMaxRows)
            throw new ArgumentOutOfRangeException(nameof(request), $"Autofocus sample rows must be in [1, {session.SingleTransferMaxRows}].");
        if (request.TiltProbeSteps == 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus tilt probe steps must be greater than zero.");
        if (request.ZProbeSteps == 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus Z probe steps must be greater than zero.");
        if (request.MotorIntervalNs < ScanDebugConstants.MotionMinIntervalNs)
            throw new ArgumentOutOfRangeException(nameof(request), $"Autofocus motor interval must be at least {ScanDebugConstants.MotionMinIntervalNs} ns.");
        if (request.MaxTiltIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus tilt iterations must be greater than zero.");
        if (request.MaxZIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus Z iterations must be greater than zero.");
        if (request.RoiSettings is null)
            throw new ArgumentNullException(nameof(request.RoiSettings));

        var focusRoi = ScanFocusRoi.Require(request.RoiSettings, _decoder.GetDecodedPixelsPerLine());

        var mappingValidation = request.FocusMotorMapping.Validate();
        if (!mappingValidation.IsValid)
            throw new ArgumentException(mappingValidation.Message, nameof(request));

        try
        {
            _ = CheckedToInt(request.TiltProbeSteps, nameof(request.TiltProbeSteps));
            _ = CheckedToInt(request.ZProbeSteps, nameof(request.ZProbeSteps));
            _ = CheckedMultiplyToInt(request.TiltProbeSteps, request.MaxTiltIterations, nameof(request.MaxTiltIterations));
            var bounds = ScanAutofocusPresetResolver.ComputeBounds(
                request.TiltProbeSteps,
                request.ZProbeSteps,
                request.MotorIntervalNs,
                request.MaxTiltIterations,
                request.MaxZIterations,
                1.0);
            if (bounds.MaxZSteps > int.MaxValue || bounds.MaxTiltSteps > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(request), "Autofocus final offsets exceed the supported range.");
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus bounds exceed the supported range.");
        }
        return focusRoi;
    }

    private sealed record FocusMetrics(double LeftSharpness, double RightSharpness, double OverallSharpness, double TiltImbalance);

    private sealed record FocusProbe(int TiltOffsetSteps, int ZOffsetSteps, FocusMetrics Metrics);
}
