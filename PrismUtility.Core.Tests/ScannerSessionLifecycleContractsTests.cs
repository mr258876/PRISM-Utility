using System.Reflection;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Lifecycle")]
public sealed class ScannerSessionLifecycleContractsTests
{
    [Fact]
    public void LifecycleStateModel_CanRepresentReconnectPromptFlow()
    {
        var owner = new ScannerSessionOwner(
            OwnerId: "scan-page",
            OwnerType: ScannerSessionOwnerType.ScanWorkflow,
            Operation: ScannerSessionOperation.Scan,
            AcquiredAtUtc: new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero),
            LeaseId: "lease-1");

        var disconnected = ScannerDeviceSessionSnapshot.Disconnected(new DateTimeOffset(2026, 6, 2, 11, 59, 0, TimeSpan.Zero));
        var connecting = disconnected.TransitionTo(ScannerSessionState.Connecting, new DateTimeOffset(2026, 6, 2, 12, 0, 1, TimeSpan.Zero), deviceId: "scanner-1");
        var connected = connecting.TransitionTo(ScannerSessionState.Connected, new DateTimeOffset(2026, 6, 2, 12, 0, 2, TimeSpan.Zero), deviceId: "scanner-1", activeOwner: owner);
        var running = connected.TransitionTo(ScannerSessionState.Running, new DateTimeOffset(2026, 6, 2, 12, 0, 3, TimeSpan.Zero), activeOwner: owner);

        var fault = new ScannerSessionFault(
            Code: ScannerSessionFaultCode.DeviceDisconnected,
            Message: "USB link dropped.",
            OccurredAtUtc: new DateTimeOffset(2026, 6, 2, 12, 0, 4, TimeSpan.Zero),
            DeviceId: "scanner-1",
            PreviousState: ScannerSessionState.Running,
            Owner: owner);

        var faulted = running.TransitionTo(ScannerSessionState.Faulted, new DateTimeOffset(2026, 6, 2, 12, 0, 5, TimeSpan.Zero), activeOwner: null, fault: fault);
        var prompt = new ScannerReconnectPromptState(
            RequiresConfirmation: true,
            DeviceId: "scanner-1",
            Fault: fault,
            PreviousOwner: owner,
            PreviousOperation: ScannerSessionOperation.Scan,
            PromptedAtUtc: new DateTimeOffset(2026, 6, 2, 12, 0, 6, TimeSpan.Zero));

        var reconnectPrompt = faulted.TransitionTo(ScannerSessionState.ReconnectPrompt, new DateTimeOffset(2026, 6, 2, 12, 0, 6, TimeSpan.Zero), reconnectPrompt: prompt);
        var reconnected = reconnectPrompt.TransitionTo(ScannerSessionState.Connected, new DateTimeOffset(2026, 6, 2, 12, 0, 7, TimeSpan.Zero), deviceId: "scanner-1");
        var finalDisconnected = reconnectPrompt.TransitionTo(ScannerSessionState.Disconnected, new DateTimeOffset(2026, 6, 2, 12, 0, 8, TimeSpan.Zero));

