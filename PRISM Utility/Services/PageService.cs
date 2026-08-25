using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.ViewModels;
using PRISM_Utility.Views;

namespace PRISM_Utility.Services;

public class PageService : IPageService
{
    private static readonly PageRouteRegistration[] RouteRegistrations =
    [
        Configure<MainPage, MainViewModel>(AppRoute.Main),
        Configure<LogPage, LogViewModel>(AppRoute.Log),
        Configure<ScanPage, ScanViewModel>(AppRoute.Scan),
        Configure<ScanDebugPage, ScanDebugViewModel>(AppRoute.ScanDebug),
        Configure<SettingsPage, SettingsViewModel>(AppRoute.Settings),
        Configure<DeviceConfigurationPage, DeviceConfigurationViewModel>(AppRoute.DeviceConfiguration)
    ];

    private readonly Dictionary<AppRoute, Type> _pages = new();

    public PageService()
    {
        foreach (var registration in RouteRegistrations)
            Configure(registration);

        ValidateExhaustiveRoutes();
    }

    public Type GetPageType(AppRoute route)
    {
        Type? pageType;
        lock (_pages)
        {
            if (!_pages.TryGetValue(route, out pageType))
            {
                throw new ArgumentException($"Page not found: {route}. Did you forget to call PageService.Configure?");
            }
        }

        return pageType;
    }

    private static PageRouteRegistration Configure<TPage, TViewModel>(AppRoute route)
        where TPage : Page, IPageViewModelHost<TViewModel>
        where TViewModel : notnull
        => new(route, typeof(TPage), typeof(TViewModel));

    private void Configure(PageRouteRegistration registration)
    {
        var route = registration.Route;
        var type = registration.PageType;

        lock (_pages)
        {
            if (_pages.ContainsKey(route))
            {
                throw new ArgumentException($"The route {route} is already configured in PageService");
            }

            if (_pages.ContainsValue(type))
            {
                throw new ArgumentException($"This type is already configured with route {_pages.First(p => p.Value == type).Key}");
            }

            _pages.Add(route, type);
        }
    }

    private void ValidateExhaustiveRoutes()
    {
        var missingRoutes = Enum.GetValues<AppRoute>().Except(_pages.Keys).ToArray();
        if (missingRoutes.Length > 0)
            throw new ArgumentException($"Missing page route registrations: {string.Join(", ", missingRoutes)}");
    }

    private sealed record PageRouteRegistration(AppRoute Route, Type PageType, Type ViewModelType);
}
