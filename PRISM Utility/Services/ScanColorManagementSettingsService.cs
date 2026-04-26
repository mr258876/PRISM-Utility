using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public sealed class ScanColorManagementSettingsService : IScanColorManagementSettingsService
{
    private const string EnabledKey = "ScanColorManagementEnabled";
    private const string RedWavelengthNmKey = "ScanColorManagementRedWavelengthNm";
    private const string GreenWavelengthNmKey = "ScanColorManagementGreenWavelengthNm";
    private const string BlueWavelengthNmKey = "ScanColorManagementBlueWavelengthNm";
    private const string OutputGammaKey = "ScanColorManagementOutputGamma";

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;

    public ScanColorManagementOptions DefaultSettings => ScanColorManagementOptions.CreateDefault();

    public ScanColorManagementOptions Settings { get; private set; } = ScanColorManagementOptions.CreateDefault();

    public ScanColorManagementSettingsService(ILocalSettingsService localSettingsService)
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

            var enabled = await _localSettingsService.ReadSettingAsync<bool?>(EnabledKey);
            var redWavelength = await _localSettingsService.ReadSettingAsync<double?>(RedWavelengthNmKey);
            var greenWavelength = await _localSettingsService.ReadSettingAsync<double?>(GreenWavelengthNmKey);
            var blueWavelength = await _localSettingsService.ReadSettingAsync<double?>(BlueWavelengthNmKey);
            var outputGamma = await _localSettingsService.ReadSettingAsync<double?>(OutputGammaKey);

            Settings = Settings with
            {
                IsEnabled = enabled ?? Settings.IsEnabled,
                RedWavelengthNm = IsVisibleWavelength(redWavelength) ? redWavelength.Value : Settings.RedWavelengthNm,
                GreenWavelengthNm = IsVisibleWavelength(greenWavelength) ? greenWavelength.Value : Settings.GreenWavelengthNm,
                BlueWavelengthNm = IsVisibleWavelength(blueWavelength) ? blueWavelength.Value : Settings.BlueWavelengthNm,
                OutputGamma = outputGamma is >= 0.1 ? outputGamma.Value : Settings.OutputGamma
            };

            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task SetSettingsAsync(ScanColorManagementOptions settings)
    {
        await InitializeAsync();
        if (Settings == settings)
            return;

        Settings = settings;

        await _localSettingsService.SaveSettingAsync(EnabledKey, settings.IsEnabled);
        await _localSettingsService.SaveSettingAsync(RedWavelengthNmKey, settings.RedWavelengthNm);
        await _localSettingsService.SaveSettingAsync(GreenWavelengthNmKey, settings.GreenWavelengthNm);
        await _localSettingsService.SaveSettingAsync(BlueWavelengthNmKey, settings.BlueWavelengthNm);
        await _localSettingsService.SaveSettingAsync(OutputGammaKey, settings.OutputGamma);
    }

    private static bool IsVisibleWavelength(double? wavelengthNm)
        => wavelengthNm is >= 380.0 and <= 780.0;
}
