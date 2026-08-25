using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Helpers;
using Xunit;

namespace PrismUtility.Core.Tests;

[CollectionDefinition(Ui003ProductionProbeCollection.Name, DisableParallelization = true)]
public sealed class Ui003ProductionProbeCollection
{
    public const string Name = "UI003 production probe";
}

[Trait("Category", "UI003")]
[Collection(Ui003ProductionProbeCollection.Name)]
public sealed class NavigationViewModelHostContractTests
{
    private static readonly (AppRoute Route, string Page, string ViewModel)[] ExpectedRouteHosts =
    [
        (AppRoute.Main, "MainPage", "MainViewModel"),
        (AppRoute.Log, "LogPage", "LogViewModel"),
        (AppRoute.Scan, "ScanPage", "ScanViewModel"),
        (AppRoute.ScanDebug, "ScanDebugPage", "ScanDebugViewModel"),
        (AppRoute.Settings, "SettingsPage", "SettingsViewModel"),
        (AppRoute.DeviceConfiguration, "DeviceConfigurationPage", "DeviceConfigurationViewModel")
    ];

    [Fact]
    public void UI003_PageServiceRegistrationsConstrainPageAndExactViewModelPairing()
    {
        var pageService = ReadAppSource("Services", "PageService.cs");
        var hostContract = ReadAppSource("Contracts", "ViewModels", "IPageViewModelHost.cs");

        Assert.Contains("public interface IPageViewModelHost", hostContract, StringComparison.Ordinal);
        Assert.Contains("public interface IPageViewModelHost<TViewModel>", hostContract, StringComparison.Ordinal);
        Assert.Contains("where TPage : Page, IPageViewModelHost<TViewModel>", pageService, StringComparison.Ordinal);

        foreach (var (route, page, viewModel) in ExpectedRouteHosts)
            Assert.Contains($"Configure<{page}, {viewModel}>(AppRoute.{route})", pageService, StringComparison.Ordinal);
    }

