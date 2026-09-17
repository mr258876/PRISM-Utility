using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views
{
    public sealed class MainPage : Page, IPageViewModelHost<MainViewModel>
    {
        public MainViewModel ViewModel => throw new NotSupportedException();
    }

    public sealed class LogPage : Page, IPageViewModelHost<LogViewModel>
    {
        public LogViewModel ViewModel => throw new NotSupportedException();
    }

    public sealed class ScanPage : Page, IPageViewModelHost<ScanViewModel>
    {
        public ScanViewModel ViewModel => throw new NotSupportedException();
    }

    public sealed class ScanDebugPage : Page, IPageViewModelHost<ScanDebugViewModel>
    {
        public ScanDebugViewModel ViewModel => throw new NotSupportedException();
    }

    public sealed class SettingsPage : Page, IPageViewModelHost<SettingsViewModel>
    {
        public SettingsViewModel ViewModel => throw new NotSupportedException();
    }

    public sealed class DeviceConfigurationPage : Page, IPageViewModelHost<DeviceConfigurationViewModel>
    {
        public DeviceConfigurationViewModel ViewModel => throw new NotSupportedException();
    }

}

namespace PRISM_Utility.ViewModels
{
    public sealed class MainViewModel;

    public sealed class LogViewModel;

    public sealed class ScanViewModel;

    public sealed class DeviceConfigurationViewModel;
}
