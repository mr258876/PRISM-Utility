using System.Reflection;
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
    private readonly IScanTransferSettingsService _scanTransferSettingsService;
    private bool _isLoadingScanTransferMode;

    [ObservableProperty]
    public partial ElementTheme ElementTheme { get; set; }

    [ObservableProperty]
    public partial string VersionDescription { get; set; }

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

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public IAsyncRelayCommand RestoreScanTransferDefaultsCommand { get; }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, IScanTransferSettingsService scanTransferSettingsService)
    {
        _themeSelectorService = themeSelectorService;
        _scanTransferSettingsService = scanTransferSettingsService;
        ElementTheme = _themeSelectorService.Theme;
        VersionDescription = GetVersionDescription();

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

        _ = LoadScanTransferSettingsAsync();
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

    private static string GetVersionDescription()
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

        return $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
