using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class MainPage : Page, IPageViewModelHost<MainViewModel>
{
    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
    }
}
