using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class UsbDebugViewModelLifecycleTests
{
    [Fact]
    public void ActiveScannerOwner_DisablesRawUsbCommands_AndShowsReadOnlyObservationMode()
    {
        var usb = new FakeUsbService();
        var coordinator = new UsbUsageCoordinator();
        var manager = new FakeScannerDeviceSessionManager(CreateOwnedSnapshot());
        using var viewModel = CreateViewModel(usb, coordinator, manager);

        viewModel.SelectedBulkInUsbDevice = usb.Devices[0];
        viewModel.SelectedBulkInConfig = usb.Configs[0];
        viewModel.SelectedBulkInInterface = usb.Interfaces[0];
        viewModel.SelectedBulkInEndpoint = usb.BulkInEndpoints[0];
        viewModel.SelectedBulkOutUsbDevice = usb.Devices[0];
        viewModel.SelectedBulkOutConfig = usb.Configs[0];
        viewModel.SelectedBulkOutInterface = usb.Interfaces[0];
        viewModel.SelectedBulkOutEndpoint = usb.BulkOutEndpoints[0];
        viewModel.BulkOutText = "ping";

        Assert.False(viewModel.StartBulkInCommand.CanExecute(null));
        Assert.False(viewModel.SendBulkOutCommand.CanExecute(null));
        Assert.Contains("Read-only observation mode", viewModel.ObservationStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, usb.OpenSessionCallCount);
    }

    [Fact]
    public async Task IdleBulkOutCommand_AcquiresExclusiveRawLease_UsesBulkOutPath_AndReleasesLease()
    {
        var usb = new FakeUsbService();
        var coordinator = new UsbUsageCoordinator();
        var manager = new FakeScannerDeviceSessionManager(ScannerDeviceSessionSnapshot.Disconnected(DateTimeOffset.UtcNow));
        using var viewModel = CreateViewModel(usb, coordinator, manager);

        viewModel.SelectedBulkOutUsbDevice = usb.Devices[0];
        viewModel.SelectedBulkOutConfig = usb.Configs[0];
        viewModel.SelectedBulkOutInterface = usb.Interfaces[0];
        viewModel.SelectedBulkOutEndpoint = usb.BulkOutEndpoints[0];
        viewModel.BulkOutText = "ping";

        await viewModel.SendBulkOutCommand.ExecuteAsync(null);

        Assert.Equal(1, usb.OpenSessionCallCount);
        Assert.Equal(1, usb.Session.WriteCalls);
        Assert.Equal("70-69-6E-67", BitConverter.ToString(usb.Session.LastWrite ?? Array.Empty<byte>()));
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task IdleBulkInCommand_AcquiresExclusiveRawLease_UsesBulkInPath_AndReleasesLeaseOnStop()
    {
        var usb = new FakeUsbService();
        var coordinator = new UsbUsageCoordinator();
        var manager = new FakeScannerDeviceSessionManager(ScannerDeviceSessionSnapshot.Disconnected(DateTimeOffset.UtcNow));
        using var viewModel = CreateViewModel(usb, coordinator, manager);

        viewModel.SelectedBulkInUsbDevice = usb.Devices[0];
        viewModel.SelectedBulkInConfig = usb.Configs[0];
        viewModel.SelectedBulkInInterface = usb.Interfaces[0];
        viewModel.SelectedBulkInEndpoint = usb.BulkInEndpoints[0];

        var executeTask = viewModel.StartBulkInCommand.ExecuteAsync(null);

        await usb.Session.ReadStarted.Task;
        Assert.NotNull(coordinator.ActiveLease);
        Assert.Equal(UsbUsageOwnerType.RawUsb, coordinator.ActiveLease!.OwnerType);
        Assert.Equal(1, usb.OpenSessionCallCount);

        viewModel.StopBulkInCommand.Execute(null);
        await executeTask;

        Assert.True(usb.Session.DisposeCalled);
        Assert.Null(coordinator.ActiveLease);
        Assert.False(viewModel.IsBulkInRunning);
    }

    private static UsbDebugViewModel CreateViewModel(FakeUsbService usb, UsbUsageCoordinator coordinator, FakeScannerDeviceSessionManager manager)
        => new(usb, coordinator, manager, new ImmediateUiDispatcher(), new FakeDebugOutputMirrorService());

    private static ScannerDeviceSessionSnapshot CreateOwnedSnapshot()
    {
        var owner = new ScannerSessionOwner(
            "scan-workflow",
            ScannerSessionOwnerType.ScanWorkflow,
            ScannerSessionOperation.Scan,
            DateTimeOffset.UtcNow,
            "lease-1");

        return ScannerDeviceSessionSnapshot.Disconnected(DateTimeOffset.UtcNow, "usb-device-1")
            .TransitionTo(ScannerSessionState.Connecting, DateTimeOffset.UtcNow, "usb-device-1")
            .TransitionTo(ScannerSessionState.Connected, DateTimeOffset.UtcNow, "usb-device-1", owner, null, ScannerReconnectPromptState.None);
    }

    private sealed class FakeScannerDeviceSessionManager : IScannerDeviceSessionManager
    {
        public FakeScannerDeviceSessionManager(ScannerDeviceSessionSnapshot snapshot)
        {
            Snapshot = snapshot;
            Targets = new ScanTargetState(true, snapshot.DeviceId, snapshot.DeviceId);
        }

        public event EventHandler<ScannerDeviceSessionSnapshot>? SnapshotChanged;

        public event EventHandler? TargetsChanged;

        public ScannerDeviceSessionSnapshot Snapshot { get; private set; }

        public ScanTargetState Targets { get; private set; }

        public IScanSessionService? TryGetOwnedSession(string leaseId)
            => null;

        public ValueTask<IAsyncDeviceLease> AcquireLeaseAsync(ScannerSessionOwner owner, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ScanOperationResult> ConnectAsync(ScannerSessionOwner owner, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ScanOperationResult> ReconnectAfterPromptAsync(ScannerSessionOwner owner, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ScanOperationResult> DisconnectAsync(string leaseId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ScanOperationResult> ShutdownAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ScanStopResult> StopAsync(string leaseId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(string leaseId, bool enabled, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<TResult> UseSessionAsync<TResult>(string leaseId, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<TResult> RunWithSessionStateAsync<TResult>(string leaseId, ScannerSessionState state, Func<IScanSessionService, Task<TResult>> action, CancellationToken ct)
            => throw new NotSupportedException();

        public ScannerSessionObserverPermission GrantObserverPermission(string observerId, ScannerSessionObserverScope requestedScope, DateTimeOffset grantedAtUtc)
            => new(observerId, requestedScope, grantedAtUtc);

        public void Publish(ScannerDeviceSessionSnapshot snapshot)
        {
            Snapshot = snapshot;
            Targets = new ScanTargetState(true, snapshot.DeviceId, snapshot.DeviceId);
            SnapshotChanged?.Invoke(this, snapshot);
            TargetsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public bool TryEnqueue(Action action)
        {
            action();
            return true;
        }
    }

    private sealed class FakeDebugOutputMirrorService : IDebugOutputMirrorService
    {
        public readonly List<string> Messages = new();

        public void Mirror(string source, string message)
            => Messages.Add($"{source}:{message}");
    }

    private sealed class FakeUsbService : IUsbService
    {
        public FakeUsbService()
        {
            Devices = new[] { new UsbDeviceDto("usb-device-1", 0x1234, 0x5678, 0x0001, "Fake USB Device") };
            Configs = new[] { new UsbConfigDto(1, 1, "Config 1") };
            Interfaces = new[] { new UsbInterfaceDto(2, 0, 2, "Interface 2") };
            BulkInEndpoints = new[] { new UsbEndpointDto(0x81, true, "Bulk", 512, "Bulk IN") };
            BulkOutEndpoints = new[] { new UsbEndpointDto(0x02, false, "Bulk", 512, "Bulk OUT") };
            Session = new FakeUsbBulkDuplexSession();
        }

        public event EventHandler? DevicesChanged;
        public event EventHandler<BulkInStateChangedEventArgs>? BulkInStateChanged;

        public UsbDeviceDto[] Devices { get; }
        public UsbConfigDto[] Configs { get; }
        public UsbInterfaceDto[] Interfaces { get; }
        public UsbEndpointDto[] BulkInEndpoints { get; }
        public UsbEndpointDto[] BulkOutEndpoints { get; }
        public FakeUsbBulkDuplexSession Session { get; }
        public int OpenSessionCallCount { get; private set; }

        public IReadOnlyList<UsbDeviceDto> GetDevices() => Devices;
        public Task RefreshDevicesAsync(CancellationToken ct) => Task.CompletedTask;
        public IReadOnlyList<UsbConfigDto> GetConfigs(string deviceId) => Configs;
        public IReadOnlyList<UsbInterfaceDto> GetInterfaces(string deviceId, byte configId) => Interfaces;
        public IReadOnlyList<UsbEndpointDto> GetBulkInEndpoints(string deviceId, byte configId, byte interfaceId, byte altId) => BulkInEndpoints;
        public IReadOnlyList<UsbEndpointDto> GetBulkOutEndpoints(string deviceId, byte configId, byte interfaceId, byte altId) => BulkOutEndpoints;
        public int? GetBulkInMaxTransferSize(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress) => 4096;
        public void StartUsbWatcher(Action onDeviceChanged)
        {
            onDeviceChanged();
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void StopUsbWatcher()
            => BulkInStateChanged?.Invoke(this, new BulkInStateChangedEventArgs(BulkInState.Stopped));
        public Task StartBulkInAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, int bufferSize, IProgress<(int transferred, byte[] data)> progress, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<int> SendBulkOutAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, byte[] data, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<byte[]> ReadBulkInExactAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, int expectedBytes, int timeoutMs, CancellationToken ct)
            => throw new NotSupportedException();

        public IUsbBulkDuplexSession OpenBulkDuplexSession(string deviceId, byte configId, byte interfaceId, byte altId, byte? inEndpointAddress, byte? outEndpointAddress)
        {
            OpenSessionCallCount++;
            return Session;
        }

        public void StopBulkIn() { }
        public void Dispose() { }
    }

    private sealed class FakeUsbBulkDuplexSession : IUsbBulkDuplexSession
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public byte[]? LastWrite { get; private set; }
        public int WriteCalls { get; private set; }
        public bool DisposeCalled { get; private set; }

        public async Task<(int transferred, byte[] data)> ReadBulkInOnceAsync(int bufferSize, int timeoutMs, CancellationToken ct)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return (0, Array.Empty<byte>());
        }

        public Task<byte[]> ReadBulkInExactAsync(int expectedBytes, int timeoutMs, CancellationToken ct, Action<int, int>? onProgress = null)
            => throw new NotSupportedException();

        public Task<byte[]> ReadBulkInExactMultiBufferedAsync(int expectedBytes, int transferSize, int maxOutstandingTransfers, int timeoutMs, bool rawIoEnabled, CancellationToken ct, Action<int, int>? onProgress = null)
            => throw new NotSupportedException();

        public int? GetBulkInMaxTransferSize() => 4096;

        public Task<int> WriteBulkOutAsync(byte[] data, int timeoutMs, CancellationToken ct)
        {
            LastWrite = data.ToArray();
            WriteCalls++;
            return Task.FromResult(data.Length);
        }

        public void Dispose()
            => DisposeCalled = true;
    }
}
