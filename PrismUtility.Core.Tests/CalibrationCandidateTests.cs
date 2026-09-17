using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "CalibrationCandidate")]
public sealed class CalibrationCandidateTests
{
    [Fact]
    public void Create_PreservesSnapshotAndMetricValueSemanticsAndDetachesValidationIssues()
    {
        var original = Snapshot(adc1Offset: -12, adc1Gain: 4);
        var candidate = Snapshot(adc1Offset: 6, adc1Gain: 9);
        var sourceIssues = new List<ScanCalibrationCandidateValidationIssue>
        {
            new(ScanCalibrationCandidateValidationSeverity.Warning, "metrics.noise", "Noise variance increased.")
        };
        var validation = new ScanCalibrationCandidateValidation(sourceIssues);

        var result = PendingCalibrationResult.Create(
            original,
            candidate,
            Metrics(4.8m, 112m, 3.2m, 18.4m),
            Metrics(0.3m, 6m, 0.1m, 12.1m),
            new CalibrationCandidateContext(" red ", " workspace-a ", " device-01 "),
            validation);
        sourceIssues.Add(new(ScanCalibrationCandidateValidationSeverity.Error, "candidate.invalid", "Must not leak."));

        Assert.Equal(PendingCalibrationResultState.Pending, result.State);
        Assert.Equal(PendingCalibrationResultIntent.None, result.Intent);
        Assert.Equal("RED", result.SelectedChannel);
        Assert.Equal("WORKSPACE-A", result.Context.WorkspaceIdentity);
        Assert.Equal("DEVICE-01", result.Context.DeviceIdentity);
        Assert.Equal(original, result.OriginalSnapshot);
        Assert.Equal(candidate, result.CandidateSnapshot);
        Assert.NotEqual(original with { Adc1Gain = 5 }, result.OriginalSnapshot);
        Assert.Equal(0.3m, result.AfterMetrics.AdcOutputDifferencePercent);
        Assert.True(result.Validation.IsValid);
        Assert.Single(result.Validation.Issues);
        Assert.Null(result.RequestedSnapshot);
    }

    [Fact]
    public void Create_InvalidValidationBlocksAcceptanceButAllowsRestoreIntent()
    {
        var context = Context();
        var result = PendingCalibrationResult.Create(
            Snapshot(),
            Snapshot(adc1Gain: 8),
            Metrics(4.8m, 112m, 3.2m, 18.4m),
            Metrics(0.3m, 6m, 0.1m, 12.1m),
            context,
            new ScanCalibrationCandidateValidation(
            [
                new ScanCalibrationCandidateValidationIssue(
                    ScanCalibrationCandidateValidationSeverity.Error,
                    "candidate.metrics",
                    "Candidate metrics are incomplete.")
            ]));

        Assert.False(result.CanAccept);
        Assert.Throws<InvalidOperationException>(() => result.Accept(context));

        var restored = result.Restore(context);

        Assert.Equal(PendingCalibrationResultState.Restored, restored.State);
        Assert.Equal(PendingCalibrationResultIntent.RestoreOriginal, restored.Intent);
        Assert.Equal(result.OriginalSnapshot, restored.RequestedSnapshot);
    }

    [Fact]
    public void Accept_RecordsDeviceOnlyIntentWithoutPerformingSideEffects()
    {
        var result = CreatePending();

        var accepted = result.Accept(Context());

        Assert.Equal(PendingCalibrationResultState.Accept, accepted.State);
        Assert.Equal(PendingCalibrationResultIntent.AcceptCandidate, accepted.Intent);
        Assert.Equal(result.CandidateSnapshot, accepted.RequestedSnapshot);
        Assert.Null(accepted.Failure);
        Assert.Equal(result.Context, accepted.Context);
    }

    [Fact]
    public void AcceptAndSave_RecordsDistinctPersistenceIntentWithoutRepositoryOwnership()
    {
        var result = CreatePending();

        var accepted = result.AcceptAndSave(Context());

        Assert.Equal(PendingCalibrationResultState.AcceptAndSave, accepted.State);
        Assert.Equal(PendingCalibrationResultIntent.AcceptAndSaveCandidate, accepted.Intent);
        Assert.Equal(result.CandidateSnapshot, accepted.RequestedSnapshot);
        Assert.Equal(result.OriginalSnapshot, accepted.OriginalSnapshot);
    }

    [Fact]
    public void Restore_RecordsOriginalSnapshotIntent()
    {
        var result = CreatePending();

        var restored = result.Restore(Context());

        Assert.Equal(PendingCalibrationResultState.Restored, restored.State);
        Assert.Equal(PendingCalibrationResultIntent.RestoreOriginal, restored.Intent);
        Assert.Equal(result.OriginalSnapshot, restored.RequestedSnapshot);
        Assert.Null(restored.Failure);
    }

