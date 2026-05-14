using System.Diagnostics;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanDeviceSettingsService : IScanDeviceSettingsService
{
    private const string SettingsKey = "ScanDeviceSettings";

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;

    public ScanDeviceSettings DefaultSettings => ScanDeviceSettings.CreateDefault();

    public ScanDeviceSettings Settings { get; private set; } = ScanDeviceSettings.CreateDefault();

    public ScanDeviceSettingsService(ILocalSettingsService localSettingsService)
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

            ScanDeviceSettings? saved = null;
            try
            {
                saved = await _localSettingsService.ReadSettingAsync<ScanDeviceSettings>(SettingsKey);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScanDeviceSettingsService] Failed to read saved device settings: {ex}");
                Trace.WriteLine($"[ScanDeviceSettingsService] Failed to read saved device settings: {ex}");
                saved = null;
            }

            Settings = NormalizeOrDefault(saved);
            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task SetSettingsAsync(ScanDeviceSettings settings)
    {
        await InitializeAsync();
        var normalized = NormalizeOrDefault(settings);
        if (Settings == normalized)
            return;

        Settings = normalized;
        await _localSettingsService.SaveSettingAsync(SettingsKey, normalized);
    }

    private ScanDeviceSettings NormalizeOrDefault(ScanDeviceSettings? settings)
        => (settings ?? DefaultSettings).Normalize();
}
