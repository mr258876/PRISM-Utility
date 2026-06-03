using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.Views;

using Windows.UI;

namespace PRISM_Utility.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    private readonly IScannerDeviceSessionManager _scannerSessionManager;
    private readonly IUiDispatcher _dispatcher;
    private bool _isScannerStatusRegistered;

    [ObservableProperty]
    public partial bool IsBackEnabled { get; set; }

    [ObservableProperty]
    public partial object? Selected { get; set; }

    [ObservableProperty]
    public partial string ScannerStatusText { get; set; }

    [ObservableProperty]
    public partial string ScannerStatusShortText { get; set; }

    [ObservableProperty]
    public partial Brush ScannerStatusDotBrush { get; set; }

    [ObservableProperty]
    public partial string ScannerStatusBadgeGlyph { get; set; }

    [ObservableProperty]
    public partial Brush ScannerStatusBadgeForegroundBrush { get; set; }

    public INavigationService NavigationService
    {
        get;
    }

    public INavigationViewService NavigationViewService
    {
        get;
    }

    public string ScannerConnectionFlyoutTitle => "Shell_ScannerConnectionFlyoutTitle".GetLocalizedOrFallback("Device connection");

    public string ScannerConnectionFlyoutDescription => "Shell_ScannerConnectionFlyoutDescription".GetLocalizedOrFallback("Use the current Scan or Scan Debug page connection workflow.");

    public string ScannerConnectionUnavailableText => "Shell_ScannerConnectionUnavailable".GetLocalizedOrFallback("Open Scan or Scan Debug to connect or disconnect the scanner.");

    public Visibility ScannerConnectionUnavailableVisibility => GetActiveConnectCommand() is null && GetActiveDisconnectCommand() is null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public ShellViewModel(
        INavigationService navigationService,
        INavigationViewService navigationViewService,
        IScannerDeviceSessionManager scannerSessionManager,
        IUiDispatcher dispatcher)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
        _scannerSessionManager = scannerSessionManager;
        _dispatcher = dispatcher;

        ScannerStatusText = string.Empty;
        ScannerStatusShortText = string.Empty;
        ScannerStatusDotBrush = BuildStatusBrush("TextFillColorTertiaryBrush", Colors.Gray);
        ScannerStatusBadgeGlyph = ScannerStatusGlyphs.Offline;
        ScannerStatusBadgeForegroundBrush = BuildBadgeForegroundBrush(Colors.Black);

        RegisterScannerStatus();
        UpdateScannerStatus(_scannerSessionManager.Snapshot, _scannerSessionManager.Targets);
    }

    public void UnregisterNavigation()
    {
        NavigationService.Navigated -= OnNavigated;
        UnregisterScannerStatus();
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;

        if (e.SourcePageType == typeof(SettingsPage))
        {
            Selected = NavigationViewService.SettingsItem;
            NotifyScannerConnectionCommandStates();
            return;
        }

        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }

        NotifyScannerConnectionCommandStates();
    }

    private void RegisterScannerStatus()
    {
        if (_isScannerStatusRegistered)
            return;

        _scannerSessionManager.SnapshotChanged += OnScannerSnapshotChanged;
        _scannerSessionManager.TargetsChanged += OnScannerTargetsChanged;
        _isScannerStatusRegistered = true;
    }

    private void UnregisterScannerStatus()
    {
        if (!_isScannerStatusRegistered)
            return;

        _scannerSessionManager.SnapshotChanged -= OnScannerSnapshotChanged;
        _scannerSessionManager.TargetsChanged -= OnScannerTargetsChanged;
        _isScannerStatusRegistered = false;
    }

    private void OnScannerSnapshotChanged(object? sender, ScannerDeviceSessionSnapshot snapshot)
        => _dispatcher.TryEnqueue(() => UpdateScannerStatus(snapshot, _scannerSessionManager.Targets));

    private void OnScannerTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(() => UpdateScannerStatus(_scannerSessionManager.Snapshot, _scannerSessionManager.Targets));

    private void UpdateScannerStatus(ScannerDeviceSessionSnapshot snapshot, ScanTargetState targets)
    {
        var status = BuildScannerStatus(snapshot, targets);
        ScannerStatusText = status.Text;
        ScannerStatusShortText = status.ShortText;
        ScannerStatusDotBrush = status.Brush;
        ScannerStatusBadgeGlyph = status.BadgeGlyph;
        ScannerStatusBadgeForegroundBrush = status.BadgeForegroundBrush;
        NotifyScannerConnectionCommandStates();
    }

    private object? ActivePageViewModel => NavigationService.Frame?.GetPageViewModel();

    private IRelayCommand? GetActiveConnectCommand()
        => ActivePageViewModel switch
        {
            ScanViewModel viewModel => viewModel.ConnectDevicesCommand,
            ScanDebugViewModel viewModel => viewModel.ConnectDevicesCommand,
            _ => null
        };

    private IRelayCommand? GetActiveDisconnectCommand()
        => ActivePageViewModel switch
        {
            ScanViewModel viewModel => viewModel.DisconnectDevicesCommand,
            ScanDebugViewModel viewModel => viewModel.DisconnectDevicesCommand,
            _ => null
        };

    private bool CanConnectScanner() => GetActiveConnectCommand()?.CanExecute(null) == true;

    private bool CanDisconnectScanner() => GetActiveDisconnectCommand()?.CanExecute(null) == true;

    [RelayCommand(CanExecute = nameof(CanConnectScanner))]
    private async Task ConnectScanner()
        => await ExecuteActiveScannerCommandAsync(GetActiveConnectCommand());

    [RelayCommand(CanExecute = nameof(CanDisconnectScanner))]
    private async Task DisconnectScanner()
        => await ExecuteActiveScannerCommandAsync(GetActiveDisconnectCommand());

    private static async Task ExecuteActiveScannerCommandAsync(IRelayCommand? command)
    {
        if (command?.CanExecute(null) != true)
            return;

        if (command is IAsyncRelayCommand asyncCommand)
        {
            await asyncCommand.ExecuteAsync(null);
            return;
        }

        command.Execute(null);
    }

    private void NotifyScannerConnectionCommandStates()
    {
        ConnectScannerCommand.NotifyCanExecuteChanged();
        DisconnectScannerCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ScannerConnectionUnavailableVisibility));
    }

    private static ScannerShellStatus BuildScannerStatus(ScannerDeviceSessionSnapshot snapshot, ScanTargetState targets)
        => snapshot.State switch
        {
            ScannerSessionState.Running => new(
                "Shell_ScannerStatus_Running".GetLocalizedOrFallback("Scanner connected and running."),
                "Shell_ScannerStatusShort_Running".GetLocalizedOrFallback("Running"),
                BuildStatusBrush("SystemAccentColor", Colors.DodgerBlue),
                ScannerStatusGlyphs.Running,
                BuildBadgeForegroundBrush(Colors.White)),
            ScannerSessionState.Connected => new(
                "Shell_ScannerStatus_Connected".GetLocalizedOrFallback("Scanner connected and ready."),
                "Shell_ScannerStatusShort_Connected".GetLocalizedOrFallback("Connected"),
                BuildStatusBrush("SystemFillColorSuccessBrush", Colors.ForestGreen),
                ScannerStatusGlyphs.Connected,
                BuildBadgeForegroundBrush(Colors.White)),
            ScannerSessionState.Connecting => new(
                "Shell_ScannerStatus_Connecting".GetLocalizedOrFallback("Scanner detected. Connecting..."),
                "Shell_ScannerStatusShort_Connecting".GetLocalizedOrFallback("Connecting"),
                BuildStatusBrush("SystemFillColorCautionBrush", Colors.Goldenrod),
                ScannerStatusGlyphs.Connecting,
                BuildBadgeForegroundBrush(Colors.Black)),
            ScannerSessionState.ReconnectPrompt => new(
                "Shell_ScannerStatus_ReconnectPrompt".GetLocalizedOrFallback("Scanner reconnect required. Confirm reconnect to continue."),
                "Shell_ScannerStatusShort_ReconnectPrompt".GetLocalizedOrFallback("Reconnect"),
                BuildStatusBrush("SystemFillColorCautionBrush", Colors.DarkOrange),
                ScannerStatusGlyphs.Reconnect,
                BuildBadgeForegroundBrush(Colors.Black)),
            ScannerSessionState.Faulted => new(
                string.IsNullOrWhiteSpace(snapshot.Fault?.Message)
                    ? "Shell_ScannerStatus_Faulted".GetLocalizedOrFallback("Scanner faulted. Reconnect the scanner to recover.")
                    : "Shell_ScannerStatus_FaultedWithMessage".GetLocalizedFormatOrFallback("Scanner faulted: {0}", snapshot.Fault.Message),
                "Shell_ScannerStatusShort_Faulted".GetLocalizedOrFallback("Faulted"),
                BuildStatusBrush("SystemFillColorCriticalBrush", Colors.IndianRed),
                ScannerStatusGlyphs.Faulted,
                BuildBadgeForegroundBrush(Colors.White)),
            _ when targets.IsDevicesPresent => new(
                "Shell_ScannerStatus_Detected".GetLocalizedOrFallback("Scanner detected. Not connected."),
                "Shell_ScannerStatusShort_Detected".GetLocalizedOrFallback("Detected"),
                BuildStatusBrush("SystemFillColorCautionBrush", Colors.Goldenrod),
                ScannerStatusGlyphs.Detected,
                BuildBadgeForegroundBrush(Colors.Black)),
            _ => new(
                "Shell_ScannerStatus_NotDetected".GetLocalizedOrFallback("Scanner not detected. Connect the scanner USB devices."),
                "Shell_ScannerStatusShort_Offline".GetLocalizedOrFallback("Offline"),
                BuildStatusBrush("TextFillColorTertiaryBrush", Colors.Gray),
                ScannerStatusGlyphs.Offline,
                BuildBadgeForegroundBrush(Colors.Black))
        };

    private static Brush BuildStatusBrush(string resourceKey, Color fallbackColor)
        => Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Brush brush
            ? brush
            : new SolidColorBrush(fallbackColor);

    private static Brush BuildBadgeForegroundBrush(Color color) => new SolidColorBrush(color);

    private static class ScannerStatusGlyphs
    {
        public const string Running = "\uE768";
        public const string Connected = "\uE73E";
        public const string Connecting = "\uE895";
        public const string Reconnect = "\uE72C";
        public const string Faulted = "\uE8C9";
        public const string Detected = "\uE823";
        public const string Offline = "\uE711";
    }

    private sealed record ScannerShellStatus(string Text, string ShortText, Brush Brush, string BadgeGlyph, Brush BadgeForegroundBrush);
}
