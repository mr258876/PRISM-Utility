using Xunit;
using Xunit.Abstractions;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;

namespace PrismUtility.Core.Tests;

[Trait("Category", "CAL001")]
public sealed class Cal001AutoCalibrationAndFocusTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task AutoBlackAdjust_DarkFrame_ProducesZeroedCalibrationAndWarmUpCleanup()
    {
        var frames = new[]
        {
            CalibrationFrame(0, "mapping-baseline", 512, 512, 512, 512),
            CalibrationFrame(1, "mapping-probe", 544, 512, 512, 512),
            CalibrationFrame(2, "black-coarse", 512, 512, 512, 512),
            CalibrationFrame(3, "black-gain-match", 512, 512, 512, 512)
        };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var parameters = new RecordingParameterService(commands);
        var statuses = new List<string>();
        var applied = new List<ScanParameterSnapshot>();
        var service = CreateCalibrationService(parameters, new ScriptedImageDecoder(16, frames));
        var initial = new ScanParameterSnapshot(0, 12, 3, -7, 4, ScanDebugConstants.MinSysClockKhz);

        var result = await service.AutoBlackAdjustAsync(
            session,
            initial,
            CalibrationRoi(),
            ConfirmAsync,
            statuses.Add,
            applied.Add,
            null,
            CancellationToken.None);

        var expected = new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);
        Assert.Equal(expected, result);
        Assert.Equal([expected, expected, expected], applied);
        Assert.Equal(["mapping-baseline", "mapping-probe", "black-coarse", "black-gain-match"], session.CapturedFrameNames);
        Assert.Contains("Auto calibration: mapping probe even-delta=32.0, odd-delta=0.0.", statuses);
        Assert.Contains("Auto black: passed with offsets adc1=0, adc2=0, gains adc1=0, adc2=0.", statuses);
        AssertOrdered(commands, "WarmUp:True:False", "StartScan:mapping-baseline:512", "StartScan:mapping-probe:512", "StartScan:black-coarse:512", "StartScan:black-gain-match:512");
        Assert.Equal("WarmUp:False:False", commands[^1]);
    }

    [Fact]
    public async Task AutoWhiteAdjust_ConvergesGainAndReportsManagedHarnessMarker()
    {
        var frames = new[]
        {
            CalibrationFrame(0, "mapping-baseline", 60000, 60000, 60000, 60000),
            CalibrationFrame(1, "mapping-probe", 60032, 60000, 60000, 60000),
            CalibrationFrame(2, "white-saturation-probe", ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
            CalibrationFrame(3, "white-low", 30000, 30000, 30000, 30000),
            CalibrationFrame(4, "white-converged", 60000, 60000, 60000, 60000),
            CalibrationFrame(5, "white-offset-balance", 60000, 60000, 60000, 60000),
            CalibrationFrame(6, "white-channel-balance", 60000, 60000, 60000, 60000)
        };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var parameters = new RecordingParameterService(commands);
        var statuses = new List<string>();
        var applied = new List<ScanParameterSnapshot>();
        var service = CreateCalibrationService(parameters, new ScriptedImageDecoder(16, frames));
        var initial = new ScanParameterSnapshot(0, 2, 4, -2, 5, ScanDebugConstants.MinSysClockKhz);

        var result = await service.AutoWhiteAdjustAsync(
            session,
            initial,
            CalibrationRoi(),
            ConfirmAsync,
            statuses.Add,
            applied.Add,
            null,
            CancellationToken.None);

        var expected = initial with { Adc1Gain = 6, Adc2Gain = 6 };
        Assert.Equal(expected, result);
        Assert.Equal([initial with { Adc1Gain = 0, Adc2Gain = 0 }, initial with { Adc1Gain = 0, Adc2Gain = 0 }, expected], applied);
        Assert.Equal(
            ["mapping-baseline", "mapping-probe", "white-saturation-probe", "white-low", "white-converged", "white-offset-balance", "white-channel-balance"],
            session.CapturedFrameNames);
        Assert.Contains("Auto white: applying gains adc1=6, adc2=6...", statuses);
        Assert.Contains("Auto white: passed with gain adc1=6, adc2=6.", statuses);
        AssertOrdered(commands, "StartScan:white-saturation-probe:512", "StartScan:white-low:512", "Apply:0:2:6:-2:6", "StartScan:white-converged:512", "StartScan:white-offset-balance:512", "StartScan:white-channel-balance:512");
        Assert.Equal("WarmUp:False:False", commands[^1]);
        _output.WriteLine("CAL001_MANAGED_HARNESS calibration=white-gain-converged result=adc1:6,adc2:6 hardware=not-used");
    }

    [Fact]
    public async Task AutoWhiteAdjust_RepeatingOverUnderFrames_RestoresBestSampledGains()
    {
        var frames = new List<ScriptedFrame>
        {
            CalibrationFrame(0, "mapping-baseline", 60000, 60000, 60000, 60000),
            CalibrationFrame(1, "mapping-probe", 60032, 60000, 60000, 60000),
            CalibrationFrame(2, "white-probe-unsaturated", 50000, 50000, 50000, 50000),
            CalibrationFrame(3, "white-probe-saturated", ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)
        };
        for (var frameId = 4; frameId < 10; frameId++)
        {
            var underTarget = ((frameId - 4) & 1) == 0;
            frames.Add(CalibrationFrame((byte)frameId, underTarget ? $"white-under-{frameId}" : $"white-over-{frameId}", underTarget ? (ushort)57100 : (ushort)63035, underTarget ? (ushort)57100 : (ushort)63035, underTarget ? (ushort)57100 : (ushort)63035, underTarget ? (ushort)57100 : (ushort)63035));
        }
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var parameters = new RecordingParameterService(commands);
        var statuses = new List<string>();
        var applied = new List<ScanParameterSnapshot>();
        var service = CreateCalibrationService(parameters, new ScriptedImageDecoder(16, frames));
        var zeroGain = new ScanParameterSnapshot(1000, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);
        var gainThirtyFive = zeroGain with { Adc1Gain = 35, Adc2Gain = 35 };
        var gainThirtySix = zeroGain with { Adc1Gain = 36, Adc2Gain = 36 };
        var gainThirtySeven = zeroGain with { Adc1Gain = 37, Adc2Gain = 37 };

        var result = await service.AutoWhiteAdjustAsync(
            session,
            zeroGain,
            CalibrationRoi(),
            ConfirmAsync,
            statuses.Add,
            applied.Add,
            null,
            CancellationToken.None);

        Assert.Equal(gainThirtyFive, result);
        Assert.Equal([zeroGain, zeroGain, gainThirtyFive, gainThirtySeven, gainThirtySix, gainThirtyFive, gainThirtySix, gainThirtyFive, gainThirtyFive], applied);
        Assert.Equal(
            ["mapping-baseline", "mapping-probe", "white-probe-unsaturated", "white-probe-saturated", "white-under-4", "white-over-5", "white-under-6", "white-over-7", "white-under-8", "white-over-9"],
            session.CapturedFrameNames);
        Assert.Contains("Auto white: detected repeating oscillation, keeping best sampled gains adc1=35, adc2=35.", statuses);
        AssertOrdered(
            commands,
            "StartScan:white-under-4:512",
            "Apply:1000:0:37:0:37",
            "StartScan:white-over-5:512",
            "Apply:1000:0:36:0:36",
            "StartScan:white-under-6:512",
            "Apply:1000:0:35:0:35",
            "StartScan:white-over-7:512",
            "Apply:1000:0:36:0:36",
            "StartScan:white-under-8:512",
            "Apply:1000:0:35:0:35",
            "StartScan:white-over-9:512",
            "Apply:1000:0:35:0:35");
        Assert.Equal("WarmUp:False:False", commands[^1]);
    }

    [Fact]
    public async Task AutoBlackAdjust_NonConvergentFrames_RevertsToBestSampledOffsets()
    {
        var frames = new List<ScriptedFrame>
        {
            CalibrationFrame(0, "mapping-baseline", 1000, 1000, 1000, 1000),
            CalibrationFrame(1, "mapping-probe", 1032, 1000, 1000, 1000)
        };
        for (var frameId = 2; frameId < 42; frameId++)
            frames.Add(CalibrationFrame((byte)frameId, $"nonconvergent-{frameId}", 1000, 1000, 1000, 1000));

        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var parameters = new RecordingParameterService(commands);
        var statuses = new List<string>();
        var applied = new List<ScanParameterSnapshot>();
        var service = CreateCalibrationService(parameters, new ScriptedImageDecoder(16, frames));
        var initial = new ScanParameterSnapshot(0, 19, 2, -13, 1, ScanDebugConstants.MinSysClockKhz);

        var result = await service.AutoBlackAdjustAsync(
            session,
            initial,
            CalibrationRoi(),
            ConfirmAsync,
            statuses.Add,
            applied.Add,
            null,
            CancellationToken.None);

        var expected = new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);
        Assert.Equal(expected, result);
        Assert.Equal(expected, applied[^1]);
        Assert.Contains("Auto black: reached iteration limit, keeping best sampled offsets adc1=0, adc2=0.", statuses);
        Assert.True(session.CapturedFrameNames.Count > 2);
        Assert.Contains("WarmUp:False:False", commands);
        Assert.Equal("Apply:0:0:0:0:0", commands[^1]);
    }

    [Fact]
    public async Task AutoBlackAdjust_RepeatingOverUnderFrames_RestoresFirstBestSampledOffsets()
    {
        var frames = new[]
        {
            CalibrationFrame(0, "mapping-baseline", 512, 512, 512, 512),
            CalibrationFrame(1, "mapping-probe", 544, 512, 512, 512),
            CalibrationFrame(2, "oscillation-under-1", 460, 460, 460, 460),
            CalibrationFrame(3, "oscillation-over-1", 564, 564, 564, 564),
            CalibrationFrame(4, "oscillation-under-2", 460, 460, 460, 460),
            CalibrationFrame(5, "oscillation-over-2", 564, 564, 564, 564)
        };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var parameters = new RecordingParameterService(commands);
        var statuses = new List<string>();
        var applied = new List<ScanParameterSnapshot>();
        var service = CreateCalibrationService(parameters, new ScriptedImageDecoder(16, frames));
        var expected = new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);

        var result = await service.AutoBlackAdjustAsync(
            session,
            expected,
            CalibrationRoi(),
            ConfirmAsync,
            statuses.Add,
            applied.Add,
            null,
            CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(
            [
                expected,
                expected,
                expected with { Adc1Offset = 1, Adc2Offset = 1 },
                expected,
                expected with { Adc1Offset = 1, Adc2Offset = 1 },
                expected
            ],
            applied);
        Assert.Equal(
            ["mapping-baseline", "mapping-probe", "oscillation-under-1", "oscillation-over-1", "oscillation-under-2", "oscillation-over-2"],
            session.CapturedFrameNames);
        Assert.Contains("Auto black: detected repeating oscillation, keeping best sampled offsets adc1=0, adc2=0.", statuses);
        AssertOrdered(
            commands,
            "StartScan:oscillation-under-1:512",
            "Apply:0:1:0:1:0",
            "StartScan:oscillation-over-1:512",
            "Apply:0:0:0:0:0",
            "StartScan:oscillation-under-2:512",
            "Apply:0:1:0:1:0",
            "StartScan:oscillation-over-2:512",
            "Apply:0:0:0:0:0");
        Assert.Equal("WarmUp:False:False", commands[^1]);
    }

    [Fact]
    public async Task AutoBlackAdjust_InvalidDecodedWidth_ThrowsDiagnosticAndDisablesWarmUpWithSafeToken()
    {
        var frames = new[] { CalibrationFrame(0, "invalid-roi", 512, 512, 512, 512) };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(0, frames));

        var exception = await Assert.ThrowsAsync<IOException>(() => service.AutoBlackAdjustAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            EmptyRoi(),
            ConfirmAsync,
            null,
            null,
            null,
            CancellationToken.None));

        Assert.Equal("Calibration ROI requires a valid effective column range.", exception.Message);
        Assert.Equal(["invalid-roi"], session.CapturedFrameNames);
        Assert.Equal("WarmUp:False:False", commands[^1]);
    }

    [Fact]
    public void CalibrationRoi_MalformedRanges_AreNormalizedByClamp()
    {
        var malformed = new ScanCalibrationRoiSettings(
            new ScanColumnRange(7, 1),
            new ScanColumnRange(17, 14),
            new ScanColumnRange(9, -4),
            new ScanColumnRange(0, 23),
            new ScanColumnRange(12, -2));

        var normalized = malformed.Clamp(16);

        Assert.Equal(new ScanColumnRange(1, 7), normalized.EffectiveRange);
        Assert.Equal(new ScanColumnRange(0, 3), normalized.ShieldRange);
        Assert.InRange(normalized.FocusLeftRange.Start, normalized.EffectiveRange.Start, normalized.EffectiveRange.EndInclusive);
        Assert.InRange(normalized.FocusRightRange.EndInclusive, normalized.EffectiveRange.Start, normalized.EffectiveRange.EndInclusive);
        Assert.InRange(normalized.FocusOverallRange.Start, normalized.EffectiveRange.Start, normalized.EffectiveRange.EndInclusive);
        Assert.InRange(normalized.FocusOverallRange.EndInclusive, normalized.EffectiveRange.Start, normalized.EffectiveRange.EndInclusive);
    }

    [Fact]
    public async Task AutoBlackAdjust_ScanFailure_ReportsPhaseAndDisablesWarmUpWithSafeToken()
    {
        var frames = new[] { new ScriptedFrame(0, "mapping-failure", static (_, _) => 0, "planned scan failure") };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, frames));

        var exception = await Assert.ThrowsAsync<IOException>(() => service.AutoBlackAdjustAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            CalibrationRoi(),
            ConfirmAsync,
            null,
            null,
            null,
            CancellationToken.None));

        Assert.Equal("Channel mapping baseline failed: planned scan failure", exception.Message);
        Assert.Equal(["mapping-failure"], session.CapturedFrameNames);
        Assert.Equal("WarmUp:False:False", commands[^1]);
    }

    [Fact]
    public async Task AutoBlackAdjust_CancellationAfterWarmUpEnabled_PreservesCancellationAndCleansUp()
    {
        var frames = new[] { CalibrationFrame(0, "canceled-mapping", 512, 512, 512, 512) };
        var commands = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var session = new ScriptedScanSession(frames, commands) { CancelOnStartScan = cancellation };
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, frames));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AutoBlackAdjustAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            CalibrationRoi(),
            ConfirmAsync,
            null,
            null,
            null,
            cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(["canceled-mapping"], session.CapturedFrameNames);
        AssertOrdered(commands, "WarmUp:True:True", "StartScan:canceled-mapping:512", "WarmUp:False:False");
        Assert.Equal("WarmUp:False:False", commands[^1]);
    }

    [Fact]
    public async Task AutoFocus_UsesUnnormalizedBrennerMetricAndStopsMotorsOnSuccess()
    {
        var frames = Enumerable.Range(0, 7)
            .Select(frameId => FocusFrame((byte)frameId, $"focus-{frameId}"))
            .ToArray();
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var statuses = new List<string>();
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        var result = await service.AutoFocusAsync(session, FocusRequest(), statuses.Add, null, CancellationToken.None);

        Assert.Equal(new ScanAutofocusResult(3, 0, -2, 40000, 20000, 20000, 0), result);
        Assert.Equal(["focus-0", "focus-1", "focus-2", "focus-3", "focus-4", "focus-5", "focus-6"], session.CapturedFrameNames);
        Assert.Contains("Autofocus: complete. tilt=0 steps, z=-2 steps, sharpness=40000.0000, imbalance=0.0000.", statuses);
        AssertOrdered(commands, "Move:0:False:2", "Move:2:False:2", "Move:0:True:1", "Move:2:True:1", "Move:0:False:3", "Move:2:False:3");
        Assert.Equal(["Stop:0:False", "Stop:2:False"], commands[^2..]);
        _output.WriteLine("CAL001_MANAGED_HARNESS autofocus=unnormalized-brenner result=overall:40000,z:-2 hardware=not-used");
    }

    [Fact]
    public async Task AutoFocus_ScanFailure_StopsBothMotorsWithSafeToken()
    {
        var frames = new[] { new ScriptedFrame(0, "focus-failure", static (_, _) => 0, "planned focus scan failure") };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        var exception = await Assert.ThrowsAsync<IOException>(() => service.AutoFocusAsync(session, FocusRequest(), null, null, CancellationToken.None));

        Assert.Equal("Autofocus baseline failed: planned focus scan failure", exception.Message);
        Assert.Equal(["focus-failure"], session.CapturedFrameNames);
        Assert.Equal(["Stop:0:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_InvalidDecodedWidth_ReportsDiagnosticAndStopsBothMotors()
    {
        var frames = new[] { FlatFocusFrame(0, "invalid-width") };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(0, frames));

        var exception = await Assert.ThrowsAsync<IOException>(() => service.AutoFocusAsync(session, FocusRequest(), null, null, CancellationToken.None));

        Assert.Equal("Autofocus requires at least 3 rows and a valid decoded scan width.", exception.Message);
        Assert.Equal(["invalid-width"], session.CapturedFrameNames);
        Assert.Equal(["Stop:0:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_MalformedRoiRanges_AreNormalizedBeforeBuildMetrics()
    {
        var frames = Enumerable.Range(0, 7)
            .Select(frameId => FlatFocusFrame((byte)frameId, $"malformed-roi-{frameId}"))
            .ToArray();
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var malformedRequest = FocusRequest() with
        {
            RoiSettings = new ScanCalibrationRoiSettings(
                new ScanColumnRange(7, 1),
                new ScanColumnRange(17, 14),
                new ScanColumnRange(9, -4),
                new ScanColumnRange(0, 23),
                new ScanColumnRange(12, -2))
        };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        var result = await service.AutoFocusAsync(session, malformedRequest, null, null, CancellationToken.None);

        Assert.Equal(new ScanAutofocusResult(3, 0, -2, 0, 0, 0, 0), result);
        Assert.Equal(["malformed-roi-0", "malformed-roi-1", "malformed-roi-2", "malformed-roi-3", "malformed-roi-4", "malformed-roi-5", "malformed-roi-6"], session.CapturedFrameNames);
        Assert.Equal(["Stop:0:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_Cancellation_StopsBothMotorsWithSafeToken()
    {
        var frames = new[] { FocusFrame(0, "cancel-focus") };
        var commands = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var session = new ScriptedScanSession(frames, commands) { CancelOnStartScan = cancellation };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AutoFocusAsync(session, FocusRequest(), null, null, cancellation.Token));

        Assert.Equal(["cancel-focus"], session.CapturedFrameNames);
        Assert.Equal(["Stop:0:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_MotionTimeout_StopsBothMotorsAndDoesNotCapture()
    {
        var commands = new List<string>();
        var session = new ScriptedScanSession([], commands) { ReturnRunningMotionState = true };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, []));

        var exception = await Assert.ThrowsAsync<IOException>(() => service.AutoFocusAsync(session, FocusRequest(), null, null, CancellationToken.None));

        Assert.Equal("Autofocus motion did not settle before timeout.", exception.Message);
        Assert.Empty(session.CapturedFrameNames);
        Assert.Equal(["MotorEnable:0:True", "MotorEnable:2:True"], commands[..2]);
        Assert.Equal(["Stop:0:False", "Stop:2:False"], commands[^2..]);
    }

    private static Task<bool> ConfirmAsync(ScanCalibrationPrompt _)
        => Task.FromResult(true);

    private static ScanAutoCalibrationService CreateCalibrationService(RecordingParameterService parameters, IScanImageDecoder decoder)
        => new(parameters, decoder, new StubTransferSettingsService());

    private static ScanCalibrationRoiSettings CalibrationRoi()
        => new(new ScanColumnRange(0, 7), new ScanColumnRange(8, 15), new ScanColumnRange(0, 3), new ScanColumnRange(4, 7), new ScanColumnRange(0, 7));

    private static ScanCalibrationRoiSettings EmptyRoi()
        => new(new ScanColumnRange(0, -1), new ScanColumnRange(0, -1), new ScanColumnRange(0, -1), new ScanColumnRange(0, -1), new ScanColumnRange(0, -1));

    private static ScanAutofocusRequest FocusRequest()
        => new(3, 1, 1, ScanDebugConstants.MotionMinIntervalNs, true, true, 1, 1, new ScanCalibrationRoiSettings(new ScanColumnRange(0, 19), new ScanColumnRange(15, 19), new ScanColumnRange(0, 4), new ScanColumnRange(10, 14), new ScanColumnRange(0, 14)));

    private static ScriptedFrame CalibrationFrame(byte id, string name, ushort even, ushort odd, ushort shieldEven, ushort shieldOdd)
        => new(id, name, (x, _) => x <= 7 ? ((x & 1) == 0 ? even : odd) : ((x & 1) == 0 ? shieldEven : shieldOdd));

    private static ScriptedFrame FocusFrame(byte id, string name)
        => new(id, name, static (x, _) => x is 2 or 12 ? (ushort)100 : (ushort)0);

    private static ScriptedFrame FlatFocusFrame(byte id, string name)
        => new(id, name, static (_, _) => 100);

    private static void AssertOrdered(IReadOnlyList<string> commands, params string[] expected)
    {
        var startIndex = 0;
        foreach (var command in expected)
        {
            var index = -1;
            for (var commandIndex = startIndex; commandIndex < commands.Count; commandIndex++)
            {
                if (commands[commandIndex] == command)
                {
                    index = commandIndex;
                    break;
                }
            }

            Assert.True(index >= 0, $"Missing ordered command '{command}' in: {string.Join(", ", commands)}");
            startIndex = index + 1;
        }
    }

    private sealed record ScriptedFrame(byte Id, string Name, Func<int, int, ushort> SampleAt, string? FailureMessage = null);

    private sealed class ScriptedImageDecoder(int width, IEnumerable<ScriptedFrame> frames) : IScanImageDecoder
    {
        private readonly IReadOnlyDictionary<byte, ScriptedFrame> _frames = frames.ToDictionary(frame => frame.Id);

        public int GetDecodedPixelsPerLine() => width;

        public (int Start, int EndInclusive) GetEffectivePixelRange() => (0, width - 1);

        public void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeToBgra(byte[] lineBuffer, int rows, Span<byte> destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample)
        {
            if (lineBuffer.Length == 0 || !_frames.TryGetValue(lineBuffer[0], out var frame))
            {
                sample = 0;
                return false;
            }

            sample = frame.SampleAt(x, y);
            return true;
        }
    }

    private sealed class RecordingParameterService(List<string> commands) : IScanParameterService
    {
        public List<ScanParameterSnapshot> AppliedSnapshots { get; } = [];

        public IReadOnlyList<ScanParameterDefinition> Definitions => [];

        public bool TryParseInput(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz, out ScanParameterSnapshot snapshot, out string error)
        {
            snapshot = new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);
            error = string.Empty;
            return true;
        }

        public ScanParameterDisplays BuildDisplays(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz)
            => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        public string FormatOffsetForInput(int offset) => offset.ToString();

        public Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct)
            => Task.FromResult(new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz));

        public Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            AppliedSnapshots.Add(snapshot);
            commands.Add($"Apply:{snapshot.ExposureTicks}:{snapshot.Adc1Offset}:{snapshot.Adc1Gain}:{snapshot.Adc2Offset}:{snapshot.Adc2Gain}");
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

    private sealed class ScriptedScanSession(IEnumerable<ScriptedFrame> frames, List<string> commands) : IScanSessionService
    {
        private readonly Queue<ScriptedFrame> _frames = new(frames);

        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public List<string> CapturedFrameNames { get; } = [];
        public CancellationTokenSource? CancelOnStartScan { get; init; }
        public bool ReturnRunningMotionState { get; init; }
        public ScanTargetState Targets => new(true, "fake-bulk-in", "fake-bulk-out");
        public bool IsConnected => true;
        public int SingleTransferMaxRows => ScanDebugConstants.CalibrationSampleRows;
        public CancellationToken ConnectionToken => CancellationToken.None;

        public void RefreshTargets()
        {
            TargetsChanged?.Invoke(this, EventArgs.Empty);
            _ = MotionEventReceived;
        }

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
            => Task.FromResult(new ScanOperationResult(true, "Connected."));

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
            => Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct) => Task.CompletedTask;

        public Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct) => Task.CompletedTask;

        public Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct) => Task.CompletedTask;

        public Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            commands.Add("MotionState");
            return Task.FromResult<IReadOnlyList<ScanMotorState>>(
            [
                new ScanMotorState(0, true, ReturnRunningMotionState, false, 0, ScanDebugConstants.MotionMinIntervalNs, ReturnRunningMotionState ? 1u : 0u),
                new ScanMotorState(2, true, ReturnRunningMotionState, false, 0, ScanDebugConstants.MotionMinIntervalNs, ReturnRunningMotionState ? 1u : 0u)
            ]);
        }

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            commands.Add($"MotorEnable:{motorId}:{enabled}");
            return Task.CompletedTask;
        }

        public Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            commands.Add($"Move:{motorId}:{direction}:{steps}");
            return Task.CompletedTask;
        }

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct) => Task.CompletedTask;

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            commands.Add($"Wait:{motorId}:{steps}");
            return Task.FromResult(new ScanMotorState(motorId, true, false, false, 0, intervalNs, 0));
        }

        public Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, true, false, direction, 0, intervalNs, 0));

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
        {
            commands.Add($"Stop:{motorId}:{ct.CanBeCanceled}");
            return Task.CompletedTask;
        }

        public Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct) => Task.CompletedTask;

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
        {
            if (ct.CanBeCanceled)
                ct.ThrowIfCancellationRequested();

            commands.Add($"WarmUp:{enabled}:{ct.CanBeCanceled}");
            return Task.FromResult(new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled."));
        }

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
        {
            var frame = _frames.Dequeue();
            CapturedFrameNames.Add(frame.Name);
            commands.Add($"StartScan:{frame.Name}:{rows}");
            CancelOnStartScan?.Cancel();
            ct.ThrowIfCancellationRequested();
            if (frame.FailureMessage is not null)
                return Task.FromResult(new ScanStartResult(false, frame.FailureMessage, null));

            var imageBytes = new[] { frame.Id };
            onRowsAvailable?.Invoke(imageBytes, rows);
            return Task.FromResult(new ScanStartResult(true, frame.Name, imageBytes));
        }

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => StartScanAsync(totalRows, ct, onStatus, onDiagnostic, onProgress, onRowsAvailable, expectedLineTimeUs);

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct) => Task.FromResult(new ScanStopResult(true, "Stopped."));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromResult(new ScanControlFrame(expectedCommand, 0, []));

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
