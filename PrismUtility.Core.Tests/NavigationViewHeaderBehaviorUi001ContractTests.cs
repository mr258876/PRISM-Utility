using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Ui001")]
public sealed class NavigationViewHeaderBehaviorUi001ContractTests
{
    [Fact]
    public void Ui001Baseline_ScanPagesDeclareHeaderModeNever()
    {
        var hostSoftwareRoot = FindHostSoftwareRoot();
        var scanPage = File.ReadAllText(Path.Combine(hostSoftwareRoot, "PRISM Utility", "Views", "ScanPage.xaml"));
        var scanDebugPage = File.ReadAllText(Path.Combine(hostSoftwareRoot, "PRISM Utility", "Views", "ScanDebugPage.xaml"));

        Assert.Contains("behaviors:NavigationViewHeaderBehavior.HeaderMode=\"Never\"", scanPage, StringComparison.Ordinal);
        Assert.Contains("behaviors:NavigationViewHeaderBehavior.HeaderMode=\"Never\"", scanDebugPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui001Contract_HeaderModeDependencyPropertyUsesNavigationViewHeaderMode()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "Behaviors", "NavigationViewHeaderBehavior.cs"));

        Assert.Contains("DependencyProperty.RegisterAttached(\"HeaderMode\", typeof(NavigationViewHeaderMode)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DependencyProperty.RegisterAttached(\"HeaderMode\", typeof(bool)", source, StringComparison.Ordinal);
    }

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
