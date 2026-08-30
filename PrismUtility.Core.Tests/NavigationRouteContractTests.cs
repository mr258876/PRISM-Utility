using System.Text.RegularExpressions;
using PRISM_Utility.Contracts.Navigation;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UI002")]
public sealed class NavigationRouteContractTests
{
    private static readonly (AppRoute Route, string Page, string ViewModel, string? ShellUid)[] ExpectedRoutes =
    [
        (AppRoute.Main, "MainPage", "MainViewModel", "Shell_Main"),
        (AppRoute.Log, "LogPage", "LogViewModel", "Shell_Log"),
        (AppRoute.Scan, "ScanPage", "ScanViewModel", "Shell_Scan"),
        (AppRoute.ScanDebug, "ScanDebugPage", "ScanDebugViewModel", "Shell_ScanDebug"),
        (AppRoute.Settings, "SettingsPage", "SettingsViewModel", null),
        (AppRoute.DeviceConfiguration, "DeviceConfigurationPage", "DeviceConfigurationViewModel", null)
    ];

    [Fact]
    public void UI002_AppRouteEnum_PageServiceAndShellMenuItemsShareOneRouteSet()
    {
        var pageService = ReadAppSource("Services", "PageService.cs");
        var shell = ReadAppSource("Views", "ShellPage.xaml");
        var enumRoutes = Enum.GetValues<AppRoute>().OrderBy(route => route.ToString()).ToArray();
        var expectedRoutes = ExpectedRoutes.Select(route => route.Route).OrderBy(route => route.ToString()).ToArray();

        Assert.Equal(expectedRoutes, enumRoutes);

        foreach (var (route, page, viewModel, shellUid) in ExpectedRoutes)
        {
            Assert.Contains($"Configure<{page}, {viewModel}>(AppRoute.{route})", pageService, StringComparison.Ordinal);
            if (shellUid is not null)
                Assert.Contains($"x:Uid=\"{shellUid}\" helpers:NavigationHelper.NavigateTo=\"{route}\"", shell, StringComparison.Ordinal);
        }

        var registrations = Regex.Matches(pageService, @"Configure<(?<page>\w+), (?<viewModel>\w+)>\(AppRoute\.(?<route>\w+)\)");
        Assert.Equal(ExpectedRoutes.Length, registrations.Count);
        Assert.Equal(ExpectedRoutes.Length, registrations.Select(match => match.Groups["route"].Value).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ExpectedRoutes.Length, registrations.Select(match => match.Groups["page"].Value).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ExpectedRoutes.Length, registrations.Select(match => match.Groups["viewModel"].Value).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void UI002_ScanDebugRouteIdentityRemainsInternalDuringDisplayRename()
    {
        var pageService = ReadAppSource("Services", "PageService.cs");
        var shell = ReadAppSource("Views", "ShellPage.xaml");
        var routeEnum = ReadAppSource("Contracts", "Navigation", "AppRoute.cs");
        var pageCodeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Equal("ScanDebug", AppRoute.ScanDebug.ToString());
        Assert.Contains("ScanDebug,", routeEnum, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfile", routeEnum, StringComparison.Ordinal);
        Assert.Contains("Configure<ScanDebugPage, ScanDebugViewModel>(AppRoute.ScanDebug)", pageService, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Shell_ScanDebug\" helpers:NavigationHelper.NavigateTo=\"ScanDebug\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Class=\"PRISM_Utility.Views.ScanDebugPage\"", ReadAppSource("Views", "ScanDebugPage.xaml"), StringComparison.Ordinal);
        Assert.Contains("class ScanDebugPage : Page, IPageViewModelHost<ScanDebugViewModel>", pageCodeBehind, StringComparison.Ordinal);
        Assert.Contains("public ScanDebugViewModel ViewModel", pageCodeBehind, StringComparison.Ordinal);
        Assert.Contains("Scan_Runtime_", viewModel, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void UI002_ScanDebugRouteIdentityContract_RejectsSyntheticMissingInternalTokens()
    {
        const string routeSources = """
            public enum AppRoute { ScanDebug }
            <NavigationViewItem helpers:NavigationHelper.NavigateTo="ScanDebug" />
            Configure<ScanDebugPage, MissingViewModel>(AppRoute.ScanDebug)
            """;

        Assert.Equal(["ScanDebugViewModel"], FilmProfileContractSource.FindMissingTokens(
            ["AppRoute.ScanDebug", "NavigateTo=\"ScanDebug\"", "ScanDebugPage", "ScanDebugViewModel"],
            routeSources));
    }

    [Fact]
    public void UI002_ProductionNavigationDoesNotUseViewModelFullNameOrStringRouteKeys()
    {
        var productionFiles = new[]
        {
            Path.Combine("Contracts", "Services", "IPageService.cs"),
            Path.Combine("Contracts", "Services", "INavigationService.cs"),
            Path.Combine("Helpers", "NavigationHelper.cs"),
            Path.Combine("Services", "PageService.cs"),
            Path.Combine("Services", "NavigationService.cs"),
            Path.Combine("Services", "NavigationViewService.cs"),
            Path.Combine("Activation", "DefaultActivationHandler.cs"),
            Path.Combine("Services", "LanguageSelectorService.cs"),
            Path.Combine("ViewModels", "MainViewModel.cs"),
            Path.Combine("ViewModels", "ShellViewModel.cs"),
            Path.Combine("ViewModels", "ScanDebugViewModel.cs"),
            Path.Combine("Views", "ShellPage.xaml")
        };

        foreach (var relativePath in productionFiles)
        {
            var source = ReadAppSource(relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Assert.DoesNotContain(".FullName", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ViewModels.", source, StringComparison.Ordinal);
            Assert.DoesNotContain("NavigateTo(string", source, StringComparison.Ordinal);
            Assert.DoesNotContain("GetPageType(string", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Dictionary<string, Type>", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("typeof(string)", ReadAppSource("Helpers", "NavigationHelper.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("helpers:NavigationHelper.NavigateTo=\"PRISM_Utility", ReadAppSource("Views", "ShellPage.xaml"), StringComparison.Ordinal);
    }

    [Fact]
    public void UI002_PageServiceSourceRejectsDuplicateMissingAndInvalidRoutes()
    {
        var source = ReadAppSource("Services", "PageService.cs");

        Assert.Contains("Type GetPageType(AppRoute route)", ReadAppSource("Contracts", "Services", "IPageService.cs"), StringComparison.Ordinal);
        Assert.Contains("public Type GetPageType(AppRoute route)", source, StringComparison.Ordinal);
        Assert.Contains("where TPage : Page, IPageViewModelHost<TViewModel>", source, StringComparison.Ordinal);
        Assert.Contains("Enum.GetValues<AppRoute>().Except(_pages.Keys)", source, StringComparison.Ordinal);
        Assert.Contains("Missing page route registrations", source, StringComparison.Ordinal);
        Assert.Contains("_pages.ContainsKey(route)", source, StringComparison.Ordinal);
        Assert.Contains("_pages.ContainsValue(type)", source, StringComparison.Ordinal);
        Assert.Contains("Page not found: {route}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UI002_NavigationHelperAndViewServiceUseNullableEnumAttachedProperty()
    {
        var helper = ReadAppSource("Helpers", "NavigationHelper.cs");
        var viewService = ReadAppSource("Services", "NavigationViewService.cs");
        var shell = ReadAppSource("Views", "ShellPage.xaml");
        var routeValues = Regex.Matches(shell, "helpers:NavigationHelper.NavigateTo=\\\"(?<route>[^\\\"]+)\\\"")
            .Select(match => match.Groups["route"].Value)
            .ToArray();

        Assert.Contains("public static AppRoute? GetNavigateTo", helper, StringComparison.Ordinal);
        Assert.Contains("public static void SetNavigateTo(NavigationViewItem item, AppRoute? value)", helper, StringComparison.Ordinal);
        Assert.Contains("DependencyProperty.RegisterAttached(\"NavigateTo\", typeof(AppRoute?), typeof(NavigationHelper), new PropertyMetadata(null))", helper, StringComparison.Ordinal);
        Assert.Contains("NavigationHelper.GetNavigateTo(selectedItem)", viewService, StringComparison.Ordinal);
        Assert.Contains("NavigationViewRouteResolver.Resolve(args.IsSettingsInvoked, itemRoute)", viewService, StringComparison.Ordinal);
        Assert.All(routeValues, route => Assert.DoesNotContain('.', route));
        Assert.All(routeValues, route => Assert.True(Enum.TryParse<AppRoute>(route, out _), $"Unknown Shell route literal: {route}"));
    }

    private static string ReadAppSource(params string[] relativePath)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", Path.Combine(relativePath)));

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }
}
