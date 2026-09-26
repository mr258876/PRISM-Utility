using System.Collections.Concurrent;
using PRISM_Utility.Views;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanDebugDialogLifetimeTests
{
    [Fact]
    public async Task UnloadRetiresRequestBeforeQueuedHideAndKeepsSlotUntilOldShowCompletes()
    {
        var harness = new PageHarness();
        var page = new object();
        var dialog = new FakeDialog();
        var request = NewRequest();
        var showing = harness.Start(page, 1, dialog, request);

        harness.Registry.Retire(page, 1);

        Assert.False(await request.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, dialog.HideCount);
        Assert.False(showing.IsCompleted);
        Assert.Null(harness.Registry.TryAcquire(new object(), 1, _ => { }));

        harness.FlushHides();
        await showing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, dialog.HideCount);
        Assert.NotNull(harness.Registry.TryAcquire(new object(), 1, _ => { }));
    }

    [Fact]
    public async Task LateOldResultAndQueuedOldHideCannotCompleteOrCloseNewDialog()
    {
        var harness = new PageHarness();
        var oldPage = new object();
        var oldDialog = new FakeDialog();
        var oldRequest = NewRequest();
        var oldShowing = harness.Start(oldPage, 4, oldDialog, oldRequest);
        harness.Registry.Retire(oldPage, 4);

        var newPage = new object();
        var rejectedRequest = NewRequest();
        await harness.Start(newPage, 1, new FakeDialog(), rejectedRequest);
        Assert.False(await rejectedRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        oldDialog.Complete(1);
        await oldShowing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await oldRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        var newDialog = new FakeDialog();
        var newRequest = NewRequest();
        var newShowing = harness.Start(newPage, 1, newDialog, newRequest);
        Assert.Equal(1, newDialog.ShowCount);
        harness.FlushHides();

        Assert.Equal(1, oldDialog.HideCount);
        Assert.Equal(0, newDialog.HideCount);
        Assert.False(newRequest.Task.IsCompleted);
        newDialog.Complete(1);
        await newShowing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await newRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task RepeatedRetireAndTerminalCancellationOffUiThreadSettleOnceAndQueueOnlyHide()
    {
        var harness = new PageHarness();
        var page = new object();
        var dialog = new FakeDialog();
        var request = NewRequest();
        using var terminal = new CancellationTokenSource();
        var showing = harness.Start(page, 2, dialog, request, terminal.Token);

        harness.Registry.Retire(new object(), 2);
        harness.Registry.Retire(page, 3);
        Assert.False(request.Task.IsCompleted);
        await Task.Run(terminal.Cancel);
        harness.Registry.Retire(page, 2);
        harness.Registry.Retire(page, 2);
        Assert.False(await request.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Single(harness.PendingHides);
        Assert.Equal(0, dialog.HideCount);
        Assert.Equal(1, harness.RetireCount);

        harness.FlushHides();
        await showing.WaitAsync(TimeSpan.FromSeconds(5));
        terminal.Cancel();
        Assert.Equal(1, harness.RetireCount);
    }

    [Fact]
    public async Task DispatcherRejectionSettlesRequestWithoutPretendingDialogWasHidden()
    {
        var harness = new PageHarness { RejectDispatch = true };
        var dialog = new FakeDialog();
        var request = NewRequest();
        var showing = harness.Start(new object(), 1, dialog, request, new CancellationToken(true));
        await showing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(await request.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, dialog.ShowCount);
        Assert.Equal(0, harness.RejectedDispatches);

        using var terminal = new CancellationTokenSource();
        var activeRequest = NewRequest();
        var owner = new object();
        showing = harness.Start(owner, 2, dialog, activeRequest, terminal.Token);
        terminal.Cancel();
        Assert.False(await activeRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, harness.RejectedDispatches);
        Assert.Equal(0, dialog.HideCount);
        Assert.False(showing.IsCompleted);

        dialog.Complete(1);
        await showing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await activeRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ShowFailureFaultsOnlyItsOwnRequestAndReleasesThreadSlot()
    {
        var harness = new PageHarness();
        var failingDialog = new FakeDialog { ShowException = new InvalidOperationException("show rejected") };
        var failingRequest = NewRequest();
        await harness.Start(new object(), 1, failingDialog, failingRequest).WaitAsync(TimeSpan.FromSeconds(5));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => failingRequest.Task);
        Assert.Equal("show rejected", error.Message);

        var nextDialog = new FakeDialog();
        var nextRequest = NewRequest();
        var nextShowing = harness.Start(new object(), 1, nextDialog, nextRequest);
        nextDialog.Complete(0);
        await nextShowing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await nextRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task InactiveBeforeShowSettlesRequestWithoutPresentingAndFinishedShowDisposesCancellation()
    {
        var harness = new PageHarness { IsActive = false };
        var inactiveDialog = new FakeDialog();
        var inactiveRequest = NewRequest();
        await harness.Start(new object(), 1, inactiveDialog, inactiveRequest).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await inactiveRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, inactiveDialog.ShowCount);
        Assert.Empty(harness.PendingHides);

        harness.IsActive = true;
        using var terminal = new CancellationTokenSource();
        var activeDialog = new FakeDialog();
        var activeRequest = NewRequest();
        var showing = harness.Start(new object(), 2, activeDialog, activeRequest, terminal.Token);
        activeDialog.Complete(1);
        await showing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await activeRequest.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        terminal.Cancel();
        Assert.Empty(harness.PendingHides);
        Assert.Equal(0, activeDialog.HideCount);
    }

    [Fact]
    public async Task ActivationLostWhileShowingCannotForwardLatePrimaryResult()
    {
        var harness = new PageHarness();
        var dialog = new FakeDialog();
        var request = NewRequest();
        var showing = harness.Start(new object(), 1, dialog, request);

        harness.IsActive = false;
        dialog.Complete(1);
        await showing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(await request.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Single(harness.PendingHides);
    }

    [Fact]
    public void PagesCreatedOnTheSameUiThreadShareOneDialogRegistry()
        => Assert.Same(ScanDebugDialogLifetime.ForCurrentThread, ScanDebugDialogLifetime.ForCurrentThread);

    private static TaskCompletionSource<bool> NewRequest()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class PageHarness
    {
        public ScanDebugDialogLifetime Registry { get; } = new();
        public ConcurrentQueue<Action> PendingHides { get; } = new();
        public bool IsActive { get; set; } = true;
        public bool RejectDispatch { get; set; }
        public int RejectedDispatches { get; private set; }
        public int RetireCount { get; private set; }

        public Task Start(object page, int activationEpoch, FakeDialog dialog,
            TaskCompletionSource<bool> request, CancellationToken cancellationToken = default)
        {
            var lease = Registry.TryAcquire(page, activationEpoch, showStarted =>
            {
                RetireCount++;
                request.TrySetResult(false);
                if (!showStarted)
                    return;

                if (RejectDispatch)
                    RejectedDispatches++;
                else
                    PendingHides.Enqueue(dialog.Hide);
            });

            if (lease is null)
            {
                request.TrySetResult(false);
                return Task.CompletedTask;
            }

            return lease.RunAsync(cancellationToken, () => IsActive, dialog.ShowAsync,
                result => request.TrySetResult(result == 1), ex => request.TrySetException(ex));
        }

        public void FlushHides()
        {
            while (PendingHides.TryDequeue(out var hide))
                hide();
        }
    }

    private sealed class FakeDialog
    {
        private readonly TaskCompletionSource<int> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? ShowException { get; set; }
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }

        public Task<int> ShowAsync()
        {
            ShowCount++;
            if (ShowException is { } error)
                throw error;

            return _result.Task;
        }

        public void Hide()
        {
            HideCount++;
            Complete(0);
        }

        public void Complete(int result) => _result.TrySetResult(result);
    }
}
