using PRISM_Utility.Core.Contracts.Services;

namespace PRISM_Utility.Core.Services;

public sealed class ScanSessionServiceFactory : IScanSessionServiceFactory
{
    private readonly IUsbService _usbService;
    private readonly IScanTransferSettingsService _transferSettingsService;

    public ScanSessionServiceFactory(IUsbService usbService, IScanTransferSettingsService transferSettingsService)
    {
        _usbService = usbService;
        _transferSettingsService = transferSettingsService;
    }

    public IScanSessionService CreateSession()
        => new ScanSessionService(_usbService, new ScanProtocolService(), _transferSettingsService);
}
