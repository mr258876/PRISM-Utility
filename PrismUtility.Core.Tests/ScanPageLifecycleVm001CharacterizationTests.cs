using System.Text.RegularExpressions;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "VM001")]
[Trait("Category", "Lifecycle")]
public sealed class ScanPageLifecycleVm001CharacterizationTests
{
    [Fact]
    public void Vm001_ScanPageCharacterizesCachedLoadedUnloadedSubscriptionLifecycle()
    {
        var scanPage = ReadAppSource("Views", "ScanPage.xaml.cs");
        var scanPageXaml = ReadAppSource("Views", "ScanPage.xaml");
        var constructor = ExtractMemberBodyAtDeclaration(scanPage, "public ScanPage()");
        var loaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnLoaded(object sender, RoutedEventArgs e)");
        var unloaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnUnloaded(object sender, RoutedEventArgs e)");

        Assert.Contains("NavigationCacheMode=\"Enabled\"", scanPageXaml, StringComparison.Ordinal);
        Assert.Contains("Loaded += OnLoaded;", constructor, StringComparison.Ordinal);
        Assert.Contains("Unloaded += OnUnloaded;", constructor, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged += OnViewModelPropertyChanged;", loaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.Activate();", loaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged -= OnViewModelPropertyChanged;", unloaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.Deactivate();", unloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.CleanupAsync()", scanPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm001_ScanPageCharacterizesDuplicateLoadedAsUnguardedPropertyChangedSubscription()
    {
        var scanPage = ReadAppSource("Views", "ScanPage.xaml.cs");
        var loaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnLoaded(object sender, RoutedEventArgs e)");
        var unloaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnUnloaded(object sender, RoutedEventArgs e)");

        Assert.Single(Regex.Matches(loaded, "PropertyChanged \\+= OnViewModelPropertyChanged", RegexOptions.CultureInvariant));
        Assert.Single(Regex.Matches(unloaded, "PropertyChanged -= OnViewModelPropertyChanged", RegexOptions.CultureInvariant));
        Assert.DoesNotContain("_isLoaded", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("_isPropertyChangedSubscribed", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Loaded -= OnLoaded", scanPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm001_ScanViewModelCharacterizesConstructorActivationAndCleanupOwnershipGap()
    {
        var app = ReadAppSource("App.xaml.cs");
        var scanPage = ReadAppSource("Views", "ScanPage.xaml.cs");
        var scanViewModel = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var constructor = ExtractMemberBodyAtDeclaration(scanViewModel, "public ScanViewModel(");
        var activate = ExtractMemberBodyAtDeclaration(scanViewModel, "public void Activate()");
        var deactivate = ExtractMemberBodyAtDeclaration(scanViewModel, "public void Deactivate()");
        var cleanup = ExtractMemberBodyAtDeclaration(scanViewModel, "public Task CleanupAsync()");

        Assert.Contains("Activate();", constructor, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.TargetsChanged += OnSessionTargetsChanged;", activate, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.SnapshotChanged += OnSessionSnapshotChanged;", activate, StringComparison.Ordinal);
        Assert.Contains("_areSessionEventsSubscribed = true;", activate, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.TargetsChanged -= OnSessionTargetsChanged;", deactivate, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.SnapshotChanged -= OnSessionSnapshotChanged;", deactivate, StringComparison.Ordinal);
        Assert.Contains("_areSessionEventsSubscribed = false;", deactivate, StringComparison.Ordinal);
        Assert.Contains("_uiLifetimeCts.Cancel();", cleanup, StringComparison.Ordinal);
        Assert.Contains("Deactivate();", cleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupAsync", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.CleanupAsync()", scanPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm001_ScanViewModelCharacterizesUiLifetimeCtsAsCleanupOnlyOwner()
    {
        var scanViewModel = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var cleanup = ExtractMemberBodyAtDeclaration(scanViewModel, "public Task CleanupAsync()");

        Assert.Contains("private readonly CancellationTokenSource _uiLifetimeCts = new();", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("var uiToken = _uiLifetimeCts.Token;", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("_uiLifetimeCts.Cancel();", cleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("_uiLifetimeCts.Dispose", scanViewModel, StringComparison.Ordinal);
    }

    private static string ReadAppSource(params string[] relativePath)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", Path.Combine(relativePath)));

    private static string ExtractMemberBodyAtDeclaration(string source, string declaration)
    {
        var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
        if (declarationIndex < 0)
            throw new InvalidOperationException($"Could not find declaration: {declaration}");

        var bodyStart = source.IndexOf('{', declarationIndex);
        if (bodyStart < 0)
            throw new InvalidOperationException($"Could not find body for declaration: {declaration}");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[bodyStart..(index + 1)];
            }
        }

        throw new InvalidOperationException($"Could not extract body for declaration: {declaration}");
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
