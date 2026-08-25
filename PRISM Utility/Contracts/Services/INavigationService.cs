using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using PRISM_Utility.Contracts.Navigation;

namespace PRISM_Utility.Contracts.Services;

public interface INavigationService
{
    event NavigatedEventHandler Navigated;

    bool CanGoBack
    {
        get;
    }

    Frame? Frame
    {
        get; set;
    }

    bool NavigateTo(AppRoute route, object? parameter = null, bool clearNavigation = false);

    bool GoBack();

    void SetListDataItemForNextConnectedAnimation(object item);
}
