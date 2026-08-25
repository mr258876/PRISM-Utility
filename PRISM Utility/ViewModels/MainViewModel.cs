using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM_Utility.Contracts.Navigation;
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
        => _navigationService.NavigateTo(AppRoute.Scan);

    [RelayCommand]
    private void NavigateToScanDebug()
        => _navigationService.NavigateTo(AppRoute.ScanDebug);

    [RelayCommand]
    private void NavigateToSettings()
        => _navigationService.NavigateTo(AppRoute.Settings);
}
