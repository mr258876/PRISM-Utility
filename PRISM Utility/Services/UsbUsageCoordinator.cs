using PRISM_Utility.Contracts.Services;

namespace PRISM_Utility.Services;

public sealed class UsbUsageCoordinator : IUsbUsageCoordinator
{
    private readonly object _gate = new();
    private bool _isScanDebugInUse;
    private bool _isUsbDebugInUse;

    public bool IsScanDebugInUse
    {
        get
        {
            lock (_gate)
                return _isScanDebugInUse;
        }
    }

    public bool IsUsbDebugInUse
    {
        get
        {
            lock (_gate)
                return _isUsbDebugInUse;
        }
    }

    public void SetScanDebugInUse(bool inUse)
    {
        lock (_gate)
            _isScanDebugInUse = inUse;
    }

    public void SetUsbDebugInUse(bool inUse)
    {
        lock (_gate)
            _isUsbDebugInUse = inUse;
    }
}
