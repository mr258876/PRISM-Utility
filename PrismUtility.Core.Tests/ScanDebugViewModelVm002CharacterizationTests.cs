using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "VM002")]
[Trait("Category", "ScanDebugViewModel")]
[Trait("Category", "Projection")]
public sealed class ScanDebugViewModelVm002CharacterizationTests
{
    [Fact]
    public void Vm002_ScanDebugViewModel_SourceOnly_PublicBindingAndCommandSurfaceStaysEnumerated()
    {
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml");
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var shellViewModel = ReadHostSource("PRISM Utility", "ViewModels", "ShellViewModel.cs");

        Assert.Contains("NavigationCacheMode=\"Enabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler<ScanCalibrationPromptRequest>? CalibrationPromptRequested;", viewModel, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler<ScanNoticeRequest>? NoticeRequested;", viewModel, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? CalibrationSectionRequested;", viewModel, StringComparison.Ordinal);

        foreach (var binding in new[]
        {
            "Text=\"{x:Bind ViewModel.StatusText, Mode=OneWay}\"",
            "Command=\"{x:Bind ViewModel.StartScanCommand}\"",
            "Command=\"{x:Bind ViewModel.StopScanCommand}\"",
            "Command=\"{x:Bind ViewModel.ExportDngCommand}\"",
            "ItemsSource=\"{x:Bind ViewModel.RowOptions, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.SelectedRows, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "IsOn=\"{x:Bind ViewModel.IsPreviewEnabled, Mode=TwoWay}\"",
            "IsOn=\"{x:Bind ViewModel.IsWaterfallEnabled, Mode=TwoWay}\"",
            "ItemsSource=\"{x:Bind ViewModel.DngExportModeOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedDebugDngExportMode, Mode=TwoWay}\"",
            "ItemsSource=\"{x:Bind ViewModel.CalibrationChannelOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannel, Mode=TwoWay}\"",
            "Command=\"{x:Bind ViewModel.SaveChannelProfileCommand}\"",
            "Command=\"{x:Bind ViewModel.ClearChannelProfileCommand}\"",
            "Command=\"{x:Bind ViewModel.ApplyParametersCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoBlackAdjustCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoWhiteAdjustCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoCalibrateCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoFocusCommand}\"",
            "Command=\"{x:Bind ViewModel.RefreshIlluminationCommand}\"",
            "Command=\"{x:Bind ViewModel.ApplyIlluminationCommand}\"",
            "Command=\"{x:Bind ViewModel.RefreshMotionCommand}\"",
            "Command=\"{x:Bind ViewModel.EnableMotorCommand}\"",
            "Command=\"{x:Bind ViewModel.DisableMotorCommand}\"",
            "Command=\"{x:Bind ViewModel.MoveMotorCommand}\"",
            "Command=\"{x:Bind ViewModel.StopMotorCommand}\"",
            "Command=\"{x:Bind ViewModel.ApplyMotorConfigCommand}\"",
            "IsOn=\"{x:Bind ViewModel.IsRoiEditModeEnabled, Mode=TwoWay}\"",
            "ItemsSource=\"{x:Bind ViewModel.RoiSelectionOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedRoiSelection, Mode=TwoWay}\"",
            "Command=\"{x:Bind ViewModel.ApplySelectedRoiInputsCommand}\"",
            "Command=\"{x:Bind ViewModel.ResetSelectedRoiCommand}\"",
            "Command=\"{x:Bind ViewModel.ResetAllRoisCommand}\""
        })
        {
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
        }

        foreach (var declaration in new[]
        {
            "private async Task ExportDng(CancellationToken cancellationToken)",
            "private async Task ConnectDevices()",
            "private async Task DisconnectDevices()",
            "private async Task ApplyParameters()",
            "private Task AutoBlackAdjust()",
            "private Task AutoWhiteAdjust()",
            "private Task AutoCalibrate()",
            "private async Task SaveChannelProfile()",
            "private async Task ClearChannelProfile()",
            "private async Task SaveFilmProfileJson()",
            "private async Task LoadFilmProfileJson()",
            "private async Task ApplyStagedFilmProfileImport()",
            "private void DiscardStagedFilmProfileImport()",
            "private void ResetSelectedRoi()",
            "private void ApplySelectedRoiInputs()",
            "private void ResetAllRois()",
            "private async Task AutoFocus()",
            "private async Task StartScan()",
            "private async Task StopScan()",
            "public void AttachRuntimeBindings()",
            "public async Task DeactivateAsync()",
            "public async Task CleanupAsync()"
        })
        {
            Assert.Contains(declaration, viewModel, StringComparison.Ordinal);
        }

        Assert.Contains("ScanDebugViewModel viewModel => viewModel.ConnectDevicesCommand", shellViewModel, StringComparison.Ordinal);
        Assert.Contains("ScanDebugViewModel viewModel => viewModel.DisconnectDevicesCommand", shellViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm002_ScanDebugViewModel_SourceOnly_PreviewRoiAndCalibrationStateTransitionsStayInViewModel()
    {
        var source = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var applyFrame = ExtractMemberBodyAtDeclaration(source, "private void ApplyScanFrame(");
        var singleScan = ExtractMemberBodyAtDeclaration(source, "private async Task RunSingleScanAsync(");
        var workflowScan = ExtractMemberBodyAtDeclaration(source, "private async Task RunWorkflowScanAsync(");
        var calibration = ExtractMemberBodyAtDeclaration(source, "private async Task RunAutoCalibrationAsync(");
        var calibrationSnapshotProjection = ExtractMemberBodyAtDeclaration(source, "private void ApplyCalibrationSnapshotProjection(");
        var calibrationProfileProjection = ExtractMemberBodyAtDeclaration(source, "private void ApplyCalibrationProfileProjection(");
        var selectedRoiUpdate = ExtractMemberBodyAtDeclaration(source, "public void UpdateSelectedRoiRange(");
        var roiStatus = ExtractMemberBodyAtDeclaration(source, "private void RefreshRoiStatus()");
        var overlays = ExtractMemberBodyAtDeclaration(source, "public IReadOnlyList<(string Key, string Label, ScanColumnRange Range, bool IsSelected)> GetPreviewRoiOverlays(");
        var sample = ExtractMemberBodyAtDeclaration(source, "public bool TryGetPreviewSample16(");

        Assert.Contains("_lineBuffer = imageBytes;", applyFrame, StringComparison.Ordinal);
        Assert.Contains("_previewRows = rows;", applyFrame, StringComparison.Ordinal);
        Assert.Contains("_hasValidScanBuffer = true;", applyFrame, StringComparison.Ordinal);
        Assert.Contains("ExportDngCommand.NotifyCanExecuteChanged();", applyFrame, StringComparison.Ordinal);
        Assert.Contains("ClearPreview();", applyFrame, StringComparison.Ordinal);
        Assert.Contains("RenderPreview(rows)", applyFrame, StringComparison.Ordinal);
        Assert.Contains("BeginStreamingScanPreview(rows)", singleScan, StringComparison.Ordinal);
        Assert.Contains("QueueStreamingPreviewFrame", singleScan, StringComparison.Ordinal);
        Assert.Contains("ApplyScanFrame(result.ImageBytes, rows", singleScan, StringComparison.Ordinal);
        Assert.Contains("_workflow.ExecuteAsync", workflowScan, StringComparison.Ordinal);
        Assert.Contains("QueueStreamingWorkflowPreviewFrame", workflowScan, StringComparison.Ordinal);
        Assert.Contains("_lastWorkflowResult = result;", workflowScan, StringComparison.Ordinal);
        Assert.Contains("ApplyScanFrame(", workflowScan, StringComparison.Ordinal);

        Assert.Contains("IsAutoCalibrating = true;", calibration, StringComparison.Ordinal);
        Assert.Contains("_sessionCoordinator.RunConnectedSessionStateAsync", calibration, StringComparison.Ordinal);
        Assert.Contains("_roiSettings.Normalize()", calibration, StringComparison.Ordinal);
        Assert.Contains("RequestCalibrationPromptAsync", calibration, StringComparison.Ordinal);
        Assert.Contains("ApplyCalibrationSnapshotProjection(applied)", calibration, StringComparison.Ordinal);
        Assert.Contains("ShowCalibrationFrame(imageBytes, rows, phase)", calibration, StringComparison.Ordinal);
        Assert.Contains("await SaveSelectedCalibrationProfileAsync(calibrated);", calibration, StringComparison.Ordinal);
        Assert.Contains("IsAutoCalibrating = false;", calibration, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace = true", calibrationSnapshotProjection, StringComparison.Ordinal);
        Assert.Contains("ApplySnapshotToInputs(snapshot);", calibrationSnapshotProjection, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection();", calibrationSnapshotProjection, StringComparison.Ordinal);
        Assert.Contains("ApplySnapshotToInputs(profile.Parameters);", calibrationProfileProjection, StringComparison.Ordinal);
        Assert.Contains("_roiSettings = profile.RoiSettings.Normalize();", calibrationProfileProjection, StringComparison.Ordinal);

        Assert.Contains("_imageDecoder.TryGetSample16(_lineBuffer, _previewRows, x, y, out sample)", sample, StringComparison.Ordinal);
        Assert.Contains("var clamped = _roiSettings.Clamp(imageWidth);", overlays, StringComparison.Ordinal);
        Assert.Contains(".Where(overlay => IsRoiOverlayVisible(overlay.Key))", overlays, StringComparison.Ordinal);
        Assert.Contains("new ScanColumnRange(start, endInclusive).Clamp(imageWidth)", selectedRoiUpdate, StringComparison.Ordinal);
        Assert.Contains("NormalizeCurrentRoiSettings();", selectedRoiUpdate, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiStatus();", selectedRoiUpdate, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs();", selectedRoiUpdate, StringComparison.Ordinal);
        Assert.Contains("EnsureRoiEditModeAvailability();", roiStatus, StringComparison.Ordinal);
        Assert.Contains("EnsureColumnSampleEditModeAvailability();", roiStatus, StringComparison.Ordinal);
        Assert.Contains("RoiOverlayVersion++;", roiStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm002_ScanDebugViewModel_Projection_SourceOnly_FilmProfileImportExportCaptureStateStaysInViewModel()
    {
        var source = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var constructor = ExtractMemberBodyAtDeclaration(source, "public ScanDebugViewModel(");
        var externalSnapshot = ExtractMemberBodyAtDeclaration(source, "private void ApplyExternalFilmProfileWorkspaceSnapshot(");
        var save = ExtractMemberBodyAtDeclaration(source, "private async Task SaveFilmProfileJson()");
        var load = ExtractMemberBodyAtDeclaration(source, "private async Task LoadFilmProfileJson()");
        var apply = ExtractMemberBodyAtDeclaration(source, "private async Task ApplyStagedFilmProfileImport()");
        var discard = ExtractMemberBodyAtDeclaration(source, "private void DiscardStagedFilmProfileImport()");
        var synchronize = ExtractMemberBodyAtDeclaration(source, "private bool SynchronizeFilmProfileDraftFromInputs(");
        var projection = ExtractMemberBodyAtDeclaration(source, "private void RefreshFilmProfileWorkspaceProjection()");
        var applyDraft = ExtractMemberBodyAtDeclaration(source, "private void ApplyDraftToFields(");

        Assert.Contains("_filmProfileWorkspace = filmProfileWorkspace;", constructor, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles = filmProfileFiles;", constructor, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace = true;", constructor, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection();", constructor, StringComparison.Ordinal);

        Assert.Contains("ReferenceEquals(snapshot.CurrentDraft, _lastProjectedFilmProfileDraft)", externalSnapshot, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(snapshot.StagedImport, _lastProjectedStagedFilmProfileImport)", externalSnapshot, StringComparison.Ordinal);
        Assert.Contains("HasStagedFilmProfileImport = snapshot.StagedImport is not null;", externalSnapshot, StringComparison.Ordinal);
        Assert.Contains("ApplyDraftToFields(snapshot.CurrentDraft);", externalSnapshot, StringComparison.Ordinal);
        Assert.Contains("SetCurrentFilmProfileValidation(_filmProfileWorkspace.BuildExportDocument().Document.Validation);", externalSnapshot, StringComparison.Ordinal);
        Assert.Contains("SetStagedFilmProfileImportValidation(snapshot.StagedImport.Validation);", externalSnapshot, StringComparison.Ordinal);
        Assert.Contains("ClearStagedFilmProfileImportValidation();", externalSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFilmProfileValidation(", source, StringComparison.Ordinal);
        Assert.Contains("CurrentFilmProfileValidationIssues", source, StringComparison.Ordinal);
        Assert.Contains("CurrentFilmProfileValidationSummary", source, StringComparison.Ordinal);
        Assert.Contains("IsCurrentFilmProfileValidationValid", source, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationIssues", source, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationSummary", source, StringComparison.Ordinal);
        Assert.Contains("IsStagedFilmProfileImportValid", source, StringComparison.Ordinal);
        Assert.Contains("CanApplyStagedFilmProfileImport", source, StringComparison.Ordinal);

        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.BuildExportDocument", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ExportAsync", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.MarkExported", save, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection();", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ImportAsync(CancellationToken.None)", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.StageImport", load, StringComparison.Ordinal);
        Assert.Contains("SetStagedFilmProfileImportValidation", load, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCurrentFilmProfileValidation", load, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection();", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("HasStagedFilmProfileImport = false;", apply, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.DiscardStagedImport();", discard, StringComparison.Ordinal);
        Assert.Contains("ClearStagedFilmProfileImportValidation();", discard, StringComparison.Ordinal);

        Assert.Contains("TryBuildCurrentFilmProfileDraft", synchronize, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.SetCurrentDraft(draft);", synchronize, StringComparison.Ordinal);
        Assert.Contains("_hasInvalidFilmProfileInput = true;", synchronize, StringComparison.Ordinal);
        Assert.Contains("ScanFilmProfileDirtyState.IsDirty", projection, StringComparison.Ordinal);
        Assert.Contains("ApplyProfileAcquisitionSettings(_selectedFilmAcquisitionSettings);", applyDraft, StringComparison.Ordinal);
        Assert.Contains("ApplyScanRecipeSettings(draft.ScanRecipeSettings);", applyDraft, StringComparison.Ordinal);
        Assert.Contains("ApplySnapshotToInputs(profile.Parameters);", applyDraft, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo2Baseline_CalibrationPromptCompletionAndPageSubscription_AreSymmetric()
    {
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var page = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var request = ExtractMemberBodyAtDeclaration(viewModel, "private Task<bool> RequestCalibrationPromptAsync(");
        var subscribe = ExtractMemberBodyAtDeclaration(page, "private void SubscribeViewModelEvents()");
        var unsubscribe = ExtractMemberBodyAtDeclaration(page, "private void UnsubscribeViewModelEvents()");
        var handler = ExtractMemberBodyAtDeclaration(page, "private async void OnCalibrationPromptRequested(");

        Assert.Contains("new ScanCalibrationPromptRequest(prompt);", request, StringComparison.Ordinal);
        Assert.Contains("CalibrationPromptRequested?.Invoke(this, request);", request, StringComparison.Ordinal);
        Assert.Contains("return request.CompletionSource.Task;", request, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CalibrationPromptRequested += OnCalibrationPromptRequested;", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CalibrationPromptRequested -= OnCalibrationPromptRequested;", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("e.CompletionSource.TrySetResult", handler, StringComparison.Ordinal);
        Assert.Contains("e.CompletionSource.TrySetException(ex);", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo2_FilmProfileDiscardConfirmation_HasDedicatedSymmetricCompletionSurface()
    {
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var page = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var subscribe = ExtractMemberBodyAtDeclaration(page, "private void SubscribeViewModelEvents()");
        var unsubscribe = ExtractMemberBodyAtDeclaration(page, "private void UnsubscribeViewModelEvents()");
        var handler = ExtractMemberBodyAtDeclaration(page, "private async void OnFilmProfileDiscardConfirmationRequested(");

        Assert.Contains("public sealed class ScanFilmProfileDiscardConfirmationRequest", viewModel, StringComparison.Ordinal);
        Assert.Contains("TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)", viewModel, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler<ScanFilmProfileDiscardConfirmationRequest>? FilmProfileDiscardConfirmationRequested;", viewModel, StringComparison.Ordinal);
        Assert.Contains("ViewModel.FilmProfileDiscardConfirmationRequested += OnFilmProfileDiscardConfirmationRequested;", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.FilmProfileDiscardConfirmationRequested -= OnFilmProfileDiscardConfirmationRequested;", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary);", handler, StringComparison.Ordinal);
        Assert.Contains("e.CompletionSource.TrySetException(ex);", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm002_ScanDebugViewModel_SourceOnly_LifecycleDialogsSessionAndWorkflowBoundariesStayInventoried()
    {
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var page = ReadHostSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var app = ReadHostSource("PRISM Utility", "App.xaml.cs");
        var loaded = ExtractMemberBodyAtDeclaration(page, "private async void OnLoaded(");
        var unloaded = ExtractMemberBodyAtDeclaration(page, "private async void OnUnloaded(");
        var subscribe = ExtractMemberBodyAtDeclaration(page, "private void SubscribeViewModelEvents()");
        var unsubscribe = ExtractMemberBodyAtDeclaration(page, "private void UnsubscribeViewModelEvents()");
        var promptDialog = ExtractMemberBodyAtDeclaration(page, "private async void OnCalibrationPromptRequested(");
        var noticeDialog = ExtractMemberBodyAtDeclaration(page, "private async void OnNoticeRequested(");
        var attach = ExtractMemberBodyAtDeclaration(viewModel, "public void AttachRuntimeBindings()");
        var detach = ExtractMemberBodyAtDeclaration(viewModel, "private void DetachRuntimeBindings()");
        var deactivate = ExtractMemberBodyAtDeclaration(viewModel, "public async Task DeactivateAsync()");
        var connect = ExtractMemberBodyAtDeclaration(viewModel, "private async Task ConnectDevices()");
        var disconnect = ExtractMemberBodyAtDeclaration(viewModel, "private async Task DisconnectDevices()");
        var start = ExtractMemberBodyAtDeclaration(viewModel, "private async Task StartScan()");
        var stop = ExtractMemberBodyAtDeclaration(viewModel, "private async Task StopScan()");
        var workflow = ExtractMemberBodyAtDeclaration(viewModel, "private async Task RunWorkflowScanAsync(");
        var promptRequest = ExtractMemberBodyAtDeclaration(viewModel, "private Task<bool> RequestCalibrationPromptAsync(");
        var noticeRequest = ExtractMemberBodyAtDeclaration(viewModel, "private Task RequestNoticeAsync(");

        Assert.Contains("SubscribeViewModelEvents();", loaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AttachRuntimeBindings();", loaded, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.RefreshDeviceSettingsBindingsAsync();", loaded, StringComparison.Ordinal);
        Assert.Contains("UnsubscribeViewModelEvents();", unloaded, StringComparison.Ordinal);
        Assert.Contains("DisposePreviewBitmap();", unloaded, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.DeactivateAsync();", unloaded, StringComparison.Ordinal);
        Assert.Contains("if (_areViewModelEventsSubscribed)", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged += OnViewModelPropertyChanged;", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CalibrationPromptRequested += OnCalibrationPromptRequested;", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.NoticeRequested += OnNoticeRequested;", subscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged -= OnViewModelPropertyChanged;", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CalibrationPromptRequested -= OnCalibrationPromptRequested;", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("ViewModel.NoticeRequested -= OnNoticeRequested;", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("new ContentDialog", promptDialog, StringComparison.Ordinal);
        Assert.Contains("e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary);", promptDialog, StringComparison.Ordinal);
        Assert.Contains("new ContentDialog", noticeDialog, StringComparison.Ordinal);
        Assert.Contains("e.CompletionSource.TrySetResult();", noticeDialog, StringComparison.Ordinal);

        Assert.Contains("SubscribeFilmProfileWorkspace();", attach, StringComparison.Ordinal);
        Assert.Contains("if (_areRuntimeBindingsAttached)", attach, StringComparison.Ordinal);
        Assert.Contains("_session.TargetsChanged += OnSessionTargetsChanged;", attach, StringComparison.Ordinal);
        Assert.Contains("_transferSettings.BulkInReadModeChanged += OnTransferSettingsChanged;", attach, StringComparison.Ordinal);
        Assert.Contains("_session.TargetsChanged -= OnSessionTargetsChanged;", detach, StringComparison.Ordinal);
        Assert.Contains("_transferSettings.BulkInReadModeChanged -= OnTransferSettingsChanged;", detach, StringComparison.Ordinal);
        Assert.Contains("DetachRuntimeBindings();", deactivate, StringComparison.Ordinal);
        Assert.Contains("UnsubscribeFilmProfileWorkspace();", deactivate, StringComparison.Ordinal);
        Assert.Contains("ClearPreview();", deactivate, StringComparison.Ordinal);
        Assert.Contains("IsScanReadProgressVisible = false;", deactivate, StringComparison.Ordinal);

        Assert.Contains("_sessionCoordinator.IsConnectBlockedByUsbDebug()", connect, StringComparison.Ordinal);
        Assert.Contains("RequestNoticeAsync", connect, StringComparison.Ordinal);
        Assert.Contains("_sessionCoordinator.ConnectAsync(CancellationToken.None)", connect, StringComparison.Ordinal);
        Assert.Contains("SwitchToConnectedSession();", connect, StringComparison.Ordinal);
        Assert.Contains("_parameters.LoadAsync(_session, _session.ConnectionToken)", connect, StringComparison.Ordinal);
        Assert.Contains("_scanCts?.Cancel();", disconnect, StringComparison.Ordinal);
        Assert.Contains("_sessionCoordinator.DisconnectAsync(CancellationToken.None)", disconnect, StringComparison.Ordinal);
        Assert.Contains("SwitchToDiscoverySession();", disconnect, StringComparison.Ordinal);
        Assert.Contains("_scanCts = new CancellationTokenSource();", start, StringComparison.Ordinal);
        Assert.Contains("IsRunning = true;", start, StringComparison.Ordinal);
        Assert.Contains("RunWorkflowScanAsync", start, StringComparison.Ordinal);
        Assert.Contains("RunContinuousScanLoopAsync", start, StringComparison.Ordinal);
        Assert.Contains("RunSingleScanAsync", start, StringComparison.Ordinal);
        Assert.Contains("IsRunning = false;", start, StringComparison.Ordinal);
        Assert.Contains("_scanCts?.Cancel();", stop, StringComparison.Ordinal);
        Assert.Contains("session.StopScanAsync(CancellationToken.None)", stop, StringComparison.Ordinal);
        Assert.Contains("_workflow.ExecuteAsync", workflow, StringComparison.Ordinal);
        Assert.Contains("_debugOutputMirror.Mirror(\"ScanDebug.WorkflowDiagnostic\", diagnostic)", workflow, StringComparison.Ordinal);
        Assert.Contains("CalibrationPromptRequested?.Invoke(this, request);", promptRequest, StringComparison.Ordinal);
        Assert.Contains("NoticeRequested?.Invoke(this, request);", noticeRequest, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<ScanDebugViewModel>()", app, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<ScanDebugPage>()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm002_SourceOnly_NoScanDebugResponsibilityPresenterOrWorkspaceSplitExistsWhileBlocked()
    {
        var productionSources = EnumerateProductionSources().ToArray();
        var app = ReadHostSource("PRISM Utility", "App.xaml.cs");
        var issues = ReadHostSource("docs", "architecture", "issues-and-remediation.md");
        var priorities = ReadHostSource("docs", "architecture", "design-correction-priorities.md");

        Assert.Contains("VM-001 -> UI-006 -> UI-007 blocked chain 解除前，VM-002 只能补行为 characterization", issues, StringComparison.Ordinal);
        Assert.Contains("gate 解除前只补行为 characterization", priorities, StringComparison.Ordinal);

        foreach (var forbidden in new[]
        {
            "IScanDebugPreviewPresenter",
            "ScanDebugPreviewPresenter",
            "IScanDebugPreviewWorkspace",
            "ScanDebugPreviewWorkspace",
            "IScanDebugRoiWorkspace",
            "ScanDebugRoiWorkspace",
            "IScanDebugRoiPresenter",
            "ScanDebugRoiPresenter",
            "IScanDebugCalibrationWorkspace",
            "ScanDebugCalibrationWorkspace",
            "IScanDebugCalibrationPresenter",
            "ScanDebugCalibrationPresenter",
            "IScanDebugProfileProjectionWorkspace",
            "ScanDebugProfileProjectionWorkspace",
            "IScanDebugProfilePresenter",
            "ScanDebugProfilePresenter",
            "ScanDebugViewModelPreviewPart",
            "ScanDebugViewModelRoiPart",
            "ScanDebugViewModelCalibrationPart",
            "ScanDebugViewModelProfilePart"
        })
        {
            Assert.DoesNotContain(productionSources, source => source.Content.Contains(forbidden, StringComparison.Ordinal));
            Assert.DoesNotContain(forbidden, app, StringComparison.Ordinal);
        }

        Assert.Contains("services.AddTransient<ScanDebugViewModel>()", app, StringComparison.Ordinal);
        Assert.DoesNotContain("IScanDebugPreviewPresenter", app, StringComparison.Ordinal);
        Assert.DoesNotContain("IScanDebugRoiWorkspace", app, StringComparison.Ordinal);
        Assert.DoesNotContain("IScanDebugCalibrationWorkspace", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Vm002_SourceOnly_Task21EvidenceRecordsSplitBlockedAndP3CanProceed()
    {
        var task18 = ReadRootSource(".omo", "evidence", "task-18-design-correction-repairs.txt");
        var task19 = ReadRootSource(".omo", "evidence", "task-19-design-correction-repairs.txt");
        var task20 = ReadRootSource(".omo", "evidence", "task-20-design-correction-repairs.txt");
        var task21 = ReadRootSource(".omo", "evidence", "task-21-design-correction-repairs.txt");
        var plan = ReadRootSource(".omo", "plans", "design-correction-repairs.md");

        Assert.Contains("Status: EXTRACTION_BLOCKED", task18, StringComparison.Ordinal);
        Assert.Contains("Status: EXTRACTION_BLOCKED", task19, StringComparison.Ordinal);
        Assert.Contains("Status: EXTRACTION_BLOCKED", task20, StringComparison.Ordinal);
        Assert.Contains("Task 21 VM-002 must use its blocked branch", task20, StringComparison.Ordinal);
        Assert.Contains("Status: SPLIT_BLOCKED", task21, StringComparison.Ordinal);
        Assert.Contains("No ScanDebugViewModel responsibility split was performed", task21, StringComparison.Ordinal);
        Assert.Contains("No production source, XAML, ViewModel, presenter, workspace, lifecycle, CAL, DNG, docs, plan, or ledger file was edited", task21, StringComparison.Ordinal);
        Assert.Contains("Production UI was unchanged, so no fresh visual QA was run", task21, StringComparison.Ordinal);
        Assert.Contains("No downstream P2 extraction task remains; P3 may proceed", task21, StringComparison.Ordinal);
        Assert.Contains("21. `VM-002` split ScanDebug responsibilities after UI extraction", plan, StringComparison.Ordinal);
        Assert.Contains("22. `CAL-001` add deterministic calibration and autofocus tests with fakes", plan, StringComparison.Ordinal);
        Assert.Contains("Parallelization: Wave P3", plan, StringComparison.Ordinal);
    }

    private static IEnumerable<(string Path, string Content)> EnumerateProductionSources()
    {
        foreach (var root in new[] { "PRISM Utility", "PRISM Utility.Core" })
        {
            var fullRoot = Path.Combine(FindHostSoftwareRoot(), root);
            foreach (var file in Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
                yield return (file, File.ReadAllText(file));
        }
    }

    private static string ReadHostSource(params string[] path)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), Path.Combine(path)));

    private static string ReadRootSource(params string[] path)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string ExtractMemberBodyAtDeclaration(string source, string declaration)
    {
        var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(declarationIndex >= 0, $"Could not find declaration: {declaration}");

        var bodyStart = source.IndexOf('{', declarationIndex);
        Assert.True(bodyStart >= 0, $"Could not find body for declaration: {declaration}");
        return ExtractBodyFromOpeningBrace(source, bodyStart, declaration);
    }

    private static string ExtractBodyFromOpeningBrace(string source, int bodyStart, string context)
    {
        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not extract body for: {context}");
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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".omo")) && Directory.Exists(Path.Combine(directory.FullName, "Host Software")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
