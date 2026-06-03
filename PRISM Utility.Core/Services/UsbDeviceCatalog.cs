using LibUsbDotNet;
using LibUsbDotNet.Main;
using PRISM_Utility.Core.Contracts.Models;
using System.Diagnostics;
using Windows.Devices.Enumeration;

internal sealed class UsbDeviceCatalog : IDisposable
{
    private readonly object _gate = new();
    private readonly object _refreshRequestGate = new();
    private readonly Func<UsbDeviceCatalogSnapshot> _readSnapshot;
    private DeviceWatcher? _watcher;
    private Dictionary<string, UsbRegistry> _byId = new();
    private List<UsbDeviceDto> _devices = new();
    private Action? _pendingRefreshCallbacks;
    private bool _refreshRunning;
    private int _disposed;

    public UsbDeviceCatalog() : this(ReadUsbDeviceSnapshot)
    {
    }

    internal UsbDeviceCatalog(Func<UsbDeviceCatalogSnapshot> readSnapshot)
    {
        _readSnapshot = readSnapshot;
    }

    public IReadOnlyList<UsbDeviceDto> GetDevices()
    {
        lock (_gate)
            return _devices.ToList();
    }

    internal Task RefreshAsync(CancellationToken ct)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(UsbDeviceCatalog));

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => completion.TrySetCanceled(ct));
        if (!RequestRefresh(() => completion.TrySetResult()))
        {
            registration.Dispose();
            throw new ObjectDisposedException(nameof(UsbDeviceCatalog));
        }

        return AwaitRefreshAsync(completion.Task, registration);
    }

    internal bool RequestRefresh(Action? onRefreshed = null)
    {
        if (IsDisposed)
            return false;

        lock (_refreshRequestGate)
        {
            if (onRefreshed is not null)
                _pendingRefreshCallbacks += onRefreshed;

            if (_refreshRunning)
                return true;

            _refreshRunning = true;
        }

        _ = Task.Run(RunRefreshLoop);
        return true;
    }

    private static async Task AwaitRefreshAsync(Task refreshTask, CancellationTokenRegistration registration)
    {
        try
        {
            await refreshTask;
        }
        finally
        {
            registration.Dispose();
        }
    }

    private void RunRefreshLoop()
    {
        while (!IsDisposed)
        {
            Action? callbacks;
            lock (_refreshRequestGate)
            {
                callbacks = _pendingRefreshCallbacks;
                _pendingRefreshCallbacks = null;
            }

            try
            {
                var snapshot = _readSnapshot();
                lock (_gate)
                {
                    _byId = snapshot.Registries.ToDictionary(pair => pair.Key, pair => pair.Value);
                    _devices = snapshot.Devices.ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UsbDeviceCatalog] Device refresh failed: {ex}");
            }

            if (!IsDisposed)
                callbacks?.Invoke();

            lock (_refreshRequestGate)
            {
                if (_pendingRefreshCallbacks is null)
                {
                    _refreshRunning = false;
                    return;
                }
            }
        }

        lock (_refreshRequestGate)
        {
            _pendingRefreshCallbacks = null;
            _refreshRunning = false;
        }
    }

    public IReadOnlyList<UsbConfigDto> GetConfigs(string deviceId)
    {
        using var dev = OpenDevice(deviceId);
        return dev.Configs
            .Select(cfg => new UsbConfigDto(cfg.Descriptor.ConfigID, cfg.Descriptor.InterfaceCount, $"Config {cfg.Descriptor.ConfigID} (Interfaces: {cfg.Descriptor.InterfaceCount})"))
            .ToList();
    }

    public IReadOnlyList<UsbInterfaceDto> GetInterfaces(string deviceId, byte configId)
    {
        using var dev = OpenDevice(deviceId);
        var cfg = dev.Configs.FirstOrDefault(c => c.Descriptor.ConfigID == configId)
            ?? throw new InvalidOperationException("Config not found.");

        return cfg.InterfaceInfoList
            .Select(itf => new UsbInterfaceDto(itf.Descriptor.InterfaceID, itf.Descriptor.AlternateID, itf.Descriptor.EndpointCount, $"IF {itf.Descriptor.InterfaceID}, Alt {itf.Descriptor.AlternateID}, EPs {itf.Descriptor.EndpointCount}"))
            .ToList();
    }

    public IReadOnlyList<UsbEndpointDto> GetBulkInEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
        => GetEndpoints(deviceId, configId, interfaceId, altId, isIn: true);

    public IReadOnlyList<UsbEndpointDto> GetBulkOutEndpoints(string deviceId, byte configId, byte interfaceId, byte altId)
        => GetEndpoints(deviceId, configId, interfaceId, altId, isIn: false);

    public UsbRegistry GetRegistry(string deviceId)
    {
        lock (_gate)
        {
            if (_byId.TryGetValue(deviceId, out var reg))
                return reg;
        }

        throw new InvalidOperationException("Device not found (maybe removed).");
    }

    public void StartWatcher(Action onDeviceChanged)
    {
        if (_watcher != null)
            throw new InvalidOperationException("USB watcher is already running.");

        const string usbInterfaceGuid = "A5DCBF10-6530-11D2-901F-00C04FB951ED";
        var aqs = $"System.Devices.InterfaceClassGuid:=\"{{{usbInterfaceGuid}}}\"";

        _watcher = DeviceInformation.CreateWatcher(aqs, null, DeviceInformationKind.DeviceInterface);
        _watcher.Added += (_, __) => RequestRefresh(onDeviceChanged);
        _watcher.Removed += (_, __) => RequestRefresh(onDeviceChanged);
        _watcher.Updated += (_, __) => RequestRefresh(onDeviceChanged);
        _watcher.Start();
    }

    public void StopWatcher()
    {
        if (_watcher is null)
            return;

        try
        {
            _watcher.Stop();
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"[UsbDeviceCatalog] Failed to stop USB watcher: {ex}");
        }

        _watcher = null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        StopWatcher();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private static UsbDeviceCatalogSnapshot ReadUsbDeviceSnapshot()
    {
        var newById = new Dictionary<string, UsbRegistry>();
        var newList = new List<UsbDeviceDto>();

        foreach (UsbRegistry reg in UsbDevice.AllDevices)
        {
            var id = !string.IsNullOrWhiteSpace(reg.SymbolicName)
                ? reg.SymbolicName
                : $"{reg.Vid:X4}:{reg.Pid:X4}:{reg.Rev:X4}:{reg.FullName}";

            var friendly = reg[DevicePropertyType.FriendlyName] as string;
            var display = $"{reg.Vid:X4}:{reg.Pid:X4}:{reg.Rev:X4} {friendly ?? reg.FullName}";

            newById[id] = reg;
            newList.Add(new UsbDeviceDto(id, (ushort)reg.Vid, (ushort)reg.Pid, (ushort)reg.Rev, display));
        }

        return new UsbDeviceCatalogSnapshot(newById, newList);
    }

    private UsbDevice OpenDevice(string deviceId)
    {
        var reg = GetRegistry(deviceId);
        if (!reg.Open(out UsbDevice dev) || dev is null)
            throw new InvalidOperationException("Open device failed (driver/permission/device removed).");

        return dev;
    }

    private IReadOnlyList<UsbEndpointDto> GetEndpoints(string deviceId, byte configId, byte interfaceId, byte altId, bool isIn)
    {
        using var dev = OpenDevice(deviceId);
        var cfg = dev.Configs.FirstOrDefault(c => c.Descriptor.ConfigID == configId)
            ?? throw new InvalidOperationException("Config not found.");
        var itf = cfg.InterfaceInfoList.FirstOrDefault(i => i.Descriptor.InterfaceID == interfaceId && i.Descriptor.AlternateID == altId)
            ?? throw new InvalidOperationException("Interface not found.");

        return itf.EndpointInfoList
            .Select(ep => new UsbEndpointDto(
                ep.Descriptor.EndpointID,
                (ep.Descriptor.EndpointID & 0x80) != 0,
                ((EndpointType)(ep.Descriptor.Attributes & 0x03)).ToString(),
                ep.Descriptor.MaxPacketSize,
                $"{(((ep.Descriptor.EndpointID & 0x80) != 0) ? "IN" : "OUT")} 0x{ep.Descriptor.EndpointID:X2} {(EndpointType)(ep.Descriptor.Attributes & 0x03)} MaxPkt={ep.Descriptor.MaxPacketSize}"))
            .Where(ep => ep.IsIn == isIn && ep.TransferType == EndpointType.Bulk.ToString())
            .ToList();
    }
}

internal sealed record UsbDeviceCatalogSnapshot(
    IReadOnlyDictionary<string, UsbRegistry> Registries,
    IReadOnlyList<UsbDeviceDto> Devices);
