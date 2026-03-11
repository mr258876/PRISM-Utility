using LibUsbDotNet;
using LibUsbDotNet.Main;
using PRISM_Utility.Core.Contracts.Models;
using Windows.Devices.Enumeration;

internal sealed class UsbDeviceCatalog : IDisposable
{
    private readonly object _gate = new();
    private DeviceWatcher? _watcher;
    private Dictionary<string, UsbRegistry> _byId = new();
    private List<UsbDeviceDto> _devices = new();

    public UsbDeviceCatalog()
    {
        RefreshDevices();
    }

    public IReadOnlyList<UsbDeviceDto> GetDevices()
    {
        RefreshDevices();
        lock (_gate)
            return _devices.ToList();
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
        _watcher.Added += (_, __) => onDeviceChanged();
        _watcher.Removed += (_, __) => onDeviceChanged();
        _watcher.Updated += (_, __) => onDeviceChanged();
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
        catch
        {
        }

        _watcher = null;
    }

    public void Dispose()
        => StopWatcher();

    private void RefreshDevices()
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

        lock (_gate)
        {
            _byId = newById;
            _devices = newList;
        }
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
