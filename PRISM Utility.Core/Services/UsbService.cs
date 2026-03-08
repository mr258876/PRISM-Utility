using LibUsbDotNet;
using LibUsbDotNet.Main;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Contracts.Services;
using Windows.Devices.Enumeration;

public class UsbService : IUsbService
{
    public event EventHandler? DevicesChanged;
    private DeviceWatcher? _watcher;

    public event EventHandler<BulkInStateChangedEventArgs>? BulkInStateChanged;

    private readonly object _gate = new();

    private Dictionary<string, UsbRegistry> _byId = new();  // DTO -> Usb Registry
    private List<UsbDeviceDto> _devices = new();
    private List<UsbConfigDto> _configs = new();
    private List<UsbInterfaceDto> _interfaces = new();
    private List<UsbEndpointDto> _endpoints = new();

    public IReadOnlyList<UsbDeviceDto> GetDevices()
    {
        RefreshDevices();
        lock (_gate)
            return _devices.ToList();
    }

    public IReadOnlyList<UsbConfigDto> GetConfigs(string deviceId)
    {
        if (!TryGetRegistry(deviceId, out var reg))
            throw new InvalidOperationException("Device not found (maybe removed).");

        // Device may be plug out after we get registry, so open may fail.
        if (!reg.Open(out UsbDevice dev) || dev is null)
            throw new InvalidOperationException("Open device failed (driver/permission/device removed).");

        lock (_gate)
        {
            _configs.Clear();
            foreach (var cfg in dev.Configs) 
                _configs.Add(new UsbConfigDto(cfg.Descriptor.ConfigID, cfg.Descriptor.InterfaceCount, $"Config {cfg.Descriptor.ConfigID} (Interfaces: {cfg.Descriptor.InterfaceCount})"));
        }
        dev.Close();

        lock (_gate)
            return _configs.ToList();

    }

    public IReadOnlyList<UsbInterfaceDto> GetInterfaces(string deviceId, byte configId)
    {
        if (!TryGetRegistry(deviceId, out var reg))
            throw new InvalidOperationException("Device not found (maybe removed).");

        // Device may be plug out after we get registry, so open may fail.
        if (!reg.Open(out UsbDevice dev) || dev is null)
            throw new InvalidOperationException("Open device failed (driver/permission/device removed).");
        
        lock (_gate)
        {
            _interfaces.Clear();
            var cfg = dev.Configs.FirstOrDefault(c => c.Descriptor.ConfigID == configId);
            if (cfg is null)
                throw new InvalidOperationException("Config not found.");

            foreach (var itf in cfg.InterfaceInfoList)
                _interfaces.Add(new UsbInterfaceDto(itf.Descriptor.InterfaceID, itf.Descriptor.AlternateID, itf.Descriptor.EndpointCount, $"IF {itf.Descriptor.InterfaceID}, Alt {itf.Descriptor.AlternateID}, EPs {itf.Descriptor.EndpointCount}"));
        }
        dev.Close();

        lock (_gate)
            return _interfaces.ToList();
    }

    public IReadOnlyList<UsbEndpointDto> GetBulkInEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
    {
        if (!TryGetRegistry(deviceId, out var reg))
            throw new InvalidOperationException("Device not found (maybe removed).");

        // Device may be plug out after we get registry, so open may fail.
        if (!reg.Open(out UsbDevice dev) || dev is null)
            throw new InvalidOperationException("Open device failed (driver/permission/device removed).");

        lock (_gate)
        {
            _endpoints.Clear();
            var cfg = dev.Configs.FirstOrDefault(c => c.Descriptor.ConfigID == configId);
            if (cfg is null)
                throw new InvalidOperationException("Config not found.");
            var itf = cfg.InterfaceInfoList.FirstOrDefault(i => i.Descriptor.InterfaceID == interfaceId && i.Descriptor.AlternateID == altId);
            if (itf is null)
                throw new InvalidOperationException("Interface not found.");

            foreach (var ep in itf.EndpointInfoList)
                _endpoints.Add(new UsbEndpointDto(
                    ep.Descriptor.EndpointID, 
                    (ep.Descriptor.EndpointID & 0x80) != 0,
                    ((EndpointType)(ep.Descriptor.Attributes & 0x03)).ToString(),
                    ep.Descriptor.MaxPacketSize,
                    $"{((ep.Descriptor.EndpointID & 0x80) != 0 ? "IN" : "OUT")} 0x{ep.Descriptor.EndpointID:X2} {(EndpointType)(ep.Descriptor.Attributes & 0x03)} MaxPkt={ep.Descriptor.MaxPacketSize}"));
        }
        dev.Close();

        lock (_gate)
            return _endpoints.ToList();
    }

