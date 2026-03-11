using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Services;

public class NavigationViewService : INavigationViewService
{
    private readonly INavigationService _navigationService;

    private readonly IPageService _pageService;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;

    private NavigationView? _navigationView;

    public IList<object>? MenuItems => _navigationView?.MenuItems;

    public object? SettingsItem => _navigationView?.SettingsItem;

    public NavigationViewService(INavigationService navigationService, IPageService pageService, IUsbUsageCoordinator usbUsageCoordinator)
    {
        _navigationService = navigationService;
        _pageService = pageService;
        _usbUsageCoordinator = usbUsageCoordinator;
    }

    [MemberNotNull(nameof(_navigationView))]
    public void Initialize(NavigationView navigationView)
    {
        _navigationView = navigationView;
        _navigationView.BackRequested += OnBackRequested;
        _navigationView.ItemInvoked += OnItemInvoked;
    }

    public void UnregisterEvents()
    {
        if (_navigationView != null)
        {
            _navigationView.BackRequested -= OnBackRequested;
            _navigationView.ItemInvoked -= OnItemInvoked;
        }
    }

    public NavigationViewItem? GetSelectedItem(Type pageType)
    {
        if (_navigationView != null)
        {
            return GetSelectedItem(_navigationView.MenuItems, pageType) ?? GetSelectedItem(_navigationView.FooterMenuItems, pageType);
        }

        return null;
    }

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) => _navigationService.GoBack();

    private async void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
        }
        else
        {
            var selectedItem = args.InvokedItemContainer as NavigationViewItem;

            if (selectedItem?.GetValue(NavigationHelper.NavigateToProperty) is string pageKey)
            {
                if (await IsNavigationBlockedAsync(pageKey))
                    return;

                _navigationService.NavigateTo(pageKey);
            }
        }
    }

    private async Task<bool> IsNavigationBlockedAsync(string pageKey)
    {
        if (pageKey == typeof(UsbDebugViewModel).FullName && _usbUsageCoordinator.IsScanDebugInUse)
        {
            await ShowNavigationBlockedDialogAsync("USB Debugging is unavailable while Scan Debug is connected. Disconnect Scan Debug first.");
            return true;
        }

        if (pageKey == typeof(ScanDebugViewModel).FullName && _usbUsageCoordinator.IsUsbDebugInUse)
        {
            await ShowNavigationBlockedDialogAsync("Scan Debug is unavailable while USB Debugging is active. Stop USB Debugging first.");
            return true;
        }

        return false;
    }

    private async Task ShowNavigationBlockedDialogAsync(string content)
    {
        if (_navigationView?.XamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = _navigationView.XamlRoot,
            Title = "USB busy",
            Content = content,
            CloseButtonText = "OK"
        };

        await dialog.ShowAsync();
    }

    private NavigationViewItem? GetSelectedItem(IEnumerable<object> menuItems, Type pageType)
    {
        foreach (var item in menuItems.OfType<NavigationViewItem>())
        {
            if (IsMenuItemForPageType(item, pageType))
            {
                return item;
            }

            var selectedChild = GetSelectedItem(item.MenuItems, pageType);
            if (selectedChild != null)
            {
                return selectedChild;
            }
        }

        return null;
    }

    private bool IsMenuItemForPageType(NavigationViewItem menuItem, Type sourcePageType)
    {
        if (menuItem.GetValue(NavigationHelper.NavigateToProperty) is string pageKey)
        {
            return _pageService.GetPageType(pageKey) == sourcePageType;
        }

        return false;
    }
}
