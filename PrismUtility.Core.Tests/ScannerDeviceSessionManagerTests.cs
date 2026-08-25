using System.Diagnostics;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Manager")]
public sealed class ScannerDeviceSessionManagerTests
{
    [Fact]
    public async Task Manager_SessionConnection_SurvivesTransientClientDisposal()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        using (var firstClient = new FakeTransientScannerClient(manager))
        {
            var connectResult = await firstClient.Manager.ConnectAsync(owner, CancellationToken.None);
            Assert.True(connectResult.Success);
        }

        var connectedSession = Assert.Single(factory.CreatedSessions);
        Assert.True(connectedSession.IsConnected);
        Assert.False(connectedSession.DisposeAsyncCalled);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.NotNull(coordinator.ActiveLease);

        using var secondClient = new FakeTransientScannerClient(manager);
        Assert.Same(manager, secondClient.Manager);
        Assert.Equal(ScannerSessionState.Connected, secondClient.Manager.Snapshot.State);
        Assert.Null(secondClient.Manager.Snapshot.ActiveOwner);

        var disconnectResult = await manager.DisconnectAsync(owner.LeaseId, CancellationToken.None);

        Assert.True(disconnectResult.Success);
        Assert.True(connectedSession.DisposeAsyncCalled);
        Assert.False(connectedSession.IsConnected);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task Manager_RejectsConcurrentMutatingCommands()
    {
        var factory = new FakeScanSessionServiceFactory
        {
            WarmUpDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);

        Assert.True(connectResult.Success);

        var firstWarmUp = manager.SetWarmUpEnabledAsync(owner.LeaseId, true, CancellationToken.None);
        await factory.LastSession!.WarmUpStarted.Task.WaitAsync(CancellationToken.None);

        var secondWarmUp = await manager.SetWarmUpEnabledAsync(owner.LeaseId, false, CancellationToken.None);
        factory.WarmUpDelay.SetResult();
        var firstWarmUpResult = await firstWarmUp;

        Assert.True(firstWarmUpResult.Success);
        Assert.False(secondWarmUp.Success);
        Assert.Contains("busy", secondWarmUp.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, factory.LastSession.WarmUpCallCount);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task Lease_ReleaseAsync_CanceledWhileWaitingForMutation_DoesNotLeakOwnership()
    {
        var factory = new FakeScanSessionServiceFactory
        {
            WarmUpDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var lease = await manager.AcquireLeaseAsync(owner, CancellationToken.None);
        var warmUpTask = manager.SetWarmUpEnabledAsync(owner.LeaseId, true, CancellationToken.None);
        await factory.LastSession!.WarmUpStarted.Task.WaitAsync(CancellationToken.None);

        using var releaseCts = new CancellationTokenSource();
        var canceledRelease = lease.ReleaseAsync(releaseCts.Token).AsTask();
        releaseCts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => canceledRelease);
        Assert.True(connectResult.Success);
        Assert.Equal(owner.LeaseId, manager.Snapshot.ActiveOwner?.LeaseId);
        Assert.Equal(ScannerSessionOperation.WarmUp, manager.Snapshot.ActiveOwner?.Operation);
        Assert.NotNull(coordinator.ActiveLease);

        factory.WarmUpDelay.SetResult();
        var warmUpResult = await warmUpTask;
        await lease.ReleaseAsync(CancellationToken.None);

        Assert.True(warmUpResult.Success);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task UseSessionAsync_WaitsForRunWithSessionStateAsyncToReleaseMutationGate()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var runEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runningTask = manager.RunWithSessionStateAsync(
            owner.LeaseId,
            ScannerSessionState.Running,
            async _ =>
            {
                runEntered.TrySetResult();
                await releaseRun.Task.WaitAsync(CancellationToken.None);
                return "running";
            },
            CancellationToken.None);

        await runEntered.Task.WaitAsync(CancellationToken.None);
        var useTask = manager.UseSessionAsync(owner.LeaseId, _ => Task.FromResult("after"), CancellationToken.None);

        await Task.Delay(50);

        Assert.True(connectResult.Success);
        Assert.Equal(ScannerSessionState.Running, manager.Snapshot.State);
        Assert.False(useTask.IsCompleted);

        releaseRun.SetResult();

        Assert.Equal("running", await runningTask);
        Assert.Equal("after", await useTask);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
    }

    [Fact]
    public async Task RunWithSessionStateAsync_WaitsForUseSessionAsyncToReleaseMutationGate()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var useEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var useTask = manager.UseSessionAsync(
            owner.LeaseId,
            async _ =>
            {
                useEntered.TrySetResult();
                await releaseUse.Task.WaitAsync(CancellationToken.None);
                return "using";
            },
            CancellationToken.None);

        await useEntered.Task.WaitAsync(CancellationToken.None);
        var runTask = manager.RunWithSessionStateAsync(
            owner.LeaseId,
            ScannerSessionState.Running,
            _ => Task.FromResult("after"),
            CancellationToken.None);

        await Task.Delay(50);

        Assert.True(connectResult.Success);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.False(runTask.IsCompleted);

        releaseUse.SetResult();

        Assert.Equal("using", await useTask);
        Assert.Equal("after", await runTask);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
    }

