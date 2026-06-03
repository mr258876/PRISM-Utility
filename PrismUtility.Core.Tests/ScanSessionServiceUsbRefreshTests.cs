using PRISM_Utility.Core.Configuration;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UsbRefresh")]
public sealed class ScanSessionServiceUsbRefreshTests
{
    [Fact]
    public async Task ConnectAsync_RefreshesUsbDevicesBeforeCheckingTargets()
    {
        using var usb = new RefreshingUsbService();
        var service = new ScanSessionService(usb, new ScanProtocolService(), new FakeTransferSettingsService());

        Assert.False(service.Targets.IsDevicesPresent);

        var result = await service.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, usb.RefreshDevicesCallCount);
        Assert.Equal(2, usb.OpenSessionCallCount);
        Assert.True(service.Targets.IsDevicesPresent);
    }

    private sealed class RefreshingUsbService : IUsbService
    {
        private bool _refreshed;

        public event EventHandler? DevicesChanged;
        public event EventHandler<BulkInStateChangedEventArgs>? BulkInStateChanged;

        public int RefreshDevicesCallCount { get; private set; }
        public int OpenSessionCallCount { get; private set; }

        public IReadOnlyList<UsbDeviceDto> GetDevices()
        {
            if (!_refreshed)
                return [];

            return
            [
                new("scanner-in", ScanDebugConstants.BulkInVid, ScanDebugConstants.BulkInPid, 0x0001, "Scanner IN"),
                new("scanner-out", ScanDebugConstants.BulkOutVid, ScanDebugConstants.BulkOutPid, 0x0001, "Scanner OUT")
            ];
        }

        public Task RefreshDevicesAsync(CancellationToken ct)
        {
            RefreshDevicesCallCount++;
            _refreshed = true;
            DevicesChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public IReadOnlyList<UsbConfigDto> GetConfigs(string deviceId)
            => [new(1, 1, "Config 1")];

        public IReadOnlyList<UsbInterfaceDto> GetInterfaces(string deviceId, byte configId)
            => [new(2, 0, 2, "Interface 2")];

        public IReadOnlyList<UsbEndpointDto> GetBulkInEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
            => deviceId == "scanner-in" ? [new(ScanDebugConstants.BulkInEndpoint, true, "Bulk", 512, "Bulk IN")] : [new(ScanDebugConstants.BulkOutAckEndpoint, true, "Bulk", 512, "ACK IN")];

        public IReadOnlyList<UsbEndpointDto> GetBulkOutEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
            => [new(ScanDebugConstants.BulkOutEndpoint, false, "Bulk", 512, "Bulk OUT")];

        public int? GetBulkInMaxTransferSize(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress)
            => 4096;

        public void StartUsbWatcher(Action onDeviceChanged)
            => onDeviceChanged();

        public void StopUsbWatcher()
        {
            BulkInStateChanged?.Invoke(this, new BulkInStateChangedEventArgs(BulkInState.Stopped));
        }

        public Task StartBulkInAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, int bufferSize, IProgress<(int transferred, byte[] data)> progress, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<int> SendBulkOutAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, byte[] data, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<byte[]> ReadBulkInExactAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, int expectedBytes, int timeoutMs, CancellationToken ct)
            => throw new NotSupportedException();

        public IUsbBulkDuplexSession OpenBulkDuplexSession(string deviceId, byte configId, byte interfaceId, byte altId, byte? inEndpointAddress, byte? outEndpointAddress)
        {
            OpenSessionCallCount++;
            return new FakeUsbBulkDuplexSession();
        }

        public void StopBulkIn()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeUsbBulkDuplexSession : IUsbBulkDuplexSession
    {
        public Task<(int transferred, byte[] data)> ReadBulkInOnceAsync(int bufferSize, int timeoutMs, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<byte[]> ReadBulkInExactAsync(int expectedBytes, int timeoutMs, CancellationToken ct, Action<int, int>? onProgress = null)
            => throw new NotSupportedException();

        public Task<byte[]> ReadBulkInExactMultiBufferedAsync(int expectedBytes, int transferSize, int maxOutstandingTransfers, int timeoutMs, bool rawIoEnabled, CancellationToken ct, Action<int, int>? onProgress = null)
            => throw new NotSupportedException();

        public int? GetBulkInMaxTransferSize() => 4096;

        public Task<int> WriteBulkOutAsync(byte[] data, int timeoutMs, CancellationToken ct)
            => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private sealed class FakeTransferSettingsService : IScanTransferSettingsService
    {
        public event EventHandler? BulkInReadModeChanged;

        public ScanBulkInReadMode BulkInReadMode => Settings.ReadMode;
        public ScanBulkInTransferOptions DefaultSettings => ScanTransferDefaults.Settings;
        public ScanBulkInTransferOptions Settings => ScanTransferDefaults.Settings;

        public Task InitializeAsync()
        {
            _ = BulkInReadModeChanged;
            return Task.CompletedTask;
        }

        public Task SetBulkInReadModeAsync(ScanBulkInReadMode mode)
            => Task.CompletedTask;

        public Task SetSettingsAsync(ScanBulkInTransferOptions settings)
            => Task.CompletedTask;
    }
}
