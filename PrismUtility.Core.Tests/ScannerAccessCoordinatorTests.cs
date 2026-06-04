using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScannerAccess")]
public sealed class ScannerAccessCoordinatorTests
{
    [Fact]
    public async Task ActivateAsync_ScanWorkflow_RoutesThroughWorkflowCoordinator()
    {
        await using var fixture = new AccessFixture();

        var result = await fixture.Access.ActivateAsync(ScannerAccessMode.ScanWorkflow, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ScannerAccessMode.ScanWorkflow, fixture.Access.Snapshot.ActiveMode);
        Assert.Equal(ScannerAccessAvailability.Active, fixture.Access.Snapshot.Availability);
        Assert.True(fixture.Access.CanDeactivate(ScannerAccessMode.ScanWorkflow));
        Assert.Equal("scan-workflow", fixture.Manager.Snapshot.ActiveOwner?.OwnerId);
    }

    [Fact]
    public async Task ActivateAsync_ScanWorkflow_WhenRawUsbActive_ReturnsBlocked()
    {
        await using var fixture = new AccessFixture();
        var rawLease = await fixture.Usb.TryAcquireLeaseAsync("usb-debug-raw", UsbUsageOwnerType.RawUsb, "Bulk IN");

        var result = await fixture.Access.ActivateAsync(ScannerAccessMode.ScanWorkflow, CancellationToken.None);

        Assert.True(rawLease.Success);
        Assert.False(result.Success);
        Assert.Contains("USB Debug", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ScannerAccessMode.UsbDebugRaw, fixture.Access.Snapshot.ActiveMode);
        Assert.Equal(ScannerAccessAvailability.BlockedByUsbDebug, fixture.Access.Snapshot.Availability);
        Assert.False(fixture.Access.CanActivate(ScannerAccessMode.ScanWorkflow));
    }

    [Fact]
    public async Task ActivateAsync_ScanWorkflow_WhenRawUsbActiveAndTargetsMissing_ReturnsUsbDebugBlockedReason()
    {
        await using var fixture = new AccessFixture(targetsPresent: false);
        var rawLease = await fixture.Usb.TryAcquireLeaseAsync("usb-debug-raw", UsbUsageOwnerType.RawUsb, "Bulk IN");

        var result = await fixture.Access.ActivateAsync(ScannerAccessMode.ScanWorkflow, CancellationToken.None);

        Assert.True(rawLease.Success);
        Assert.False(result.Success);
        Assert.Contains("USB Debug", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connect a PRISM scanner", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ScannerAccessAvailability.BlockedByUsbDebug, fixture.Access.Snapshot.Availability);
    }

    [Fact]
    public async Task CanDeactivate_ScanWorkflow_IsFalseWhileRunning()
    {
        await using var fixture = new AccessFixture();
        var connectResult = await fixture.Access.ActivateAsync(ScannerAccessMode.ScanWorkflow, CancellationToken.None);
        var leaseId = fixture.Manager.Snapshot.ActiveOwner!.LeaseId;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runningTask = fixture.Manager.RunWithSessionStateAsync(
            leaseId,
            ScannerSessionState.Running,
            async _ =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(CancellationToken.None);
                return true;
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.Equal(ScannerAccessAvailability.Running, fixture.Access.Snapshot.Availability);
        Assert.False(fixture.Access.CanDeactivate(ScannerAccessMode.ScanWorkflow));

        release.SetResult();
        Assert.True(await runningTask);
    }

    [Fact]
    public async Task CanDeactivate_ScanDebug_IsFalseWhileRunning()
    {
        await using var fixture = new AccessFixture();
        var connectResult = await fixture.Access.ActivateAsync(ScannerAccessMode.ScanDebug, CancellationToken.None);
        var leaseId = fixture.Manager.Snapshot.ActiveOwner!.LeaseId;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runningTask = fixture.Manager.RunWithSessionStateAsync(
            leaseId,
            ScannerSessionState.Running,
            async _ =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(CancellationToken.None);
                return true;
            },
            CancellationToken.None);

        await entered.Task.WaitAsync(CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.Equal(ScannerAccessAvailability.Running, fixture.Access.Snapshot.Availability);
        Assert.False(fixture.Access.CanDeactivate(ScannerAccessMode.ScanDebug));

        release.SetResult();
        Assert.True(await runningTask);
    }

    [Fact]
    public async Task SnapshotChanged_RaisesWhenRawUsbLeaseChanges()
    {
        await using var fixture = new AccessFixture();
        var snapshots = new List<ScannerAccessSnapshot>();
        fixture.Access.SnapshotChanged += (_, snapshot) => snapshots.Add(snapshot);

        var rawLease = await fixture.Usb.TryAcquireLeaseAsync("usb-debug-raw", UsbUsageOwnerType.RawUsb, "Bulk IN");
        await rawLease.Lease!.ReleaseAsync();

        Assert.True(rawLease.Success);
        Assert.Contains(snapshots, snapshot => snapshot.ActiveMode == ScannerAccessMode.UsbDebugRaw);
        Assert.Contains(snapshots, snapshot => snapshot.ActiveMode == ScannerAccessMode.None);
    }

    private sealed class AccessFixture : IAsyncDisposable
    {
        public AccessFixture(bool targetsPresent = true)
        {
            Usb = new UsbUsageCoordinator();
            Manager = new ScannerDeviceSessionManager(new FakeScanSessionServiceFactory(targetsPresent), Usb);
            Workflow = new ScanWorkflowSessionCoordinator(Usb, Manager);
            Debug = new ScanDebugSessionCoordinator(Usb, Manager);
            Access = new ScannerAccessCoordinator(Manager, Usb, Workflow, Debug);
        }

        public UsbUsageCoordinator Usb { get; }
        public ScannerDeviceSessionManager Manager { get; }
        public ScanWorkflowSessionCoordinator Workflow { get; }
        public ScanDebugSessionCoordinator Debug { get; }
        public ScannerAccessCoordinator Access { get; }

        public async ValueTask DisposeAsync()
        {
            Access.Dispose();
            await Manager.DisposeAsync();
        }
    }

    private sealed class FakeScanSessionServiceFactory : IScanSessionServiceFactory
    {
        private readonly bool _targetsPresent;

        public FakeScanSessionServiceFactory(bool targetsPresent)
        {
            _targetsPresent = targetsPresent;
        }

        public IScanSessionService CreateSession()
            => new FakeScanSessionService(_targetsPresent);
    }

    private sealed class FakeScanSessionService : IScanSessionService
    {
        public FakeScanSessionService(bool targetsPresent)
        {
            Targets = targetsPresent
                ? new ScanTargetState(true, "bulk-in-1", "bulk-out-1")
                : new ScanTargetState(false, null, null);
        }

        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets { get; private set; }
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

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
            => Task.FromResult(new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled."));

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
