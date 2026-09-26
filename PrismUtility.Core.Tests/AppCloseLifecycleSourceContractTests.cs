using System.Text.RegularExpressions;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "AppClose")]
public sealed class AppCloseLifecycleSourceContractTests
{
    [Fact]
    public void AppClose_SourceOnly_TracksResolvedTransientAndSingletonWithoutChangingDiLifetimes()
    {
        var source = ReadAppSource("App.xaml.cs");
        var resolve = ExtractMethod(source, "public static T GetService<T>()");
        var track = ExtractMethod(source, "private void TrackResolvedViewModel(object service)");

        Assert.Contains("services.AddTransient<ScanViewModel>();", source, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<ScanDebugViewModel>();", source, StringComparison.Ordinal);
        AssertBefore(resolve, "Host.Services.GetService(typeof(T))", "app.TrackResolvedViewModel(service);");
        AssertBefore(resolve, "app.TrackResolvedViewModel(service);", "return service;");
        Assert.Contains("List<WeakReference<ScanViewModel>>", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebugViewModel? _createdScanDebugViewModel", source, StringComparison.Ordinal);
        Assert.Contains("case ScanViewModel scanViewModel:", track, StringComparison.Ordinal);
        Assert.Contains("new WeakReference<ScanViewModel>(scanViewModel)", track, StringComparison.Ordinal);
        Assert.Contains("case ScanDebugViewModel scanDebugViewModel:", track, StringComparison.Ordinal);
        Assert.Contains("_createdScanDebugViewModel = scanDebugViewModel;", track, StringComparison.Ordinal);
        Assert.DoesNotContain("List<ScanViewModel>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AppClose_SourceOnly_SynchronouslyCancelsRepeatedSystemCloseBeforeOneAsyncShutdownAndFinalClose()
    {
        var source = ReadAppSource("App.xaml.cs");
        var launch = ExtractMethod(source, "protected async override void OnLaunched(");
        var closing = ExtractMethod(source, "private async void MainWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)");

        Assert.DoesNotContain("MainWindow.Closed", source, StringComparison.Ordinal);
        AssertBefore(launch, "MainWindow.AppWindow.Closing += MainWindow_Closing;", "await App.GetService<IActivationService>().ActivateAsync(args);");
        Assert.Matches(@"\A\{\s*if \(_allowWindowClose\)\s*return;\s*args.Cancel = true;\s*if \(Interlocked.Exchange\(ref _windowShutdownStarted, 1\) != 0\)\s*return;", closing);
        AssertBefore(closing, "args.Cancel = true;", "await Task.Yield();");
        AssertBefore(closing, "await Task.Yield();", "await ShutdownAsync();");
        AssertBefore(closing, "await ShutdownAsync();", "_allowWindowClose = true;");
        Assert.DoesNotContain("finally", closing, StringComparison.Ordinal);
        Assert.Matches(@"_allowWindowClose = true;\s*MainWindow.Close\(\);\s*\}\z", closing);
        Assert.Single(Regex.Matches(closing, @"await ShutdownAsync\(\);"));
        Assert.Single(Regex.Matches(closing, @"MainWindow.Close\(\);"));
        Assert.DoesNotContain(".Wait()", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AppClose_SourceOnly_SignalsTerminalVmCleanupBeforeSettingsIdleAndSharedManagerShutdown()
    {
        var shutdown = ExtractMethod(ReadAppSource("App.xaml.cs"), "private async Task ShutdownAsync()");

        AssertBefore(shutdown, "await CleanupCreatedViewModelsAsync();", "_settingsSaveCoordinator.CancelPendingOperations();");
        AssertBefore(shutdown, "await CleanupCreatedViewModelsAsync();", "_settingsSaveCoordinator.WhenIdleAsync()");
        AssertBefore(shutdown, "await CleanupCreatedViewModelsAsync();", "_scannerDeviceSessionManager.ShutdownAsync");
    }

    [Fact]
    public void AppClose_SourceOnly_CleansActualInstancesBeforeSharedManagerAndNeverResolvesAnotherVm()
    {
        var source = ReadAppSource("App.xaml.cs");
        var shutdown = ExtractMethod(source, "private async Task ShutdownAsync()");
        var cleanup = ExtractMethod(source, "private async Task CleanupCreatedViewModelsAsync()");

        AssertBefore(shutdown, "await CleanupCreatedViewModelsAsync();", "_scannerDeviceSessionManager.ShutdownAsync");
        Assert.Contains("_createdScanViewModels", cleanup, StringComparison.Ordinal);
        Assert.Contains("TryGetTarget(out var scanViewModel)", cleanup, StringComparison.Ordinal);
        Assert.Contains("scanViewModel.CleanupAsync", cleanup, StringComparison.Ordinal);
        Assert.Contains("_createdScanDebugViewModel", cleanup, StringComparison.Ordinal);
        Assert.Contains("scanDebugViewModel.CleanupAsync", cleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("GetService<ScanViewModel>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetService<ScanDebugViewModel>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRequiredService<ScanViewModel>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRequiredService<ScanDebugViewModel>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ScanViewModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ScanDebugViewModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DisconnectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DisposeAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupAsync", ReadAppSource("Views", "ScanPage.xaml.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupAsync", ReadAppSource("Views", "ScanDebugPage.xaml.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void AppClose_SourceOnly_DrainsAcceptedQaCaptureBeforeDisablingContentAndDestructiveVmCleanupOnlyInQaBuild()
    {
        var source = ReadAppSource("App.xaml.cs");
        var closing = ExtractMethod(source, "private async void MainWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)");
        var shutdown = ExtractMethod(source, "private async Task ShutdownAsync()");
        var qaBlock = Regex.Match(shutdown, @"#if PRISM_VISUAL_QA(?<body>[\s\S]*?)#endif");

        Assert.True(qaBlock.Success, "Capture drain must be compiled out of ordinary builds.");
        Assert.Contains("await PrismVisualQaCaptureService.DrainActiveAsync();", qaBlock.Groups["body"].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnabled = false", closing, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(source, @"content\.IsEnabled = false;"));
        AssertBefore(shutdown, "#endif", "if (MainWindow.Content is Control content)");
        AssertBefore(shutdown, "await PrismVisualQaCaptureService.DrainActiveAsync();", "content.IsEnabled = false;");
        AssertBefore(shutdown, "await PrismVisualQaCaptureService.DrainActiveAsync();", "await CleanupCreatedViewModelsAsync();");
        AssertBefore(shutdown, "content.IsEnabled = false;", "await CleanupCreatedViewModelsAsync();");
        Assert.DoesNotContain("PrismVisualQa", shutdown.Remove(qaBlock.Index, qaBlock.Length), StringComparison.Ordinal);
    }

    [Fact]
    public void AppClose_SourceOnly_FailedQaDrainAbortsDestructiveShutdownAndAllowsLaterCloseRetry()
    {
        var source = ReadAppSource("App.xaml.cs");
        var shutdown = ExtractMethod(source, "private async Task ShutdownAsync()");
        var closing = ExtractMethod(source, "private async void MainWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)");
        var qaBlock = Regex.Match(shutdown, @"#if PRISM_VISUAL_QA(?<body>[\s\S]*?)#endif");

        Assert.True(qaBlock.Success);
        Assert.Matches(@"catch \(Exception ex\)\s*\{\s*Debugger\.Log\([^\r\n]*\);\s*throw;\s*\}", qaBlock.Groups["body"].Value);
        Assert.Matches(@"catch \(Exception ex\)\s*\{\s*Debugger\.Log\([^\r\n]*\);\s*#if PRISM_VISUAL_QA\s*PrismVisualQaCaptureService\.CancelShutdown\(\);\s*#endif\s*Interlocked\.Exchange\(ref _windowShutdownStarted, 0\);\s*return;\s*\}\s*_allowWindowClose = true;\s*MainWindow.Close\(\);", closing);
        Assert.DoesNotContain("finally", closing, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnabled = false", closing, StringComparison.Ordinal);
    }

    [Fact]
    public void AppClose_SourceOnly_QaAdmissionClosesSynchronouslyAndReopensOnlyOnCanceledClose()
    {
        var source = ReadAppSource("App.xaml.cs");
        var closing = ExtractMethod(source, "private async void MainWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)");
        var capture = ReadAppSource("PrismVisualQaCaptureService.cs");
        var start = ExtractMethod(capture, "internal static void Start(ScanDebugPage page)");

        Assert.Contains("internal static bool IsShuttingDown => Current is App app && Volatile.Read(ref app._windowShutdownStarted) != 0;", source, StringComparison.Ordinal);
        AssertBefore(closing, "Interlocked.Exchange(ref _windowShutdownStarted, 1)", "PrismVisualQaCaptureService.BeginShutdown();");
        AssertBefore(closing, "PrismVisualQaCaptureService.BeginShutdown();", "await Task.Yield();");
        AssertBefore(closing, "PrismVisualQaCaptureService.CancelShutdown();", "Interlocked.Exchange(ref _windowShutdownStarted, 0);");
        Assert.Contains("internal static void BeginShutdown() => _shutdownRequested = true;", capture, StringComparison.Ordinal);
        Assert.Contains("internal static void CancelShutdown() => _shutdownRequested = false;", capture, StringComparison.Ordinal);
        AssertBefore(start, "_shutdownRequested", "_page = page;");
        Assert.DoesNotContain("_shutdownRequested = false", start, StringComparison.Ordinal);
    }

    [Fact]
    public void AppClose_SourceOnly_QaShutdownRejectsTimerAndQueuedRequestsButDrainsAcceptedWork()
    {
        var capture = ReadAppSource("PrismVisualQaCaptureService.cs");
        var start = ExtractMethod(capture, "internal static void Start(ScanDebugPage page)");
        var tick = ExtractMethod(capture, "private static async void ProcessRequests(object? sender, object e)");
        var process = ExtractMethod(capture, "private static async Task ProcessRequestsAsync(ScanDebugPage page)");
        var stop = ExtractMethod(capture, "internal static async Task StopAsync(ScanDebugPage page)");

        Assert.Matches(@"\A\{\s*if \(_shutdownRequested \|\| _stopping \|\| _processingTask is \{ IsCompleted: false \}\)\s*return;", start);
        AssertBefore(start, "if (_shutdownRequested || _stopping", "_page = page;");
        Assert.Matches(@"\A\{\s*if \(_shutdownRequested\)\s*return;\s*if \(_stopping\)\s*return;", tick);
        AssertBefore(tick, "if (_shutdownRequested)", "_processingTask = ProcessRequestsAsync(page);");
        Assert.Matches(@"foreach \(var requestPath in Directory\.GetFiles\(GetRequestDirectory\(\), ""\*\.request\.json""\)\)\s*\{\s*if \(_shutdownRequested\)\s*break;\s*if \(_stopping\)\s*break;", process);
        AssertBefore(process, "if (_shutdownRequested)", "var resultPath = Path.ChangeExtension(requestPath, \".result.json\");");
        Assert.Single(Regex.Matches(process, @"\b_shutdownRequested\b"));
        Assert.Contains("var result = await ProcessRequestAsync(page, request);", process, StringComparison.Ordinal);
        Assert.Contains("await WriteResultAsync(resultPath, result);", process, StringComparison.Ordinal);
        AssertBefore(stop, "var processingTask = _processingTask;", "await processingTask;");
        AssertBefore(stop, "await processingTask;", "_page = null;");
    }

    [Fact]
    public void AppClose_SourceOnly_BoundsSettingsVmAndScannerWaitsAndReportsFailuresBeforeMirrorLast()
    {
        var source = ReadAppSource("App.xaml.cs");
        var shutdown = ExtractMethod(source, "private async Task ShutdownAsync()");
        var cleanup = ExtractMethod(source, "private async Task CleanupCreatedViewModelsAsync()");
        var cleanupOne = ExtractMethod(source, "private static async Task CleanupViewModelAsync(");

        AssertBefore(shutdown, "_settingsSaveCoordinator.CancelPendingOperations();", "_settingsSaveCoordinator.WhenIdleAsync().WaitAsync(settingsShutdownTimeout.Token)");
        AssertBefore(shutdown, "_settingsSaveCoordinator.WhenIdleAsync()", "_scannerDeviceSessionManager.ShutdownAsync");
        AssertBefore(shutdown, "_scannerDeviceSessionManager.ShutdownAsync", "_settingsSaveCoordinator.Dispose();");
        AssertBefore(shutdown, "_settingsSaveCoordinator.Dispose();", "GetService<IDebugOutputMirrorService>().ShutdownAsync");
        Assert.Contains("new CancellationTokenSource(SettingsShutdownTimeout)", shutdown, StringComparison.Ordinal);
        Assert.Contains("new CancellationTokenSource(ScannerShutdownTimeout)", shutdown, StringComparison.Ordinal);
        Assert.Contains(".WaitAsync(scannerShutdownTimeout.Token)", shutdown, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll(cleanupTasks).WaitAsync(ViewModelShutdownTimeout)", cleanup, StringComparison.Ordinal);
        Assert.Contains("catch (TimeoutException", cleanup, StringComparison.Ordinal);
        Assert.Contains("catch (Exception", cleanupOne, StringComparison.Ordinal);
        Assert.Contains("Debugger.Log", cleanupOne, StringComparison.Ordinal);
        Assert.True(Regex.Matches(shutdown, @"catch \(Exception").Count >= 4, "Settings, VM, scanner, disposal and mirror failures must not escape their stages.");
        Assert.Contains("Debugger.Log", shutdown, StringComparison.Ordinal);
    }

    private static void AssertBefore(string source, string first, string second)
    {
        var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        var secondIndex = source.IndexOf(second, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, $"Missing '{first}'.");
        Assert.True(secondIndex > firstIndex, $"Expected '{first}' before '{second}'.");
    }

    private static string ExtractMethod(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Could not find {signature} in App.xaml.cs.");
        var bodyStart = source.IndexOf('{', signatureIndex);
        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not find the end of {signature}.");
    }

    private static string ReadAppSource(params string[] relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var appRoot = Path.Combine(directory.FullName, "PRISM Utility");
            if (Directory.Exists(appRoot))
                return File.ReadAllText(Path.Combine(appRoot, Path.Combine(relativePath)));
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }
}
