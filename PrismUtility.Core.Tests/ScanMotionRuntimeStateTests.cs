using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Todo18")]
public sealed class ScanMotionRuntimeStateTests
{
    [Fact]
    public void GlobalStop_CancelsCapturedProducerAndRejectsItsDelayedSubmission()
    {
        using var state = new ScanMotionRuntimeState();
        var producer = state.CaptureProducer();

        state.BeginGlobalStop();

        Assert.True(producer.CancellationToken.IsCancellationRequested);
        Assert.False(state.IsCurrent(producer));
    }

    [Fact]
    public void NewerFault_PreventsAnEarlierCompleteReadFromClearingReadRequired()
    {
        using var state = new ScanMotionRuntimeState();
        var read = state.CaptureRead();

        state.MarkReadRequired();

        Assert.False(state.TryAcceptCompleteRead(read, CreateCompleteStates()));
        Assert.True(state.RequiresRead);
    }

    [Fact]
    public void SuccessfulGlobalStop_PreventsAnEarlierCompleteReadFromChangingFreshness()
    {
        using var state = new ScanMotionRuntimeState();
        var read = state.CaptureRead();

        state.BeginGlobalStop();

        Assert.False(state.TryAcceptCompleteRead(read, CreateCompleteStates()));
        Assert.False(state.RequiresRead);
    }

    [Fact]
    public void CompleteCurrentRead_ClearsReadRequiredButIncompleteReadDoesNot()
    {
        using var state = new ScanMotionRuntimeState();
        state.MarkReadRequired();
        var read = state.CaptureRead();

        Assert.False(state.TryAcceptCompleteRead(read, CreateCompleteStates()[..2]));
        Assert.True(state.RequiresRead);
        Assert.True(state.TryAcceptCompleteRead(read, CreateCompleteStates()));
        Assert.False(state.RequiresRead);
    }

    [Fact]
    public void SessionChange_PreventsPriorSessionReadFromChangingFreshness()
    {
        using var state = new ScanMotionRuntimeState();
        var read = state.CaptureRead();
        state.AdvanceSession();

        Assert.False(state.TryAcceptCompleteRead(read, CreateCompleteStates()));
    }

    private static ScanMotorState[] CreateCompleteStates()
        =>
        [
            new(0, true, false, false, 0, 1_000, 0),
            new(1, true, false, false, 0, 1_000, 0),
            new(2, true, false, false, 0, 1_000, 0)
        ];
}
