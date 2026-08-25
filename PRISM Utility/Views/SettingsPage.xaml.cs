using System.Diagnostics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page, IPageViewModelHost<SettingsViewModel>
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        try
        {
            await ViewModel.TeardownSettingsPersistenceAsync();
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Settings persistence unload flush timed out.");
        }
    }
}
