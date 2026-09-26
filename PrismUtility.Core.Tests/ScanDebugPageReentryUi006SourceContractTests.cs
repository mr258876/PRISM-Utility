using System.Text.RegularExpressions;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UI006")]
public sealed class ScanDebugPageReentryUi006SourceContractTests
{
    [Fact]
    public void CachedPage_LoadContinuationOnlyUpdatesItsOwnActiveInterval()
    {
        var source = ReadPageSource();
        var loaded = ExtractBody(source, "private async void OnLoaded(");
        var current = ExtractBody(source, "private bool IsCurrentActivation(");

        Assert.Contains("if (_isPageActive)", loaded, StringComparison.Ordinal);
        Assert.Contains("var activationEpoch = ++_activationEpoch;", loaded, StringComparison.Ordinal);
        Assert.Contains("_isPageActive = true;", loaded, StringComparison.Ordinal);
        AssertBefore(loaded, "var pageOwner = ViewModel.ReservePageActivation();", "await PrismVisualQaCaptureService.DrainActiveAsync();");
        AssertBefore(loaded, "await PrismVisualQaCaptureService.DrainActiveAsync();", "SubscribeViewModelEvents();");
        AssertBefore(loaded, "if (!IsCurrentActivation(activationEpoch) || !ViewModel.IsCurrentPageOwner(pageOwner))", "ViewModel.AttachRuntimeBindingsForPage(pageOwner)");
        AssertBefore(loaded, "var activationEpoch = ++_activationEpoch;", "await ViewModel.RefreshDeviceSettingsBindingsAsync();");
        var afterRefresh = loaded[loaded.IndexOf("await ViewModel.RefreshDeviceSettingsBindingsAsync();", StringComparison.Ordinal)..];
        AssertBefore(afterRefresh, "if (!IsCurrentActivation(activationEpoch) || !ViewModel.IsCurrentPageOwner(pageOwner))", "InitializeZoomScaleComboBox();");
        AssertBefore(loaded, "await PrismVisualQaCaptureService.DrainActiveAsync();", "PrismVisualQaPendingCalibrationHook.ApplyPageState(this, SetActiveWorkbenchSection);");
        Assert.Contains("_isPageActive && activationEpoch == _activationEpoch", current, StringComparison.Ordinal);
    }

    [Fact]
    public void CachedPage_UnloadInvalidatesCallbacksThenDrainsQaBeforeDetachingOrDisposing()
    {
        var unloaded = ExtractBody(ReadPageSource(), "private async void OnUnloaded(");

        Assert.Contains("if (!_isPageActive)", unloaded, StringComparison.Ordinal);
        Assert.Contains("_isPageActive = false;", unloaded, StringComparison.Ordinal);
        Assert.Contains("var unloadEpoch = ++_activationEpoch;", unloaded, StringComparison.Ordinal);
        AssertBefore(unloaded, "var unloadEpoch = ++_activationEpoch;", "ViewModel.InvalidatePageActivation(pageOwner);");
        AssertBefore(unloaded, "var pageOwner = _pageActivationOwner;", "ViewModel.InvalidatePageActivation(pageOwner);");
        AssertBefore(unloaded, "ViewModel.InvalidatePageActivation(pageOwner);", "_dialogLifetime.Retire(this, unloadEpoch - 1);");
        AssertBefore(unloaded, "ViewModel.InvalidatePageActivation(pageOwner);", "await PrismVisualQaCaptureService.StopAsync(this);");
        AssertBefore(unloaded, "var unloadEpoch = ++_activationEpoch;", "App.MainWindow.Activated -= MainWindow_Activated;");
        AssertBefore(unloaded, "var unloadEpoch = ++_activationEpoch;", "await PrismVisualQaCaptureService.StopAsync(this);");
        AssertBefore(unloaded, "await PrismVisualQaCaptureService.StopAsync(this);", "if (_isPageActive || unloadEpoch != _activationEpoch)");
        AssertBefore(unloaded, "await PrismVisualQaCaptureService.StopAsync(this);", "UnsubscribeViewModelEvents();");
        AssertBefore(unloaded, "UnsubscribeViewModelEvents();", "await ViewModel.DeactivateForPageAsync(pageOwner);");
        var afterQaStop = unloaded[unloaded.IndexOf("await PrismVisualQaCaptureService.StopAsync(this);", StringComparison.Ordinal)..];
        AssertBefore(afterQaStop, "if (_isPageActive || unloadEpoch != _activationEpoch)", "DisposePreviewBitmap();");
        AssertBefore(afterQaStop, "await ViewModel.DeactivateForPageAsync(pageOwner);", "DisposePreviewBitmap();");
    }

