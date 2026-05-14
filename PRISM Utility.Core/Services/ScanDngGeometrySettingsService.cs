using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanDngGeometrySettingsService : IScanDngGeometrySettingsService
{
    private const string SettingsKey = "ScanDngGeometrySettings";

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;

    public ScanDngGeometrySettings DefaultSettings => ScanDngGeometrySettings.CreateDefault();

    public ScanDngGeometrySettings Settings { get; private set; } = ScanDngGeometrySettings.CreateDefault();

    public ScanDngGeometrySettingsService(ILocalSettingsService localSettingsService)
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

            ScanDngGeometrySettings? saved = null;
            try
            {
                saved = await _localSettingsService.ReadSettingAsync<ScanDngGeometrySettings>(SettingsKey);
            }
            catch
            {
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

    public async Task SetSettingsAsync(ScanDngGeometrySettings settings)
    {
        await InitializeAsync();
        var normalized = NormalizeOrDefault(settings);
        if (Settings == normalized)
            return;

        Settings = normalized;
        await _localSettingsService.SaveSettingAsync(SettingsKey, normalized);
    }

    private ScanDngGeometrySettings NormalizeOrDefault(ScanDngGeometrySettings? settings)
    {
        var normalized = (settings ?? DefaultSettings).Normalize();
        return normalized.MaskedBlackRanges.Length == 0 ? DefaultSettings.Normalize() : normalized;
    }
}
