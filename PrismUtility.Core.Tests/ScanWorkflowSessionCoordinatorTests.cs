using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanWorkflow")]
public sealed class ScanWorkflowSessionCoordinatorTests
{
    [Fact]
    public async Task ConnectAsync_CreatesGlobalScannerConnection()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var coordinator = new ScanWorkflowSessionCoordinator(usbCoordinator, manager);

        var result = await coordinator.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(coordinator.HasConnectedSession);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.False(coordinator.OwnsSnapshot(manager.Snapshot));
        Assert.Single(factory.CreatedSessions);
    }

    [Fact]
    public async Task ConnectAsync_WhenScannerConnectedByScanDebug_ReusesGlobalSession()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var scanDebugOwner = CreateOwner("scan-debug", ScannerSessionOwnerType.ScanDebug, ScannerSessionOperation.Connect, "scan-debug-lease");
        var scanDebugConnect = await manager.ConnectAsync(scanDebugOwner, CancellationToken.None);
        var coordinator = new ScanWorkflowSessionCoordinator(usbCoordinator, manager);

        var result = await coordinator.ConnectAsync(CancellationToken.None);

        Assert.True(scanDebugConnect.Success);
        Assert.True(result.Success);
        Assert.Null(manager.Snapshot.ActiveOwner);
        Assert.True(coordinator.HasConnectedSession);
        Assert.Single(factory.CreatedSessions);
    }

    [Fact]
    public async Task UseConnectedSessionAsync_RoutesThroughSharedWorkflowLease()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var coordinator = new ScanWorkflowSessionCoordinator(usbCoordinator, manager);

        var connectResult = await coordinator.ConnectAsync(CancellationToken.None);
        var observedConnected = await coordinator.UseConnectedSessionAsync(
            (session, _) => Task.FromResult(session.IsConnected),
            CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.True(observedConnected);
        Assert.Null(manager.Snapshot.ActiveOwner);
    }

    [Fact]
    public async Task RunConnectedSessionStateAsync_PublishesRunningStateUntilActionCompletes()
    {
        var factory = new FakeScanSessionServiceFactory();
        var usbCoordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, usbCoordinator);
        var coordinator = new ScanWorkflowSessionCoordinator(usbCoordinator, manager);

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

    private static ScannerSessionOwner CreateOwner(string ownerId, ScannerSessionOwnerType ownerType, ScannerSessionOperation operation, string leaseId)
        => new(ownerId, ownerType, operation, new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero), leaseId);

    private sealed class FakeScanSessionServiceFactory : IScanSessionServiceFactory
    {
        public List<FakeScanSessionService> CreatedSessions { get; } = [];

        public IScanSessionService CreateSession()
        {
            var session = new FakeScanSessionService();
            CreatedSessions.Add(session);
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
            => Task.FromResult(new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled."));

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
            _ = TargetsChanged;
            _ = MotionEventReceived;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
