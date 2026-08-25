using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class LogPage : Page, IPageViewModelHost<LogViewModel>
{
    public LogViewModel ViewModel
    {
        get;
    }

    public LogPage()
    {
        ViewModel = App.GetService<LogViewModel>();
        InitializeComponent();

        Unloaded += (_, __) => ViewModel.Dispose();
    }
}
