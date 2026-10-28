using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UI002")]
public sealed class NavigationRouteRuntimeHarnessTests
{
    [Fact]
    public void PageService_ResolvesEveryAppRouteWithoutLaunchingHardwareOrShell()
    {
        var service = new PageService();
        var expectedPages = new Dictionary<AppRoute, Type>
        {
            [AppRoute.Main] = typeof(PRISM_Utility.Views.MainPage),
            [AppRoute.Log] = typeof(PRISM_Utility.Views.LogPage),
            [AppRoute.Scan] = typeof(PRISM_Utility.Views.ScanPage),
            [AppRoute.ScanDebug] = typeof(PRISM_Utility.Views.ScanDebugPage),
            [AppRoute.Settings] = typeof(PRISM_Utility.Views.SettingsPage),
            [AppRoute.DeviceConfiguration] = typeof(PRISM_Utility.Views.SettingsPage)
        };

        foreach (var route in Enum.GetValues<AppRoute>())
            Assert.Equal(expectedPages[route], service.GetPageType(route));
    }

    [Fact]
    public void LegacyDeviceRouteOpensTheSameEditorAndSectionAsTheSettingsEntry()
    {
        var pages = new PageService();
        var target = AppNavigationTarget.Resolve(AppRoute.DeviceConfiguration);

        Assert.Equal(AppRoute.Settings, target.Route);
        Assert.Equal(AppNavigationTarget.DeviceSettingsParameter, target.Parameter);
        Assert.Equal(pages.GetPageType(AppRoute.Settings), pages.GetPageType(AppRoute.DeviceConfiguration));
        Assert.Equal(pages.GetPageType(target.Route), pages.GetPageType(AppRoute.DeviceConfiguration));
    }

    [Fact]
    public void OtherNavigationTargetsKeepTheirParameters()
    {
        var parameter = new object();
        foreach (var route in Enum.GetValues<AppRoute>().Where(route => route != AppRoute.DeviceConfiguration))
        {
            var target = AppNavigationTarget.Resolve(route, parameter);
            Assert.Equal(route, target.Route);
            Assert.Same(parameter, target.Parameter);
        }
    }

    [Fact]
    public void PageService_RejectsInvalidUnregisteredEnumRoute()
    {
        var service = new PageService();

        var error = Assert.Throws<ArgumentException>(() => service.GetPageType((AppRoute)999));

        Assert.Contains("Page not found: 999", error.Message, StringComparison.Ordinal);
    }
}
