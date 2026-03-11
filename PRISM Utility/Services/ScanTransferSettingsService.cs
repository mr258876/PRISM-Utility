using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Configuration;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Services;

public sealed class ScanTransferSettingsService : IScanTransferSettingsService
{
    private const string BulkInReadModeKey = "ScanBulkInReadMode";
    private const string RequestBytesKey = "ScanBulkInRequestBytes";
    private const string OutstandingReadsKey = "ScanBulkInOutstandingReads";
    private const string TimeoutMsKey = "ScanBulkInTimeoutMs";
    private const string RawIoEnabledKey = "ScanBulkInRawIoEnabled";

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;

    public event EventHandler? BulkInReadModeChanged;

    public ScanBulkInReadMode BulkInReadMode => Settings.ReadMode;

    public ScanBulkInTransferOptions DefaultSettings => ScanTransferDefaults.Settings;

    public ScanBulkInTransferOptions Settings { get; private set; } = ScanTransferDefaults.Settings;

    public ScanTransferSettingsService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _initializeGate.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            var savedMode = await _localSettingsService.ReadSettingAsync<ScanBulkInReadMode?>(BulkInReadModeKey);
            var requestBytes = await _localSettingsService.ReadSettingAsync<int?>(RequestBytesKey);
            var outstandingReads = await _localSettingsService.ReadSettingAsync<int?>(OutstandingReadsKey);
            var timeoutMs = await _localSettingsService.ReadSettingAsync<int?>(TimeoutMsKey);
            var rawIoEnabled = await _localSettingsService.ReadSettingAsync<bool?>(RawIoEnabledKey);

            Settings = Settings with
            {
                ReadMode = savedMode ?? Settings.ReadMode,
                RequestBytes = requestBytes is > 0 ? requestBytes.Value : Settings.RequestBytes,
                OutstandingReads = outstandingReads is > 0 ? outstandingReads.Value : Settings.OutstandingReads,
                TimeoutMs = timeoutMs is > 0 ? timeoutMs.Value : Settings.TimeoutMs,
                RawIoEnabled = rawIoEnabled ?? Settings.RawIoEnabled
            };

            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task SetBulkInReadModeAsync(ScanBulkInReadMode mode)
        => await SetSettingsAsync(Settings with { ReadMode = mode });

    public async Task SetSettingsAsync(ScanBulkInTransferOptions settings)
    {
        await InitializeAsync();
        if (Settings == settings)
            return;

        Settings = settings;

        await _localSettingsService.SaveSettingAsync(BulkInReadModeKey, settings.ReadMode);
        await _localSettingsService.SaveSettingAsync(RequestBytesKey, settings.RequestBytes);
        await _localSettingsService.SaveSettingAsync(OutstandingReadsKey, settings.OutstandingReads);
        await _localSettingsService.SaveSettingAsync(TimeoutMsKey, settings.TimeoutMs);
        await _localSettingsService.SaveSettingAsync(RawIoEnabledKey, settings.RawIoEnabled);

        BulkInReadModeChanged?.Invoke(this, EventArgs.Empty);
    }
}
