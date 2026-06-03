using CommunityToolkit.Mvvm.ComponentModel;

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

    public INavigationService NavigationService
    {
        get;
    }

    public INavigationViewService NavigationViewService
    {
        get;
    }

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
            return;
        }

        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
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
    }

    private static ScannerShellStatus BuildScannerStatus(ScannerDeviceSessionSnapshot snapshot, ScanTargetState targets)
        => snapshot.State switch
        {
            ScannerSessionState.Running => new(
                "Shell_ScannerStatus_Running".GetLocalizedOrFallback("Scanner connected and running."),
                "Shell_ScannerStatusShort_Running".GetLocalizedOrFallback("Running"),
                BuildStatusBrush("SystemAccentColor", Colors.DodgerBlue)),
            ScannerSessionState.Connected => new(
                "Shell_ScannerStatus_Connected".GetLocalizedOrFallback("Scanner connected and ready."),
                "Shell_ScannerStatusShort_Connected".GetLocalizedOrFallback("Connected"),
                BuildStatusBrush("SystemFillColorSuccessBrush", Colors.ForestGreen)),
            ScannerSessionState.Connecting => new(
                "Shell_ScannerStatus_Connecting".GetLocalizedOrFallback("Scanner detected. Connecting..."),
                "Shell_ScannerStatusShort_Connecting".GetLocalizedOrFallback("Connecting"),
                BuildStatusBrush("SystemFillColorCautionBrush", Colors.Goldenrod)),
            ScannerSessionState.ReconnectPrompt => new(
                "Shell_ScannerStatus_ReconnectPrompt".GetLocalizedOrFallback("Scanner reconnect required. Confirm reconnect to continue."),
                "Shell_ScannerStatusShort_ReconnectPrompt".GetLocalizedOrFallback("Reconnect"),
                BuildStatusBrush("SystemFillColorCautionBrush", Colors.DarkOrange)),
            ScannerSessionState.Faulted => new(
                string.IsNullOrWhiteSpace(snapshot.Fault?.Message)
                    ? "Shell_ScannerStatus_Faulted".GetLocalizedOrFallback("Scanner faulted. Reconnect the scanner to recover.")
                    : "Shell_ScannerStatus_FaultedWithMessage".GetLocalizedFormatOrFallback("Scanner faulted: {0}", snapshot.Fault.Message),
                "Shell_ScannerStatusShort_Faulted".GetLocalizedOrFallback("Faulted"),
                BuildStatusBrush("SystemFillColorCriticalBrush", Colors.IndianRed)),
            _ when targets.IsDevicesPresent => new(
                "Shell_ScannerStatus_Detected".GetLocalizedOrFallback("Scanner detected. Not connected."),
                "Shell_ScannerStatusShort_Detected".GetLocalizedOrFallback("Detected"),
                BuildStatusBrush("SystemFillColorCautionBrush", Colors.Goldenrod)),
            _ => new(
                "Shell_ScannerStatus_NotDetected".GetLocalizedOrFallback("Scanner not detected. Connect the scanner USB devices."),
                "Shell_ScannerStatusShort_Offline".GetLocalizedOrFallback("Offline"),
                BuildStatusBrush("TextFillColorTertiaryBrush", Colors.Gray))
        };

    private static Brush BuildStatusBrush(string resourceKey, Color fallbackColor)
        => Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Brush brush
            ? brush
            : new SolidColorBrush(fallbackColor);

    private sealed record ScannerShellStatus(string Text, string ShortText, Brush Brush);
}