    [Theory]
    [InlineData(CalibrationCandidateStaleness.Channel)]
    [InlineData(CalibrationCandidateStaleness.Workspace)]
    [InlineData(CalibrationCandidateStaleness.Device)]
    public void Accept_StaleContextInvalidatesInsteadOfReturningAnAcceptanceIntent(
        CalibrationCandidateStaleness expectedStaleness)
    {
        var result = CreatePending();
        var staleContext = expectedStaleness switch
        {
            CalibrationCandidateStaleness.Channel => new CalibrationCandidateContext("Blue", "workspace-a", "device-01"),
            CalibrationCandidateStaleness.Workspace => new CalibrationCandidateContext("Red", "workspace-b", "device-01"),
            CalibrationCandidateStaleness.Device => new CalibrationCandidateContext("Red", "workspace-a", "device-02"),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedStaleness))
        };

        var invalidated = result.Accept(staleContext);

        Assert.Equal(PendingCalibrationResultState.Invalidated, invalidated.State);
        Assert.Equal(PendingCalibrationResultIntent.None, invalidated.Intent);
        Assert.Equal(expectedStaleness, invalidated.Staleness);
        Assert.Null(invalidated.RequestedSnapshot);
        Assert.Equal(result.CandidateSnapshot, invalidated.CandidateSnapshot);
    }

    [Fact]
    public void InvalidateIfStale_LeavesMatchingContextPendingAndReportsAllChangedIdentityParts()
    {
        var result = CreatePending();

        var unchanged = result.InvalidateIfStale(new CalibrationCandidateContext(" red ", " workspace-a ", " device-01 "));
        var invalidated = result.InvalidateIfStale(new CalibrationCandidateContext("blue", "workspace-b", "device-02"));

        Assert.Same(result, unchanged);
        Assert.Equal(
            CalibrationCandidateStaleness.Channel | CalibrationCandidateStaleness.Workspace | CalibrationCandidateStaleness.Device,
            invalidated.Staleness);
    }

    [Fact]
    public void Cancel_RecordsCancellationWithoutAnApplyIntent()
    {
        var cancelled = CreatePending().Cancel();

        Assert.Equal(PendingCalibrationResultState.Cancelled, cancelled.State);
        Assert.Equal(PendingCalibrationResultIntent.None, cancelled.Intent);
        Assert.Null(cancelled.RequestedSnapshot);
    }

    [Fact]
    public void Fail_RecordsTypedFailureDetailWithoutAnApplyIntent()
    {
        var failure = new ScanCalibrationCandidateFailure("capture.failed", "Frame decode failed after calibration sampling.");

        var failed = CreatePending().Fail(failure);

        Assert.Equal(PendingCalibrationResultState.Failed, failed.State);
        Assert.Equal(PendingCalibrationResultIntent.None, failed.Intent);
        Assert.Equal(failure, failed.Failure);
        Assert.Null(failed.RequestedSnapshot);
    }

    [Fact]
    public void TerminalTransitions_RejectRepeatedOrIllegalLifecycleChanges()
    {
        var pending = CreatePending();
        var staleContext = new CalibrationCandidateContext("Blue", "workspace-a", "device-01");
        var terminalResults = new[]
        {
            pending.Accept(Context()),
            pending.AcceptAndSave(Context()),
            pending.Restore(Context()),
            pending.InvalidateIfStale(staleContext),
            pending.Cancel(),
            pending.Fail(new ScanCalibrationCandidateFailure("capture.failed", "Calibration capture failed."))
        };

        foreach (var terminal in terminalResults)
        {
            Assert.True(terminal.IsTerminal);
            Assert.Throws<InvalidOperationException>(() => terminal.Accept(Context()));
            Assert.Throws<InvalidOperationException>(() => terminal.AcceptAndSave(Context()));
            Assert.Throws<InvalidOperationException>(() => terminal.Restore(Context()));
            Assert.Throws<InvalidOperationException>(() => terminal.InvalidateIfStale(Context()));
            Assert.Throws<InvalidOperationException>(() => terminal.Cancel());
            Assert.Throws<InvalidOperationException>(() => terminal.Fail(new ScanCalibrationCandidateFailure("later.failure", "Must be rejected.")));
        }
    }

    private static PendingCalibrationResult CreatePending()
        => PendingCalibrationResult.Create(
            Snapshot(adc1Offset: -12, adc1Gain: 4),
            Snapshot(adc1Offset: 6, adc1Gain: 9),
            Metrics(4.8m, 112m, 3.2m, 18.4m),
            Metrics(0.3m, 6m, 0.1m, 12.1m),
            Context());

    private static CalibrationCandidateContext Context()
        => new("Red", "workspace-a", "device-01");

    private static ScanParameterSnapshot Snapshot(int adc1Offset = 0, ushort adc1Gain = 0)
        => new(1_000, adc1Offset, adc1Gain, -4, 2, 125_000);

    private static ScanCalibrationMetrics Metrics(
        decimal adcOutputDifferencePercent,
        decimal blackLevelDeviation,
        decimal saturatedPixelPercent,
        decimal noiseStandardDeviation)
        => new(
            adcOutputDifferencePercent,
            blackLevelDeviation,
            saturatedPixelPercent,
            noiseStandardDeviation);
}
