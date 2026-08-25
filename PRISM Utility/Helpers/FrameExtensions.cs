using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Contracts.ViewModels;

namespace PRISM_Utility.Helpers;

public static class FrameExtensions
{
    public static object? GetPageViewModel(this Frame frame) => GetPageViewModel(frame.Content);

    public static object? GetPageViewModel(object? content)
    {
        if (content is null)
            return null;

        if (content is IPageViewModelHost host)
            return host.ViewModel;

        throw new InvalidOperationException($"Frame content {content.GetType().FullName} must implement IPageViewModelHost.");
    }

    public static INavigationAware? GetNavigationAwareViewModel(this Frame frame) => GetNavigationAwareViewModel(frame.Content);

    public static INavigationAware? GetNavigationAwareViewModel(object? content) => GetPageViewModel(content) as INavigationAware;
}
