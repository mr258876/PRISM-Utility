using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM_Utility.Contracts.Services;

namespace PRISM_Utility.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    private readonly INavigationService _navigationService;

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    [RelayCommand]
    private void NavigateToScan()
        => _navigationService.NavigateTo(typeof(ScanViewModel).FullName!);

    [RelayCommand]
    private void NavigateToScanDebug()
        => _navigationService.NavigateTo(typeof(ScanDebugViewModel).FullName!);

    [RelayCommand]
    private void NavigateToSettings()
        => _navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
}
