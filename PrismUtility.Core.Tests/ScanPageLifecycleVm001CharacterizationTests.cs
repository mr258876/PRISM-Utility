using System.Text.RegularExpressions;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "VM001")]
[Trait("Category", "Lifecycle")]
public sealed class ScanPageLifecycleVm001CharacterizationTests
{
    [Fact]
    public void Vm001_SourceOnly_ScanPageCharacterizesCachedLoadedUnloadedSubscriptionLifecycle()
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
    public void Vm001_SourceOnly_ScanPageGuardsDuplicateLoadedAndSymmetricallyDetachesOnUnloaded()
    {
        var scanPage = ReadAppSource("Views", "ScanPage.xaml.cs");
        var loaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnLoaded(object sender, RoutedEventArgs e)");
        var unloaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnUnloaded(object sender, RoutedEventArgs e)");

        Assert.Single(Regex.Matches(loaded, "PropertyChanged \\+= OnViewModelPropertyChanged", RegexOptions.CultureInvariant));
        Assert.Single(Regex.Matches(unloaded, "PropertyChanged -= OnViewModelPropertyChanged", RegexOptions.CultureInvariant));
        Assert.Contains("private bool _isLoaded;", scanPage, StringComparison.Ordinal);
        Assert.Matches(@"\A\{\s*if \(_isLoaded\)\s*return;[\s\S]*?ViewModel.PropertyChanged \+= OnViewModelPropertyChanged;\s*_isLoaded = true;\s*ViewModel.Activate\(\);", loaded);
        Assert.Matches(@"\A\{\s*if \(!_isLoaded\)\s*return;[\s\S]*?ViewModel.PropertyChanged -= OnViewModelPropertyChanged;\s*_isLoaded = false;\s*ViewModel.Deactivate\(\);", unloaded);
        Assert.DoesNotContain("Loaded -= OnLoaded", scanPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm001_ScanViewModelCharacterizesConstructorActivationAndAppOwnedTerminalCleanup()
    {
        var app = ReadAppSource("App.xaml.cs");
        var scanPage = ReadAppSource("Views", "ScanPage.xaml.cs");
        var scanViewModel = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var resolve = ExtractMemberBodyAtDeclaration(app, "public static T GetService<T>()");
        var track = ExtractMemberBodyAtDeclaration(app, "private void TrackResolvedViewModel(object service)");
        var closing = ExtractMemberBodyAtDeclaration(app, "private async void MainWindow_Closing(");
        var shutdown = ExtractMemberBodyAtDeclaration(app, "private async Task ShutdownAsync()");
        var appCleanup = ExtractMemberBodyAtDeclaration(app, "private async Task CleanupCreatedViewModelsAsync()");
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
        Assert.Contains("app.TrackResolvedViewModel(service);", resolve, StringComparison.Ordinal);
        Assert.Contains("new WeakReference<ScanViewModel>(scanViewModel)", track, StringComparison.Ordinal);
        Assert.Contains("await ShutdownAsync();", closing, StringComparison.Ordinal);
        Assert.Contains("scanViewModels = _createdScanViewModels.ToArray();", appCleanup, StringComparison.Ordinal);
        Assert.Contains("reference.TryGetTarget(out var scanViewModel)", appCleanup, StringComparison.Ordinal);
        Assert.Contains("scanViewModel.CleanupAsync", appCleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("GetService<ScanViewModel>", appCleanup, StringComparison.Ordinal);
        Assert.Matches(@"await CleanupCreatedViewModelsAsync\(\);[\s\S]*?_scannerDeviceSessionManager\.ShutdownAsync\(", shutdown);
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

    [Fact]
    public void Vm001_SourceOnly_TerminalCleanupCancelsScanWithoutReleasingHardware()
    {
        var scanViewModel = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var cleanup = ExtractMemberBodyAtDeclaration(scanViewModel, "public Task CleanupAsync()");
        var deactivate = ExtractMemberBodyAtDeclaration(scanViewModel, "public void Deactivate()");
        var startScan = ExtractMemberBodyAtDeclaration(scanViewModel, "private async Task StartScan()");

        Assert.Matches(@"_isDisposed = true;\s*_uiLifetimeCts.Cancel\(\);\s*_scanCts\?\.Cancel\(\);", cleanup);
        Assert.Contains("_scanCts?.Dispose();", startScan, StringComparison.Ordinal);
        Assert.DoesNotContain("_scanCts", deactivate, StringComparison.Ordinal);
        Assert.DoesNotContain("_scanCts?.Dispose()", cleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("StopMotorAsync", cleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("ShutdownAsync", cleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("DisconnectAsync", cleanup, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm001_SourceOnly_QueuedScanProgressAndStatusCheckUiLifetimeAtDispatch()
    {
        var startScan = ExtractMemberBodyAtDeclaration(ReadAppSource("ViewModels", "ScanViewModel.cs"), "private async Task StartScan()");

        Assert.Matches(@"progress\s*=>\s*\{\s*if \(!uiToken.IsCancellationRequested\)\s*_dispatcher.TryEnqueue\(\(\) =>\s*\{\s*if \(!uiToken.IsCancellationRequested\)\s*ApplyProgress\(progress\);\s*\}\);", startScan);
        Assert.Matches(@"status\s*=>\s*\{\s*if \(!uiToken.IsCancellationRequested\)\s*_dispatcher.TryEnqueue\(\(\) =>\s*\{\s*if \(!uiToken.IsCancellationRequested\)\s*StatusText = ScanRuntimeMessageLocalizer.LocalizeScanViewStatus\(status\);\s*\}\);", startScan);
    }

    [Fact]
    public void Vm001_SourceOnly_DelayedValidationCannotStartScanAfterTerminalCleanup()
    {
        var scanViewModel = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var startScan = ExtractMemberBodyAtDeclaration(scanViewModel, "private async Task StartScan()");
        var cleanup = ExtractMemberBodyAtDeclaration(scanViewModel, "public Task CleanupAsync()");
        var deactivate = ExtractMemberBodyAtDeclaration(scanViewModel, "public void Deactivate()");

        Assert.Contains("_isDisposed = true;", cleanup, StringComparison.Ordinal);
        Assert.Matches(@"\A\{\s*var request = await ValidateStartScanAsync\(\);\s*if \(_isDisposed \|\| request is null\)\s*return;\s*ClearStartValidationState\(\);[\s\S]*?_scanCts = new CancellationTokenSource\(\);", startScan);
        Assert.DoesNotContain("_isDisposed = true;", deactivate, StringComparison.Ordinal);
        Assert.DoesNotContain("_scanCts", deactivate, StringComparison.Ordinal);
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
