using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanDebug")]
public sealed class ScanDebugSessionCoordinatorTests
{
    [Fact]
    public async Task ConnectAsync_WhenScannerOwnedByScanWorkflow_ReturnsBusyWithoutClearingOwner()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var workflowOwner = CreateOwner("scan-page", ScannerSessionOwnerType.ScanWorkflow, ScannerSessionOperation.Connect, "workflow-lease");
        var workflowConnect = await manager.ConnectAsync(workflowOwner, CancellationToken.None);

        Assert.True(workflowConnect.Success);

        var coordinator = new ScanDebugSessionCoordinator(usbCoordinator, manager);
        var result = await coordinator.ConnectAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("read-only", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(workflowOwner, manager.Snapshot.ActiveOwner);
        Assert.False(coordinator.HasConnectedSession);
        Assert.Null(coordinator.ConnectedSession);
        Assert.Single(factory.CreatedSessions);
    }

    [Fact]
    public async Task SetWarmUpAsync_RoutesThroughManagerOwnedSharedSession()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var coordinator = new ScanDebugSessionCoordinator(usbCoordinator, manager);

        var connectResult = await coordinator.ConnectAsync(CancellationToken.None);

        Assert.True(connectResult.Success);
        var connectedSession = Assert.IsType<FakeScanSessionService>(coordinator.ConnectedSession);

        var warmUpResult = await coordinator.SetWarmUpAsync(true, CancellationToken.None);

        Assert.True(warmUpResult.Success);
        Assert.Same(factory.LastSession, connectedSession);
        Assert.Equal(1, connectedSession.WarmUpCallCount);
        Assert.Equal(ScannerSessionOwnerType.ScanDebug, manager.Snapshot.ActiveOwner?.OwnerType);
        Assert.Equal("scan-debug", manager.Snapshot.ActiveOwner?.OwnerId);
    }

    [Fact]
    public async Task UseConnectedSessionAsync_RoutesMutatingCommandsThroughManagerOwnedSharedSession()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var coordinator = new ScanDebugSessionCoordinator(usbCoordinator, manager);

        var connectResult = await coordinator.ConnectAsync(CancellationToken.None);
        var operationResult = await coordinator.UseConnectedSessionAsync(
            async (session, token) =>
            {
                await session.SetMotorEnabledAsync(1, true, token);
                return true;
            },
            CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.True(operationResult);
        Assert.Equal(1, factory.LastSession!.SetMotorEnabledCallCount);
        Assert.Equal(ScannerSessionOwnerType.ScanDebug, manager.Snapshot.ActiveOwner?.OwnerType);
    }

    [Fact]
    public async Task RunConnectedSessionStateAsync_PublishesRunningStateUntilActionCompletes()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var coordinator = new ScanDebugSessionCoordinator(usbCoordinator, manager);

        var connectResult = await coordinator.ConnectAsync(CancellationToken.None);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runningTask = coordinator.RunConnectedSessionStateAsync(
            ScannerSessionState.Running,
            async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(CancellationToken.None);
                return "done";
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.Equal(ScannerSessionState.Running, manager.Snapshot.State);

        release.SetResult();

        Assert.Equal("done", await runningTask);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
    }

    [Fact]
    public async Task UseConnectedSessionAsync_UsesCoordinatorLeaseBinding()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var coordinator = new ScanDebugSessionCoordinator(usbCoordinator, manager);

        var connectResult = await coordinator.ConnectAsync(CancellationToken.None);
        var observedConnected = await coordinator.UseConnectedSessionAsync(
            (session, _) => Task.FromResult(session.IsConnected),
            CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.True(observedConnected);
        Assert.Equal(ScannerSessionOwnerType.ScanDebug, manager.Snapshot.ActiveOwner?.OwnerType);
    }

    private static ScannerSessionOwner CreateOwner(string ownerId, ScannerSessionOwnerType ownerType, ScannerSessionOperation operation, string leaseId)
        => new(ownerId, ownerType, operation, new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero), leaseId);

    private sealed class FakeScanSessionServiceFactory : IScanSessionServiceFactory
    {
        public List<FakeScanSessionService> CreatedSessions { get; } = [];

        public FakeScanSessionService? LastSession { get; private set; }

        public IScanSessionService CreateSession()
        {
            var session = new FakeScanSessionService();
            CreatedSessions.Add(session);
            LastSession = session;
            return session;
        }
    }

    private sealed class FakeScanSessionService : IScanSessionService
    {
        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets { get; private set; } = new(true, "bulk-in-1", "bulk-out-1");

        public bool IsConnected { get; private set; }

        public int SingleTransferMaxRows => 1;

        public CancellationToken ConnectionToken => CancellationToken.None;

        public int WarmUpCallCount { get; private set; }

        public int SetMotorEnabledCallCount { get; private set; }

        public void RefreshTargets()
            => TargetsChanged?.Invoke(this, EventArgs.Empty);

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
        {
            IsConnected = true;
            return Task.FromResult(new ScanOperationResult(true, "Connected."));
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
            => Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
        {
            WarmUpCallCount++;
            return Task.FromResult(new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled."));
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
        {
            SetMotorEnabledCallCount++;
            return Task.CompletedTask;
        }

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

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromResult(new ScanStopResult(true, "Stopped."));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromException<ScanControlFrame>(new NotSupportedException());

        public void Dispose()
        {
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
