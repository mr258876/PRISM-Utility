using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    private readonly INavigationService _navigationService;
    private readonly IScannerAccessCoordinator _scannerAccess;
    private readonly IUiDispatcher _dispatcher;
    private bool _isActive;

    [ObservableProperty]
    public partial string ScannerStatusTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ScannerStatusDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity ScannerStatusSeverity { get; set; }

    [ObservableProperty]
    public partial string ScannerWorkspaceActionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility ScannerWorkspaceActionVisibility { get; set; } = Visibility.Collapsed;

    public MainViewModel(
        INavigationService navigationService,
        IScannerAccessCoordinator scannerAccess,
        IUiDispatcher dispatcher)
    {
        _navigationService = navigationService;
        _scannerAccess = scannerAccess;
        _dispatcher = dispatcher;
        RefreshScannerStatus();
    }

    public void Activate()
    {
        if (!_isActive)
        {
            _isActive = true;
            _scannerAccess.SnapshotChanged += OnScannerSnapshotChanged;
        }

        RefreshScannerStatus();
    }

    public void Deactivate()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _scannerAccess.SnapshotChanged -= OnScannerSnapshotChanged;
    }

    private void OnScannerSnapshotChanged(object? sender, ScannerAccessSnapshot snapshot)
        => _dispatcher.TryEnqueue(() =>
        {
            if (_isActive)
                RefreshScannerStatus();
        });

    private void RefreshScannerStatus()
    {
        var snapshot = _scannerAccess.Snapshot;
        var status = snapshot.IsRawUsbActive
            ? "Diagnostics"
            : snapshot.ScannerSession.State switch
            {
                ScannerSessionState.Connecting => "Connecting",
                ScannerSessionState.Connected => "Connected",
                ScannerSessionState.Running => "Running",
                ScannerSessionState.Faulted => "Faulted",
                ScannerSessionState.ReconnectPrompt => "Reconnect",
                _ => snapshot.Targets.IsDevicesPresent ? "Detected" : "Missing"
            };

        ScannerStatusTitle = $"Main_Runtime_Device{status}Title".GetLocalized();
        ScannerStatusDescription = $"Main_Runtime_Device{status}Description".GetLocalized();
        ScannerStatusSeverity = status switch
        {
            "Connected" => InfoBarSeverity.Success,
            "Faulted" or "Reconnect" or "Diagnostics" => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Informational
        };
        ScannerWorkspaceActionText = (snapshot.ActiveMode == ScannerAccessMode.ScanDebug
            ? "Main_Runtime_OpenCalibrationWorkspace"
            : "Main_Runtime_OpenScanWorkspace").GetLocalized();
        ScannerWorkspaceActionVisibility = status is "Running" or "Faulted" or "Reconnect"
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    [RelayCommand]
    private void NavigateToScannerWorkspace()
        => _navigationService.NavigateTo(_scannerAccess.Snapshot.ActiveMode == ScannerAccessMode.ScanDebug
            ? AppRoute.ScanDebug
            : AppRoute.Scan);

    [RelayCommand]
    private void NavigateToScan()
        => _navigationService.NavigateTo(AppRoute.Scan);

    [RelayCommand]
    private void NavigateToScanDebug()
        => _navigationService.NavigateTo(AppRoute.ScanDebug);

    [RelayCommand]
    private void NavigateToSettings()
        => _navigationService.NavigateTo(AppRoute.Settings);
}
