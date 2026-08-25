using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class DeviceConfigurationPage : Page, IPageViewModelHost<DeviceConfigurationViewModel>
{
    public DeviceConfigurationViewModel ViewModel
    {
        get;
    }

    public DeviceConfigurationPage()
    {
        ViewModel = App.GetService<DeviceConfigurationViewModel>();
        InitializeComponent();
    }
}