    [Fact]
    public async Task RunConnectedSessionStateAsync_WhenBusyAndNonBlocking_ThrowsBusyWithoutWaiting()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var runEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningTask = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async _ =>
            {
                runEntered.TrySetResult();
                await releaseRun.Task.WaitAsync(CancellationToken.None);
                return "running";
            },
            CancellationToken.None);

        await runEntered.Task.WaitAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RunConnectedSessionStateAsync<string>(
            scanOwner,
            ScannerSessionState.Running,
            _ => Task.FromResult("blocked"),
            CancellationToken.None,
            waitForAvailability: false));

        Assert.True(connectResult.Success);
        Assert.Contains("busy", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ScannerSessionState.Running, manager.Snapshot.State);

        releaseRun.SetResult();

        Assert.Equal("running", await runningTask);
    }

    [Fact]
    public async Task DisconnectAsync_WaitsForActiveOperationToFinishBeforeDisposingSession()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromSeconds(1));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        using var operationCts = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var runningTask = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async _ =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, operationCts.Token);
                return "unexpected";
            },
            operationCts.Token);

        await entered.Task.WaitAsync(CancellationToken.None);
        var disconnectTask = manager.DisconnectAsync(owner.LeaseId, CancellationToken.None);

        await Task.Delay(50);

        Assert.False(disconnectTask.IsCompleted);
        Assert.Equal(ScannerSessionState.Running, manager.Snapshot.State);
        Assert.False(factory.LastSession!.DisposeAsyncCalled);

        operationCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runningTask);
        var disconnectResult = await disconnectTask;

        Assert.True(disconnectResult.Success);
        Assert.Equal(1, factory.LastSession.DisconnectCallCount);
        Assert.True(factory.LastSession.DisposeAsyncCalled);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task DisconnectAsync_CancelsOperationContextBeforeDisposingSession()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator(), TimeSpan.FromSeconds(1));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var runningTask = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, operationToken) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, operationToken);
                return "unexpected";
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);
        var disconnectResult = await manager.DisconnectAsync(owner.LeaseId, CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runningTask);
        Assert.True(disconnectResult.Success);
        Assert.True(factory.LastSession!.DisposeAsyncCalled);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
    }

    [Fact]
    public async Task DisconnectAsync_WhenOperationIgnoresCancellation_TimesOutWithoutDisposingSession()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator(), TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var runningTask = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return "released";
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);
        var disconnectResult = await manager.DisconnectAsync(owner.LeaseId, CancellationToken.None);

        Assert.False(disconnectResult.Success);
        Assert.Contains("timed out", disconnectResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(factory.LastSession!.DisposeAsyncCalled);
        Assert.Equal(ScannerSessionState.Running, manager.Snapshot.State);

        release.SetResult();
        Assert.Equal("released", await runningTask);

        Assert.True((await manager.DisconnectAsync(owner.LeaseId, CancellationToken.None)).Success);
        Assert.True(factory.LastSession.DisposeAsyncCalled);
    }

    [Fact]
    public async Task LeaseReleaseAsync_CancelsOperationContextAndCleansUp()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromSeconds(1));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var lease = await manager.AcquireLeaseAsync(owner, CancellationToken.None);
        var runningTask = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, operationToken) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, operationToken);
                return "unexpected";
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);
        await lease.ReleaseAsync(CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runningTask);
        Assert.True(factory.LastSession!.DisposeAsyncCalled);
        Assert.Null(coordinator.ActiveLease);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
    }

    [Fact]
    public async Task DisposeAsync_CancelsOperationContextBeforeCleanup()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromSeconds(1));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var runningTask = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, operationToken) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, operationToken);
                return "unexpected";
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);
        await manager.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runningTask);
        Assert.True(factory.LastSession!.DisposeAsyncCalled);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task DisposeAsync_WhenOperationIgnoresCancellation_DefersAndCompletesCleanup()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var operation = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return "released";
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);
        await manager.DisposeAsync();

        Assert.False(factory.LastSession!.DisposeAsyncCalled);
        release.TrySetResult();
        Assert.Equal("released", await operation);
        await manager.DisposeAsync();

        Assert.True(factory.LastSession.DisposeAsyncCalled);
        Assert.Null(coordinator.ActiveLease);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
    }

    [Fact]
    public async Task DisposeAsync_WhenMutationGateIsBusy_KeepsOneCleanupWorkerAliveUntilCleanupCompletes()
    {
        var factory = new FakeScanSessionServiceFactory
        {
            WarmUpDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var coordinator = new UsbUsageCoordinator();
        var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var session = factory.LastSession!;
        var warmUp = manager.SetWarmUpEnabledAsync(owner.LeaseId, true, CancellationToken.None);
        await session.WarmUpStarted.Task.WaitAsync(CancellationToken.None);

        var firstDispose = manager.DisposeAsync().AsTask();
        await firstDispose.WaitAsync(TimeSpan.FromSeconds(1));
        var secondDispose = manager.DisposeAsync().AsTask();
        await secondDispose.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(session.DisposeAsyncCalled);

        factory.WarmUpDelay.SetResult();
        Assert.True((await warmUp).Success);
        await manager.DisposeAsync();

        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
    }

    [Fact]
    public async Task DisposeAsync_JoinsActiveTeardownAndCleansOnlyOnce()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var teardownCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var session = factory.LastSession!;
        var operation = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, operationToken) =>
            {
                using var registration = operationToken.Register(() => teardownCancellationObserved.TrySetResult());
                operationStarted.TrySetResult();
                await releaseOperation.Task;
                return "released";
            },
            CancellationToken.None);

        await operationStarted.Task.WaitAsync(CancellationToken.None);
        var disconnect = manager.DisconnectAsync(owner.LeaseId, CancellationToken.None);
        await teardownCancellationObserved.Task.WaitAsync(CancellationToken.None);

        var dispose = manager.DisposeAsync().AsTask();
        await dispose.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(session.DisposeAsyncCalled);

        releaseOperation.SetResult();
        Assert.Equal("released", await operation);
        _ = await disconnect;
        await manager.DisposeAsync();

        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
    }

    [Fact]
    public async Task DisposeAsync_ConcurrentCallsRemainBoundedAndJoinDeferredCleanup()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var teardownCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var session = factory.LastSession!;
        var operation = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, operationToken) =>
            {
                using var registration = operationToken.Register(() => teardownCancellationObserved.TrySetResult());
                operationStarted.TrySetResult();
                await releaseOperation.Task;
                return "released";
            },
            CancellationToken.None);

        await operationStarted.Task.WaitAsync(CancellationToken.None);
        var firstDispose = manager.DisposeAsync().AsTask();
        await teardownCancellationObserved.Task.WaitAsync(CancellationToken.None);
        await firstDispose.WaitAsync(TimeSpan.FromSeconds(1));

        var secondDispose = manager.DisposeAsync().AsTask();
        await secondDispose.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(session.DisposeAsyncCalled);

        releaseOperation.SetResult();
        Assert.Equal("released", await operation);
        await manager.DisposeAsync();

        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
    }

    [Fact]
    public async Task DisposeAsync_CleanupFaultIsObservedByLaterDisposeCalls()
    {
        var factory = new FakeScanSessionServiceFactory
        {
            DisconnectException = new InvalidOperationException("disconnect failed")
        };
        var coordinator = new UsbUsageCoordinator();
        var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var session = factory.LastSession!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)));

        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
    }

    [Fact]
    public async Task DisposeAsync_AfterIgnoredCancellation_LeavesTerminalSnapshotOutOfRunningState()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var operation = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, _) =>
            {
                operationStarted.TrySetResult();
                await releaseOperation.Task;
                return "released";
            },
            CancellationToken.None);

        await operationStarted.Task.WaitAsync(CancellationToken.None);
        await manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        releaseOperation.SetResult();
        Assert.Equal("released", await operation);
        await manager.DisposeAsync();

        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.NotEqual(ScannerSessionState.Running, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task DisconnectAsync_WhenOperationCompletesAfterTimeout_PublishesTerminalSnapshot()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator(), TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        var operation = manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return "released";
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);
        Assert.False((await manager.DisconnectAsync(owner.LeaseId, CancellationToken.None)).Success);
        release.TrySetResult();
        Assert.Equal("released", await operation);

        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task SnapshotChanged_ReentrantMutationRunsAfterSnapshotDispatchCompletes()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var callbackObserved = new TaskCompletionSource<ScanOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriberEntered = 0;

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        manager.SnapshotChanged += (_, snapshot) =>
        {
            if (snapshot.State != ScannerSessionState.Connected || Interlocked.Exchange(ref subscriberEntered, 1) != 0)
                return;

            _ = Task.Run(async () => callbackObserved.TrySetResult(await manager.SetWarmUpEnabledAsync(owner.LeaseId, true, CancellationToken.None)));
        };

        Assert.Equal(
            "completed",
            await manager.RunConnectedSessionStateAsync(scanOwner, ScannerSessionState.Running, _ => Task.FromResult("completed"), CancellationToken.None));

        var reentrantResult = await callbackObserved.Task.WaitAsync(CancellationToken.None);
        Assert.True(reentrantResult.Success);
    }

    [Fact]
    public async Task SnapshotChanged_SynchronousReentrantMutationCompletesDuringDispatch()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");
        var callbackObserved = new TaskCompletionSource<ScanOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriberEntered = 0;

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        manager.SnapshotChanged += (_, snapshot) =>
        {
            if (snapshot.State != ScannerSessionState.Connected || Interlocked.Exchange(ref subscriberEntered, 1) != 0)
                return;

            try
            {
                callbackObserved.TrySetResult(manager.SetWarmUpEnabledAsync(owner.LeaseId, true, CancellationToken.None).GetAwaiter().GetResult());
            }
            catch (Exception ex)
            {
                callbackObserved.TrySetException(ex);
            }
        };

        Assert.Equal(
            "completed",
            await manager.RunConnectedSessionStateAsync(scanOwner, ScannerSessionState.Running, _ => Task.FromResult("completed"), CancellationToken.None));

        Assert.True((await callbackObserved.Task.WaitAsync(CancellationToken.None)).Success);
    }

    [Fact]
    public async Task SnapshotChanged_QueuedMutationDoesNotEnterUntilSubscriberCompletes()
    {
        var factory = new FakeScanSessionServiceFactory
        {
            WarmUpDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedCallback = 0;

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        manager.SnapshotChanged += (_, snapshot) =>
        {
            if (snapshot.State != ScannerSessionState.Connected
                || snapshot.ActiveOwner is not null
                || Interlocked.Exchange(ref blockedCallback, 1) != 0)
            {
                return;
            }

            callbackEntered.TrySetResult();
            releaseCallback.Task.GetAwaiter().GetResult();
        };

        var warmUp = manager.SetWarmUpEnabledAsync(owner.LeaseId, true, CancellationToken.None);
        await factory.LastSession!.WarmUpStarted.Task.WaitAsync(CancellationToken.None);
        var queuedMutation = manager.UseSessionAsync(owner.LeaseId, _ => Task.FromResult("queued"), CancellationToken.None);

        factory.WarmUpDelay.SetResult();
        await callbackEntered.Task.WaitAsync(CancellationToken.None);

        Assert.False(queuedMutation.IsCompleted);

        releaseCallback.SetResult();

        Assert.True((await warmUp).Success);
        Assert.Equal("queued", await queuedMutation);
    }

    [Fact]
    public async Task SnapshotChanged_IsolatesFailingSubscribers()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var delivered = 0;

        Assert.True((await manager.ConnectAsync(owner, CancellationToken.None)).Success);
        manager.SnapshotChanged += (_, _) => throw new InvalidOperationException("subscriber failed");
        manager.SnapshotChanged += (_, _) => Interlocked.Increment(ref delivered);

        Assert.True((await manager.SetWarmUpEnabledAsync(owner.LeaseId, true, CancellationToken.None)).Success);

        Assert.Equal(2, Volatile.Read(ref delivered));
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
    }

    [Fact]
    public async Task RunConnectedSessionStateAsync_WhenActionFailsAfterDisconnect_ClearsActiveOwner()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");
        var scanOwner = CreateOwner("scan-page", ScannerSessionOperation.Scan, "scan-operation");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RunConnectedSessionStateAsync<string>(
            scanOwner,
            ScannerSessionState.Running,
            async session =>
            {
                await session.DisconnectAsync();
                throw new InvalidOperationException("Scan failed after the device session disconnected.");
            },
            CancellationToken.None));

        Assert.True(connectResult.Success);
        Assert.Equal("Scan failed after the device session disconnected.", ex.Message);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);

        var reconnectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var retryResult = await manager.RunConnectedSessionStateAsync(
            scanOwner,
            ScannerSessionState.Running,
            _ => Task.FromResult("retry succeeded"),
            CancellationToken.None);

        Assert.True(reconnectResult.Success);
        Assert.Equal("retry succeeded", retryResult);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task Manager_TargetsAndSessionAccess_SurviveTransientClientCleanupWithoutDisposal()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var targetsChangedCount = 0;
        manager.TargetsChanged += (_, _) => targetsChangedCount++;

        var initialTargets = manager.Targets;
        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);

        Assert.True(initialTargets.IsDevicesPresent);
        Assert.True(connectResult.Success);

        var session = Assert.Single(factory.CreatedSessions);
        session.RefreshTargets();

        var observedConnection = await manager.UseSessionAsync(owner.LeaseId, currentSession => Task.FromResult(currentSession.IsConnected), CancellationToken.None);

        Assert.True(observedConnection);
        Assert.True(targetsChangedCount > 0);
        Assert.True(session.IsConnected);
        Assert.False(session.DisposeAsyncCalled);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task Manager_RunWithSessionState_PublishesRunningUntilWorkCompletes()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var operationBlock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runningOperation = manager.RunWithSessionStateAsync(
            owner.LeaseId,
            ScannerSessionState.Running,
            async session =>
            {
                await operationBlock.Task;
                return session.IsConnected;
            },
            CancellationToken.None);

        await Task.Yield();

        Assert.True(connectResult.Success);
        Assert.Equal(ScannerSessionState.Running, manager.Snapshot.State);

        operationBlock.SetResult();
        var result = await runningOperation;

        Assert.True(result);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task Manager_Disconnect_ReleasesOwnedSessionExactlyOnce()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var session = Assert.Single(factory.CreatedSessions);

        var firstDisconnect = await manager.DisconnectAsync(owner.LeaseId, CancellationToken.None);
        var secondDisconnect = await manager.DisconnectAsync(owner.LeaseId, CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.True(firstDisconnect.Success);
        Assert.False(secondDisconnect.Success);
        Assert.Contains("supplied lease", secondDisconnect.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task DisconnectAsync_RejectsWrongLeaseId()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var result = await manager.DisconnectAsync("wrong-lease", CancellationToken.None);
        var session = Assert.Single(factory.CreatedSessions);

        Assert.True(connectResult.Success);
        Assert.False(result.Success);
        Assert.Contains("supplied lease", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Equal(0, session.DisconnectCallCount);
        Assert.Equal(0, session.DisposeAsyncCallCount);
        Assert.NotNull(coordinator.ActiveLease);
    }

    [Fact]
    public async Task StopAsync_RejectsWrongLeaseId()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var result = await manager.StopAsync("wrong-lease", CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.False(result.Success);
        Assert.Contains("supplied lease", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task SetWarmUpEnabledAsync_RejectsWrongLeaseId()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var result = await manager.SetWarmUpEnabledAsync("wrong-lease", true, CancellationToken.None);
        var session = Assert.Single(factory.CreatedSessions);

        Assert.True(connectResult.Success);
        Assert.False(result.Success);
        Assert.Contains("supplied lease", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, session.WarmUpCallCount);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task Manager_ConnectFailure_PublishesFaultAndCleansUp()
    {
        var factory = new FakeScanSessionServiceFactory
        {
            ConnectResult = new ScanOperationResult(false, "Open failed.")
        };
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);

        var result = await manager.ConnectAsync(CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect"), CancellationToken.None);

        var session = Assert.Single(factory.CreatedSessions);
        Assert.False(result.Success);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Equal(ScannerSessionFaultCode.CommandFailed, manager.Snapshot.Fault?.Code);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task Manager_ProtocolTimeout_PublishesFaultAndDisconnects()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => manager.RunWithSessionStateAsync(
            owner.LeaseId,
            ScannerSessionState.Running,
            _ => Task.FromException<bool>(new TimeoutException("ACK timed out.")),
            CancellationToken.None));

        var session = Assert.Single(factory.CreatedSessions);
        Assert.True(connectResult.Success);
        Assert.Equal("ACK timed out.", ex.Message);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Equal(ScannerSessionFaultCode.TransferFailed, manager.Snapshot.Fault?.Code);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task Manager_ProtocolCancellation_PublishesFaultAndDisconnects()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var session = Assert.Single(factory.CreatedSessions);
        session.CancelConnection();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.UseSessionAsync<bool>(
            owner.LeaseId,
            _ => Task.FromCanceled<bool>(session.ConnectionToken),
            CancellationToken.None));

        Assert.True(connectResult.Success);
        Assert.Equal(ScannerSessionState.Faulted, manager.Snapshot.State);
        Assert.Equal(ScannerSessionFaultCode.DeviceAccessLost, manager.Snapshot.Fault?.Code);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task UseSessionAsync_RejectsWrongLeaseId()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UseSessionAsync(
            "wrong-lease",
            _ => Task.FromResult(true),
            CancellationToken.None));

        Assert.True(connectResult.Success);
        Assert.Contains("supplied lease", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
    }

    [Fact]
    public async Task RunWithSessionStateAsync_RejectsWrongLeaseId()
    {
        var factory = new FakeScanSessionServiceFactory();
        await using var manager = new ScannerDeviceSessionManager(factory, new UsbUsageCoordinator());
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RunWithSessionStateAsync(
            "wrong-lease",
            ScannerSessionState.Running,
            _ => Task.FromResult(true),
            CancellationToken.None));

        Assert.True(connectResult.Success);
        Assert.Contains("supplied lease", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
    }

    [Fact]
    public async Task Manager_DeviceRedetection_RequiresExplicitReconnectConfirmation()
    {
        var factory = new FakeScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var connectOwner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(connectOwner, CancellationToken.None);
        var connectedSession = Assert.Single(factory.CreatedSessions);

        connectedSession.SetTargets(new ScanTargetState(false, null, null));
        await WaitForStateAsync(manager, ScannerSessionState.Faulted);

        Assert.True(connectResult.Success);
        Assert.Equal(2, factory.CreatedSessions.Count);
        Assert.Equal(ScannerSessionFaultCode.DeviceDisconnected, manager.Snapshot.Fault?.Code);
        Assert.Equal(1, connectedSession.DisconnectCallCount);
        Assert.Equal(1, connectedSession.DisposeAsyncCallCount);
        Assert.Null(coordinator.ActiveLease);

        var discoverySession = factory.CreatedSessions[1];
        discoverySession.SetTargets(new ScanTargetState(true, "bulk-in-2", "bulk-out-2"));
        await WaitForStateAsync(manager, ScannerSessionState.ReconnectPrompt);

        var blockedReconnect = await manager.ConnectAsync(connectOwner, CancellationToken.None);
        var reconnectOwner = CreateOwner("scan-page", ScannerSessionOperation.Reconnect, "lease-connect");
        var confirmedReconnect = await manager.ReconnectAfterPromptAsync(reconnectOwner, CancellationToken.None);

        Assert.False(blockedReconnect.Success);
        Assert.Contains("confirmation", blockedReconnect.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(confirmedReconnect.Success);
        Assert.Equal(1, discoverySession.ConnectCallCount);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.False(manager.Snapshot.ReconnectPrompt.RequiresConfirmation);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task Manager_ShutdownAsync_UsesBoundedTimeoutAndReleasesOnce()
    {
        var factory = new FakeScanSessionServiceFactory
        {
            DisconnectDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator, TimeSpan.FromMilliseconds(50));
        var owner = CreateOwner("scan-page", ScannerSessionOperation.Connect, "lease-connect");

        var connectResult = await manager.ConnectAsync(owner, CancellationToken.None);
        var session = Assert.Single(factory.CreatedSessions);

        var stopwatch = Stopwatch.StartNew();
        var timedOutShutdown = await manager.ShutdownAsync(CancellationToken.None);
        stopwatch.Stop();

        Assert.True(connectResult.Success);
        Assert.False(timedOutShutdown.Success);
        Assert.Contains("timed out", timedOutShutdown.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Equal(1, session.DisconnectCallCount);

        factory.DisconnectDelay.SetResult();
        await WaitForConditionAsync(() => session.DisposeAsyncCallCount == 1);
        await WaitForConditionAsync(() => coordinator.ActiveLease is null);

        var secondShutdown = await manager.ShutdownAsync(CancellationToken.None);

        Assert.True(secondShutdown.Success);
        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeAsyncCallCount);
        Assert.Equal(ScannerSessionState.Disconnected, manager.Snapshot.State);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.Null(coordinator.ActiveLease);
    }

    private static ScannerSessionOwner CreateOwner(string ownerId, ScannerSessionOperation operation, string leaseId)
        => new(ownerId, ScannerSessionOwnerType.ScanWorkflow, operation, new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero), leaseId);

    private static async Task WaitForStateAsync(IScannerDeviceSessionManager manager, ScannerSessionState state)
        => await WaitForConditionAsync(() => manager.Snapshot.State == state);

    private static async Task WaitForConditionAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        while (!predicate())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class FakeTransientScannerClient : IDisposable
    {
        public FakeTransientScannerClient(IScannerDeviceSessionManager manager)
        {
            Manager = manager;
        }

        public IScannerDeviceSessionManager Manager { get; }

        public void Dispose()
        {
        }
    }

    private sealed class FakeScanSessionServiceFactory : IScanSessionServiceFactory
    {
        public List<FakeScanSessionService> CreatedSessions { get; } = [];

        public FakeScanSessionService? LastSession { get; private set; }

        public TaskCompletionSource? WarmUpDelay { get; set; }

        public TaskCompletionSource? DisconnectDelay { get; set; }

        public Exception? DisconnectException { get; set; }

        public ScanOperationResult ConnectResult { get; set; } = new(true, "Connected.");

        public Queue<ScanTargetState> SessionTargets { get; } = new([new ScanTargetState(true, "bulk-in-1", "bulk-out-1")]);

        public IScanSessionService CreateSession()
        {
            var targets = SessionTargets.Count > 0
                ? SessionTargets.Dequeue()
                : new ScanTargetState(true, "bulk-in-1", "bulk-out-1");
            var session = new FakeScanSessionService(this, targets);
            CreatedSessions.Add(session);
            LastSession = session;
            return session;
        }
    }

    private sealed class FakeScanSessionService : IScanSessionService
    {
        private readonly FakeScanSessionServiceFactory _factory;
        private readonly CancellationTokenSource _connectionCts = new();

        public FakeScanSessionService(FakeScanSessionServiceFactory factory, ScanTargetState targets)
        {
            _factory = factory;
            Targets = targets;
        }

        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets { get; private set; }

        public bool IsConnected { get; private set; }

        public int SingleTransferMaxRows => 1;

        public CancellationToken ConnectionToken => _connectionCts.Token;

        public bool DisposeAsyncCalled { get; private set; }

        public int DisposeAsyncCallCount { get; private set; }

        public int DisconnectCallCount { get; private set; }

        public int ConnectCallCount { get; private set; }

        public int WarmUpCallCount { get; private set; }

        public TaskCompletionSource WarmUpStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RefreshTargets()
            => TargetsChanged?.Invoke(this, EventArgs.Empty);

        public void SetTargets(ScanTargetState targets)
        {
            Targets = targets;
            TargetsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void CancelConnection()
            => _connectionCts.Cancel();

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
        {
            ConnectCallCount++;
            if (_factory.ConnectResult.Success)
                IsConnected = true;

            return Task.FromResult(_factory.ConnectResult);
        }

        public async Task DisconnectAsync()
        {
            DisconnectCallCount++;
            if (_factory.DisconnectDelay is not null)
                await _factory.DisconnectDelay.Task;

            if (_factory.DisconnectException is not null)
                throw _factory.DisconnectException;

            IsConnected = false;
            _connectionCts.Cancel();
        }

        public Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
            => Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public async Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
        {
            WarmUpCallCount++;
            WarmUpStarted.TrySetResult();
            if (_factory.WarmUpDelay is not null)
                await _factory.WarmUpDelay.Task.WaitAsync(ct);

            return new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled.");
        }

        public Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ScanMotorState>>([]);

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
            => Task.CompletedTask;

        public Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, false, false, false, 0, intervalNs, 0));

        public Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, false, false, direction, 0, intervalNs, 0));

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromResult(new ScanStopResult(true, "Stopped."));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromException<ScanControlFrame>(new NotSupportedException());

        public void Dispose()
        {
            DisposeAsyncCalled = true;
            DisposeAsyncCallCount++;
            IsConnected = false;
            _ = MotionEventReceived;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