    public IReadOnlyList<UsbEndpointDto> GetBulkOutEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
    {
        if (!TryGetRegistry(deviceId, out var reg))
            throw new InvalidOperationException("Device not found (maybe removed).");

        if (!reg.Open(out UsbDevice dev) || dev is null)
            throw new InvalidOperationException("Open device failed (driver/permission/device removed).");

        lock (_gate)
        {
            _endpoints.Clear();
            var cfg = dev.Configs.FirstOrDefault(c => c.Descriptor.ConfigID == configId);
            if (cfg is null)
                throw new InvalidOperationException("Config not found.");
            var itf = cfg.InterfaceInfoList.FirstOrDefault(i => i.Descriptor.InterfaceID == interfaceId && i.Descriptor.AlternateID == altId);
            if (itf is null)
                throw new InvalidOperationException("Interface not found.");

            foreach (var ep in itf.EndpointInfoList)
            {
                var endpointId = ep.Descriptor.EndpointID;
                var isIn = (endpointId & 0x80) != 0;
                var transferType = ((EndpointType)(ep.Descriptor.Attributes & 0x03)).ToString();

                if (!isIn && transferType == EndpointType.Bulk.ToString())
                {
                    _endpoints.Add(new UsbEndpointDto(
                        endpointId,
                        false,
                        transferType,
                        ep.Descriptor.MaxPacketSize,
                        $"OUT 0x{endpointId:X2} {(EndpointType)(ep.Descriptor.Attributes & 0x03)} MaxPkt={ep.Descriptor.MaxPacketSize}"));
                }
            }
        }

        dev.Close();

        lock (_gate)
            return _endpoints.ToList();
    }


    private bool TryGetRegistry(string deviceId, out UsbRegistry reg)
    {
        lock (_gate)
            return _byId.TryGetValue(deviceId, out reg!);
    }

    public UsbService()
    {
        RefreshDevices();
        StartUsbWatcher(() => DevicesChanged?.Invoke(this, EventArgs.Empty));
    }

    private void RefreshDevices()
    {
        var newById = new Dictionary<string, UsbRegistry>();
        var newList = new List<UsbDeviceDto>();

        foreach (UsbRegistry reg in UsbDevice.AllDevices)
        {
            var id = !string.IsNullOrWhiteSpace(reg.SymbolicName)
                ? reg.SymbolicName
                : $"{reg.Vid:X4}:{reg.Pid:X4}:{reg.Rev:X4}:{reg.FullName}"; // fallback

            var friendly = reg[DevicePropertyType.FriendlyName] as string;
            var display = $"{reg.Vid:X4}:{reg.Pid:X4}:{reg.Rev:X4} {friendly ?? reg.FullName}";

            newById[id] = reg;
            newList.Add(new UsbDeviceDto(id, (ushort)reg.Vid, (ushort)reg.Pid, (ushort)reg.Rev, display));
        }

        lock (_gate)
        {
            _byId = newById;
            _devices = newList;
        }
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

        if (!TryGetRegistry(deviceId, out var reg))
            throw new InvalidOperationException("Device not found (maybe removed).");

        return new UsbBulkDuplexSession(reg, configId, interfaceId, altId, inEndpointAddress, outEndpointAddress);
    }

    public void StopBulkIn() => throw new NotImplementedException();

    private void RaiseBulkInStatus(BulkInState state, string? err = null)
        => BulkInStateChanged?.Invoke(this, new BulkInStateChangedEventArgs(state, err));

    public void StartUsbWatcher(Action onDeviceChanged)
    {
        if (_watcher != null)
        {
            throw new InvalidOperationException("USB watcher is already running.");
        }

        // GUID_DEVINTERFACE_USB_DEVICE
        const string usbInterfaceGuid = "A5DCBF10-6530-11D2-901F-00C04FB951ED";

        var aqs = $"System.Devices.InterfaceClassGuid:=\"{{{usbInterfaceGuid}}}\"";

        _watcher = DeviceInformation.CreateWatcher(
            aqsFilter: aqs,
            additionalProperties: null,
            kind: DeviceInformationKind.DeviceInterface);

        _watcher.Added += (_, __) => onDeviceChanged();
        _watcher.Removed += (_, __) => onDeviceChanged();
        _watcher.Updated += (_, __) => onDeviceChanged();

        _watcher.Start();
    }

    public void StopUsbWatcher()
    {
        if (_watcher is null)
            return;

        try
        {
            _watcher.Stop();
        }
        catch
        {
            /* ignore */
        }

        _watcher = null;
    }
    public void Dispose()
    {
        StopUsbWatcher();
    }

