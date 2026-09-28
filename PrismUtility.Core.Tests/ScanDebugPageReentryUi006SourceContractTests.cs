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
    public void VisualQaTaskMemoryOverride_SkipsBothForcedSelectionsButStillStartsCapture()
    {
        var hook = ReadAppSource("PrismVisualQaPendingCalibrationHook.cs");
        var apply = ExtractBody(hook, "internal static void ApplyPageState(");
        var selection = ExtractBody(apply, "if (!preserveTaskSelection)");

        Assert.StartsWith("#if PRISM_VISUAL_QA", hook, StringComparison.Ordinal);
        Assert.Matches(@"var preserveTaskSelection = string\.Equals\(\s*Environment\.GetEnvironmentVariable\(""PRISM_VISUAL_QA_PRESERVE_TASK_SELECTION""\),\s*""1"",\s*StringComparison\.Ordinal\);", apply);
        Assert.Contains("if (IsEmptyStateCapture())\n                setActiveWorkbenchSection(1);", selection.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("setActiveWorkbenchSection(forceMotionReadRequired ? 5 : 2);", selection, StringComparison.Ordinal);
        Assert.Contains("Environment.GetEnvironmentVariable(\"PRISM_VISUAL_QA_SECTION_INDEX\")", selection, StringComparison.Ordinal);
        Assert.Contains("setActiveWorkbenchSection(sectionIndex);", selection, StringComparison.Ordinal);
        Assert.Equal(3, Count(selection, "setActiveWorkbenchSection("));
        AssertBefore(apply, "if (!preserveTaskSelection)", "PrismVisualQaCaptureService.Start(page);");
        Assert.DoesNotContain("PrismVisualQaCaptureService.Start(page);", selection, StringComparison.Ordinal);
        Assert.Equal(3, Count(apply, "setActiveWorkbenchSection("));
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
        var beginIssueFocus = ExtractBody(source, "private void BeginIssueTargetFocus(");
        var completeIssueFocus = ExtractBody(source, "private void TryCompleteIssueTargetFocus(");
        var guardStart = source.IndexOf("private bool IsCurrentFilmProfileNavigationStillValid()", StringComparison.Ordinal);
        Assert.True(guardStart >= 0);
        var guardEnd = source.IndexOf("private void BeginIssueTargetFocus(", guardStart, StringComparison.Ordinal);
        Assert.True(guardEnd > guardStart);
        var navigationGuard = source[guardStart..guardEnd];
        var roiNavigation = ExtractBody(source, "private void OnRoiIssueNavigationRequested(");
        var sizeChanged = ExtractBody(source, "private void ScanDebugRootGrid_SizeChanged(");
        var previewLayout = ExtractBody(source, "private void RefreshPreviewLayout()");
        var responsiveLayout = ExtractBody(source, "private void UpdateWorkbenchPreviewLayout(double availableWidth)");

        AssertBefore(enqueue, "var activationEpoch = _activationEpoch;", "DispatcherQueue.TryEnqueue(() =>");
        AssertBefore(enqueue, "DispatcherQueue.TryEnqueue(() =>", "if (IsCurrentActivation(activationEpoch))");
        AssertBefore(enqueue, "if (IsCurrentActivation(activationEpoch))", "action();");
        Assert.Equal(5, Count(propertyChanged, "EnqueueForCurrentActivation("));
        Assert.Contains("nameof(ScanDebugViewModel.SelectedCalibrationChannel)", propertyChanged, StringComparison.Ordinal);
        AssertBefore(propertyChanged, "nameof(ScanDebugViewModel.SelectedCalibrationChannel)", "ExitBwIssueView(returnFocus: false);");
        Assert.Contains("CancelIssueTargetFocus();", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("CancelBwIssueFocus();", ExtractBody(source, "private async void OnUnloaded("), StringComparison.Ordinal);
        Assert.Contains("CancelIssueTargetFocus();", ExtractBody(source, "private async void OnUnloaded("), StringComparison.Ordinal);
        Assert.Contains("ViewModel.CancelPendingCalibrationChannelSelection();", ExtractBody(source, "private async void OnUnloaded("), StringComparison.Ordinal);
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        Assert.Contains("Visibility=\"{x:Bind ViewModel.ManualReferenceLocalEditVisibility, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.ManualReferenceFeedbackVisibility, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherQueue.TryEnqueue", propertyChanged, StringComparison.Ordinal);
        Assert.Contains("BeginIssueTargetFocus(sectionIndex, scroller, target, request.ChannelRole", profileNavigation, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(() =>", beginIssueFocus, StringComparison.Ordinal);
        AssertBefore(completeIssueFocus, "!IsCurrentFilmProfileNavigationStillValid()", "target.StartBringIntoView();");
        Assert.Contains("_areViewModelEventsSubscribed", navigationGuard, StringComparison.Ordinal);
        Assert.Contains("IsLoaded", navigationGuard, StringComparison.Ordinal);
        Assert.Contains("FilmProfileImportResultReviewVisibility != Visibility.Visible", navigationGuard, StringComparison.Ordinal);
        Assert.Contains("BeginIssueTargetFocus(sectionIndex, scroller, target", roiNavigation, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(UpdateWorkbenchPreviewLayout);", sizeChanged, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(ApplyInitialFitZoom);", previewLayout, StringComparison.Ordinal);
        AssertBefore(responsiveLayout, "var activationEpoch = _activationEpoch;", "DispatcherQueue.TryEnqueue(() =>");
        AssertBefore(responsiveLayout, "DispatcherQueue.TryEnqueue(() =>", "if (!IsCurrentActivation(activationEpoch)");
        Assert.Equal(2, Count(source, "DispatcherQueue.TryEnqueue("));
    }

    [Fact]
    public void BwIssueNavigation_ReturnAndReopenSupersedeAnAwaitingRoleLoad()
    {
        var page = ReadPageSource();
        var vm = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var navigate = ExtractBody(page, "private async void BwIssueButton_Click(");
        var returnToParameters = ExtractBody(page, "private void BwReturnToParametersButton_Click(");
        var reopenIssues = ExtractBody(page, "private void BwViewIssuesButton_Click(");
        var requestNavigation = ExtractBody(vm, "public async Task<bool> TryRequestCurrentFilmProfileIssueNavigationAsync(");

        Assert.Contains("await ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(display);", navigate, StringComparison.Ordinal);
        AssertBefore(returnToParameters, "ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();", "ExitBwIssueView(returnFocus: true);");
        AssertBefore(reopenIssues, "ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();", "CancelBwIssueFocus();");
        AssertBefore(reopenIssues, "ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();", "ChannelCalibrationIssuePanel.Visibility = Visibility.Visible;");
        Assert.Contains("public void CancelPendingCurrentFilmProfileIssueNavigation()\n        => _filmProfileNavigationRequestVersion++;",
            vm.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        AssertBefore(requestNavigation, "var navigationVersion = ++_filmProfileNavigationRequestVersion;", "await EnsureCurrentFilmProfileNavigationRoleReadyAsync(request.ChannelRole);");
        AssertBefore(requestNavigation, "navigationVersion != _filmProfileNavigationRequestVersion", "CurrentFilmProfileIssueNavigationRequested?.Invoke(refreshedRequest);");
    }

    [Fact]
    public void BwCompactReturn_RestoresSavedEditorOffsetOnlyForCurrentReturnContext()
    {
        var page = ReadPageSource();
        var openIssues = ExtractBody(page, "private void BwViewIssuesButton_Click(");
        var returnToParameters = ExtractBody(page, "private void BwReturnToParametersButton_Click(");
        var exitIssues = ExtractBody(page, "private void ExitBwIssueView(bool returnFocus)");
        var restore = ExtractBody(page, "private void RestoreBwCompactEditorScrollAfterLayout(");
        var cancelReturnScroll = ExtractBody(page, "private void CancelBwCompactReturnScroll()");
        var cancelFocus = ExtractBody(page, "private void CancelBwIssueFocus()");
        var unloaded = ExtractBody(page, "private async void OnUnloaded(");

        AssertBefore(openIssues, "_bwCompactEditorScrollOffset = _bwCompactMode ? ChannelCalibrationCompactScrollViewer.VerticalOffset : null;",
            "ChannelCalibrationIssuePanel.Visibility = Visibility.Visible;");
        Assert.Contains("ExitBwIssueView(returnFocus: true);", returnToParameters, StringComparison.Ordinal);
        AssertBefore(exitIssues, "var restoreOffset = returnFocus && _bwCompactMode ? _bwCompactEditorScrollOffset : null;",
            "_bwCompactEditorScrollOffset = null;");
        AssertBefore(exitIssues, "_bwCompactEditorScrollOffset = null;", "CancelBwIssueFocus();");
        AssertBefore(exitIssues, "if (restoreOffset is { } offset)", "RestoreBwCompactEditorScrollAfterLayout(offset, requestVersion, channelRole);");
        Assert.Contains("CancelBwCompactReturnScroll();", cancelFocus, StringComparison.Ordinal);
        Assert.Contains("_bwCompactEditorScrollOffset = null;", unloaded, StringComparison.Ordinal);
        Assert.Contains("CancelBwIssueFocus();", unloaded, StringComparison.Ordinal);

        AssertBefore(restore, "CancelBwCompactReturnScroll();", "requestVersion == _bwFocusRequestVersion");
        AssertBefore(restore, "requestVersion == _bwFocusRequestVersion", "ChannelCalibrationCompactScrollViewer.ChangeView(null, offset, null, true)");
        Assert.Contains("IsCurrentActivation(activationEpoch)", restore, StringComparison.Ordinal);
        Assert.Contains("_activeWorkbenchSectionIndex == 2 && _bwCompactMode", restore, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationIssuePanel.Visibility != Visibility.Visible", restore, StringComparison.Ordinal);
        Assert.Contains("string.Equals(channelRole, ViewModel.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)", restore, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationCompactScrollViewer.LayoutUpdated += _bwCompactReturnLayoutHandler;", restore, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationCompactScrollViewer.LayoutUpdated -= _bwCompactReturnLayoutHandler;", cancelReturnScroll, StringComparison.Ordinal);
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