    [Fact]
    public void CachedPage_PendingReservationCannotReplaceLastAttachedOwnerBeforeFinalUnload()
    {
        var source = ReadPageSource();
        var loaded = ExtractBody(source, "private async void OnLoaded(");
        var unloaded = ExtractBody(source, "private async void OnUnloaded(");
        const string attach = "if (!ViewModel.AttachRuntimeBindingsForPage(pageOwner))";
        var beforeAttach = loaded[..loaded.IndexOf(attach, StringComparison.Ordinal)];

        Assert.DoesNotContain("_pageActivationOwner = pageOwner;", beforeAttach, StringComparison.Ordinal);
        Assert.Equal(1, Count(loaded, "_pageActivationOwner = pageOwner;"));
        AssertBefore(loaded, "await PrismVisualQaCaptureService.DrainActiveAsync();", attach);
        AssertBefore(loaded, attach, "_pageActivationOwner = pageOwner;");
        var failedAttachBranch = loaded[loaded.IndexOf(attach, StringComparison.Ordinal)..loaded.IndexOf("_pageActivationOwner = pageOwner;", StringComparison.Ordinal)];
        Assert.Contains("return;", failedAttachBranch, StringComparison.Ordinal);

        Assert.Contains("var pageOwner = _pageActivationOwner;", unloaded, StringComparison.Ordinal);
        Assert.Contains("if (pageOwner is not null)\n            await ViewModel.DeactivateForPageAsync(pageOwner);",
            unloaded.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        var afterDeactivation = unloaded[unloaded.IndexOf("await ViewModel.DeactivateForPageAsync(pageOwner);", StringComparison.Ordinal)..];
        AssertBefore(afterDeactivation, "if (_isPageActive || unloadEpoch != _activationEpoch)", "_pageActivationOwner = null;");
        AssertBefore(afterDeactivation, "_pageActivationOwner = null;", "DisposePreviewBitmap();");
    }

    [Fact]
    public void DifferentPage_ReservationDoesNotChangePreviewAndOnlyCurrentOwnerCanAttachOrDeactivate()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var reserve = ExtractBody(source, "internal object ReservePageActivation()");
        var attach = ExtractBody(source, "internal bool AttachRuntimeBindingsForPage(object owner)");
        var publicDeactivate = ExtractBody(source, "public async Task DeactivateAsync()");

        Assert.Contains("_reservedPageOwner = owner;", reserve, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewFrame", reserve, StringComparison.Ordinal);
        Assert.DoesNotContain("DeactivateAsync", reserve, StringComparison.Ordinal);
        Assert.Contains("internal bool IsCurrentPageOwner(object owner)\n        => !_isCleanedUp && ReferenceEquals(_reservedPageOwner, owner);", source.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        AssertBefore(attach, "!IsCurrentPageOwner(owner)", "AttachRuntimeBindings();");
        AssertBefore(attach, "AttachRuntimeBindings();", "_attachedPageOwner = owner;");
        Assert.Contains("internal Task DeactivateForPageAsync(object owner)\n        => ReferenceEquals(_attachedPageOwner, owner) ? DeactivateAsync() : Task.CompletedTask;", source.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("_attachedPageOwner = null;", publicDeactivate, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualQaDrain_JoinsAcceptedProcessingBeforeAllowingANewStart()
    {
        var source = ReadAppSource("PrismVisualQaCaptureService.cs");
        var start = ExtractBody(source, "internal static void Start(ScanDebugPage page)");
        var stop = ExtractBody(source, "internal static async Task StopAsync(ScanDebugPage page)");

        AssertBefore(start, "_shutdownRequested || _stopping || _processingTask is { IsCompleted: false }", "_page = page;");
        Assert.Contains("_page is { } page ? StopAsync(page) : Task.CompletedTask", source, StringComparison.Ordinal);
        AssertBefore(stop, "await processingTask;", "_page = null;");
        AssertBefore(stop, "_page = null;", "_stopping = false;");
        Assert.Contains("var result = await ProcessRequestAsync(page, request);", source, StringComparison.Ordinal);
        Assert.Contains("await WriteResultAsync(resultPath, result);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualQaDrain_CompletedOrFaultedTaskClearsOnlyItsOwnPageAndReopensStart()
    {
        var source = ReadAppSource("PrismVisualQaCaptureService.cs");
        var stop = ExtractBody(source, "internal static async Task StopAsync(ScanDebugPage page)");
        var finalizationIndex = stop.IndexOf("finally", StringComparison.Ordinal);
        Assert.True(finalizationIndex >= 0, "StopAsync must finalize a settled task even when its await throws.");
        var finalization = stop[finalizationIndex..];

        AssertBefore(stop, "_stopping = true;", "await processingTask;");
        AssertBefore(stop, "await processingTask;", "finally");
        AssertBefore(finalization, "processingTask is null || processingTask.IsCompleted", "_page = null;");
        AssertBefore(finalization, "ReferenceEquals(_page, page)", "_page = null;");
        AssertBefore(finalization, "ReferenceEquals(_processingTask, processingTask)", "_page = null;");
        AssertBefore(finalization, "_page = null;", "_stopping = false;");
        Assert.Contains("_processingTask = null;", finalization, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualQaTimer_ReportsFaultedTickWithoutThrowingFromAsyncVoid()
    {
        var tick = ExtractBody(ReadAppSource("PrismVisualQaCaptureService.cs"), "private static async void ProcessRequests(");

        Assert.Matches(@"try\s*\{\s*await _processingTask;\s*\}\s*catch \(Exception ex\)\s*\{\s*Debugger\.Log\(", tick);
    }

    [Fact]
    public void CachedPage_QaDrainFaultIsReportedAndTerminalLoadCannotApplyPageState()
    {
        var source = ReadPageSource();
        var loaded = ExtractBody(source, "private async void OnLoaded(");
        var unloaded = ExtractBody(source, "private async void OnUnloaded(");
        var current = ExtractBody(source, "private bool IsCurrentActivation(");

        Assert.Matches(@"try\s*\{\s*await PrismVisualQaCaptureService\.DrainActiveAsync\(\);\s*\}\s*catch \(Exception ex\)\s*\{\s*Debugger\.Log\(", loaded);
        Assert.Matches(@"try\s*\{\s*await PrismVisualQaCaptureService\.StopAsync\(this\);\s*\}\s*catch \(Exception ex\)\s*\{\s*Debugger\.Log\(", unloaded);
        AssertBefore(unloaded, "await PrismVisualQaCaptureService.StopAsync(this);", "DisposePreviewBitmap();");
        AssertBefore(loaded, "if (App.IsShuttingDown)", "var pageOwner = ViewModel.ReservePageActivation();");
        Assert.Contains("!App.IsShuttingDown", current, StringComparison.Ordinal);
        var afterRefresh = loaded[loaded.IndexOf("await ViewModel.RefreshDeviceSettingsBindingsAsync();", StringComparison.Ordinal)..];
        AssertBefore(afterRefresh, "if (!IsCurrentActivation(activationEpoch) || !ViewModel.IsCurrentPageOwner(pageOwner))", "PrismVisualQaPendingCalibrationHook.ApplyPageState(this, SetActiveWorkbenchSection);");
    }

    [Fact]
    public void CachedPage_QueuedPageCallbacksCaptureEpochAndCheckItWhenTheyExecute()
    {
        var source = ReadPageSource();
        var enqueue = ExtractBody(source, "private void EnqueueForCurrentActivation(Action action)");
        var propertyChanged = ExtractBody(source, "private void OnViewModelPropertyChanged(");
        var profileNavigation = ExtractBody(source, "private void OnCurrentFilmProfileIssueNavigationRequested(");
        var roiNavigation = ExtractBody(source, "private void OnRoiIssueNavigationRequested(");
        var sizeChanged = ExtractBody(source, "private void ScanDebugRootGrid_SizeChanged(");
        var previewLayout = ExtractBody(source, "private void RefreshPreviewLayout()");
        var responsiveLayout = ExtractBody(source, "private void UpdateWorkbenchPreviewLayout(double availableWidth)");

        AssertBefore(enqueue, "var activationEpoch = _activationEpoch;", "DispatcherQueue.TryEnqueue(() =>");
        AssertBefore(enqueue, "DispatcherQueue.TryEnqueue(() =>", "if (IsCurrentActivation(activationEpoch))");
        AssertBefore(enqueue, "if (IsCurrentActivation(activationEpoch))", "action();");
        Assert.Equal(5, Count(propertyChanged, "EnqueueForCurrentActivation("));
        Assert.DoesNotContain("DispatcherQueue.TryEnqueue", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(() =>", profileNavigation, StringComparison.Ordinal);
        Assert.Contains("IsCurrentFilmProfileNavigationStillValid()", profileNavigation, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(() =>", roiNavigation, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(UpdateWorkbenchPreviewLayout);", sizeChanged, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(ApplyInitialFitZoom);", previewLayout, StringComparison.Ordinal);
        AssertBefore(responsiveLayout, "var activationEpoch = _activationEpoch;", "DispatcherQueue.TryEnqueue(() =>");
        AssertBefore(responsiveLayout, "DispatcherQueue.TryEnqueue(() =>", "if (!IsCurrentActivation(activationEpoch)");
        Assert.Equal(2, Count(source, "DispatcherQueue.TryEnqueue("));
    }

    private static void AssertBefore(string source, string before, string after)
    {
        var beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
        var afterIndex = source.IndexOf(after, StringComparison.Ordinal);
        Assert.True(beforeIndex >= 0 && afterIndex > beforeIndex, $"Expected '{before}' before '{after}'.");
    }

    private static int Count(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ExtractBody(string source, string declaration)
    {
        var start = source.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing method {declaration}");
        var openingBrace = source.IndexOf('{', start);
        Assert.True(openingBrace >= 0, $"Missing method body {declaration}");
        var depth = 0;
        for (var index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[openingBrace..(index + 1)];
        }

        throw new InvalidOperationException($"Unclosed method body {declaration}");
    }

    private static string ReadPageSource() => ReadAppSource("Views", "ScanDebugPage.xaml.cs");

    private static string ReadAppSource(params string[] path)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var sourcePath = Path.Combine(directory.FullName, "PRISM Utility", Path.Combine(path));
            if (File.Exists(sourcePath))
                return File.ReadAllText(sourcePath);
        }

        throw new FileNotFoundException($"Could not locate app source: {Path.Combine(path)}");
    }
}