    private sealed class UsbBulkDuplexSession : IUsbBulkDuplexSession
    {
        private readonly UsbDevice _dev;
        private readonly byte _interfaceId;
        private readonly UsbEndpointReader? _reader;
        private readonly UsbEndpointWriter? _writer;
        private readonly object _readLock = new();
        private readonly object _writeLock = new();
        private bool _disposed;

        public UsbBulkDuplexSession(UsbRegistry reg, byte configId, byte interfaceId, byte altId, byte? inEndpointAddress, byte? outEndpointAddress)
        {
            _interfaceId = interfaceId;

            if (!reg.Open(out UsbDevice dev) || dev is null)
                throw new InvalidOperationException("Open device failed (driver/permission/device removed).");

            _dev = dev;

            try
            {
                if (_dev is IUsbDevice wholeUsb)
                {
                    wholeUsb.SetConfiguration(configId);
                    wholeUsb.ClaimInterface(interfaceId);
                }

                if (inEndpointAddress is not null)
                    _reader = _dev.OpenEndpointReader((ReadEndpointID)inEndpointAddress.Value, 0, EndpointType.Bulk);

                if (outEndpointAddress is not null)
                    _writer = _dev.OpenEndpointWriter((WriteEndpointID)outEndpointAddress.Value, EndpointType.Bulk);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public Task<(int transferred, byte[] data)> ReadBulkInOnceAsync(int bufferSize, int timeoutMs, CancellationToken ct)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (timeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            if (_reader is null)
                throw new InvalidOperationException("Bulk IN endpoint is not opened for this session.");

            ThrowIfDisposed();

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                lock (_readLock)
                {
                    var buf = new byte[bufferSize];
                    var ec = _reader.Read(buf, timeoutMs, out var transferred);
                    if (ec == ErrorCode.IoTimedOut && transferred <= 0)
                        return (0, Array.Empty<byte>());
                    if (ec == ErrorCode.IoTimedOut)
                    {
                        var timedOutCopy = new byte[transferred];
                        Buffer.BlockCopy(buf, 0, timedOutCopy, 0, transferred);
                        return (transferred, timedOutCopy);
                    }
                    if (ec != ErrorCode.None)
                        throw new IOException($"Read error: {ec}");

                    var copy = new byte[transferred];
                    Buffer.BlockCopy(buf, 0, copy, 0, transferred);
                    return (transferred, copy);
                }
            }, ct);
        }

        public Task<byte[]> ReadBulkInExactAsync(int expectedBytes, int timeoutMs, CancellationToken ct)
        {
            if (expectedBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedBytes));
            if (timeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            if (_reader is null)
                throw new InvalidOperationException("Bulk IN endpoint is not opened for this session.");

            ThrowIfDisposed();

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                lock (_readLock)
                {
                    const int extraReadSlackBytes = 64 * 1024;
                    var readBufferSize = checked(expectedBytes + extraReadSlackBytes);
                    var buffer = new byte[readBufferSize];
                    var ec = _reader.Read(buffer, timeoutMs, out var transferred);
                    if (ec != ErrorCode.None)
                        throw new IOException($"Read error: {ec}");
                    if (transferred > expectedBytes)
                        throw new IOException($"Long read: expected {expectedBytes}, actual {transferred}");
                    if (transferred != expectedBytes)
                        throw new IOException($"Short read: expected {expectedBytes}, actual {transferred}");

                    var exact = new byte[expectedBytes];
                    Buffer.BlockCopy(buffer, 0, exact, 0, expectedBytes);
                    return exact;
                }
            }, ct);
        }

        public Task<int> WriteBulkOutAsync(byte[] data, int timeoutMs, CancellationToken ct)
        {
            if (data.Length == 0)
                return Task.FromResult(0);
            if (timeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            if (_writer is null)
                throw new InvalidOperationException("Bulk OUT endpoint is not opened for this session.");

            ThrowIfDisposed();

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                lock (_writeLock)
                {
                    var ec = _writer.Write(data, timeoutMs, out var transferred);
                    if (ec != ErrorCode.None)
                        throw new IOException($"Write error: {ec}");
                    return transferred;
                }
            }, ct);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _reader?.Dispose();
            }
            catch { }

            try
            {
                _writer?.Dispose();
            }
            catch { }

            try
            {
                if (_dev is IUsbDevice wholeUsb)
                    wholeUsb.ReleaseInterface(_interfaceId);
            }
            catch { }

            try
            {
                _dev.Close();
            }
            catch { }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UsbBulkDuplexSession));
        }
    }
}
