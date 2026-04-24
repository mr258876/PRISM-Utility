using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanAutoFocusService : IScanAutoFocusService
{
    private const byte FocusMotor1Id = 0;
    private const byte FocusMotor3Id = 2;
    private const int IgnoredLeadingProbeRows = 64;
    private const int MotionPollDelayMs = 75;
    private const int MotionTimeoutPaddingMs = 10000;
    private const double MotionTimeoutMultiplier = 2.0;
    private const double TiltBalanceTolerance = 0.035;
    private const double TiltImprovementEpsilon = 0.0025;
    private const double SharpnessImprovementRatio = 0.005;
    private const uint FineZProbeDivisor = 4;

    private readonly IScanImageDecoder _decoder;

    public ScanAutoFocusService(IScanImageDecoder decoder)
    {
        _decoder = decoder;
    }

    public async Task<ScanAutofocusResult> AutoFocusAsync(IScanSessionService session, ScanAutofocusRequest request, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        ValidateRequest(session, request);

        try
        {
            onStatus?.Invoke($"Autofocus: using {request.SampleRows} fresh rows per probe.");
            await session.SetMotorEnabledAsync(FocusMotor1Id, true, ct);
            await session.SetMotorEnabledAsync(FocusMotor3Id, true, ct);
            await WaitForFocusMotorsIdleAsync(session, 0, request.MotorIntervalUs, ct);

            var current = await CaptureFocusProbeAsync(session, request.SampleRows, 0, 0, "Autofocus baseline", onStatus, onFrameCaptured, ct);
            current = await OptimizeZAsync(session, request, current, onStatus, onFrameCaptured, ct);
            current = await BalanceTiltAsync(session, request, current, onStatus, onFrameCaptured, ct);
            current = await OptimizeZAsync(session, request, current, onStatus, onFrameCaptured, ct);

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
        catch
        {
            await TryStopFocusMotorsAsync(session);
            throw;
        }
    }

    private async Task<FocusProbe> BalanceTiltAsync(IScanSessionService session, ScanAutofocusRequest request, FocusProbe baseline, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var current = baseline;
        LogProbe(onStatus, "Autofocus tilt baseline", current.Metrics);
        if (Math.Abs(current.Metrics.TiltImbalance) <= TiltBalanceTolerance)
        {
            onStatus?.Invoke("Autofocus tilt: baseline already balanced.");
            return current;
        }

        var positive = await ProbeTiltFromCurrentAsync(session, request, current, true, "Autofocus tilt +probe", onStatus, onFrameCaptured, ct);
        var negative = await ProbeTiltFromCurrentAsync(session, request, current, false, "Autofocus tilt -probe", onStatus, onFrameCaptured, ct);
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

            var next = await ProbeTiltForwardAsync(session, request, current, stepPositive, $"Autofocus tilt iteration {iteration}", onStatus, onFrameCaptured, ct);
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

    private async Task<FocusProbe> OptimizeZAsync(IScanSessionService session, ScanAutofocusRequest request, FocusProbe baseline, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var current = baseline;
        current = await OptimizeZAtStepAsync(session, request, current, request.ZProbeSteps, "coarse", onStatus, onFrameCaptured, ct);

        var fineZProbeSteps = Math.Max(1u, request.ZProbeSteps / FineZProbeDivisor);
        if (fineZProbeSteps < request.ZProbeSteps)
        {
            current = await OptimizeZAtStepAsync(session, request, current, fineZProbeSteps, "fine", onStatus, onFrameCaptured, ct);
        }

        return current;
    }

    private async Task<FocusProbe> OptimizeZAtStepAsync(IScanSessionService session, ScanAutofocusRequest request, FocusProbe baseline, uint zProbeSteps, string phase, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        var current = baseline;
        LogProbe(onStatus, $"Autofocus z {phase} baseline", current.Metrics);

        var positive = await ProbeZFromCurrentAsync(session, request, current, true, zProbeSteps, $"Autofocus z {phase} +probe", onStatus, onFrameCaptured, ct);
        var negative = await ProbeZFromCurrentAsync(session, request, current, false, zProbeSteps, $"Autofocus z {phase} -probe", onStatus, onFrameCaptured, ct);
        var best = SelectBestSharpnessProbe(current, positive, negative);
        if (ReferenceEquals(best, current))
        {
            onStatus?.Invoke($"Autofocus z {phase}: no single-step sharpness improvement found; keeping current Z.");
            return current;
        }

        var stepPositive = best.ZOffsetSteps > current.ZOffsetSteps;
        await MoveZAsync(session, request, stepPositive, zProbeSteps, ct);
        current = best;
        LogProbe(onStatus, $"Autofocus z {phase} step", current.Metrics);

        for (var iteration = 2; iteration <= request.MaxZIterations; iteration++)
        {
            var next = await ProbeZForwardAsync(session, request, current, stepPositive, zProbeSteps, $"Autofocus z {phase} iteration {iteration}", onStatus, onFrameCaptured, ct);
            if (!IsSharpnessMeaningfullyBetter(next.Metrics.OverallSharpness, current.Metrics.OverallSharpness))
            {
                await MoveZAsync(session, request, !stepPositive, zProbeSteps, ct);
                onStatus?.Invoke($"Autofocus z {phase}: further movement stopped improving overall sharpness.");
                return current;
            }

            current = next;
            LogProbe(onStatus, $"Autofocus z {phase} iteration {iteration}", current.Metrics);
        }

        return current;
    }

    private async Task<FocusProbe> ProbeTiltFromCurrentAsync(IScanSessionService session, ScanAutofocusRequest request, FocusProbe current, bool positive, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        await MoveTiltAsync(session, request, positive, request.TiltProbeSteps, ct);
        var probe = await CaptureFocusProbeAsync(
            session,
            request.SampleRows,
            current.TiltOffsetSteps + (positive ? (int)request.TiltProbeSteps : -(int)request.TiltProbeSteps),
            current.ZOffsetSteps,
            label,
            onStatus,
            onFrameCaptured,
            ct);
        await MoveTiltAsync(session, request, !positive, request.TiltProbeSteps, ct);
        return probe;
    }

    private async Task<FocusProbe> ProbeTiltForwardAsync(IScanSessionService session, ScanAutofocusRequest request, FocusProbe current, bool positive, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        await MoveTiltAsync(session, request, positive, request.TiltProbeSteps, ct);
        return await CaptureFocusProbeAsync(
            session,
            request.SampleRows,
            current.TiltOffsetSteps + (positive ? (int)request.TiltProbeSteps : -(int)request.TiltProbeSteps),
            current.ZOffsetSteps,
            label,
            onStatus,
            onFrameCaptured,
            ct);
    }

    private async Task<FocusProbe> ProbeZFromCurrentAsync(IScanSessionService session, ScanAutofocusRequest request, FocusProbe current, bool positive, uint zProbeSteps, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        await MoveZAsync(session, request, positive, zProbeSteps, ct);
        var probe = await CaptureFocusProbeAsync(
            session,
            request.SampleRows,
            current.TiltOffsetSteps,
            current.ZOffsetSteps + (positive ? (int)zProbeSteps : -(int)zProbeSteps),
            label,
            onStatus,
            onFrameCaptured,
            ct);
        await MoveZAsync(session, request, !positive, zProbeSteps, ct);
        return probe;
    }

    private async Task<FocusProbe> ProbeZForwardAsync(IScanSessionService session, ScanAutofocusRequest request, FocusProbe current, bool positive, uint zProbeSteps, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        await MoveZAsync(session, request, positive, zProbeSteps, ct);
        return await CaptureFocusProbeAsync(
            session,
            request.SampleRows,
            current.TiltOffsetSteps,
            current.ZOffsetSteps + (positive ? (int)zProbeSteps : -(int)zProbeSteps),
            label,
            onStatus,
            onFrameCaptured,
            ct);
    }

    private async Task MoveTiltAsync(IScanSessionService session, ScanAutofocusRequest request, bool positive, uint steps, CancellationToken ct)
    {
        if (steps == 0)
            return;

        var motor1Direction = positive ? request.TiltPositiveDirection : !request.TiltPositiveDirection;
        var motor3Direction = !motor1Direction;

        await session.MoveMotorStepsAsync(FocusMotor1Id, motor1Direction, steps, request.MotorIntervalUs, ct);
        await session.MoveMotorStepsAsync(FocusMotor3Id, motor3Direction, steps, request.MotorIntervalUs, ct);
        await WaitForFocusMotorMotionCompleteEventsAsync(session, steps, request.MotorIntervalUs, ct);
    }

    private async Task MoveZAsync(IScanSessionService session, ScanAutofocusRequest request, bool positive, uint steps, CancellationToken ct)
    {
        if (steps == 0)
            return;

        var direction = positive ? request.ZPositiveDirection : !request.ZPositiveDirection;
        await session.MoveMotorStepsAsync(FocusMotor1Id, direction, steps, request.MotorIntervalUs, ct);
        await session.MoveMotorStepsAsync(FocusMotor3Id, direction, steps, request.MotorIntervalUs, ct);
        await WaitForFocusMotorMotionCompleteEventsAsync(session, steps, request.MotorIntervalUs, ct);
    }

    private static async Task WaitForFocusMotorMotionCompleteEventsAsync(IScanSessionService session, uint steps, uint intervalUs, CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                session.WaitForMotorMotionCompleteAsync(FocusMotor1Id, steps, intervalUs, ct),
                session.WaitForMotorMotionCompleteAsync(FocusMotor3Id, steps, intervalUs, ct));
        }
        catch (IOException)
        {
            await WaitForFocusMotorsIdleAsync(session, steps, intervalUs, ct);
        }
    }

    private static async Task WaitForFocusMotorsIdleAsync(IScanSessionService session, uint steps, uint intervalUs, CancellationToken ct)
    {
        var expectedTravelMs = Math.Ceiling((double)steps * intervalUs / 1000.0);
        var timeoutMs = Math.Max(ScanDebugConstants.AckTimeoutMs, (expectedTravelMs * MotionTimeoutMultiplier) + MotionTimeoutPaddingMs);
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow <= timeoutAt)
        {
            ct.ThrowIfCancellationRequested();
            var states = await session.GetMotionStateAsync(ct);
            var focusStates = states.Where(state => state.MotorId == FocusMotor1Id || state.MotorId == FocusMotor3Id).ToArray();
            if (focusStates.Length >= 2 && focusStates.All(state => !state.Running && state.RemainingSteps == 0))
                return;

            await Task.Delay(MotionPollDelayMs, ct);
        }

        throw new IOException("Autofocus motion did not settle before timeout.");
    }

    private async Task<FocusProbe> CaptureFocusProbeAsync(IScanSessionService session, int rows, int tiltOffsetSteps, int zOffsetSteps, string label, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
    {
        onStatus?.Invoke($"{label}: capturing {rows} rows...");
        var result = await session.StartScanAsync(rows, ct);
        if (!result.Success || result.ImageBytes is null)
            throw new IOException($"{label} failed: {result.Message}");

        onFrameCaptured?.Invoke(result.ImageBytes, rows, label);
        var metrics = BuildMetrics(result.ImageBytes, rows);
        return new FocusProbe(tiltOffsetSteps, zOffsetSteps, metrics);
    }

    private FocusMetrics BuildMetrics(byte[] lineBuffer, int rows)
    {
        var width = _decoder.GetDecodedPixelsPerLine();
        if (rows < 3 || width < 16)
            throw new IOException("Autofocus requires at least 3 rows and a valid decoded scan width.");

        var analysisStartRow = Math.Min(IgnoredLeadingProbeRows, Math.Max(rows - 3, 0));
        var effectiveRange = _decoder.GetEffectivePixelRange();
        if (effectiveRange.EndInclusive - effectiveRange.Start < 15)
            throw new IOException("Autofocus requires a valid effective-pixel region after excluding line-buffer and dummy-pixel margins.");

        var leftRange = BuildRegionRange(effectiveRange.Start, effectiveRange.EndInclusive, 0.14, 0.42);
        var rightRange = BuildRegionRange(effectiveRange.Start, effectiveRange.EndInclusive, 0.58, 0.86);
        var overallRange = BuildRegionRange(effectiveRange.Start, effectiveRange.EndInclusive, 0.14, 0.86);

        var left = ComputeNormalizedSharpness(lineBuffer, rows, analysisStartRow, leftRange.Start, leftRange.EndInclusive);
        var right = ComputeNormalizedSharpness(lineBuffer, rows, analysisStartRow, rightRange.Start, rightRange.EndInclusive);
        var overall = ComputeNormalizedSharpness(lineBuffer, rows, analysisStartRow, overallRange.Start, overallRange.EndInclusive);
        var imbalance = (left - right) / Math.Max(left + right, 1e-9);

        return new FocusMetrics(left, right, overall, imbalance);
    }

    private static async Task TryStopFocusMotorsAsync(IScanSessionService session)
    {
        try { await session.StopMotorAsync(FocusMotor1Id, CancellationToken.None); } catch { }
        try { await session.StopMotorAsync(FocusMotor3Id, CancellationToken.None); } catch { }
    }

    private double ComputeNormalizedSharpness(byte[] lineBuffer, int rows, int startRow, int startX, int endX)
    {
        var averagedLine = BuildAveragedLineProfile(lineBuffer, rows, startRow, startX, endX);
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

    private double[] BuildAveragedLineProfile(byte[] lineBuffer, int rows, int startRow, int startX, int endX)
    {
        var width = endX - startX + 1;
        if (width <= 0 || startRow >= rows)
            return Array.Empty<double>();

        var sums = new double[width];
        var counts = new int[width];

        for (var y = startRow; y < rows; y++)
        {
            for (var x = startX; x <= endX; x++)
            {
                if (!_decoder.TryGetSample16(lineBuffer, rows, x, y, out var sample))
                    continue;

                var index = x - startX;
                sums[index] += sample;
                counts[index]++;
            }
        }

        var averaged = new double[width];
        for (var index = 0; index < width; index++)
            averaged[index] = counts[index] > 0 ? sums[index] / counts[index] : 0.0;

        return averaged;
    }

    private static (int Start, int EndInclusive) BuildRegionRange(int startInclusive, int endInclusive, double startRatio, double endRatio)
    {
        var width = endInclusive - startInclusive + 1;
        var start = startInclusive + Math.Clamp((int)Math.Round(width * startRatio, MidpointRounding.ToZero), 0, Math.Max(width - 1, 0));
        var end = startInclusive + Math.Clamp((int)Math.Round(width * endRatio, MidpointRounding.ToZero), (start - startInclusive) + 2, width - 1);
        return (start, end);
    }

    private static FocusProbe SelectBestTiltProbe(FocusProbe current, FocusProbe positive, FocusProbe negative)
    {
        var candidates = new[] { current, positive, negative };
        return candidates.OrderBy(candidate => Math.Abs(candidate.Metrics.TiltImbalance)).First();
    }

    private static FocusProbe SelectBestSharpnessProbe(FocusProbe current, FocusProbe positive, FocusProbe negative)
    {
        var candidates = new[] { current, positive, negative };
        return candidates.OrderByDescending(candidate => candidate.Metrics.OverallSharpness).First();
    }

    private static bool IsTiltMeaningfullyBetter(double candidateImbalance, double currentImbalance)
        => Math.Abs(candidateImbalance) + TiltImprovementEpsilon < Math.Abs(currentImbalance);

    private static bool IsSharpnessMeaningfullyBetter(double candidateSharpness, double currentSharpness)
        => candidateSharpness > currentSharpness + Math.Max(Math.Abs(currentSharpness) * SharpnessImprovementRatio, 0.0001);

    private static void LogProbe(Action<string>? onStatus, string label, FocusMetrics metrics)
        => onStatus?.Invoke($"{label}: overall={metrics.OverallSharpness:0.0000}, left={metrics.LeftSharpness:0.0000}, right={metrics.RightSharpness:0.0000}, imbalance={metrics.TiltImbalance:+0.0000;-0.0000;0.0000}");

    private static void ValidateRequest(IScanSessionService session, ScanAutofocusRequest request)
    {
        if (request.SampleRows <= 0 || request.SampleRows > session.SingleTransferMaxRows)
            throw new ArgumentOutOfRangeException(nameof(request), $"Autofocus sample rows must be in [1, {session.SingleTransferMaxRows}].");
        if (request.TiltProbeSteps == 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus tilt probe steps must be greater than zero.");
        if (request.ZProbeSteps == 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus Z probe steps must be greater than zero.");
        if (request.MotorIntervalUs < ScanDebugConstants.MotionMinIntervalUs)
            throw new ArgumentOutOfRangeException(nameof(request), $"Autofocus motor interval must be at least {ScanDebugConstants.MotionMinIntervalUs} us.");
        if (request.MaxTiltIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus tilt iterations must be greater than zero.");
        if (request.MaxZIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Autofocus Z iterations must be greater than zero.");
    }

    private sealed record FocusMetrics(double LeftSharpness, double RightSharpness, double OverallSharpness, double TiltImbalance);

    private sealed record FocusProbe(int TiltOffsetSteps, int ZOffsetSteps, FocusMetrics Metrics);
}
