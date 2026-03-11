using LibUsbDotNet;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Contracts.Services;

public class UsbService : IUsbService
{
    public event EventHandler? DevicesChanged;
    public event EventHandler<BulkInStateChangedEventArgs>? BulkInStateChanged;
    private readonly UsbDeviceCatalog _catalog;

    public UsbService()
    {
        _catalog = new UsbDeviceCatalog();
        _catalog.StartWatcher(() => DevicesChanged?.Invoke(this, EventArgs.Empty));
    }

    public IReadOnlyList<UsbDeviceDto> GetDevices()
        => _catalog.GetDevices();

    public IReadOnlyList<UsbConfigDto> GetConfigs(string deviceId)
        => _catalog.GetConfigs(deviceId);

    public IReadOnlyList<UsbInterfaceDto> GetInterfaces(string deviceId, byte configId)
        => _catalog.GetInterfaces(deviceId, configId);

    public IReadOnlyList<UsbEndpointDto> GetBulkInEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
        => _catalog.GetBulkInEndpoints(deviceId, configId, interfaceId, altId);

    public IReadOnlyList<UsbEndpointDto> GetBulkOutEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
        => _catalog.GetBulkOutEndpoints(deviceId, configId, interfaceId, altId);

    public int? GetBulkInMaxTransferSize(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress)
    {
        using var session = new UsbBulkDuplexSession(_catalog.GetRegistry(deviceId), configId, interfaceId, altId, endpointAddress, null);
        return session.GetBulkInMaxTransferSize();
    }

    public async Task StartBulkInAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, int bufferSize, IProgress<(int transferred, byte[] data)> progress, CancellationToken ct)
    {
        RaiseBulkInStatus(BulkInState.Starting);

        try
        {
            using var session = OpenBulkDuplexSession(deviceId, configId, interfaceId, altId, endpointAddress, null);

            await Task.Run(() =>
            {
                RaiseBulkInStatus(BulkInState.Running);

                while (!ct.IsCancellationRequested)
                {
                    var chunk = session.ReadBulkInOnceAsync(bufferSize, 2000, ct).GetAwaiter().GetResult();
                    if (chunk.transferred > 0)
                    {
                        progress.Report(chunk);
                    }
                }

                RaiseBulkInStatus(BulkInState.Stopping);
            }, ct);
        }
        catch (OperationCanceledException)
        {
            RaiseBulkInStatus(BulkInState.Stopping);
        }
        finally
        {
            RaiseBulkInStatus(BulkInState.Stopped);
        }

    }

    public async Task<int> SendBulkOutAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, byte[] data, CancellationToken ct)
    {
        if (data.Length == 0)
            return 0;

        using var session = OpenBulkDuplexSession(deviceId, configId, interfaceId, altId, null, endpointAddress);
        return await session.WriteBulkOutAsync(data, 3000, ct);
    }

    public async Task<byte[]> ReadBulkInExactAsync(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress, int expectedBytes, int timeoutMs, CancellationToken ct)
    {
        using var session = OpenBulkDuplexSession(deviceId, configId, interfaceId, altId, endpointAddress, null);
        return await session.ReadBulkInExactAsync(expectedBytes, timeoutMs, ct);
    }

    public IUsbBulkDuplexSession OpenBulkDuplexSession(string deviceId, byte configId, byte interfaceId, byte altId, byte? inEndpointAddress, byte? outEndpointAddress)
    {
        if (inEndpointAddress is null && outEndpointAddress is null)
            throw new ArgumentException("At least one endpoint must be provided.");

        return new UsbBulkDuplexSession(_catalog.GetRegistry(deviceId), configId, interfaceId, altId, inEndpointAddress, outEndpointAddress);
    }

    public void StopBulkIn() => throw new NotImplementedException();

    private void RaiseBulkInStatus(BulkInState state, string? err = null)
        => BulkInStateChanged?.Invoke(this, new BulkInStateChangedEventArgs(state, err));

    public void StartUsbWatcher(Action onDeviceChanged)
        => _catalog.StartWatcher(onDeviceChanged);

    public void StopUsbWatcher()
        => _catalog.StopWatcher();
    public void Dispose()
    {
        _catalog.Dispose();
    }
}
