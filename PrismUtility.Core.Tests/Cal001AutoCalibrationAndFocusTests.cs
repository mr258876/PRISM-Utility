using System.Text.Json;
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
    public async Task AutoBlackAdjust_InvalidDecodedWidth_IsRejectedBeforeSessionIo()
    {
        var frames = new[] { CalibrationFrame(0, "invalid-roi", 512, 512, 512, 512) };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(0, frames));

        var exception = await Assert.ThrowsAsync<ScanRoiValidationException>(() => service.AutoBlackAdjustAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            EmptyRoi(),
            ConfirmAsync,
            null,
            null,
            null,
            CancellationToken.None));

        Assert.Contains(new ScanRoiValidationIssue(ScanRoiOperationOwner.AdcCalibration, ScanRoiValidationCode.Empty, "EffectiveRange"), exception.Issues);
        AssertNoSessionIoOrStops(session, commands);
    }

    [Fact]
    public async Task AutoBlackAdjust_UsesExactlyTwoValidatedAdcColumnsDespiteInvalidFocusRanges()
    {
        var frames = new[]
        {
            ExactAdcRoiFrame(0, "mapping-baseline", 512, 512, 512, 512),
            ExactAdcRoiFrame(1, "mapping-probe", 544, 512, 512, 512),
            ExactAdcRoiFrame(2, "black-coarse", 512, 512, 512, 512),
            ExactAdcRoiFrame(3, "black-gain-match", 512, 512, 512, 512)
        };
        var commands = new List<string>();
        var statuses = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, frames));
        var roi = new ScanCalibrationRoiSettings(
            new ScanColumnRange(0, 1),
            new ScanColumnRange(2, 3),
            new ScanColumnRange(5, 4),
            new ScanColumnRange(8, 7),
            new ScanColumnRange(10, 9));

        var result = await service.AutoBlackAdjustAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            roi,
            ConfirmAsync,
            statuses.Add,
            null,
            null,
            CancellationToken.None);

        Assert.Equal(new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz), result);
        Assert.Contains("Auto calibration: mapping probe even-delta=32.0, odd-delta=0.0.", statuses);
        Assert.Equal(["mapping-baseline", "mapping-probe", "black-coarse", "black-gain-match"], session.CapturedFrameNames);
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
    public async Task AutoBlackCandidate_DeclinedPreparation_DoesNotScanBaseline()
    {
        var commands = new List<string>();
        var session = new ScriptedScanSession([], commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, []));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AutoBlackAdjustCandidateAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            CalibrationRoi(),
            prompt =>
            {
                commands.Add($"Prompt:{prompt.Title}");
                return Task.FromResult(false);
            },
            null,
            null,
            null,
            CancellationToken.None));

        Assert.Equal("Black calibration canceled by user.", exception.Message);
        Assert.Contains("Prompt:Black Calibration", commands);
        Assert.DoesNotContain(commands, command => command.StartsWith("Apply:", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.StartsWith("StartScan:", StringComparison.Ordinal));
        Assert.Empty(session.CapturedFrameNames);
    }

    [Fact]
    public async Task AutoBlackCandidate_AcceptedPreparation_CapturesComparableMetricsAfterPrompt()
    {
        var frames = new[]
        {
            CalibrationFrame(0, "black-before", 1000, 1000, 1000, 1000),
            CalibrationFrame(1, "mapping-baseline", 512, 512, 512, 512),
            CalibrationFrame(2, "mapping-probe", 544, 512, 512, 512),
            CalibrationFrame(3, "black-coarse", 512, 512, 512, 512),
            CalibrationFrame(4, "black-gain-match", 512, 512, 512, 512),
            CalibrationFrame(5, "black-after", 512, 512, 512, 512)
        };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, frames));

        var result = await service.AutoBlackAdjustCandidateAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            CalibrationRoi(),
            prompt =>
            {
                commands.Add($"Prompt:{prompt.Title}");
                return Task.FromResult(true);
            },
            null,
            null,
            null,
            CancellationToken.None);

        Assert.True(commands.IndexOf("Prompt:Black Calibration") < commands.IndexOf("StartScan:black-before:512"));
        Assert.True(commands.IndexOf("Prompt:Black Calibration") < commands.FindIndex(command => command.StartsWith("Apply:", StringComparison.Ordinal)));
        AssertWarmUpBracketsScan(commands, "StartScan:black-before:512");
        AssertWarmUpBracketsScan(commands, "StartScan:black-after:512");
        Assert.Equal(["black-before", "mapping-baseline", "mapping-probe", "black-coarse", "black-gain-match", "black-after"], session.CapturedFrameNames);
        Assert.Equal(1000m, result.BeforeMetrics.BlackLevelDeviation);
        Assert.Equal(512m, result.AfterMetrics.BlackLevelDeviation);
        Assert.Equal(result.BeforeMetrics.NoiseStandardDeviation, result.AfterMetrics.NoiseStandardDeviation);
    }

    [Fact]
    public async Task AutoWhiteCandidate_DeclinedPreparation_DoesNotApplyOrScan()
    {
        var commands = new List<string>();
        var session = new ScriptedScanSession([], commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, []));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AutoWhiteAdjustCandidateAsync(
            session,
            new ScanParameterSnapshot(0, 2, 4, -2, 5, ScanDebugConstants.MinSysClockKhz),
            CalibrationRoi(),
            prompt =>
            {
                commands.Add($"Prompt:{prompt.Title}");
                return Task.FromResult(false);
            },
            null,
            null,
            null,
            CancellationToken.None));

        Assert.Equal("White calibration canceled by user.", exception.Message);
        Assert.Equal(["Prompt:White Calibration"], commands);
        Assert.Empty(session.CapturedFrameNames);
    }

    [Fact]
    public async Task AutoWhiteCandidate_AcceptedPreparation_CapturesWhiteMetricsBeforeAnyApply()
    {
        var frames = new[]
        {
            CalibrationFrame(0, "white-before", 60000, 30000, 60000, 30000),
            CalibrationFrame(1, "mapping-baseline", 60000, 60000, 60000, 60000),
            CalibrationFrame(2, "mapping-probe", 60032, 60000, 60000, 60000),
            CalibrationFrame(3, "white-saturation-probe", ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
            CalibrationFrame(4, "white-low", 30000, 30000, 30000, 30000),
            CalibrationFrame(5, "white-converged", 60000, 60000, 60000, 60000),
            CalibrationFrame(6, "white-offset-balance", 60000, 60000, 60000, 60000),
            CalibrationFrame(7, "white-channel-balance", 60000, 60000, 60000, 60000),
            CalibrationFrame(8, "white-after", 60000, 60000, 60000, 60000)
        };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, frames));

        var result = await service.AutoWhiteAdjustCandidateAsync(
            session,
            new ScanParameterSnapshot(0, 2, 4, -2, 5, ScanDebugConstants.MinSysClockKhz),
            CalibrationRoi(),
            prompt =>
            {
                commands.Add($"Prompt:{prompt.Title}");
                return Task.FromResult(true);
            },
            null,
            null,
            null,
            CancellationToken.None);

        Assert.True(commands.IndexOf("Prompt:White Calibration") < commands.IndexOf("StartScan:white-before:512"));
        Assert.True(commands.IndexOf("Prompt:White Calibration") < commands.FindIndex(command => command.StartsWith("Apply:", StringComparison.Ordinal)));
        AssertWarmUpBracketsScan(commands, "StartScan:white-before:512");
        AssertWarmUpBracketsScan(commands, "StartScan:white-after:512");
        Assert.True(result.BeforeMetrics.AdcOutputDifferencePercent > 60m);
        Assert.Equal(0m, result.BeforeMetrics.SaturatedPixelPercent);
        Assert.Equal(0m, result.AfterMetrics.AdcOutputDifferencePercent);
        Assert.Equal(0m, result.AfterMetrics.SaturatedPixelPercent);
    }

    [Fact]
    public async Task AutoCalibrateCandidate_ComposesBlackAndWhiteStageMetricsWithoutExtraPrompts()
    {
        var frames = new[]
        {
            CalibrationFrame(0, "black-before", 1000, 1000, 1000, 1000),
            CalibrationFrame(1, "black-mapping-baseline", 512, 512, 512, 512),
            CalibrationFrame(2, "black-mapping-probe", 544, 512, 512, 512),
            CalibrationFrame(3, "black-coarse", 512, 512, 512, 512),
            CalibrationFrame(4, "black-gain-match", 512, 512, 512, 512),
            CalibrationFrame(5, "black-after", 512, 512, 512, 512),
            CalibrationFrame(6, "white-before", 60000, 30000, 60000, 30000),
            CalibrationFrame(7, "white-mapping-baseline", 60000, 60000, 60000, 60000),
            CalibrationFrame(8, "white-mapping-probe", 60032, 60000, 60000, 60000),
            CalibrationFrame(9, "white-saturation-probe", ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
            CalibrationFrame(10, "white-low", 30000, 30000, 30000, 30000),
            CalibrationFrame(11, "white-converged", 60000, 60000, 60000, 60000),
            CalibrationFrame(12, "white-offset-balance", 60000, 60000, 60000, 60000),
            CalibrationFrame(13, "white-channel-balance", 60000, 60000, 60000, 60000),
            CalibrationFrame(14, "white-after", 60000, 60000, 60000, 60000)
        };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(16, frames));

        var result = await service.AutoCalibrateCandidateAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            CalibrationRoi(),
            prompt =>
            {
                commands.Add($"Prompt:{prompt.Title}");
                return Task.FromResult(true);
            },
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Equal(["Prompt:Black Calibration", "Prompt:White Calibration"], commands.Where(command => command.StartsWith("Prompt:", StringComparison.Ordinal)).ToArray());
        Assert.True(commands.IndexOf("Prompt:Black Calibration") < commands.IndexOf("StartScan:black-before:512"));
        Assert.True(commands.IndexOf("Prompt:White Calibration") < commands.IndexOf("StartScan:white-before:512"));
        Assert.Equal(1000m, result.BeforeMetrics.BlackLevelDeviation);
        Assert.Equal(0m, result.BeforeMetrics.NoiseStandardDeviation);
        Assert.True(result.BeforeMetrics.AdcOutputDifferencePercent > 60m);
        Assert.Equal(0m, result.BeforeMetrics.SaturatedPixelPercent);
        Assert.Equal(512m, result.AfterMetrics.BlackLevelDeviation);
        Assert.Equal(0m, result.AfterMetrics.AdcOutputDifferencePercent);
        Assert.Equal(0m, result.AfterMetrics.SaturatedPixelPercent);
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
    public async Task AutoFocus_UsesExactlyThreeValidatedColumnsForEachBrennerRangeDespiteInvalidAdcShield()
    {
        var frames = Enumerable.Range(0, 16)
            .Select(frameId => ExactFocusRoiFrame((byte)frameId, $"exact-focus-{frameId}"))
            .ToArray();
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));
        var request = FocusRequest() with
        {
            RoiSettings = new ScanCalibrationRoiSettings(
                new ScanColumnRange(0, 1),
                new ScanColumnRange(5, 4),
                new ScanColumnRange(0, 2),
                new ScanColumnRange(3, 5),
                new ScanColumnRange(0, 5))
        };

        var result = await service.AutoFocusAsync(session, request, null, null, CancellationToken.None);

        var (_, _, _, overallSharpness, leftSharpness, rightSharpness, tiltImbalance) = result;
        Assert.Equal(100d, leftSharpness);
        Assert.Equal(400d, rightSharpness);
        Assert.Equal(600d, overallSharpness);
        Assert.Equal(-0.6d, tiltImbalance, 8);
        Assert.NotEmpty(session.CapturedFrameNames);
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
    public async Task AutoFocus_InvalidDecodedWidth_IsRejectedBeforeMotorIo()
    {
        var frames = new[] { FlatFocusFrame(0, "invalid-width") };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(0, frames));

        var exception = await Assert.ThrowsAsync<ScanRoiValidationException>(() => service.AutoFocusAsync(session, FocusRequest(), null, null, CancellationToken.None));

        Assert.Equal(new ScanRoiValidationIssue(ScanRoiOperationOwner.AutoFocus, ScanRoiValidationCode.Empty, "FocusLeftRange"), exception.Issues[0]);
        AssertNoSessionIoOrStops(session, commands);
    }

    [Fact]
    public async Task AutoFocus_MalformedRoiRanges_AreRejectedBeforeMotorIo()
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

        var exception = await Assert.ThrowsAsync<ScanRoiValidationException>(() => service.AutoFocusAsync(session, malformedRequest, null, null, CancellationToken.None));

        Assert.Equal(new ScanRoiValidationIssue(ScanRoiOperationOwner.AutoFocus, ScanRoiValidationCode.Inverted, "FocusLeftRange"), exception.Issues[0]);
        AssertNoSessionIoOrStops(session, commands);
    }

    [Fact]
    public async Task AutoBlack_InvalidAdcRoiIsRejectedBeforePromptOrSessionIo()
    {
        var commands = new List<string>();
        var session = new ScriptedScanSession([], commands);
        var service = CreateCalibrationService(new RecordingParameterService(commands), new ScriptedImageDecoder(20, []));
        var roi = new ScanCalibrationRoiSettings(
            new ScanColumnRange(0, 0),
            new ScanColumnRange(8, 11),
            new ScanColumnRange(0, 4),
            new ScanColumnRange(8, 12),
            new ScanColumnRange(0, 12));

        var exception = await Assert.ThrowsAsync<ScanRoiValidationException>(() => service.AutoBlackAdjustAsync(
            session,
            new ScanParameterSnapshot(0, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            roi,
            _ => throw new Xunit.Sdk.XunitException("Prompt must not run for an invalid ADC ROI."),
            null,
            null,
            null,
            CancellationToken.None));

        Assert.Equal(new ScanRoiValidationIssue(ScanRoiOperationOwner.AdcCalibration, ScanRoiValidationCode.TooNarrow, "EffectiveRange"), Assert.Single(exception.Issues));
        AssertNoSessionIoOrStops(session, commands);
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

    [Fact]
    public void ScanFocusMotorMapping_DefaultsAreImmutableAndValid()
    {
        var mapping = new ScanFocusMotorMapping();
        var replacement = mapping with { LeftMotorId = 1, RightMotorId = 2 };

        Assert.Equal((byte)0, mapping.LeftMotorId);
        Assert.Equal((byte)2, mapping.RightMotorId);
        Assert.False(mapping.ZPositiveDirection);
        Assert.False(mapping.TiltPositiveDirection);
        Assert.Equal((byte)0, mapping.LeftMotorId);
        Assert.Equal((byte)1, replacement.LeftMotorId);
        Assert.Equal((byte)2, replacement.RightMotorId);

        ScanFocusMotorMappingValidationResult validation = mapping.Validate();
        Assert.True(validation.IsValid);
        Assert.Equal(ScanFocusMotorMappingValidationError.None, validation.Error);
    }

    [Fact]
    public async Task ScanFocusMotorMapping_InvalidValuesHaveTypedFailuresAndAreRejectedBeforeSessionIo()
    {
        var invalidMappings = new[]
        {
            (Mapping: new ScanFocusMotorMapping(1, 1), Error: ScanFocusMotorMappingValidationError.DuplicateMotorIds),
            (Mapping: new ScanFocusMotorMapping(3, 2), Error: ScanFocusMotorMappingValidationError.LeftMotorIdOutOfRange),
            (Mapping: new ScanFocusMotorMapping(1, 3), Error: ScanFocusMotorMappingValidationError.RightMotorIdOutOfRange)
        };

        foreach (var invalid in invalidMappings)
        {
            ScanFocusMotorMappingValidationResult validation = invalid.Mapping.Validate();
            Assert.False(validation.IsValid);
            Assert.Equal(invalid.Error, validation.Error);

            await AssertAutoFocusMappingRejectedBeforeSessionIoAsync(MappedFocusRequest(invalid.Mapping));
        }
    }

    [Fact]
    public void ScanAutofocusRequest_UsesMappingAsItsOnlyDirectionAuthority()
    {
        var roi = FocusRequest().RoiSettings;
        var legacy = new ScanAutofocusRequest(3, 1, 1, ScanDebugConstants.MotionMinIntervalNs, true, false, 1, 1, roi);
        var mapping = new ScanFocusMotorMapping(1, 2, ZPositiveDirection: true, TiltPositiveDirection: true);
        var explicitMapping = new ScanAutofocusRequest(3, 1, 1, ScanDebugConstants.MotionMinIntervalNs, 1, 1, roi, mapping);
        var replacement = mapping with { LeftMotorId = 0, RightMotorId = 2 };

        Assert.Equal(new ScanFocusMotorMapping(0, 2, true, false), legacy.FocusMotorMapping);
        Assert.Equal(mapping, explicitMapping.FocusMotorMapping);
        Assert.NotEqual(replacement, explicitMapping.FocusMotorMapping);
        Assert.Equal(mapping, explicitMapping.FocusMotorMapping);
        Assert.DoesNotContain(
            typeof(ScanAutofocusRequest).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic),
            property => property.Name.Contains("Legacy", StringComparison.Ordinal));
        Assert.Null(typeof(ScanAutofocusRequest).GetProperty("ZPositiveDirection"));
        Assert.Null(typeof(ScanAutofocusRequest).GetProperty("TiltPositiveDirection"));
    }

    [Fact]
    public async Task AutoFocus_WithCopyUsesOnlyCopiedMappingDirectionsForZAndTilt()
    {
        var frames = Enumerable.Range(0, 9)
            .Select(frameId => new ScriptedFrame((byte)frameId, $"mapping-direction-{frameId}", static (x, _) => x == 2 ? (ushort)100 : (ushort)0))
            .ToArray();
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands) { MotionStates = MappedIdleMotionStates() };
        var request = FocusRequest() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, ZPositiveDirection: false, TiltPositiveDirection: false)
        };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        await service.AutoFocusAsync(session, request, null, null, CancellationToken.None);

        AssertOrdered(commands, "Move:1:True:2", "Move:2:True:2");
        AssertOrdered(commands, "Move:1:False:1", "Move:2:True:1");
        Assert.Equal(["Stop:1:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_PreflightValidationRejectsInvalidRowsStepsIntervalsAndIterationsBeforeSessionIo()
    {
        await AssertAutoFocusPreflightRejectedAsync(FocusRequest() with { SampleRows = 0 });
        await AssertAutoFocusPreflightRejectedAsync(FocusRequest() with { TiltProbeSteps = 0 });
        await AssertAutoFocusPreflightRejectedAsync(FocusRequest() with { ZProbeSteps = 0 });
        await AssertAutoFocusPreflightRejectedAsync(FocusRequest() with { MotorIntervalNs = ScanDebugConstants.MotionMinIntervalNs - 1 });
        await AssertAutoFocusPreflightRejectedAsync(FocusRequest() with { MaxTiltIterations = 0 });
        await AssertAutoFocusPreflightRejectedAsync(FocusRequest() with { MaxZIterations = 0 });
    }

    [Fact]
    public async Task AutoFocus_NullRoiIsRejectedBeforeSessionIoOrStops()
    {
        var commands = new List<string>();
        var session = new ScriptedScanSession([FlatFocusFrame(0, "null-roi")], commands)
        {
            MotionStates = MappedIdleMotionStates()
        };
        var request = MappedFocusRequest(new ScanFocusMotorMapping(1, 2)) with { RoiSettings = null! };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, []));

        var exception = await Record.ExceptionAsync(() => service.AutoFocusAsync(session, request, null, null, CancellationToken.None));

        _output.WriteLine($"NULL_ROI_OBSERVED exception={exception?.GetType().Name} session={session.SessionCallCount} motorIo={session.MotorIoCallCount} stops={session.StopCallCount} commands={string.Join(',', commands)}");
        Assert.IsType<ArgumentNullException>(exception);
        AssertNoSessionIoOrStops(session, commands);
    }

    [Fact]
    public async Task AutoFocus_PreCanceledRequestDoesNotReachSessionOrStopMotors()
    {
        var commands = new List<string>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var session = new ScriptedScanSession([], commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, []));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AutoFocusAsync(session, FocusRequest(), null, null, cancellation.Token));

        AssertNoSessionIoOrStops(session, commands);
    }

    [Fact]
    public async Task AutoFocus_FirstStatusCallbackCancellationDoesNotReachSessionOrStopMotors()
    {
        var commands = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var session = new ScriptedScanSession([], commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, []));

        var exception = await Record.ExceptionAsync(() => service.AutoFocusAsync(
            session,
            MappedFocusRequest(new ScanFocusMotorMapping(1, 2)),
            _ => cancellation.Cancel(),
            null,
            cancellation.Token));

        _output.WriteLine($"STATUS_CANCEL_OBSERVED exception={exception?.GetType().Name} session={session.SessionCallCount} motorIo={session.MotorIoCallCount} stops={session.StopCallCount} commands={string.Join(',', commands)}");
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        AssertNoSessionIoOrStops(session, commands);
    }

    [Fact]
    public async Task AutoFocus_PreflightArithmeticOverflowDoesNotReachSessionOrStopMotors()
    {
        var overflow = FocusRequest() with
        {
            TiltProbeSteps = uint.MaxValue,
            ZProbeSteps = uint.MaxValue,
            MaxTiltIterations = int.MaxValue,
            MaxZIterations = int.MaxValue
        };

        await AssertAutoFocusPreflightRejectedAsync(overflow);
    }

    [Fact]
    public async Task AutoFocus_ExplicitMappingUsesOnlyMappedMotorsForIoIdleFilteringAndStops()
    {
        var frames = Enumerable.Range(0, 7)
            .Select(frameId => FocusFrame((byte)frameId, $"mapped-focus-{frameId}"))
            .ToArray();
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands)
        {
            MotionStates =
            [
                new ScanMotorState(0, true, true, false, 0, ScanDebugConstants.MotionMinIntervalNs, 1),
                new ScanMotorState(1, true, false, false, 0, ScanDebugConstants.MotionMinIntervalNs, 0),
                new ScanMotorState(2, true, false, false, 0, ScanDebugConstants.MotionMinIntervalNs, 0)
            ]
        };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        await service.AutoFocusAsync(session, MappedFocusRequest(new ScanFocusMotorMapping(1, 2)), null, null, CancellationToken.None);

        Assert.Contains("MotorEnable:1:True", commands);
        Assert.Contains("MotorEnable:2:True", commands);
        Assert.Contains("MotionState", commands);
        Assert.Contains(commands, command => command.StartsWith("Move:1:", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.StartsWith("Move:2:", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.StartsWith("Wait:1:", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.StartsWith("Wait:2:", StringComparison.Ordinal));
        Assert.Equal(["Stop:1:False", "Stop:2:False"], commands[^2..]);
        Assert.DoesNotContain(commands, command => command.StartsWith("MotorEnable:0:", StringComparison.Ordinal)
            || command.StartsWith("Move:0:", StringComparison.Ordinal)
            || command.StartsWith("Wait:0:", StringComparison.Ordinal)
            || command.StartsWith("Stop:0:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AutoFocus_MappedScanFailureStopsMappedMotorsWithSafeToken()
    {
        var frames = new[] { new ScriptedFrame(0, "mapped-failure", static (_, _) => 0, "planned mapped scan failure") };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands) { MotionStates = MappedIdleMotionStates() };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        var exception = await Assert.ThrowsAsync<IOException>(() => service.AutoFocusAsync(session, MappedFocusRequest(new ScanFocusMotorMapping(1, 2)), null, null, CancellationToken.None));

        Assert.Equal("Autofocus baseline failed: planned mapped scan failure", exception.Message);
        Assert.Equal(["Stop:1:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_MappedCancellationStopsMappedMotorsWithSafeToken()
    {
        var frames = new[] { FocusFrame(0, "mapped-cancel") };
        var commands = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var session = new ScriptedScanSession(frames, commands)
        {
            CancelOnStartScan = cancellation,
            MotionStates = MappedIdleMotionStates()
        };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AutoFocusAsync(session, MappedFocusRequest(new ScanFocusMotorMapping(1, 2)), null, null, cancellation.Token));

        Assert.Equal(["Stop:1:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_StopFailuresAfterIoBeginsAreSurfacedAfterAttemptingBothMappedMotors()
    {
        var frames = Enumerable.Range(0, 7)
            .Select(frameId => FocusFrame((byte)frameId, $"stop-failure-{frameId}"))
            .ToArray();
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands)
        {
            MotionStates = MappedIdleMotionStates(),
            ThrowOnStop = true
        };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        var exception = await Assert.ThrowsAsync<AggregateException>(() => service.AutoFocusAsync(session, MappedFocusRequest(new ScanFocusMotorMapping(1, 2)), null, null, CancellationToken.None));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.All(exception.InnerExceptions, error => Assert.IsType<IOException>(error));
        Assert.Equal(2, session.StopCallCount);
        Assert.Equal(["Stop:1:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_PrimaryFailureAndStopFailure_PreservesPrimaryAndAttemptsBothMappedMotors()
    {
        var frames = new[] { new ScriptedFrame(0, "primary-cleanup-failure", static (_, _) => 0, "planned scan failure") };
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands)
        {
            MotionStates = MappedIdleMotionStates(),
            ThrowOnStop = true
        };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        var exception = await Assert.ThrowsAsync<AggregateException>(() => service.AutoFocusAsync(session, MappedFocusRequest(new ScanFocusMotorMapping(1, 2)), null, null, CancellationToken.None));

        var primary = Assert.IsType<IOException>(exception.InnerExceptions[0]);
        Assert.Equal("Autofocus baseline failed: planned scan failure", primary.Message);
        Assert.Equal(["Stop:1:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public async Task AutoFocus_LeftStopFailureStillAttemptsMappedRightStopWithSafeTokens()
    {
        var frames = Enumerable.Range(0, 7)
            .Select(frameId => FocusFrame((byte)frameId, $"single-stop-failure-{frameId}"))
            .ToArray();
        var commands = new List<string>();
        var session = new ScriptedScanSession(frames, commands)
        {
            MotionStates = MappedIdleMotionStates(),
            ThrowOnStopMotorId = 1
        };
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, frames));

        var exception = await Assert.ThrowsAsync<AggregateException>(() => service.AutoFocusAsync(session, MappedFocusRequest(new ScanFocusMotorMapping(1, 2)), null, null, CancellationToken.None));

        Assert.IsType<IOException>(Assert.Single(exception.InnerExceptions));
        Assert.Equal([1, 2], session.StopAttemptedMotorIds);
        Assert.Equal([2], session.StopCompletedMotorIds);
        Assert.Equal(2, session.StopCallCount);
        Assert.Equal(1, session.StopCompletedCount);
        Assert.Equal([false, false], session.StopTokenCanBeCanceled);
        Assert.Equal(["Stop:1:False", "Stop:2:False"], commands[^2..]);
    }

    [Fact]
    public void ScanAutofocusPresetCatalog_UsesImmutableReadOnlyDeterministicDefinitions()
    {
        var definitionsProperty = typeof(ScanAutofocusPresetCatalog).GetProperty(nameof(ScanAutofocusPresetCatalog.Definitions));

        Assert.True(typeof(ScanAutofocusPresetKind).IsEnum);
        Assert.Equal(
            [ScanAutofocusPresetKind.Quick, ScanAutofocusPresetKind.Standard, ScanAutofocusPresetKind.Fine, ScanAutofocusPresetKind.Custom],
            Enum.GetValues<ScanAutofocusPresetKind>());
        Assert.NotNull(definitionsProperty);
        Assert.Equal(typeof(IReadOnlyList<ScanAutofocusPresetDefinition>), definitionsProperty!.PropertyType);
        Assert.True(typeof(ScanAutofocusPresetDefinition).IsSealed);
        Assert.True(typeof(ScanAutofocusResolvedOptions).IsSealed);

        var firstRead = ScanAutofocusPresetCatalog.Definitions.ToArray();
        var secondRead = ScanAutofocusPresetCatalog.Definitions.ToArray();
        Assert.Equal(firstRead, secondRead);
        Assert.Equal(
            [ScanAutofocusPresetKind.Quick, ScanAutofocusPresetKind.Standard, ScanAutofocusPresetKind.Fine],
            firstRead.Select(definition => definition.Kind).ToArray());
    }

    [Fact]
    public void ScanAutofocusPresetResolver_QuickHasExactDefinitionAndBounds()
    {
        AssertPresetResolution(
            ScanAutofocusPresetKind.Quick,
            new ScanAutofocusPresetDefinition(ScanAutofocusPresetKind.Quick, 64, 0.25, 0.5, 500_000, false, false, 4, 5),
            100,
            200,
            new ScanAutofocusBounds(2500, 6.25, 400, 1.0, 50, 15400, 38.5, 7700));
    }

    [Fact]
    public void ScanAutofocusPresetResolver_StandardHasExactDefinitionAndBounds()
    {
        AssertPresetResolution(
            ScanAutofocusPresetKind.Standard,
            new ScanAutofocusPresetDefinition(ScanAutofocusPresetKind.Standard, 128, 0.5, 1.0, 500_000, false, false, 8, 10),
            200,
            400,
            new ScanAutofocusBounds(10000, 25.0, 1600, 4.0, 94, 56600, 141.5, 28300));
    }

    [Fact]
    public void ScanAutofocusPresetResolver_FineHasExactDefinitionAndBounds()
    {
        AssertPresetResolution(
            ScanAutofocusPresetKind.Fine,
            new ScanAutofocusPresetDefinition(ScanAutofocusPresetKind.Fine, 128, 0.25, 0.5, 500_000, false, false, 16, 20),
            100,
            200,
            new ScanAutofocusBounds(10000, 25.0, 1600, 4.0, 182, 54100, 135.25, 27050));
    }

    [Fact]
    public void ScanAutofocusPresetResolver_ComparativeBoundsAndCustomValuesDoNotMutateBuiltIns()
    {
        var quick = ResolveBuiltInPreset(ScanAutofocusPresetKind.Quick);
        var standard = ResolveBuiltInPreset(ScanAutofocusPresetKind.Standard);
        var fine = ResolveBuiltInPreset(ScanAutofocusPresetKind.Fine);
        var builtInsBeforeCustomResolution = ScanAutofocusPresetCatalog.Definitions.ToArray();
        var custom = new ScanAutofocusPresetDefinition(ScanAutofocusPresetKind.Custom, 32, 0.75, 1.25, 500_000, true, true, 2, 3);

        var customResolved = ScanAutofocusPresetResolver.Resolve(custom, ScanMotorMechanicalSettings.CreateDefault());

        Assert.True(quick.Bounds.MaxZSteps < standard.Bounds.MaxZSteps);
        Assert.Equal(standard.Bounds.MaxZSteps, fine.Bounds.MaxZSteps);
        Assert.True(quick.Bounds.MaxTiltSteps < standard.Bounds.MaxTiltSteps);
        Assert.Equal(standard.Bounds.MaxTiltSteps, fine.Bounds.MaxTiltSteps);
        Assert.True(quick.Bounds.CaptureCount < standard.Bounds.CaptureCount);
        Assert.True(standard.Bounds.CaptureCount < fine.Bounds.CaptureCount);
        Assert.True(quick.Bounds.TotalMovementSteps < fine.Bounds.TotalMovementSteps);
        Assert.True(fine.Bounds.TotalMovementSteps < standard.Bounds.TotalMovementSteps);
        Assert.True(quick.Bounds.EstimatedDurationMs < fine.Bounds.EstimatedDurationMs);
        Assert.True(fine.Bounds.EstimatedDurationMs < standard.Bounds.EstimatedDurationMs);
        Assert.Equal(custom, customResolved.Definition);
        Assert.Equal(300u, customResolved.TiltProbeSteps);
        Assert.Equal(500u, customResolved.ZProbeSteps);
        Assert.Equal(builtInsBeforeCustomResolution, ScanAutofocusPresetCatalog.Definitions.ToArray());
    }

    [Fact]
    public void ScanFocusMappingAndPresetContracts_DoNotChangeRoiNormalization()
    {
        var malformed = new ScanCalibrationRoiSettings(
            new ScanColumnRange(7, 1),
            new ScanColumnRange(17, 14),
            new ScanColumnRange(9, -4),
            new ScanColumnRange(0, 23),
            new ScanColumnRange(12, -2));
        var legacy = FocusRequest() with { RoiSettings = malformed };
        var mapped = MappedFocusRequest(new ScanFocusMotorMapping(1, 2)) with { RoiSettings = malformed };

        Assert.Equal(legacy.RoiSettings.Clamp(20), mapped.RoiSettings.Clamp(20));
        Assert.Equal(new ScanColumnRange(1, 7), mapped.RoiSettings.Clamp(20).EffectiveRange);
        Assert.Equal(new ScanColumnRange(0, 3), mapped.RoiSettings.Clamp(20).ShieldRange);
    }

    [Fact]
    public async Task ScanDeviceSettings_MissingFocusMappingDefaultsAndRoundTripsThroughExistingSettingsKey()
    {
        var defaults = ScanDeviceSettings.CreateDefault();
        var legacyJson = JsonSerializer.Serialize(new
        {
            defaults.Motor1,
            defaults.Motor2,
            defaults.Motor3,
            defaults.Channel1Role,
            defaults.Channel2Role,
            defaults.Channel3Role,
            defaults.Channel4Role
        });
        var legacySettings = JsonSerializer.Deserialize<ScanDeviceSettings>(legacyJson);
        Assert.NotNull(legacySettings);

        var store = new InMemoryLocalSettingsService();
        store.Seed("ScanDeviceSettings", legacySettings!);
        var missingMappingService = new ScanDeviceSettingsService(store);

        await missingMappingService.InitializeAsync();

        Assert.Equal(new ScanFocusMotorMapping(), missingMappingService.Settings.FocusMotorMapping);

        var mapping = new ScanFocusMotorMapping(1, 2, ZPositiveDirection: true, TiltPositiveDirection: true);
        var settings = ScanDeviceSettings.CreateDefault() with { FocusMotorMapping = mapping };
        var serialized = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<ScanDeviceSettings>(serialized);
        Assert.NotNull(deserialized);
        Assert.Contains("\"FocusMotorMapping\"", serialized, StringComparison.Ordinal);
        Assert.Equal(mapping, deserialized!.FocusMotorMapping);

        var writeService = new ScanDeviceSettingsService(store);
        await writeService.SetSettingsAsync(settings);
        Assert.Contains("ScanDeviceSettings", store.SavedKeys);

        var readService = new ScanDeviceSettingsService(store);
        await readService.InitializeAsync();
        Assert.Equal(mapping, readService.Settings.FocusMotorMapping);
    }

    [Fact]
    public async Task ScanDeviceSettings_InvalidExplicitFocusMappingIsNotNormalizedAndIsRejected()
    {
        var invalidMapping = new ScanFocusMotorMapping(1, 1);
        var explicitInvalidSettings = new ScanDeviceSettings(
            null,
            new ScanMotorMechanicalSettings(0, 0, double.NaN),
            null,
            " invalid ",
            " white ",
            "Red",
            "Green",
            invalidMapping);
        var store = new InMemoryLocalSettingsService();
        var service = new ScanDeviceSettingsService(store);

        await service.InitializeAsync();
        var previous = service.Settings;
        var normalized = explicitInvalidSettings.Normalize();

        Assert.Equal(invalidMapping, normalized.FocusMotorMapping);
        Assert.Equal(ScanMotorMechanicalSettings.CreateDefault(), normalized.Motor2);
        Assert.Equal("Blue", normalized.Channel1Role);
        Assert.Equal("White", normalized.Channel2Role);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SetSettingsAsync(explicitInvalidSettings));
        Assert.Empty(store.SavedKeys);
        Assert.Same(previous, service.Settings);
    }

    [Fact]
    public async Task ScanDeviceSettings_MalformedPersistedMappingsInitializeAndRemainInspectable()
    {
        var invalidMappings = new[]
        {
            new ScanFocusMotorMapping(1, 1),
            new ScanFocusMotorMapping(3, 2)
        };

        foreach (var invalidMapping in invalidMappings)
        {
            var persisted = new ScanDeviceSettings(
                null,
                new ScanMotorMechanicalSettings(0, 0, double.NaN),
                null,
                "blue",
                "bad-role",
                " red ",
                "Green",
                invalidMapping);
            var store = new InMemoryLocalSettingsService();
            store.Seed("ScanDeviceSettings", persisted);
            var service = new ScanDeviceSettingsService(store);

            await service.InitializeAsync();

            Assert.Equal(invalidMapping, service.Settings.FocusMotorMapping);
            Assert.Equal(ScanMotorMechanicalSettings.CreateDefault(), service.Settings.Motor1);
            Assert.Equal(ScanMotorMechanicalSettings.CreateDefault(), service.Settings.Motor2);
            Assert.Equal("Blue", service.Settings.Channel1Role);
            Assert.Equal("White", service.Settings.Channel2Role);
            Assert.Empty(store.SavedKeys);
        }
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

    private static ScanAutofocusRequest MappedFocusRequest(
        ScanFocusMotorMapping mapping,
        int sampleRows = 3,
        uint tiltProbeSteps = 1,
        uint zProbeSteps = 1,
        uint motorIntervalNs = ScanDebugConstants.MotionMinIntervalNs,
        int maxTiltIterations = 1,
        int maxZIterations = 1)
        => new(
            sampleRows,
            tiltProbeSteps,
            zProbeSteps,
            motorIntervalNs,
            maxTiltIterations,
            maxZIterations,
            new ScanCalibrationRoiSettings(new ScanColumnRange(0, 19), new ScanColumnRange(15, 19), new ScanColumnRange(0, 4), new ScanColumnRange(10, 14), new ScanColumnRange(0, 14)),
            mapping);

    private static IReadOnlyList<ScanMotorState> MappedIdleMotionStates()
        =>
        [
            new ScanMotorState(1, true, false, false, 0, ScanDebugConstants.MotionMinIntervalNs, 0),
            new ScanMotorState(2, true, false, false, 0, ScanDebugConstants.MotionMinIntervalNs, 0)
        ];

    private static async Task AssertAutoFocusMappingRejectedBeforeSessionIoAsync(ScanAutofocusRequest request)
    {
        var commands = new List<string>();
        var session = new ScriptedScanSession([], commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, []));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.AutoFocusAsync(session, request, null, null, CancellationToken.None));

        AssertNoSessionIoOrStops(session, commands);
    }

    private static async Task AssertAutoFocusPreflightRejectedAsync(ScanAutofocusRequest request)
    {
        var commands = new List<string>();
        var session = new ScriptedScanSession([], commands);
        var service = new ScanAutoFocusService(new ScriptedImageDecoder(20, []));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.AutoFocusAsync(session, request, null, null, CancellationToken.None));

        AssertNoSessionIoOrStops(session, commands);
    }

    private static void AssertNoSessionIoOrStops(ScriptedScanSession session, IReadOnlyList<string> commands)
    {
        Assert.Equal(0, session.SessionCallCount);
        Assert.Equal(0, session.MotorIoCallCount);
        Assert.Equal(0, session.StopCallCount);
        Assert.Empty(commands);
    }

    private static void AssertPresetResolution(
        ScanAutofocusPresetKind kind,
        ScanAutofocusPresetDefinition expectedDefinition,
        uint expectedTiltProbeSteps,
        uint expectedZProbeSteps,
        ScanAutofocusBounds expectedBounds)
    {
        var definition = ScanAutofocusPresetCatalog.Get(kind);
        var resolved = ScanAutofocusPresetResolver.Resolve(definition, ScanMotorMechanicalSettings.CreateDefault());
        var repeated = ScanAutofocusPresetResolver.Resolve(ScanAutofocusPresetCatalog.Get(kind), ScanMotorMechanicalSettings.CreateDefault());

        Assert.Equal(expectedDefinition, definition);
        Assert.Equal(expectedDefinition, resolved.Definition);
        Assert.Equal(expectedTiltProbeSteps, resolved.TiltProbeSteps);
        Assert.Equal(expectedZProbeSteps, resolved.ZProbeSteps);
        Assert.Equal(expectedBounds, resolved.Bounds);
        Assert.Equal(resolved, repeated);
    }

    private static ScanAutofocusResolvedOptions ResolveBuiltInPreset(ScanAutofocusPresetKind kind)
        => ScanAutofocusPresetResolver.Resolve(ScanAutofocusPresetCatalog.Get(kind), ScanMotorMechanicalSettings.CreateDefault());

    private static ScriptedFrame CalibrationFrame(byte id, string name, ushort even, ushort odd, ushort shieldEven, ushort shieldOdd)
        => new(id, name, (x, _) => x <= 7 ? ((x & 1) == 0 ? even : odd) : ((x & 1) == 0 ? shieldEven : shieldOdd));

    private static ScriptedFrame ExactAdcRoiFrame(byte id, string name, ushort even, ushort odd, ushort shieldEven, ushort shieldOdd)
        => new(id, name, (x, _) => x <= 1 ? ((x & 1) == 0 ? even : odd) : x <= 3 ? ((x & 1) == 0 ? shieldEven : shieldOdd) : (ushort)60000);

    private static ScriptedFrame FocusFrame(byte id, string name)
        => new(id, name, static (x, _) => x is 2 or 12 ? (ushort)100 : (ushort)0);

    private static ScriptedFrame ExactFocusRoiFrame(byte id, string name)
        => new(id, name, static (x, _) => x switch
        {
            0 or 1 or 3 or 4 => 0,
            2 => 10,
            5 => 20,
            _ => 1000
        });

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

    private static void AssertWarmUpBracketsScan(IReadOnlyList<string> commands, string scanCommand)
    {
        var scanIndex = -1;
        for (var index = 0; index < commands.Count; index++)
        {
            if (commands[index] == scanCommand)
            {
                scanIndex = index;
                break;
            }
        }

        Assert.True(scanIndex > 0, $"Missing scan command '{scanCommand}' in: {string.Join(", ", commands)}");
        Assert.True(scanIndex < commands.Count - 1, $"Missing warm-up cleanup after '{scanCommand}' in: {string.Join(", ", commands)}");
        Assert.Equal("WarmUp:True:False", commands[scanIndex - 1]);
        Assert.Equal("WarmUp:False:False", commands[scanIndex + 1]);
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

        public Task ApplyGlobalClockAsync(IScanSessionService session, uint sysClockKhz, CancellationToken ct)
            => Task.CompletedTask;

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
        public IReadOnlyList<ScanMotorState>? MotionStates { get; init; }
        public bool ThrowOnStop { get; init; }
        public byte? ThrowOnStopMotorId { get; init; }
        public List<byte> StopAttemptedMotorIds { get; } = [];
        public List<byte> StopCompletedMotorIds { get; } = [];
        public List<bool> StopTokenCanBeCanceled { get; } = [];
        public int SessionCallCount { get; private set; }
        public int MotorIoCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public int StopCompletedCount { get; private set; }
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
            SessionCallCount++;
            MotorIoCallCount++;
            commands.Add("MotionState");
            if (MotionStates is not null)
                return Task.FromResult(MotionStates);

            return Task.FromResult<IReadOnlyList<ScanMotorState>>(
            [
                new ScanMotorState(0, true, ReturnRunningMotionState, false, 0, ScanDebugConstants.MotionMinIntervalNs, ReturnRunningMotionState ? 1u : 0u),
                new ScanMotorState(2, true, ReturnRunningMotionState, false, 0, ScanDebugConstants.MotionMinIntervalNs, ReturnRunningMotionState ? 1u : 0u)
            ]);
        }

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            SessionCallCount++;
            MotorIoCallCount++;
            commands.Add($"MotorEnable:{motorId}:{enabled}");
            return Task.CompletedTask;
        }

        public Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            SessionCallCount++;
            MotorIoCallCount++;
            commands.Add($"Move:{motorId}:{direction}:{steps}");
            return Task.CompletedTask;
        }

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct) => Task.CompletedTask;

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            SessionCallCount++;
            MotorIoCallCount++;
            commands.Add($"Wait:{motorId}:{steps}");
            return Task.FromResult(new ScanMotorState(motorId, true, false, false, 0, intervalNs, 0));
        }

        public Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, true, false, direction, 0, intervalNs, 0));

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
        {
            SessionCallCount++;
            MotorIoCallCount++;
            StopCallCount++;
            StopAttemptedMotorIds.Add(motorId);
            StopTokenCanBeCanceled.Add(ct.CanBeCanceled);
            commands.Add($"Stop:{motorId}:{ct.CanBeCanceled}");
            if (ThrowOnStop || ThrowOnStopMotorId == motorId)
                throw new IOException("planned stop failure");

            StopCompletedMotorIds.Add(motorId);
            StopCompletedCount++;
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
            SessionCallCount++;
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

    private sealed class InMemoryLocalSettingsService : ILocalSettingsService
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public List<string> SavedKeys { get; } = [];

        public void Seed<T>(string key, T value)
            => _values[key] = value;

        public Task<T?> ReadSettingAsync<T>(string key)
            => Task.FromResult(_values.TryGetValue(key, out var value) ? (T?)value : default);

        public Task SaveSettingAsync<T>(string key, T value)
        {
            SavedKeys.Add(key);
            _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
