using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.Contracts.Navigation;

public static class NavigationLifecycleDispatcher
{
    public static INavigationAware? CaptureNavigationAware(object? content)
        => FrameExtensions.GetNavigationAwareViewModel(content);

    public static void NotifyNavigatedToThenPublish(object? content, object parameter, Action publishNavigated)
    {
        var navigationAware = FrameExtensions.GetNavigationAwareViewModel(content);
        navigationAware?.OnNavigatedTo(parameter);
        publishNavigated();
    }

    public static void NotifyNavigatedFromAfterFrameResult(INavigationAware? previousViewModel, bool succeeded)
    {
        if (succeeded)
            previousViewModel?.OnNavigatedFrom();
    }
}
