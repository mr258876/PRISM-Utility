using System.Diagnostics;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanColorManagementSettingsService : IScanColorManagementSettingsService
{
    private const int SchemaVersion = 1;
    private const string DocumentKey = "ScanColorManagementSettingsDocument";
    private const string EnabledKey = "ScanColorManagementEnabled";
    private const string RedWavelengthNmKey = "ScanColorManagementRedWavelengthNm";
    private const string GreenWavelengthNmKey = "ScanColorManagementGreenWavelengthNm";
    private const string BlueWavelengthNmKey = "ScanColorManagementBlueWavelengthNm";
    private const string OutputGammaKey = "ScanColorManagementOutputGamma";
    private const string TargetWhitePointModeKey = "ScanColorManagementTargetWhitePointMode";
    private const string ManualWhitePointColorTemperatureKKey = "ScanColorManagementManualWhitePointColorTemperatureK";

    private static readonly string[] LegacyKeys = [EnabledKey, RedWavelengthNmKey, GreenWavelengthNmKey, BlueWavelengthNmKey, OutputGammaKey, TargetWhitePointModeKey, ManualWhitePointColorTemperatureKKey];

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private bool _isInitialized;

    public ScanColorManagementOptions DefaultSettings => ScanColorManagementOptions.CreateDefault();

    public ScanColorManagementOptions Settings { get; private set; } = ScanColorManagementOptions.CreateDefault();

    public ScanColorManagementSettingsService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _settingsGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InitializeUnderGateAsync();
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task SetSettingsAsync(ScanColorManagementOptions settings, CancellationToken cancellationToken = default)
    {
        await _settingsGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InitializeUnderGateAsync();
            var normalized = NormalizeOrDefault(settings);
            if (Settings == normalized)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            await SaveDocumentAsync(normalized);
            Settings = normalized;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private async Task InitializeUnderGateAsync()
    {
        if (_isInitialized)
            return;

        VersionedSettingsDocument<ScanColorManagementOptions>? document;
        try
        {
            document = await _localSettingsService.ReadSettingAsync<VersionedSettingsDocument<ScanColorManagementOptions>>(DocumentKey);
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Failed to read versioned color settings: {ex}");
            _isInitialized = true;
            return;
        }

        if (document is not null)
        {
            var payload = document.Payload;
            if (document.SchemaVersion != SchemaVersion || payload is null || !IsValidDocumentPayload(payload))
            {
                WriteDiagnostic($"Ignoring color settings document schema {document.SchemaVersion}.");
                _isInitialized = true;
                return;
            }

            Settings = payload;
            _isInitialized = true;
            return;
        }

        var legacy = await ReadLegacySettingsAsync();
        await SaveDocumentAsync(legacy.Settings);
        if (legacy.HasValues)
            await CleanupLegacySettingsAsync();

        Settings = legacy.Settings;
        _isInitialized = true;
    }

    private async Task<(ScanColorManagementOptions Settings, bool HasValues)> ReadLegacySettingsAsync()
    {
        var enabled = await ReadLegacyAsync<bool?>(EnabledKey);
        var redWavelength = await ReadLegacyAsync<double?>(RedWavelengthNmKey);
        var greenWavelength = await ReadLegacyAsync<double?>(GreenWavelengthNmKey);
        var blueWavelength = await ReadLegacyAsync<double?>(BlueWavelengthNmKey);
        var outputGamma = await ReadLegacyAsync<double?>(OutputGammaKey);
        var targetWhitePointMode = await ReadLegacyAsync<ScanTargetWhitePointMode?>(TargetWhitePointModeKey);
        var manualWhitePointColorTemperature = await ReadLegacyAsync<double?>(ManualWhitePointColorTemperatureKKey);
        var hasValues = enabled.HasValue || redWavelength.HasValue || greenWavelength.HasValue || blueWavelength.HasValue || outputGamma.HasValue || targetWhitePointMode.HasValue || manualWhitePointColorTemperature.HasValue;
        return (NormalizeOrDefault(new ScanColorManagementOptions(
            enabled ?? DefaultSettings.IsEnabled,
            redWavelength ?? DefaultSettings.RedWavelengthNm,
            greenWavelength ?? DefaultSettings.GreenWavelengthNm,
            blueWavelength ?? DefaultSettings.BlueWavelengthNm,
            outputGamma ?? DefaultSettings.OutputGamma,
            targetWhitePointMode ?? DefaultSettings.TargetWhitePointMode,
            manualWhitePointColorTemperature ?? DefaultSettings.ManualWhitePointColorTemperatureK)), hasValues);
    }

    private async Task<T?> ReadLegacyAsync<T>(string key)
    {
        try
        {
            return await _localSettingsService.ReadSettingAsync<T>(key);
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Ignoring malformed color legacy setting '{key}': {ex}");
            return default;
        }
    }

    private Task SaveDocumentAsync(ScanColorManagementOptions settings)
        => _localSettingsService.SaveSettingAsync(DocumentKey, new VersionedSettingsDocument<ScanColorManagementOptions>(SchemaVersion, settings));

    private async Task CleanupLegacySettingsAsync()
    {
        if (_localSettingsService is not ILocalSettingsLegacyCleanupService cleaner)
            return;

        try
        {
            await cleaner.RemoveSettingsAsync(LegacyKeys);
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Color legacy cleanup failed after document commit: {ex}");
        }
    }

    private ScanColorManagementOptions NormalizeOrDefault(ScanColorManagementOptions? settings)
    {
        var candidate = settings ?? DefaultSettings;
        return new ScanColorManagementOptions(
            candidate.IsEnabled,
            IsVisibleWavelength(candidate.RedWavelengthNm) ? candidate.RedWavelengthNm : DefaultSettings.RedWavelengthNm,
            IsVisibleWavelength(candidate.GreenWavelengthNm) ? candidate.GreenWavelengthNm : DefaultSettings.GreenWavelengthNm,
            IsVisibleWavelength(candidate.BlueWavelengthNm) ? candidate.BlueWavelengthNm : DefaultSettings.BlueWavelengthNm,
            candidate.OutputGamma >= 0.1 && double.IsFinite(candidate.OutputGamma) ? candidate.OutputGamma : DefaultSettings.OutputGamma,
            IsSupportedTargetWhitePointMode(candidate.TargetWhitePointMode) ? candidate.TargetWhitePointMode : DefaultSettings.TargetWhitePointMode,
            IsSupportedColorTemperature(candidate.ManualWhitePointColorTemperatureK) ? candidate.ManualWhitePointColorTemperatureK : DefaultSettings.ManualWhitePointColorTemperatureK);
    }

    private static bool IsValidDocumentPayload(ScanColorManagementOptions? settings)
        => settings is not null
            && IsVisibleWavelength(settings.RedWavelengthNm)
            && IsVisibleWavelength(settings.GreenWavelengthNm)
            && IsVisibleWavelength(settings.BlueWavelengthNm)
            && settings.OutputGamma >= 0.1
            && double.IsFinite(settings.OutputGamma)
            && IsSupportedTargetWhitePointMode(settings.TargetWhitePointMode)
            && IsSupportedColorTemperature(settings.ManualWhitePointColorTemperatureK);

    private static bool IsVisibleWavelength(double? wavelengthNm)
        => wavelengthNm is >= 380.0 and <= 780.0;

    private static bool IsSupportedTargetWhitePointMode(ScanTargetWhitePointMode? mode)
        => mode is ScanTargetWhitePointMode.D65 or ScanTargetWhitePointMode.D50 or ScanTargetWhitePointMode.ManualColorTemperature;

    private static bool IsSupportedColorTemperature(double? colorTemperatureK)
        => colorTemperatureK is >= 1667.0 and <= 25000.0;

    private static void WriteDiagnostic(string message)
    {
        Debug.WriteLine($"[ScanColorManagementSettingsService] {message}");
        Trace.WriteLine($"[ScanColorManagementSettingsService] {message}");
    }
}
