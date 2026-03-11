using PRISM_Utility.Core.Contracts.Models;

namespace PRISM_Utility.Core.Contracts.Services;
public interface IUsbService : IDisposable
{
    event EventHandler DevicesChanged;
    event EventHandler<BulkInStateChangedEventArgs> BulkInStateChanged;

    IReadOnlyList<UsbDeviceDto> GetDevices();
    IReadOnlyList<UsbConfigDto> GetConfigs(string deviceId);
    IReadOnlyList<UsbInterfaceDto> GetInterfaces(string deviceId, byte configId);
    IReadOnlyList<UsbEndpointDto> GetBulkInEndpoints(string deviceId, byte configId, byte interfaceId, byte altId);
    IReadOnlyList<UsbEndpointDto> GetBulkOutEndpoints(string deviceId, byte configId, byte interfaceId, byte altId);
    int? GetBulkInMaxTransferSize(string deviceId, byte configId, byte interfaceId, byte altId, byte endpointAddress);

    void StartUsbWatcher(Action onDeviceChanged);
    void StopUsbWatcher();

    Task StartBulkInAsync(
        string deviceId,
        byte configId,
        byte interfaceId,
        byte altId,
        byte endpointAddress,
        int bufferSize,
        IProgress<(int transferred, byte[] data)> progress,
        CancellationToken ct);

    Task<int> SendBulkOutAsync(
        string deviceId,
        byte configId,
        byte interfaceId,
        byte altId,
        byte endpointAddress,
        byte[] data,
        CancellationToken ct);

    Task<byte[]> ReadBulkInExactAsync(
        string deviceId,
        byte configId,
        byte interfaceId,
        byte altId,
        byte endpointAddress,
        int expectedBytes,
        int timeoutMs,
        CancellationToken ct);

    IUsbBulkDuplexSession OpenBulkDuplexSession(
        string deviceId,
        byte configId,
        byte interfaceId,
        byte altId,
        byte? inEndpointAddress,
        byte? outEndpointAddress);

    void StopBulkIn();
}

public interface IUsbBulkDuplexSession : IDisposable
{
    Task<(int transferred, byte[] data)> ReadBulkInOnceAsync(int bufferSize, int timeoutMs, CancellationToken ct);
    Task<byte[]> ReadBulkInExactAsync(int expectedBytes, int timeoutMs, CancellationToken ct, Action<int, int>? onProgress = null);
    Task<byte[]> ReadBulkInExactMultiBufferedAsync(int expectedBytes, int transferSize, int maxOutstandingTransfers, int timeoutMs, bool rawIoEnabled, CancellationToken ct, Action<int, int>? onProgress = null);
    int? GetBulkInMaxTransferSize();
    Task<int> WriteBulkOutAsync(byte[] data, int timeoutMs, CancellationToken ct);
}

public enum BulkInState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

public sealed record BulkInStateChangedEventArgs(BulkInState State, string? Error = null);
