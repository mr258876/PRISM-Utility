using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Contracts.Navigation;

namespace PRISM_Utility.Helpers;

// Helper class to set the navigation target for a NavigationViewItem.
public class NavigationHelper
{
    public static AppRoute? GetNavigateTo(NavigationViewItem item) => (AppRoute?)item.GetValue(NavigateToProperty);

    public static void SetNavigateTo(NavigationViewItem item, AppRoute? value) => item.SetValue(NavigateToProperty, value);

    public static readonly DependencyProperty NavigateToProperty =
        DependencyProperty.RegisterAttached("NavigateTo", typeof(AppRoute?), typeof(NavigationHelper), new PropertyMetadata(null));
}