    [Fact]
    public void UI003_AllRoutedPagesDeclareExactGenericHostContract()
    {
        foreach (var (_, page, viewModel) in ExpectedRouteHosts)
        {
            var source = ReadAppSource("Views", $"{page}.xaml.cs");

            Assert.Contains($"class {page} : Page, IPageViewModelHost<{viewModel}>", source, StringComparison.Ordinal);
            Assert.Contains($"public {viewModel} ViewModel", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UI003_ExpectedFailureCompileHarnessRejectsMissingHostWrongPairingAndVariance()
        => CompileFailureHarness.RunAll(FindHostSoftwareRoot());

    [Fact]
    public void UI003_ProductionAppContractProbeVerifiesActualRoutedPageHostMetadata()
        => ProductionAppContractProbe.Run(FindHostSoftwareRoot(), ExpectedRouteHosts);

    [Fact]
    public void UI003_DirectHostRetrievalReturnsViewModelAndRejectsNonHostContent()
    {
        var viewModel = new PlainViewModel();
        var host = new PlainHost(viewModel);

        Assert.Same(viewModel, FrameExtensions.GetPageViewModel(host));
        Assert.Null(FrameExtensions.GetPageViewModel((object?)null));

        var error = Assert.Throws<InvalidOperationException>(() => FrameExtensions.GetPageViewModel(new object()));
        Assert.Contains("must implement IPageViewModelHost", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UI003_NavigationAwareRetrievalIsExplicitForPresentAndAbsentViewModels()
    {
        var awareViewModel = new AwareViewModel("aware");
        var plainViewModel = new PlainViewModel();

        Assert.Same(awareViewModel, FrameExtensions.GetNavigationAwareViewModel(new AwareHost(awareViewModel)));
        Assert.Null(FrameExtensions.GetNavigationAwareViewModel(new PlainHost(plainViewModel)));
        Assert.Null(FrameExtensions.GetNavigationAwareViewModel((object?)null));
    }

    [Fact]
    public void UI003_ProductionNavigationContainsNoReflectionOrStringViewModelPropertyScan()
    {
        var productionFiles = new[]
        {
            Path.Combine("Contracts", "ViewModels", "IPageViewModelHost.cs"),
            Path.Combine("Helpers", "FrameExtensions.cs"),
            Path.Combine("Services", "PageService.cs"),
            Path.Combine("Services", "NavigationService.cs"),
            Path.Combine("ViewModels", "ShellViewModel.cs")
        };

        foreach (var relativePath in productionFiles)
        {
            var source = ReadAppSource(relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Assert.DoesNotContain("GetProperty(\"ViewModel\")", source, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Reflection", source, StringComparison.Ordinal);
            Assert.DoesNotContain("BindingFlags", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UI003_LifecycleDispatcherPreservesCallbackOrderingAndSuccessfulFromOnly()
    {
        var events = new List<string>();
        var newHost = new AwareHost(new AwareViewModel("new", events));
        var oldViewModel = new AwareViewModel("old", events);

        NavigationLifecycleDispatcher.NotifyNavigatedToThenPublish(newHost, "parameter", () => events.Add("service-navigated"));
        Assert.Equal(["new:to:parameter", "service-navigated"], events);

        events.Clear();
        NavigationLifecycleDispatcher.NotifyNavigatedFromAfterFrameResult(oldViewModel, succeeded: false);
        Assert.Empty(events);

        NavigationLifecycleDispatcher.NotifyNavigatedFromAfterFrameResult(oldViewModel, succeeded: true);
        Assert.Equal(["old:from"], events);
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

    private sealed record PlainViewModel;

    private sealed class AwareViewModel(string name, List<string>? events = null) : INavigationAware
    {
        public void OnNavigatedTo(object parameter)
        {
            events?.Add($"{name}:to:{parameter}");
        }

        public void OnNavigatedFrom()
        {
            events?.Add($"{name}:from");
        }
    }

    private sealed class PlainHost(PlainViewModel viewModel) : IPageViewModelHost<PlainViewModel>
    {
        public PlainViewModel ViewModel { get; } = viewModel;
    }

    private sealed class AwareHost(AwareViewModel viewModel) : IPageViewModelHost<AwareViewModel>
    {
        public AwareViewModel ViewModel { get; } = viewModel;
    }

    private static class CompileFailureHarness
    {
        public static void RunAll(string hostSoftwareRoot)
        {
            RunCase(hostSoftwareRoot, "MissingHostContractPage", "public sealed class MissingHostContractPage : Page { public BaseViewModel ViewModel { get; } = new(); }", "Configure<MissingHostContractPage, BaseViewModel>();");
            RunCase(hostSoftwareRoot, "WrongViewModelPairingPage", "public sealed class WrongViewModelPairingPage : Page, IPageViewModelHost<DerivedViewModel> { public DerivedViewModel ViewModel { get; } = new(); }", "Configure<WrongViewModelPairingPage, BaseViewModel>();");
            RunCase(hostSoftwareRoot, "InvariantHostPage", "public sealed class InvariantHostPage : Page, IPageViewModelHost<DerivedViewModel> { public DerivedViewModel ViewModel { get; } = new(); }", "Configure<InvariantHostPage, BaseViewModel>();");
        }

        private static void RunCase(string hostSoftwareRoot, string typeName, string pageSource, string probeCall)
        {
            var workspace = CreateTempDirectory($"ui003-negative-{typeName}");
            try
            {
                var contractPath = Path.Combine(hostSoftwareRoot, "PRISM Utility", "Contracts", "ViewModels", "IPageViewModelHost.cs");
                File.WriteAllText(Path.Combine(workspace, "Harness.csproj"), $$"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net8.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <LangVersion>preview</LangVersion>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include="{{contractPath}}" Link="IPageViewModelHost.cs" />
                      </ItemGroup>
                    </Project>
                    """);
                File.WriteAllText(Path.Combine(workspace, "Program.cs"), $$"""
                    using Microsoft.UI.Xaml.Controls;
                    using PRISM_Utility.Contracts.ViewModels;

                    namespace Microsoft.UI.Xaml.Controls
                    {
                        public class Page;
                    }

                    namespace Ui003NegativeCompileHarness
                    {
                        public class BaseViewModel;
                        public sealed class DerivedViewModel : BaseViewModel;
                        {{pageSource}}

                        public static class Probe
                        {
                            public static object Value => {{probeCall}}

                            private static object Configure<TPage, TViewModel>()
                                where TPage : Page, IPageViewModelHost<TViewModel>
                                where TViewModel : notnull
                                => new();
                        }
                    }
                    """);

                var result = DotnetRunner.Run("build Harness.csproj --nologo", workspace, TimeSpan.FromSeconds(120));
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("CS0311", result.CombinedOutput, StringComparison.Ordinal);
                Assert.Contains(typeName, result.CombinedOutput, StringComparison.Ordinal);
                Assert.Contains("IPageViewModelHost", result.CombinedOutput, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectory(workspace);
            }

            Assert.False(Directory.Exists(workspace), $"Temporary compile harness was not cleaned up: {workspace}");
        }
    }

    private static class ProductionAppContractProbe
    {
        public static void Run(string hostSoftwareRoot, IReadOnlyList<(AppRoute Route, string Page, string ViewModel)> expectedRouteHosts)
        {
            var workspace = CreateTempDirectory("ui003-production-probe");
            try
            {
                var appProjectPath = Path.Combine(hostSoftwareRoot, "PRISM Utility", "PrismUtility.csproj");
                File.WriteAllText(Path.Combine(workspace, "ProductionProbe.csproj"), $$"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
                        <UseWinUI>true</UseWinUI>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <LangVersion>preview</LangVersion>
                        <Platforms>x64</Platforms>
                      </PropertyGroup>
                      <ItemGroup>
                        <ProjectReference Include="{{appProjectPath}}" />
                      </ItemGroup>
                    </Project>
                    """);
                File.WriteAllText(Path.Combine(workspace, "Program.cs"), """
                    using PRISM_Utility.Contracts.Navigation;
                    using PRISM_Utility.Contracts.ViewModels;
                    using PRISM_Utility.Services;
                    using PRISM_Utility.ViewModels;
                    using PRISM_Utility.Views;

                    var expected = new (AppRoute Route, Type Page, Type ViewModel)[]
                    {
                        (AppRoute.Main, typeof(MainPage), typeof(MainViewModel)),
                        (AppRoute.Log, typeof(LogPage), typeof(LogViewModel)),
                        (AppRoute.Scan, typeof(ScanPage), typeof(ScanViewModel)),
                        (AppRoute.ScanDebug, typeof(ScanDebugPage), typeof(ScanDebugViewModel)),
                        (AppRoute.Settings, typeof(SettingsPage), typeof(SettingsViewModel)),
                        (AppRoute.DeviceConfiguration, typeof(DeviceConfigurationPage), typeof(DeviceConfigurationViewModel))
                    };

                    var pageService = new PageService();
                    var failures = new List<string>();

                    foreach (var (route, pageType, viewModelType) in expected)
                    {
                        var registeredPageType = pageService.GetPageType(route);
                        if (registeredPageType != pageType)
                            failures.Add($"{route}: registered {registeredPageType.FullName}, expected {pageType.FullName}");

                        var expectedHostType = typeof(IPageViewModelHost<>).MakeGenericType(viewModelType);
                        if (!expectedHostType.IsAssignableFrom(pageType))
                            failures.Add($"{route}: {pageType.FullName} does not implement {expectedHostType.FullName}");

                        var genericHosts = pageType.GetInterfaces()
                            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IPageViewModelHost<>))
                            .Select(type => type.GenericTypeArguments[0].FullName)
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToArray();
                        if (!genericHosts.SequenceEqual([viewModelType.FullName], StringComparer.Ordinal))
                            failures.Add($"{route}: exact host identities were {string.Join(",", genericHosts)}, expected {viewModelType.FullName}");

                        Console.WriteLine($"{route}|{pageType.Assembly.GetName().Name}|{pageType.FullName}|{viewModelType.FullName}|{expectedHostType.FullName}");
                    }

                    if (failures.Count > 0)
                    {
                        foreach (var failure in failures)
                            Console.Error.WriteLine(failure);
                        Environment.Exit(1);
                    }
                    """);

                var result = DotnetRunner.Run("run --project ProductionProbe.csproj --disable-build-servers --no-launch-profile --no-self-contained -p:Platform=x64", workspace, TimeSpan.FromMinutes(2));
                Assert.Equal(0, result.ExitCode);
                foreach (var (route, page, viewModel) in expectedRouteHosts)
                {
                    Assert.Contains($"{route}|PrismUtility|PRISM_Utility.Views.{page}|PRISM_Utility.ViewModels.{viewModel}|", result.StandardOutput, StringComparison.Ordinal);
                    Assert.Contains($"PRISM_Utility.Contracts.ViewModels.IPageViewModelHost`1[[PRISM_Utility.ViewModels.{viewModel}", result.StandardOutput, StringComparison.Ordinal);
                }
            }
            finally
            {
                DeleteDirectory(workspace);
            }

            Assert.False(Directory.Exists(workspace), $"Temporary production probe was not cleaned up: {workspace}");
        }
    }

    private static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private sealed record DotnetResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => StandardOutput + Environment.NewLine + StandardError;
    }

    private static class DotnetRunner
    {
        public static DotnetResult Run(string arguments, string workingDirectory, TimeSpan timeout)
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
                throw new TimeoutException($"dotnet {arguments} did not finish within {timeout.TotalSeconds:0} seconds.");
            }

            return new DotnetResult(process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
        }
    }
}
