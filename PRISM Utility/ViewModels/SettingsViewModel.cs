using System.Reflection;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;

using Windows.ApplicationModel;

namespace PRISM_Utility.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly IDebugOutputSettingsService _debugOutputSettingsService;
    private readonly IScanTransferSettingsService _scanTransferSettingsService;
    private readonly IScanDeviceSettingsService _deviceSettingsService;
    private readonly IScanColorManagementSettingsService _colorManagementSettingsService;
    private readonly IScanDngGeometrySettingsService _dngGeometrySettingsService;
    private bool _isLoadingLanguageSettings;
    private bool _isLoadingDebugOutputSettings;
    private bool _isLoadingScanTransferMode;
    private bool _isLoadingDeviceSettings;
    private bool _isLoadingColorManagementSettings;
    private bool _isLoadingDngGeometrySettings;

    [ObservableProperty]
    public partial ElementTheme ElementTheme { get; set; }

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionDescription { get; set; }

    [ObservableProperty]
    public partial string BuildDescription { get; set; }

    [ObservableProperty]
    public partial Visibility BuildVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial bool IsDebugConsoleMirrorEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsDebugFileLogEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsMultiBufferedBulkInEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawIoEnabled { get; set; }

    [ObservableProperty]
    public partial string MultiBufferedRequestBytes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MultiBufferedOutstandingReads { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MultiBufferedTimeoutMs { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor1StepsPerRevolution { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor1Microsteps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor1LeadLengthMm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor2StepsPerRevolution { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor2Microsteps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor2LeadLengthMm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor3StepsPerRevolution { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor3Microsteps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor3LeadLengthMm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel1Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel2Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel3Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel4Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsColorManagementEnabled { get; set; }

    [ObservableProperty]
    public partial string RedWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GreenWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BlueWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputGamma { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngActiveStart { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngActiveEndInclusive { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngMaskedRanges { get; set; } = string.Empty;

    public IReadOnlyList<AppLanguageOption> LanguageOptions { get; } =
    [
        new("system", "Settings_Language_System".GetLocalized()),
        new("en-US", "Settings_Language_English".GetLocalized()),
        new("zh-CN", "Settings_Language_SimplifiedChinese".GetLocalized())
    ];

    public ObservableCollection<string> ChannelRoleOptions { get; } = new() { "Red", "Green", "Blue", "White", "IR", "Unused" };

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public IAsyncRelayCommand RestoreScanTransferDefaultsCommand { get; }

    public IAsyncRelayCommand RestoreDeviceSettingsDefaultsCommand { get; }

    public IAsyncRelayCommand RestoreColorManagementDefaultsCommand { get; }

    public IAsyncRelayCommand RestoreDngGeometryDefaultsCommand { get; }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILanguageSelectorService languageSelectorService, IDebugOutputSettingsService debugOutputSettingsService, IScanTransferSettingsService scanTransferSettingsService, IScanDeviceSettingsService deviceSettingsService, IScanColorManagementSettingsService colorManagementSettingsService, IScanDngGeometrySettingsService dngGeometrySettingsService)
    {
        _themeSelectorService = themeSelectorService;
        _languageSelectorService = languageSelectorService;
        _debugOutputSettingsService = debugOutputSettingsService;
        _scanTransferSettingsService = scanTransferSettingsService;
        _deviceSettingsService = deviceSettingsService;
        _colorManagementSettingsService = colorManagementSettingsService;
        _dngGeometrySettingsService = dngGeometrySettingsService;
        ElementTheme = _themeSelectorService.Theme;
        var versionInfo = GetVersionInfo();
        VersionDescription = versionInfo.VersionDescription;
        BuildDescription = versionInfo.BuildDescription;

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });

        RestoreScanTransferDefaultsCommand = new AsyncRelayCommand(RestoreScanTransferDefaultsAsync);
        RestoreDeviceSettingsDefaultsCommand = new AsyncRelayCommand(RestoreDeviceSettingsDefaultsAsync);
        RestoreColorManagementDefaultsCommand = new AsyncRelayCommand(RestoreColorManagementDefaultsAsync);
        RestoreDngGeometryDefaultsCommand = new AsyncRelayCommand(RestoreDngGeometryDefaultsAsync);

        _ = LoadLanguageSettingsAsync();
        _ = LoadDebugOutputSettingsAsync();
        _ = LoadScanTransferSettingsAsync();
        _ = LoadDeviceSettingsAsync();
        _ = LoadColorManagementSettingsAsync();
        _ = LoadDngGeometrySettingsAsync();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_isLoadingLanguageSettings || string.IsNullOrWhiteSpace(value))
            return;

        _ = _languageSelectorService.SetLanguageAsync(value);
    }

    partial void OnIsDebugConsoleMirrorEnabledChanged(bool value)
    {
        if (_isLoadingDebugOutputSettings)
            return;

        _ = _debugOutputSettingsService.SetDebugConsoleEnabledAsync(value);
    }

    partial void OnIsDebugFileLogEnabledChanged(bool value)
    {
        if (_isLoadingDebugOutputSettings)
            return;

        _ = _debugOutputSettingsService.SetFileLogEnabledAsync(value);
    }

    partial void OnIsMultiBufferedBulkInEnabledChanged(bool value)
    {
        if (_isLoadingScanTransferMode)
            return;

        _ = SaveTransferSettingsAsync(settings => settings with
        {
            ReadMode = value ? ScanBulkInReadMode.MultiBuffered : ScanBulkInReadMode.SingleRequest
        });
    }

    partial void OnIsRawIoEnabledChanged(bool value)
    {
        if (_isLoadingScanTransferMode)
            return;

        _ = SaveTransferSettingsAsync(settings => settings with { RawIoEnabled = value });
    }

    partial void OnMultiBufferedRequestBytesChanged(string value)
    {
        if (_isLoadingScanTransferMode || !TryParsePositiveInt(value, out var parsed))
            return;

        _ = SaveTransferSettingsAsync(settings => settings with { RequestBytes = parsed });
    }

    partial void OnMultiBufferedOutstandingReadsChanged(string value)
    {
        if (_isLoadingScanTransferMode || !TryParsePositiveInt(value, out var parsed))
            return;

        _ = SaveTransferSettingsAsync(settings => settings with { OutstandingReads = parsed });
    }

    partial void OnMultiBufferedTimeoutMsChanged(string value)
    {
        if (_isLoadingScanTransferMode || !TryParsePositiveInt(value, out var parsed))
            return;

        _ = SaveTransferSettingsAsync(settings => settings with { TimeoutMs = parsed });
    }

    partial void OnMotor1StepsPerRevolutionChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor1MicrostepsChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor1LeadLengthMmChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor2StepsPerRevolutionChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor2MicrostepsChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor2LeadLengthMmChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor3StepsPerRevolutionChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor3MicrostepsChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor3LeadLengthMmChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel1RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel2RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel3RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel4RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnIsColorManagementEnabledChanged(bool value)
    {
        if (_isLoadingColorManagementSettings)
            return;

        _ = SaveColorManagementSettingsAsync(settings => settings with { IsEnabled = value });
    }

    private async Task LoadDebugOutputSettingsAsync()
    {
        _isLoadingDebugOutputSettings = true;
        try
        {
            await _debugOutputSettingsService.InitializeAsync();
            IsDebugConsoleMirrorEnabled = _debugOutputSettingsService.IsDebugConsoleEnabled;
            IsDebugFileLogEnabled = _debugOutputSettingsService.IsFileLogEnabled;
        }
        finally
        {
            _isLoadingDebugOutputSettings = false;
        }
    }

    private async Task LoadLanguageSettingsAsync()
    {
        _isLoadingLanguageSettings = true;
        try
        {
            await _languageSelectorService.InitializeAsync();
            SelectedLanguage = _languageSelectorService.CurrentLanguage;
        }
        finally
        {
            _isLoadingLanguageSettings = false;
        }
    }

    partial void OnRedWavelengthNmChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        _ = SaveColorManagementSettingsAsync(settings => settings with { RedWavelengthNm = parsed });
    }

    partial void OnGreenWavelengthNmChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        _ = SaveColorManagementSettingsAsync(settings => settings with { GreenWavelengthNm = parsed });
    }

    partial void OnBlueWavelengthNmChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        _ = SaveColorManagementSettingsAsync(settings => settings with { BlueWavelengthNm = parsed });
    }

    partial void OnOutputGammaChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        _ = SaveColorManagementSettingsAsync(settings => settings with { OutputGamma = parsed });
    }

    partial void OnDngActiveStartChanged(string value)
    {
        if (_isLoadingDngGeometrySettings)
            return;

        _ = SaveDngGeometrySettingsFromInputsAsync();
    }

    partial void OnDngActiveEndInclusiveChanged(string value)
    {
        if (_isLoadingDngGeometrySettings)
            return;

        _ = SaveDngGeometrySettingsFromInputsAsync();
    }

    partial void OnDngMaskedRangesChanged(string value)
    {
        if (_isLoadingDngGeometrySettings)
            return;

        _ = SaveDngGeometrySettingsFromInputsAsync();
    }

    private async Task LoadScanTransferSettingsAsync()
    {
        _isLoadingScanTransferMode = true;
        try
        {
            await _scanTransferSettingsService.InitializeAsync();
            ApplyTransferSettings(_scanTransferSettingsService.Settings);
        }
        finally
        {
            _isLoadingScanTransferMode = false;
        }
    }

    private void ApplyTransferSettings(ScanBulkInTransferOptions settings)
    {
        IsMultiBufferedBulkInEnabled = settings.ReadMode == ScanBulkInReadMode.MultiBuffered;
        IsRawIoEnabled = settings.RawIoEnabled;
        MultiBufferedRequestBytes = settings.RequestBytes.ToString();
        MultiBufferedOutstandingReads = settings.OutstandingReads.ToString();
        MultiBufferedTimeoutMs = settings.TimeoutMs.ToString();
    }

    private async Task SaveTransferSettingsAsync(Func<ScanBulkInTransferOptions, ScanBulkInTransferOptions> mutate)
    {
        await _scanTransferSettingsService.InitializeAsync();
        await _scanTransferSettingsService.SetSettingsAsync(mutate(_scanTransferSettingsService.Settings));
    }

    private async Task RestoreScanTransferDefaultsAsync()
    {
        _isLoadingScanTransferMode = true;
        try
        {
            await _scanTransferSettingsService.InitializeAsync();
            var defaults = _scanTransferSettingsService.DefaultSettings;
            ApplyTransferSettings(defaults);
            await _scanTransferSettingsService.SetSettingsAsync(defaults);
        }
        finally
        {
            _isLoadingScanTransferMode = false;
        }
    }

    private static bool TryParsePositiveInt(string value, out int parsed)
        => int.TryParse(value, out parsed) && parsed > 0;

    private async Task LoadDngGeometrySettingsAsync()
    {
        _isLoadingDngGeometrySettings = true;
        try
        {
            await _dngGeometrySettingsService.InitializeAsync();
            ApplyDngGeometrySettings(_dngGeometrySettingsService.Settings);
        }
        finally
        {
            _isLoadingDngGeometrySettings = false;
        }
    }

    private async Task LoadDeviceSettingsAsync()
    {
        _isLoadingDeviceSettings = true;
        try
        {
            await _deviceSettingsService.InitializeAsync();
            ApplyDeviceSettings(_deviceSettingsService.Settings);
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private void ApplyDeviceSettings(ScanDeviceSettings settings)
    {
        var normalized = settings.Normalize();
        ApplyMotorSettings(normalized.Motor1 ?? ScanMotorMechanicalSettings.CreateDefault(),
            value => Motor1StepsPerRevolution = value,
            value => Motor1Microsteps = value,
            value => Motor1LeadLengthMm = value);
        ApplyMotorSettings(normalized.Motor2 ?? ScanMotorMechanicalSettings.CreateDefault(),
            value => Motor2StepsPerRevolution = value,
            value => Motor2Microsteps = value,
            value => Motor2LeadLengthMm = value);
        ApplyMotorSettings(normalized.Motor3 ?? ScanMotorMechanicalSettings.CreateDefault(),
            value => Motor3StepsPerRevolution = value,
            value => Motor3Microsteps = value,
            value => Motor3LeadLengthMm = value);
        DeviceChannel1Role = NormalizeChannelRoleSelection(normalized.Channel1Role, "Blue");
        DeviceChannel2Role = NormalizeChannelRoleSelection(normalized.Channel2Role, "White");
        DeviceChannel3Role = NormalizeChannelRoleSelection(normalized.Channel3Role, "Red");
        DeviceChannel4Role = NormalizeChannelRoleSelection(normalized.Channel4Role, "Green");
    }

    private string NormalizeChannelRoleSelection(string? role, string fallback)
        => ChannelRoleOptions.FirstOrDefault(option => string.Equals(option, role, StringComparison.OrdinalIgnoreCase)) ?? fallback;

    private static void ApplyMotorSettings(ScanMotorMechanicalSettings settings, Action<string> setStepsPerRevolution, Action<string> setMicrosteps, Action<string> setLeadLengthMm)
    {
        var normalized = settings.Normalize();
        setStepsPerRevolution(normalized.StepsPerRevolution.ToString(CultureInfo.InvariantCulture));
        setMicrosteps(normalized.Microsteps.ToString(CultureInfo.InvariantCulture));
        setLeadLengthMm(FormatDeviceDouble(normalized.LeadLengthMm));
    }

    private void OnDeviceSettingsInputChanged()
    {
        if (_isLoadingDeviceSettings)
            return;

        _ = SaveDeviceSettingsFromInputsAsync();
    }

    private async Task SaveDeviceSettingsFromInputsAsync()
    {
        if (!TryBuildDeviceSettingsFromInputs(out var settings))
            return;

        await _deviceSettingsService.InitializeAsync();
        await _deviceSettingsService.SetSettingsAsync(settings);
        _isLoadingDeviceSettings = true;
        try
        {
            ApplyDeviceSettings(_deviceSettingsService.Settings);
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private async Task RestoreDeviceSettingsDefaultsAsync()
    {
        _isLoadingDeviceSettings = true;
        try
        {
            await _deviceSettingsService.InitializeAsync();
            var defaults = _deviceSettingsService.DefaultSettings;
            ApplyDeviceSettings(defaults);
            await _deviceSettingsService.SetSettingsAsync(defaults);
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private bool TryBuildDeviceSettingsFromInputs(out ScanDeviceSettings settings)
    {
        settings = _deviceSettingsService.DefaultSettings;
        if (!TryBuildMotorMechanicalSettings(Motor1StepsPerRevolution, Motor1Microsteps, Motor1LeadLengthMm, out var motor1)
            || !TryBuildMotorMechanicalSettings(Motor2StepsPerRevolution, Motor2Microsteps, Motor2LeadLengthMm, out var motor2)
            || !TryBuildMotorMechanicalSettings(Motor3StepsPerRevolution, Motor3Microsteps, Motor3LeadLengthMm, out var motor3))
        {
            return false;
        }

        settings = new ScanDeviceSettings(
            motor1,
            motor2,
            motor3,
            DeviceChannel1Role,
            DeviceChannel2Role,
            DeviceChannel3Role,
            DeviceChannel4Role).Normalize();
        return true;
    }

    private static bool TryBuildMotorMechanicalSettings(string stepsPerRevolutionText, string microstepsText, string leadLengthMmText, out ScanMotorMechanicalSettings settings)
    {
        settings = ScanMotorMechanicalSettings.CreateDefault();
        if (!uint.TryParse(stepsPerRevolutionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stepsPerRevolution)
            || stepsPerRevolution == 0
            || !uint.TryParse(microstepsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microsteps)
            || microsteps == 0
            || !double.TryParse(leadLengthMmText, NumberStyles.Float, CultureInfo.InvariantCulture, out var leadLengthMm)
            || !double.IsFinite(leadLengthMm)
            || leadLengthMm <= 0.0)
        {
            return false;
        }

        settings = new ScanMotorMechanicalSettings(stepsPerRevolution, microsteps, leadLengthMm).Normalize();
        return true;
    }

    private void ApplyDngGeometrySettings(ScanDngGeometrySettings settings)
    {
        DngActiveStart = settings.ActiveRange.Start.ToString(CultureInfo.InvariantCulture);
        DngActiveEndInclusive = settings.ActiveRange.EndInclusive.ToString(CultureInfo.InvariantCulture);
        DngMaskedRanges = string.Join(", ",
            settings.MaskedBlackRanges.Select(range => $"{range.Start}-{range.EndInclusive}"));
    }

    private async Task SaveDngGeometrySettingsFromInputsAsync()
    {
        if (!TryBuildDngGeometrySettingsFromInputs(out var settings))
            return;

        await _dngGeometrySettingsService.InitializeAsync();
        await _dngGeometrySettingsService.SetSettingsAsync(settings);
        _isLoadingDngGeometrySettings = true;
        try
        {
            ApplyDngGeometrySettings(_dngGeometrySettingsService.Settings);
        }
        finally
        {
            _isLoadingDngGeometrySettings = false;
        }
    }

    private async Task RestoreDngGeometryDefaultsAsync()
    {
        _isLoadingDngGeometrySettings = true;
        try
        {
            await _dngGeometrySettingsService.InitializeAsync();
            var defaults = _dngGeometrySettingsService.DefaultSettings;
            ApplyDngGeometrySettings(defaults);
            await _dngGeometrySettingsService.SetSettingsAsync(defaults);
        }
        finally
        {
            _isLoadingDngGeometrySettings = false;
        }
    }

    private bool TryBuildDngGeometrySettingsFromInputs(out ScanDngGeometrySettings settings)
    {
        settings = _dngGeometrySettingsService.DefaultSettings;
        if (!int.TryParse(DngActiveStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(DngActiveEndInclusive, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            return false;
        }

        if (!TryParseMaskedRanges(DngMaskedRanges, out var ranges))
            return false;

        settings = new ScanDngGeometrySettings(new ScanColumnRange(start, end), ranges);
        return true;
    }

    private static bool TryParseMaskedRanges(string value, out ScanColumnRange[] ranges)
    {
        ranges = [];
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var parsedRanges = new List<ScanColumnRange>(parts.Length);
        foreach (var part in parts)
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (bounds.Length != 2
                || !int.TryParse(bounds[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(bounds[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
            {
                return false;
            }

            parsedRanges.Add(new ScanColumnRange(start, end));
        }

        ranges = parsedRanges.ToArray();
        return parsedRanges.Count is > 0 and <= ScanDngGeometrySettings.MaxMaskedBlackRangeCount;
    }

    private async Task LoadColorManagementSettingsAsync()
    {
        _isLoadingColorManagementSettings = true;
        try
        {
            await _colorManagementSettingsService.InitializeAsync();
            ApplyColorManagementSettings(_colorManagementSettingsService.Settings);
        }
        finally
        {
            _isLoadingColorManagementSettings = false;
        }
    }

    private void ApplyColorManagementSettings(ScanColorManagementOptions settings)
    {
        IsColorManagementEnabled = settings.IsEnabled;
        RedWavelengthNm = FormatColorDouble(settings.RedWavelengthNm);
        GreenWavelengthNm = FormatColorDouble(settings.GreenWavelengthNm);
        BlueWavelengthNm = FormatColorDouble(settings.BlueWavelengthNm);
        OutputGamma = FormatColorDouble(settings.OutputGamma);
    }

    private async Task SaveColorManagementSettingsAsync(Func<ScanColorManagementOptions, ScanColorManagementOptions> mutate)
    {
        await _colorManagementSettingsService.InitializeAsync();
        await _colorManagementSettingsService.SetSettingsAsync(mutate(_colorManagementSettingsService.Settings));
    }

    private async Task RestoreColorManagementDefaultsAsync()
    {
        _isLoadingColorManagementSettings = true;
        try
        {
            await _colorManagementSettingsService.InitializeAsync();
            var defaults = _colorManagementSettingsService.DefaultSettings;
            ApplyColorManagementSettings(defaults);
            await _colorManagementSettingsService.SetSettingsAsync(defaults);
        }
        finally
        {
            _isLoadingColorManagementSettings = false;
        }
    }

    private static bool TryParseColorDouble(string value, out double parsed)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);

    private static string FormatColorDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatDeviceDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static (string VersionDescription, string BuildDescription) GetVersionInfo()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var buildLabel = TryGetBuildLabel(assembly);

        var versionText = "Settings_VersionDescriptionFormat".GetLocalizedFormat(
            "AppDisplayName".GetLocalized(),
            $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
        if (string.IsNullOrWhiteSpace(buildLabel))
            return (versionText, string.Empty);

        return string.IsNullOrWhiteSpace(buildLabel)
            ? (versionText, string.Empty)
            : (versionText, "Settings_BuildDescriptionFormat".GetLocalizedFormat(buildLabel));
    }

    partial void OnBuildDescriptionChanged(string value)
        => BuildVisibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    private static string? TryGetBuildLabel(Assembly assembly)
    {
        var metadataBuildLabel = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "BuildLabel", StringComparison.Ordinal))?
            .Value;

        if (!string.IsNullOrWhiteSpace(metadataBuildLabel))
            return metadataBuildLabel;

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var buildLabelStart = informationalVersion.IndexOf('(');
            var buildLabelEnd = informationalVersion.LastIndexOf(')');
            if (buildLabelStart >= 0 && buildLabelEnd > buildLabelStart)
            {
                var buildText = informationalVersion.Substring(buildLabelStart + 1, buildLabelEnd - buildLabelStart - 1).Trim();
                if (!string.IsNullOrWhiteSpace(buildText))
                    return buildText;
            }
        }

        return null;
    }
}
