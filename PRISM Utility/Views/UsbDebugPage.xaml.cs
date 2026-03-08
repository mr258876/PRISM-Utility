using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public partial class UsbDebugPage : Page
{
    public UsbDebugViewModel ViewModel
    {
        get;
    }

    public UsbDebugPage()
    {
        ViewModel = App.GetService<UsbDebugViewModel>();
        InitializeComponent();

        ViewModel.DialogRequested += OnDialogRequested;
        Unloaded += (_, __) =>
        {
            ViewModel.DialogRequested -= OnDialogRequested;
            ViewModel.Dispose();
        };
    }

    private async void OnDialogRequested(object? sender, DialogRequest e)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = e.Title,
            Content = e.Content,
            CloseButtonText = "OK"
        };
        await dlg.ShowAsync();
    }
}
