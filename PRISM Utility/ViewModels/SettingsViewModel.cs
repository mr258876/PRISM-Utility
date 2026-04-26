using System.Reflection;
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
    private readonly IDebugOutputSettingsService _debugOutputSettingsService;
    private readonly IScanTransferSettingsService _scanTransferSettingsService;
    private readonly IScanColorManagementSettingsService _colorManagementSettingsService;
    private bool _isLoadingDebugOutputSettings;
    private bool _isLoadingScanTransferMode;
    private bool _isLoadingColorManagementSettings;

    [ObservableProperty]
    public partial ElementTheme ElementTheme { get; set; }

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
    public partial bool IsColorManagementEnabled { get; set; }

    [ObservableProperty]
    public partial string RedWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GreenWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BlueWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputGamma { get; set; } = string.Empty;

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public IAsyncRelayCommand RestoreScanTransferDefaultsCommand { get; }

    public IAsyncRelayCommand RestoreColorManagementDefaultsCommand { get; }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, IDebugOutputSettingsService debugOutputSettingsService, IScanTransferSettingsService scanTransferSettingsService, IScanColorManagementSettingsService colorManagementSettingsService)
    {
        _themeSelectorService = themeSelectorService;
        _debugOutputSettingsService = debugOutputSettingsService;
        _scanTransferSettingsService = scanTransferSettingsService;
        _colorManagementSettingsService = colorManagementSettingsService;
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
        RestoreColorManagementDefaultsCommand = new AsyncRelayCommand(RestoreColorManagementDefaultsAsync);

        _ = LoadDebugOutputSettingsAsync();
        _ = LoadScanTransferSettingsAsync();
        _ = LoadColorManagementSettingsAsync();
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

        var versionText = $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        if (string.IsNullOrWhiteSpace(buildLabel))
            return (versionText, string.Empty);

        return string.IsNullOrWhiteSpace(buildLabel)
            ? (versionText, string.Empty)
            : (versionText, $"Build - {buildLabel}");
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
