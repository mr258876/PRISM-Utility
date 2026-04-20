using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class ScanPage : Page
{
    public ScanViewModel ViewModel
    {
        get;
    }

    public ScanPage()
    {
        ViewModel = App.GetService<ScanViewModel>();
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await ViewModel.CleanupAsync();
}