        Assert.Equal(ScannerSessionState.Connecting, connecting.State);
        Assert.Equal(ScannerSessionState.Connected, connected.State);
        Assert.Equal(owner, connected.ActiveOwner);
        Assert.Equal(ScannerSessionState.Running, running.State);
        Assert.Equal(ScannerSessionState.Faulted, faulted.State);
        Assert.Equal(fault, faulted.Fault);
        Assert.Equal(ScannerSessionState.ReconnectPrompt, reconnectPrompt.State);
        Assert.True(reconnectPrompt.ReconnectPrompt.RequiresConfirmation);
        Assert.Equal(owner, reconnectPrompt.ReconnectPrompt.PreviousOwner);
        Assert.Equal(ScannerSessionState.Connected, reconnected.State);
        Assert.Equal(ScannerReconnectPromptState.None, reconnected.ReconnectPrompt);
        Assert.Equal(ScannerSessionState.Disconnected, finalDisconnected.State);
    }

    [Fact]
    public void LifecycleStateModel_RejectsInvalidReconnectTransition()
    {
        var connected = ScannerDeviceSessionSnapshot.Disconnected(DateTimeOffset.UtcNow)
            .TransitionTo(ScannerSessionState.Connecting, DateTimeOffset.UtcNow.AddSeconds(1), deviceId: "scanner-1")
            .TransitionTo(ScannerSessionState.Connected, DateTimeOffset.UtcNow.AddSeconds(2), deviceId: "scanner-1");

        var error = Assert.Throws<InvalidOperationException>(() => connected.TransitionTo(ScannerSessionState.ReconnectPrompt, DateTimeOffset.UtcNow.AddSeconds(3)));

        Assert.Contains("Connected", error.Message, StringComparison.Ordinal);
        Assert.Contains("ReconnectPrompt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleOwnerModel_CapturesLeaseMetadataForFutureAsyncLeases()
    {
        var owner = new ScannerSessionOwner(
            OwnerId: "debug-session-42",
            OwnerType: ScannerSessionOwnerType.ScanDebug,
            Operation: ScannerSessionOperation.Diagnostics,
            AcquiredAtUtc: new DateTimeOffset(2026, 6, 2, 12, 30, 0, TimeSpan.Zero),
            LeaseId: "lease-debug-42");

        Assert.Equal("debug-session-42", owner.OwnerId);
        Assert.Equal(ScannerSessionOwnerType.ScanDebug, owner.OwnerType);
        Assert.Equal(ScannerSessionOperation.Diagnostics, owner.Operation);
        Assert.Equal("lease-debug-42", owner.LeaseId);
    }
}

[Trait("Category", "Lifecycle")]
public sealed class ScannerSessionObserverPermissionLifecycleTests
{
    [Fact]
    public void LifecycleObserverPermission_DoesNotCarryMutatingLeaseSemantics()
    {
        var permission = new ScannerSessionObserverPermission(
            ObserverId: "usb-debug-observer",
            Scope: ScannerSessionObserverScope.SessionState | ScannerSessionObserverScope.DeviceCatalog | ScannerSessionObserverScope.Diagnostics,
            GrantedAtUtc: new DateTimeOffset(2026, 6, 2, 13, 0, 0, TimeSpan.Zero));

        Assert.True(permission.Allows(ScannerSessionObserverScope.SessionState));
        Assert.True(permission.Allows(ScannerSessionObserverScope.Diagnostics));
        Assert.False(permission.Allows(ScannerSessionObserverScope.None));
        Assert.False(typeof(IAsyncDeviceLease).IsAssignableFrom(typeof(ScannerSessionObserverPermission)));
        Assert.False(typeof(ScannerSessionOwner).IsAssignableFrom(typeof(ScannerSessionObserverPermission)));
    }

    [Fact]
    public void LifecycleManagerContract_SeparatesObserverGrantFromMutatingLeaseAcquisition()
    {
        var acquireLease = typeof(IScannerDeviceSessionManager).GetMethod(nameof(IScannerDeviceSessionManager.AcquireLeaseAsync));
        var grantObserver = typeof(IScannerDeviceSessionManager).GetMethod(nameof(IScannerDeviceSessionManager.GrantObserverPermission));

        Assert.NotNull(acquireLease);
        Assert.NotNull(grantObserver);
        Assert.Equal(typeof(ValueTask<IAsyncDeviceLease>), acquireLease!.ReturnType);
        Assert.Equal(typeof(ScannerSessionOwner), acquireLease.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(ScannerSessionObserverPermission), grantObserver!.ReturnType);
        Assert.Equal(typeof(ScannerSessionObserverScope), grantObserver.GetParameters()[1].ParameterType);
    }

    [Fact]
    public async Task LifecycleAsyncDeviceLeaseContract_ExposesReleaseTokenRelationship()
    {
        using var cts = new CancellationTokenSource();
        var owner = new ScannerSessionOwner(
            OwnerId: "workflow",
            OwnerType: ScannerSessionOwnerType.ScanWorkflow,
            Operation: ScannerSessionOperation.Scan,
            AcquiredAtUtc: DateTimeOffset.UtcNow,
            LeaseId: "lease-99");

        var lease = new FakeAsyncDeviceLease(owner, cts.Token);

        Assert.Equal(owner, lease.Owner);
        Assert.Equal("lease-99", lease.LeaseId);
        Assert.Equal(cts.Token, lease.ReleaseToken);

        await lease.ReleaseAsync();

        Assert.True(lease.WasReleased);
    }

    private sealed class FakeAsyncDeviceLease : IAsyncDeviceLease
    {
        public FakeAsyncDeviceLease(ScannerSessionOwner owner, CancellationToken releaseToken)
        {
            Owner = owner;
            LeaseId = owner.LeaseId;
            ReleaseToken = releaseToken;
        }

        public string LeaseId { get; }

        public ScannerSessionOwner Owner { get; }

        public CancellationToken ReleaseToken { get; }

        public bool WasReleased { get; private set; }

        public ValueTask ReleaseAsync(CancellationToken ct = default)
        {
            WasReleased = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
            => ReleaseAsync();
    }
}
