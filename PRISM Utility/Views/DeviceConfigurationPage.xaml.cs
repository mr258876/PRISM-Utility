using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class DeviceConfigurationPage : Page
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
