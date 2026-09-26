using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanDebugFilmProfileOrchestrationSourceTests
{
    [Fact]
    public void LegacyFixtures_CharacterizePreTodo8ObservableCalls()
    {
        var save = ReadFixture("legacy-scan-debug-save-method.txt");
        var load = ReadFixture("legacy-scan-debug-load-method.txt");

        Assert.Contains("SaveSelectedCalibrationProfileAsync", save, StringComparison.Ordinal);
        Assert.Contains("ReplaceProfilesAsync", load, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveFilmProfileJson_UsesWorkspaceAndCoordinatorWithoutRepositorySave()
    {
        var save = ExtractMethod(ReadViewModelSource(), "SaveFilmProfileJson");

        Assert.DoesNotContain("SaveSelectedCalibrationProfileAsync", save, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProfileAsync", save, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceProfilesAsync", save, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", save, StringComparison.Ordinal);
        Assert.True(save.IndexOf("SynchronizeFilmProfileDraftFromInputs", StringComparison.Ordinal) < save.IndexOf("_filmProfileFiles.ExportAsync", StringComparison.Ordinal));
        Assert.True(save.IndexOf("_filmProfileFiles.ExportAsync", StringComparison.Ordinal) < save.IndexOf("_filmProfileWorkspace.MarkExported", StringComparison.Ordinal));
        Assert.Contains("_filmProfileWorkspace.MarkExported(export.Document.Document!)", save, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadAndApply_KeepPreviewPureAndApplyUsesWorkspaceSnapshotProjection()
    {
        var source = ReadViewModelSource();
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");

        Assert.DoesNotContain("ReplaceProfilesAsync", load, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSelectedCalibrationChannelAsync", load, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.StageImport", load, StringComparison.Ordinal);
        Assert.Contains("ApplyStagedImportAsync", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", apply, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_InvalidCoordinatorResult_ReturnsBeforeStageImport()
    {
        var load = ExtractMethod(ReadViewModelSource(), "LoadFilmProfileJson");
        var importIndex = load.IndexOf("_filmProfileFiles.ImportAsync(CancellationToken.None)", StringComparison.Ordinal);
        var invalidIndex = load.IndexOf("if (imported.Profile is null)", StringComparison.Ordinal);
        var returnIndex = load.IndexOf("return;", invalidIndex, StringComparison.Ordinal);
        var stageIndex = load.IndexOf("_filmProfileWorkspace.StageImport", StringComparison.Ordinal);

        Assert.True(importIndex >= 0, "Load must import through the file coordinator.");
        Assert.True(invalidIndex > importIndex, "Invalid coordinator results must be handled after import.");
        Assert.True(returnIndex > invalidIndex, "The null-profile branch must return.");
        Assert.True(stageIndex > returnIndex, "Invalid coordinator results must return before staging.");
    }

    [Fact]
    public void Todo7Fix_LoadStagedApplyAndRejectedImportMessagesHaveTruthfulDistinctSemantics()
    {
        var source = ReadViewModelSource();
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var discard = ExtractMethod(source, "DiscardStagedFilmProfileImport");
        var canDiscard = ExtractMethod(source, "CanDiscardStagedFilmProfileImport");

        Assert.Contains("ScanDebug_Runtime_StatusFilmProfileStaged", load, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_Runtime_StatusFilmProfileLoaded\".GetLocalizedFormat(imported.Profile.ProfileName)", load, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_StatusFilmProfileApplied", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_Runtime_StatusFilmProfileLoaded\".GetLocalizedFormat(FilmProfileName)", apply, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_StatusFilmProfileImportInvalid\".GetLocalized()", load, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_Runtime_StatusLoadFilmProfileFailed\".GetLocalizedFormat(FormatFilmProfileValidationIssues", load, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_Runtime_StatusLoadFilmProfileFailed\".GetLocalized()", load + apply, StringComparison.Ordinal);
        Assert.Contains("HasPendingFilmProfileImportResult", canDiscard, StringComparison.Ordinal);
        Assert.DoesNotContain("StagedFilmProfileImportValidationIssues.Count > 0", canDiscard, StringComparison.Ordinal);
        Assert.Contains("SetFilmProfileImportNone();", discard, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo3Baseline_LifecycleMessagesAndHardwareOfflineGates_RemainEnumerated()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");

        foreach (var messageKey in new[]
        {
            "ScanDebug_Runtime_StatusFilmProfileInvalid",
            "ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary",
            "ScanDebug_Runtime_StatusFilmProfileExportCanceled",
            "ScanDebug_Runtime_StatusFilmProfileExported",
            "ScanDebug_Runtime_StatusSaveFilmProfileFailed"
        })
        {
            Assert.Contains(messageKey, save, StringComparison.Ordinal);
        }

        foreach (var messageKey in new[]
        {
            "ScanDebug_Runtime_StatusLoadFilmProfileCanceled",
            "ScanDebug_Runtime_StatusFilmProfileStaged",
            "ScanDebug_Runtime_StatusFilmProfileApplied",
            "ScanDebug_Runtime_StatusFilmProfileImportInvalid",
            "ScanDebug_Runtime_StatusLoadFilmProfileFailed"
        })
        {
            Assert.Contains(messageKey, load + apply, StringComparison.Ordinal);
        }

        Assert.Contains("private bool CanStartScan() =>", source, StringComparison.Ordinal);
        foreach (var command in new[]
        {
            "StartScan",
            "ApplyParameters",
            "ApplyIllumination",
            "RefreshIllumination",
            "RefreshMotion",
            "AutoBlackAdjust",
            "AutoWhiteAdjust",
            "AutoCalibrate",
            "AutoFocus",
            "StartManualFocus"
        })
        {
            Assert.Contains($"CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.{command})", source, StringComparison.Ordinal);
        }

        foreach (var operation in new[]
        {
            "ApplyParameters",
            "RefreshIllumination",
            "ApplyIllumination",
            "RefreshMotion",
            "MoveMotor",
            "StopMotor",
            "ApplyMotorConfig",
            "AutoFocus",
            "StartScan",
            "RunAutoCalibrationAsync",
            "SetMotorEnabledCoreAsync"
        })
        {
            Assert.Contains(
                operation == "AutoFocus" ? "if (!IsDeviceConnected)" : "if (!IsConnected)",
                ExtractMethod(source, operation),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Todo3DesiredContract_FilmProfileOperationsUseDedicatedVisibleSeveritySurface()
    {
        var source = ReadViewModelSource();

        Assert.Contains("public partial string FilmProfileOperationMessage", source, StringComparison.Ordinal);
        Assert.Contains("public partial InfoBarSeverity FilmProfileOperationSeverity", source, StringComparison.Ordinal);
        Assert.Contains("public partial Visibility FilmProfileOperationVisibility", source, StringComparison.Ordinal);
        Assert.Contains("private void PublishFilmProfileOperation", source, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationMessage = message;", source, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationSeverity = severity;", source, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationVisibility = Visibility.Visible;", source, StringComparison.Ordinal);
        Assert.Contains("public string ProfileSaveStateText => HasUnsavedProfileChanges", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_FilmProfileWorkbenchOperationReady", source, StringComparison.Ordinal);

        foreach (var operation in new[]
        {
            "SaveFilmProfileJson",
            "LoadFilmProfileJson",
            "ApplyStagedFilmProfileImport",
            "DiscardStagedFilmProfileImport",
            "NewFilmProfile",
            "ValidateFilmProfile"
        })
        {
            var body = ExtractMethod(source, operation);
            Assert.Contains("PublishFilmProfileOperation", body, StringComparison.Ordinal);
            Assert.DoesNotContain("StatusText", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Todo5Characterization_ExportGateAndOperationPublicationStayDedicatedToFileOperations()
    {
        var source = ReadViewModelSource();
        var canSave = ExtractMethod(source, "CanSaveFilmProfile");
        var publish = ExtractMethod(source, "PublishFilmProfileOperation");
        var save = ExtractMethod(source, "SaveFilmProfileJson");

        Assert.Contains("IsCurrentFilmProfileValidationValid", canSave, StringComparison.Ordinal);
        Assert.DoesNotContain("HasUnsavedProfileChanges", canSave, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationMessage = message;", publish, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationSeverity = severity;", publish, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationIsOpen = true;", publish, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationVisibility = Visibility.Visible;", publish, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusText", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.BuildExportDocument", save, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo5DesiredContract_ExportIsCurrentValidationGatedAndDirtyIndependent()
    {
        var source = ReadViewModelSource();
        var canSave = ExtractMethod(source, "CanSaveFilmProfile");
        var synchronize = ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");
        var projection = ExtractMethod(source, "RefreshFilmProfileWorkspaceProjection");

        Assert.Contains("CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.SaveFilmProfileJson)", canSave, StringComparison.Ordinal);
        Assert.Contains("IsCurrentFilmProfileValidationValid", canSave, StringComparison.Ordinal);
        Assert.Contains("!_hasInvalidFilmProfileInput", canSave, StringComparison.Ordinal);
        Assert.DoesNotContain("HasUnsavedProfileChanges", canSave, StringComparison.Ordinal);
        Assert.Contains("SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();", synchronize, StringComparison.Ordinal);
        Assert.Contains("SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo5DesiredContract_OperationInfoBarUsesCancelableTimerAndCloseState()
    {
        var source = ReadViewModelSource();
        var publish = ExtractMethod(source, "PublishFilmProfileOperation");
        var schedule = ExtractMethod(source, "ScheduleFilmProfileOperationAutoCloseTimer");
        var close = ExtractMethod(source, "CloseFilmProfileOperation");
        var cancel = ExtractMethod(source, "CancelFilmProfileOperationAutoCloseTimer");
        var deactivate = ExtractMethod(source, "DeactivateAsync");

        Assert.Contains("public partial bool FilmProfileOperationIsOpen", source, StringComparison.Ordinal);
        Assert.Contains("private readonly TimeProvider _operationTimeProvider;", source, StringComparison.Ordinal);
        Assert.Contains("private ITimer? _filmProfileOperationAutoCloseTimer;", source, StringComparison.Ordinal);
        Assert.Contains("TimeProvider? operationTimeProvider = null", source, StringComparison.Ordinal);
        Assert.Contains("_operationTimeProvider = operationTimeProvider ?? TimeProvider.System;", source, StringComparison.Ordinal);
        Assert.Contains("var publicationId = Interlocked.Increment(ref _filmProfileOperationPublicationId);", publish, StringComparison.Ordinal);
        Assert.Contains("CancelFilmProfileOperationAutoCloseTimer();", publish, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationIsOpen = true;", publish, StringComparison.Ordinal);
        Assert.Contains("ShouldAutoCloseFilmProfileOperation(severity)", publish, StringComparison.Ordinal);
        Assert.Contains("_operationTimeProvider.CreateTimer", schedule, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(4)", source, StringComparison.Ordinal);
        Assert.Contains("Timeout.InfiniteTimeSpan", schedule, StringComparison.Ordinal);
        Assert.Contains("Volatile.Read(ref _filmProfileOperationPublicationId)", close, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationIsOpen = false;", close, StringComparison.Ordinal);
        Assert.Contains("_filmProfileOperationAutoCloseTimer?.Dispose();", cancel, StringComparison.Ordinal);
        Assert.Contains("CancelFilmProfileOperationAutoCloseTimer();", deactivate, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", publish + schedule + close, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo3DesiredContract_LifecycleRemainsOfflineAndDeviceRuntimeStateCannotOverwriteOperationState()
    {
        var source = ReadViewModelSource();
        var refreshTargets = ExtractMethod(source, "RefreshTargets");

        Assert.Contains("public string FilmProfileDeviceStatusText =>", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_FilmProfileWorkbenchDeviceConnected", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_FilmProfileWorkbenchDeviceDisconnected", source, StringComparison.Ordinal);
        Assert.Contains("public string FilmProfileHardwareUnavailableReasonText =>", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_DisabledReasonConnectDevice", source, StringComparison.Ordinal);
        Assert.Contains("StatusText =", refreshTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileOperation", refreshTargets, StringComparison.Ordinal);

        foreach (var operation in new[]
        {
            "SaveFilmProfileJson",
            "LoadFilmProfileJson",
            "ApplyStagedFilmProfileImport",
            "DiscardStagedFilmProfileImport",
            "NewFilmProfile",
            "ValidateFilmProfile"
        })
        {
            var body = ExtractMethod(source, operation);
            Assert.DoesNotContain("IsConnected", body, StringComparison.Ordinal);
            Assert.DoesNotContain("_session", body, StringComparison.Ordinal);
            Assert.DoesNotContain("_sessionCoordinator", body, StringComparison.Ordinal);
        }

        foreach (var hardwareOperation in new[]
        {
            "ApplyParameters",
            "RefreshIllumination",
            "ApplyIllumination",
            "RefreshMotion",
            "MoveMotor",
            "StopMotor",
            "ApplyMotorConfig",
            "AutoFocus",
            "StartScan",
            "RunAutoCalibrationAsync",
            "SetMotorEnabledCoreAsync"
        })
        {
            Assert.DoesNotContain("PublishFilmProfileOperation", ExtractMethod(source, hardwareOperation), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Todo2_NewAndValidateFilmProfileCommands_AreOfflineAndUseCurrentWorkspaceStateOnly()
    {
        var source = ReadViewModelSource();
        var newProfile = ExtractMethod(source, "NewFilmProfile");
        var validate = ExtractMethod(source, "ValidateFilmProfile");
        var confirmation = ExtractMethod(source, "RequestFilmProfileDiscardConfirmationAsync");
        var boundedConfirmation = ExtractMethod(source, "RequestFilmProfileImportConfirmationAsync");
        var currentChannelPatch = ExtractMethod(source, "BuildCurrentChannelPatch");

        Assert.Contains("public event EventHandler<ScanFilmProfileDiscardConfirmationRequest>? FilmProfileDiscardConfirmationRequested;", source, StringComparison.Ordinal);
        Assert.Contains("if (HasUnsavedProfileChanges && !await RequestFilmProfileDiscardConfirmationAsync())", newProfile, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ResetToDefaultDraft();", newProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", newProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileFiles", newProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("_session", newProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("_deviceSettings", newProfile, StringComparison.Ordinal);
        Assert.Contains("RequestFilmProfileImportConfirmationAsync", confirmation, StringComparison.Ordinal);
        Assert.Contains("new ScanFilmProfileDiscardConfirmationRequest()", confirmation, StringComparison.Ordinal);
        Assert.Contains("var handler = FilmProfileDiscardConfirmationRequested;", boundedConfirmation, StringComparison.Ordinal);
        Assert.Contains("if (handler is null)", boundedConfirmation, StringComparison.Ordinal);
        Assert.Contains("return false;", boundedConfirmation, StringComparison.Ordinal);
        Assert.Contains("handler(this, request);", boundedConfirmation, StringComparison.Ordinal);
        Assert.Contains("return await request.CompletionSource.Task;", boundedConfirmation, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs();", validate, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStagedFilmProfileImportValidation", validate, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileFiles", validate, StringComparison.Ordinal);
        Assert.DoesNotContain("_session", validate, StringComparison.Ordinal);
        Assert.DoesNotContain("_deviceSettings", validate, StringComparison.Ordinal);
        Assert.DoesNotContain("_calibrationProfiles", currentChannelPatch, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo2_NewDefaultLifecycle_RemainsUnsavedUntilSuccessfulExternalReplacement()
    {
        var source = ReadViewModelSource();
        var newProfile = ExtractMethod(source, "NewFilmProfile");
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var synchronize = ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");
        var projection = ExtractMethod(source, "RefreshFilmProfileWorkspaceProjection");

        Assert.Contains("private bool _isNewFilmProfilePendingExport;", source, StringComparison.Ordinal);
        Assert.Contains("public string ProfileSaveStateText => HasUnsavedProfileChanges", source, StringComparison.Ordinal);
        Assert.True(newProfile.IndexOf("_filmProfileWorkspace.ResetToDefaultDraft();", StringComparison.Ordinal) < newProfile.IndexOf("_isNewFilmProfilePendingExport = true;", StringComparison.Ordinal));
        var saveClearIndex = save.IndexOf("_isNewFilmProfilePendingExport = false;", StringComparison.Ordinal);
        var applyClearIndex = apply.IndexOf("_isNewFilmProfilePendingExport = false;", StringComparison.Ordinal);

        Assert.True(save.IndexOf("if (!exported)", StringComparison.Ordinal) < saveClearIndex);
        Assert.True(save.IndexOf("_filmProfileWorkspace.MarkExported", StringComparison.Ordinal) < saveClearIndex);
        Assert.Equal(1, Count(save, "_isNewFilmProfilePendingExport = false;"));
        Assert.True(apply.IndexOf("if (result.Status != ScanFilmProfileApplyStatus.Applied)", StringComparison.Ordinal) < applyClearIndex);
        Assert.Equal(1, Count(apply, "_isNewFilmProfilePendingExport = false;"));
        Assert.Contains("_isNewFilmProfilePendingExport", projection, StringComparison.Ordinal);
        Assert.Contains("ScanFilmProfileDirtyState.IsDirty", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("_isNewFilmProfilePendingExport = false;", synchronize, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo4DesiredContract_ImportLifecycleHasExplicitResultStatesAndPendingGuards()
    {
        var source = ReadViewModelSource();
        var workspaceModels = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility.Core", "Models", "ScanFilmProfileWorkspaceModels.cs"));
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var discard = ExtractMethod(source, "DiscardStagedFilmProfileImport");
        var newProfile = ExtractMethod(source, "NewFilmProfile");

        Assert.Contains("public enum ScanFilmProfileImportResultState", workspaceModels, StringComparison.Ordinal);
        Assert.Contains("public sealed record ScanFilmProfileImportResult", workspaceModels, StringComparison.Ordinal);
        Assert.Contains("public ScanFilmProfileImportResult ImportResult", workspaceModels, StringComparison.Ordinal);
        Assert.Contains("HasPendingFilmProfileImportResult", source, StringComparison.Ordinal);
        Assert.Contains("SetFilmProfileImportError", source, StringComparison.Ordinal);
        Assert.Contains("RequestFilmProfileImportReplacementConfirmationAsync", source, StringComparison.Ordinal);
        Assert.Contains("RequestFilmProfileImportDiscardConfirmationAsync", source, StringComparison.Ordinal);

        Assert.True(load.IndexOf("RequestFilmProfileImportReplacementConfirmationAsync", StringComparison.Ordinal) < load.IndexOf("_filmProfileFiles.ImportAsync", StringComparison.Ordinal));
        Assert.Contains("SetFilmProfileImportError(imported.Validation ?? new ScanFilmProfileValidationResult())", load, StringComparison.Ordinal);
        Assert.Contains("SetFilmProfileImportError(staged.Validation)", load, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCurrentFilmProfileValidation", load, StringComparison.Ordinal);
        Assert.Contains("HasPendingFilmProfileImportResult && !await RequestFilmProfileImportDiscardConfirmationAsync()", newProfile, StringComparison.Ordinal);
        Assert.Contains("CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport)", source, StringComparison.Ordinal);
        Assert.Contains("HasStagedFilmProfileImport", source, StringComparison.Ordinal);
        Assert.Contains("IsStagedFilmProfileImportValid", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.DiscardStagedImport();", discard, StringComparison.Ordinal);
        Assert.Contains("SetFilmProfileImportNone();", apply + discard, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo4NavigationPreservesPendingImportByKeepingScanDebugViewModelSingleton()
    {
        var app = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "App.xaml.cs"));

        Assert.Contains("services.AddSingleton<ScanDebugViewModel>();", app, StringComparison.Ordinal);
        Assert.DoesNotContain("services.AddTransient<ScanDebugViewModel>();", app, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMethod_IgnoresBracesInSupportedLexicalContexts()
    {
        const string source = """"
            private void Target()
            {
                if (true) { var nested = 1; }
                // } line comment {
                /* { block comment } */
                var character = '}';
                var normal = "{ normal }";
                var verbatim = @"{ verbatim ""quote"" }";
                var interpolated = $"{ interpolated }";
                var interpolatedVerbatim = $@"{ interpolated verbatim ""quote"" }";
                var raw = """{ raw }""";
                var interpolatedRaw = $"""{ interpolated raw }""";
            }
            private void After() { }
            """";

        var body = ExtractMethod(source, "Target");

        Assert.Contains("var nested = 1;", body, StringComparison.Ordinal);
        Assert.Contains("interpolated raw", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private void After", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMethod_HandlesEscapedQuotesAndNestedBlocks()
    {
        const string source = """
            private async Task Target()
            {
                var escaped = "quote: \" and brace: }";
                for (var index = 0; index < 1; index++)
                {
                    if (index == 0) { continue; }
                }
            }
            [RelayCommand]
            private async Task After() { }
            """;

        var body = ExtractMethod(source, "Target");

        Assert.Contains("continue;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("After", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMethod_InterpretsVariableInterpolatedRawDelimiterBeforeGenericString()
    {
        var source = "private void Target()\n{\n" +
            "    var raw = $$$\"\"\"\"\n" +
            "    } } } method-like {\n" +
            "    \"\"\"\";\n" +
            "    var marker = 1;\n" +
            "}\nprivate void After() { }\n";

        var body = ExtractMethod(source, "Target");

        Assert.Contains("var marker = 1;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private void After", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9Baseline_RecordsPreMigrationDirtyAssignmentAndCallbackSurface()
    {
        var evidence = ReadTodo9AnalysisEvidence();

        Assert.Contains("HasUnsavedProfileChanges has 4 direct assignments", evidence, StringComparison.Ordinal);
        Assert.Contains("MarkProfileDirty has 16 current callback call sites", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9DesiredContract_UsesSingleSemanticDirtyProjection()
    {
        var source = ReadViewModelSource();
        var projection = ExtractMethod(source, "RefreshFilmProfileWorkspaceProjection");

        Assert.Equal(1, Count(source, "HasUnsavedProfileChanges ="));
        Assert.Contains("ScanFilmProfileDirtyState.IsDirty", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkProfileDirty", source, StringComparison.Ordinal);
        Assert.Contains("_hasInvalidFilmProfileInput", source, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace", source, StringComparison.Ordinal);
    }

    public static TheoryData<string> PersistedFilmProfileCallbacks => new()
    {
        { "OnFilmProfileNameChanged" },
        { "OnSelectedCalibrationChannelChanged" },
        { "OnExposureTicksChanged" },
        { "OnAdc1OffsetChanged" },
        { "OnAdc1GainChanged" },
        { "OnAdc2OffsetChanged" },
        { "OnAdc2GainChanged" },
        { "OnSysClockKhzChanged" },
        { "OnIsMultiChannelScanEnabledChanged" },
        { "OnIsChannel1ReversedChanged" },
        { "OnIsChannel2ReversedChanged" },
        { "OnIsChannel3ReversedChanged" },
        { "OnIsChannel4ReversedChanged" },
        { "OnIsScanRecipeColorManagementEnabledChanged" },
        { "OnScanRecipeRedWavelengthNmChanged" },
        { "OnScanRecipeGreenWavelengthNmChanged" },
        { "OnScanRecipeBlueWavelengthNmChanged" },
        { "OnScanRecipeOutputGammaChanged" },
        { "OnSelectedScanRecipeTargetWhitePointModeChanged" },
        { "OnScanRecipeManualWhitePointColorTemperatureKChanged" },
        { "OnSelectedProfileAlignmentModeChanged" },
        { "OnSelectedProfileDngExportModeChanged" }
    };

    [Theory]
    [MemberData(nameof(PersistedFilmProfileCallbacks))]
    public void Todo9PersistedCallbacks_SynchronizeSemanticDraft(string callback)
        => Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractCallback(ReadViewModelSource(), callback), StringComparison.Ordinal);

    [Fact]
    public void Todo9PersistedInputMutators_UseSynchronizationAndGuardedProjections()
    {
        var source = ReadViewModelSource();
        var synchronization = ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");
        var apply = ExtractMethod(source, "ApplyDraftToFields");
        var selection = ExtractCallback(source, "OnSelectedCalibrationChannelChanged");
        var acquisition = ExtractMethod(source, "TryBuildFilmAcquisitionSettings");

        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "NotifyCurrentCalibrationIlluminationInputChanged"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "OnMotorSpeedInputChanged"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "UpdateSelectedRoiRange"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "ResetSelectedRoi"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "ResetAllRois"), StringComparison.Ordinal);
        Assert.Contains("TryBuildCurrentFilmProfileDraft", synchronization, StringComparison.Ordinal);
        Assert.Contains("_hasInvalidFilmProfileInput = true", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileFiles", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProfileAsync", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSelectedCalibrationChannelAsync", synchronization, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace = true", apply, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace = wasSynchronizing", apply, StringComparison.Ordinal);
        Assert.True(selection.IndexOf("if (_isSynchronizingFilmProfileWorkspace)", StringComparison.Ordinal) < selection.IndexOf("HandleSelectedCalibrationChannelChangedAsync", StringComparison.Ordinal));
        Assert.DoesNotContain("existing.Led1ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.Led2ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.Led3ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.Led4ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.Contains("var acquisitionProjection = _selectedFilmAcquisitionSettings?.Normalize()", acquisition, StringComparison.Ordinal);
        Assert.Contains("acquisitionProjection.Led1ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.Contains("acquisitionProjection.Led2ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.Contains("acquisitionProjection.Led3ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.Contains("acquisitionProjection.Led4ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.Contains("clearUnusedInputs: false", acquisition, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9OperationTransitions_RefreshWithoutAdHocDirtyToggles()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var discard = ExtractMethod(source, "DiscardStagedFilmProfileImport");

        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", save, StringComparison.Ordinal);
        Assert.True(save.IndexOf("_filmProfileWorkspace.MarkExported", StringComparison.Ordinal) < save.LastIndexOf("RefreshFilmProfileWorkspaceProjection", StringComparison.Ordinal));
        Assert.Contains("RefreshFilmProfileWorkspaceProjection", load, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection", apply, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection", discard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Todo9WorkspaceTransitionBaseline_StageDiscardApplyAndExportAreSemantic()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = new PRISM_Utility.Core.Services.ScanFilmProfileWorkspace(repository, new PRISM_Utility.Core.Services.ScanFilmProfileDocumentService());
        var json = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PrismUtility.Core.Tests", "Fixtures", "FilmProfile", "full-v5.json"));

        workspace.StageImport(json);
        workspace.DiscardStagedImport();
        Assert.False(workspace.Snapshot.IsDirty);
        workspace.StageImport(json);
        await workspace.ApplyStagedImportAsync(CancellationToken.None);
        Assert.False(workspace.Snapshot.IsDirty);
        var current = workspace.Snapshot.CurrentDraft;
        workspace.SetCurrentDraft(new PRISM_Utility.Core.Models.ScanFilmProfileDraft(
            "Changed",
            current.SavedAtUtc,
            current.ChannelProfiles,
            current.SelectedCalibrationChannel,
            current.AcquisitionSettings,
            current.ScanRecipeSettings));
        Assert.True(workspace.Snapshot.IsDirty);
        var exported = Assert.IsType<PRISM_Utility.Core.Models.ScanFilmParameterProfileSet>(workspace.BuildExportDocument().Document.Document);
        workspace.MarkExported(exported);
        Assert.False(workspace.Snapshot.IsDirty);
    }

    [Fact]
    public void Todo13Baseline_ScanDebugPreservesTodo8_9_12AuthoringAndSelectionBehavior()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var selection = ExtractCallback(source, "OnSelectedCalibrationChannelChanged");
        var projection = ExtractMethod(source, "RefreshFilmProfileWorkspaceProjection");
        var baseline = ReadTodo13BaselineEvidence();

        Assert.Contains("ScanDebugFacadeReferences=28", baseline, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.BuildExportDocument", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ExportAsync", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.MarkExported", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.StageImport", load, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", apply, StringComparison.Ordinal);
        Assert.True(selection.IndexOf("if (_isSynchronizingFilmProfileWorkspace)", StringComparison.Ordinal) < selection.IndexOf("HandleSelectedCalibrationChannelChangedAsync", StringComparison.Ordinal));
        Assert.Contains("ScanFilmProfileDirtyState.IsDirty", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo1ValidationSurfacesAndProjectionOwnership_AreSplitAndSnapshotBound()
    {
        var source = ReadViewModelSource();
        var snapshotProjection = ExtractMethod(source, "ApplyExternalFilmProfileWorkspaceSnapshot");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var discard = ExtractMethod(source, "DiscardStagedFilmProfileImport");

        Assert.Contains("CurrentFilmProfileValidationIssues", source, StringComparison.Ordinal);
        Assert.Contains("CurrentFilmProfileValidationSummary", source, StringComparison.Ordinal);
        Assert.Contains("IsCurrentFilmProfileValidationValid", source, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationIssues", source, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationSummary", source, StringComparison.Ordinal);
        Assert.Contains("IsStagedFilmProfileImportValid", source, StringComparison.Ordinal);
        Assert.Contains("CanApplyStagedFilmProfileImport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCurrentFilmProfileValidation", load, StringComparison.Ordinal);
        Assert.Contains("SetFilmProfileImportError(imported.Validation ?? new ScanFilmProfileValidationResult())", load, StringComparison.Ordinal);
        Assert.Contains("SetFilmProfileImportError(staged.Validation)", load, StringComparison.Ordinal);
        Assert.Contains("SetCurrentFilmProfileValidation", snapshotProjection, StringComparison.Ordinal);
        Assert.Contains("SetStagedFilmProfileImportValidation(snapshot.ImportResult.Validation)", snapshotProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearStagedFilmProfileImportValidation", snapshotProjection, StringComparison.Ordinal);
        Assert.Contains("SetFilmProfileImportNone();", discard, StringComparison.Ordinal);
        Assert.Equal(1, Count(snapshotProjection, "ApplyDraftToFields("));
        Assert.DoesNotContain("ApplyDraftToFields", apply, StringComparison.Ordinal);
        Assert.Equal(2, Count(source, "ApplyDraftToFields("));
    }

    [Fact]
    public void Todo23ValidationOwnership_LegacyMirrorMembersAndCallersAreAbsent()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var validate = ExtractMethod(source, "ValidateFilmProfile");
        var setCurrent = ExtractMethod(source, "SetCurrentFilmProfileValidation");

        AssertNoExactSymbol(source, "FilmProfileValidationIssues");
        AssertNoExactSymbol(source, "FilmProfileValidationSummary");
        Assert.Contains("CurrentFilmProfileValidationSummary", save, StringComparison.Ordinal);
        Assert.Contains("CurrentFilmProfileValidationSummary", validate, StringComparison.Ordinal);
        AssertNoExactSymbol(setCurrent, "FilmProfileValidationIssues");
        AssertNoExactSymbol(setCurrent, "FilmProfileValidationSummary");
    }

    [Fact]
    public void Todo13DesiredContract_ScanDebugUsesRepositorySnapshotsWithoutFacade()
    {
        var source = ReadViewModelSource();

        Assert.DoesNotContain("IScanChannelParameterProfileService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_channelProfiles", source, StringComparison.Ordinal);
        Assert.Contains("private readonly IScanCalibrationProfileRepository _calibrationProfiles;", source, StringComparison.Ordinal);
        Assert.Contains("IScanCalibrationProfileRepository calibrationProfiles", source, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.InitializeAsync(CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.GetSelectedChannelAsync(CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.Snapshot.Profiles", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13DesiredContract_RuntimeMutationsAndUserSelectionUseRepository()
    {
        var source = ReadViewModelSource();
        var clear = ExtractMethod(source, "ClearChannelProfile");
        var loadSelected = ExtractMethod(source, "LoadSelectedCalibrationProfileAsync");
        var save = ExtractMethod(source, "SaveSelectedCalibrationProfileAsync");
        var saveLevels = ExtractMethod(source, "SaveCalibrationLevelsAsync");
        var selection = ExtractCallback(source, "OnSelectedCalibrationChannelChanged");

        Assert.Contains("var channelRole = SelectedCalibrationChannel;", clear, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.ClearProfileAsync(channelRole, CancellationToken.None);", clear, StringComparison.Ordinal);
        Assert.Contains("_copiedUnverifiedCalibrationProfiles.Remove(channelRole);", clear, StringComparison.Ordinal);
        Assert.Contains("if (!IsCurrentCalibrationChannel(channelRole))", clear, StringComparison.Ordinal);
        Assert.True(
            clear.IndexOf("if (!IsCurrentCalibrationChannel(channelRole))", StringComparison.Ordinal) <
            clear.IndexOf("StatusText = removed", StringComparison.Ordinal));
        Assert.Contains("await _calibrationProfiles.SetSelectedChannelAsync(channelRole, CancellationToken.None);", loadSelected, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.SaveProfileAsync(", save, StringComparison.Ordinal);
        Assert.Contains("CancellationToken.None", save, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.SaveProfileAsync(", saveLevels, StringComparison.Ordinal);
        Assert.True(selection.IndexOf("if (_isSynchronizingFilmProfileWorkspace)", StringComparison.Ordinal) < selection.IndexOf("HandleSelectedCalibrationChannelChangedAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void Todo13DesiredContract_WorkspaceAuthoringStaysPureAndMissingProfilesDoNotFallback()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var workflow = ExtractMethod(source, "TryBuildDebugWorkflowRequest");

        Assert.DoesNotContain("_calibrationProfiles.SaveProfileAsync", save, StringComparison.Ordinal);
        Assert.DoesNotContain("_calibrationProfiles.ReplaceAsync", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", apply, StringComparison.Ordinal);
        Assert.Contains("if (!_calibrationProfiles.TryGetProfile", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("? profile.Parameters : fallbackSnapshot", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo11DesiredContract_ChannelStatusProjectionDerivesFromRepositorySelectionValidationAndLedMapping()
    {
        var source = ReadViewModelSource();
        var selection = ExtractCallback(source, "OnSelectedCalibrationChannelChanged");
        var save = ExtractMethod(source, "SaveSelectedCalibrationProfileAsync");
        var clear = ExtractMethod(source, "ClearChannelProfile");
        var statusKind = ExtractMethod(source, "GetCalibrationChannelStatusKind");
        var currentProfile = ExtractMethod(source, "TryBuildCurrentCalibrationProfile");

        Assert.Contains("public enum CalibrationChannelStatusKind", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class ScanDebugCalibrationChannelItemViewModel", source, StringComparison.Ordinal);
        Assert.Contains("public ObservableCollection<ScanDebugCalibrationChannelItemViewModel> CalibrationChannelItems", source, StringComparison.Ordinal);
        Assert.Contains("public ScanDebugCalibrationChannelItemViewModel? SelectedCalibrationChannelItem", source, StringComparison.Ordinal);
        Assert.Contains("SelectedCalibrationChannel = value.Role;", source, StringComparison.Ordinal);
        Assert.Contains("RefreshCalibrationChannelItems();", selection, StringComparison.Ordinal);
        Assert.Contains("RefreshCalibrationChannelItems();", save, StringComparison.Ordinal);
        Assert.Contains("RefreshCalibrationChannelItems();", clear, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.TryGetProfile(channelRole, out var persisted)", statusKind, StringComparison.Ordinal);
        Assert.Contains("string.Equals(channelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)", statusKind, StringComparison.Ordinal);
        Assert.Contains("TryBuildCurrentCalibrationProfile(out profile!)", statusKind, StringComparison.Ordinal);
        Assert.Contains("_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz", currentProfile, StringComparison.Ordinal);
        Assert.Contains("&& TryValidateRoiInputs()", currentProfile, StringComparison.Ordinal);
        Assert.Contains("_roiSettings,", currentProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("_roiSettings.Normalize()", currentProfile, StringComparison.Ordinal);
        Assert.Contains("existingProfile?.BlackLevel", currentProfile, StringComparison.Ordinal);
        Assert.Contains("existingProfile?.WhiteLevel", currentProfile, StringComparison.Ordinal);
        Assert.Contains("GetBoundLedName(_role)", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_ChannelStatusAccessibility", source, StringComparison.Ordinal);
        Assert.Contains("CalibrationChannelStatusKind.Invalid", source, StringComparison.Ordinal);
        Assert.Contains("SelectedCalibrationCopySourceChannel", source, StringComparison.Ordinal);
        Assert.Contains("CalibrationCopySourceChannelOptions", source, StringComparison.Ordinal);
        Assert.Contains("CopyCalibrationProfileFromChannelCommand", source, StringComparison.Ordinal);
        Assert.Contains("_copiedUnverifiedCalibrationProfiles", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo7DesiredContract_CopySourceCommandStagesOnlyAndStatusTextUsesFiveLocalizedStates()
    {
        var source = ReadViewModelSource();
        var copy = ExtractMethod(source, "CopyCalibrationProfileFromChannel");
        var statusText = ExtractMethod(source, "GetCalibrationChannelStatusText");

        Assert.Contains("public IReadOnlyList<string> CalibrationCopySourceChannelOptions", source, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)", source, StringComparison.Ordinal);
        Assert.Contains("[NotifyCanExecuteChangedFor(nameof(CopyCalibrationProfileFromChannelCommand))]", source, StringComparison.Ordinal);
        Assert.Contains("public partial string? SelectedCalibrationCopySourceChannel", source, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(SelectedCalibrationChannel, SelectedCalibrationCopySourceChannel, StringComparison.OrdinalIgnoreCase)", source, StringComparison.Ordinal);
        Assert.Contains("TryGetCalibrationProfile(SelectedCalibrationCopySourceChannel", source, StringComparison.Ordinal);

        Assert.Contains("_copiedUnverifiedCalibrationProfiles[SelectedCalibrationChannel] = sourceProfile;", copy, StringComparison.Ordinal);
        Assert.Contains("ApplyCalibrationProfileProjection(sourceProfile);", copy, StringComparison.Ordinal);
        Assert.Contains("RefreshCalibrationChannelItems();", copy, StringComparison.Ordinal);
        Assert.Contains("NotifyChannelProfileOverviewChanged();", copy, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProfileAsync", copy, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearProfileAsync", copy, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceAsync", copy, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveFilmProfileJson", copy, StringComparison.Ordinal);

        foreach (var resourceKey in new[]
        {
            "ScanDebug_Runtime_ChannelStatusSaved",
            "ScanDebug_Runtime_ChannelStatusModified",
            "ScanDebug_Runtime_ChannelStatusInvalid",
            "ScanDebug_Runtime_ChannelStatusMissing",
            "ScanDebug_Runtime_ChannelStatusCopiedUnverified"
        })
        {
            Assert.Contains(resourceKey, statusText, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("CalibrationChannelStatusKind.Modified => \"ScanDebug_Runtime_ChannelStatusUnconfigured\"", statusText, StringComparison.Ordinal);
        Assert.DoesNotContain("CalibrationChannelStatusKind.CopiedUnverified => \"ScanDebug_Runtime_ChannelStatusUnconfigured\"", statusText, StringComparison.Ordinal);
        Assert.DoesNotContain("CalibrationChannelStatusKind.Missing => \"ScanDebug_Runtime_ChannelStatusUnconfigured\"", statusText, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalValidationMessageContract_ScanDebugIssuesUseStableMessageKeys()
    {
        var source = ReadViewModelSource();
        var draftBuilder = ExtractMethod(source, "TryBuildCurrentFilmProfileDraft");
        var issueFactory = ExtractMethod(source, "CreateFilmProfileValidationIssue");

        Assert.Contains("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, \"ChannelProfiles.Selected.Parameters\", \"FilmProfile.Validation.ChannelParametersInvalid\")", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("TryBuildFilmAcquisitionSettings(out var acquisitionSettings, out var acquisitionIssue)", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("issues.Add(acquisitionIssue);", draftBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, \"AcquisitionSettings\"", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidRoiInput, \"ChannelProfiles.Selected.RoiSettings\", \"FilmProfile.Validation.RoiInputInvalid\")", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("TryBuildScanRecipeSettings(out var recipeSettings, issues)", draftBuilder, StringComparison.Ordinal);
        foreach (var path in new[]
        {
            "ScanRecipeSettings.ColorManagement.RedWavelengthNm",
            "ScanRecipeSettings.ColorManagement.GreenWavelengthNm",
            "ScanRecipeSettings.ColorManagement.BlueWavelengthNm",
            "ScanRecipeSettings.ColorManagement.OutputGamma",
            "ScanRecipeSettings.ColorManagement.ManualWhitePointColorTemperatureK"
        })
        {
            Assert.Contains(path, source, StringComparison.Ordinal);
        }
        Assert.Contains("string messageKey", issueFactory, StringComparison.Ordinal);
        Assert.Contains("ScanFilmProfileValidationSeverity.Error", issueFactory, StringComparison.Ordinal);
        Assert.Contains("ScanFilmProfileValidationSeverity severity", issueFactory, StringComparison.Ordinal);
        Assert.Contains("new(code, fieldPath, severity, messageKey)", issueFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("parameterError", issueFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("acquisitionError", issueFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("recipeError", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugWorkspaceSnapshotProjection_DistinguishesExternalCurrentDraftFromLocalAndStagedOnlyChanges()
    {
        var source = ReadViewModelSource();
        var snapshotChanged = ExtractMethod(source, "OnFilmProfileWorkspaceSnapshotChanged");
        var synchronization = ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");

        Assert.Contains("_lastProjectedFilmProfileDraft", source, StringComparison.Ordinal);
        Assert.Contains("_lastProjectedFilmProfileImportResult", source, StringComparison.Ordinal);
        Assert.Contains("ApplyExternalFilmProfileWorkspaceSnapshot(snapshot)", snapshotChanged, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", synchronization, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(snapshot.CurrentDraft, _lastProjectedFilmProfileDraft)", source, StringComparison.Ordinal);
        Assert.Contains("var importResultChanged = !ReferenceEquals(snapshot.ImportResult, _lastProjectedFilmProfileImportResult);", source, StringComparison.Ordinal);
        Assert.Contains("_lastProjectedFilmProfileImportResult = snapshot.ImportResult;", source, StringComparison.Ordinal);
        Assert.Contains("SetStagedFilmProfileImportValidation(snapshot.ImportResult.Validation)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo4SnapshotOwnedProjection_RestoresEveryProfileFieldAndRefreshesSelectedChannelDependents()
    {
        var source = ReadViewModelSource();
        var build = ExtractMethod(source, "TryBuildCurrentFilmProfileDraft");
        var patch = ExtractMethod(source, "BuildCurrentChannelPatch");
        var synchronize = ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");
        var apply = ExtractMethod(source, "ApplyDraftToFields");
        var applyAcquisition = ExtractMethod(source, "ApplyProfileAcquisitionSettings");
        var applyRecipe = ExtractMethod(source, "ApplyScanRecipeSettings");
        var save = ExtractMethod(source, "SaveFilmProfileJson");

        Assert.Contains("FilmProfileName.Trim()", build, StringComparison.Ordinal);
        Assert.Contains("TryBuildFilmAcquisitionSettings", build, StringComparison.Ordinal);
        Assert.Contains("BuildCurrentChannelPatch(snapshot, patchChannel)", build, StringComparison.Ordinal);
        Assert.Contains("SelectedCalibrationChannel, acquisitionSettings, recipeSettings", build, StringComparison.Ordinal);
        Assert.Contains("existing?.BlackLevel, existing?.WhiteLevel", patch, StringComparison.Ordinal);

        Assert.Contains("ApplyProfileAcquisitionSettings", apply, StringComparison.Ordinal);
        Assert.Contains("ApplyScanRecipeSettings", apply, StringComparison.Ordinal);
        Assert.Contains("ResolveProfileChannelToLoad", apply, StringComparison.Ordinal);
        Assert.Contains("ApplyCalibrationSnapshotProjection(profile.Parameters)", apply, StringComparison.Ordinal);
        Assert.Contains("_roiSettings = profile.RoiSettings.Normalize()", apply, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiStatus", apply, StringComparison.Ordinal);
        Assert.Contains("RefreshColumnSampleStatus", apply, StringComparison.Ordinal);
        Assert.Contains("NotifyChannelProfileOverviewChanged", apply, StringComparison.Ordinal);

        foreach (var field in new[]
        {
            "ApplyDraftIlluminationStateToInputs",
            "MotorIntervalUs = FormatMotorIntervalInput",
            "RefreshDerivedMotorDistanceFromCurrentInterval",
            "ApplyMotorSpeedFromIntervalNs"
        })
        {
            Assert.Contains(field, applyAcquisition, StringComparison.Ordinal);
        }

        foreach (var field in new[]
        {
            "IsChannel1Reversed = assignment.Channel1Reversed",
            "IsChannel2Reversed = assignment.Channel2Reversed",
            "IsChannel3Reversed = assignment.Channel3Reversed",
            "IsChannel4Reversed = assignment.Channel4Reversed",
            "IsScanRecipeColorManagementEnabled = colorManagement.IsEnabled",
            "ScanRecipeRedWavelengthNm = FormatColorDouble",
            "ScanRecipeGreenWavelengthNm = FormatColorDouble",
            "ScanRecipeBlueWavelengthNm = FormatColorDouble",
            "ScanRecipeOutputGamma = FormatColorDouble",
            "SelectedScanRecipeTargetWhitePointMode = colorManagement.TargetWhitePointMode.ToString()",
            "ScanRecipeManualWhitePointColorTemperatureK = FormatColorDouble",
            "SelectedProfileAlignmentMode = alignmentMode",
            "SelectedProfileDngExportMode = dngExportMode"
        })
        {
            Assert.Contains(field, applyRecipe, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("SelectedDebugDngExportMode", build + apply + applyRecipe, StringComparison.Ordinal);

        var invalidBranch = synchronize[synchronize.IndexOf("else", StringComparison.Ordinal)..];
        Assert.Contains("_hasInvalidFilmProfileInput = true", invalidBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileWorkspace.SetCurrentDraft", invalidBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("StagedFilmProfileImportValidation", synchronize, StringComparison.Ordinal);
        Assert.True(save.IndexOf("SynchronizeFilmProfileDraftFromInputs", StringComparison.Ordinal) < save.IndexOf("BuildExportDocument", StringComparison.Ordinal));
        Assert.True(save.IndexOf("return;", save.IndexOf("SynchronizeFilmProfileDraftFromInputs", StringComparison.Ordinal), StringComparison.Ordinal) < save.IndexOf("BuildExportDocument", StringComparison.Ordinal));
    }

    [Fact]
    public void Todo3ResetThenOverlay_AtoNewAndNameOnlyExport_ClearPriorChannelEditorState()
    {
        var source = ReadViewModelSource();
        var apply = ExtractMethod(source, "ApplyDraftToFields");

        Assert.Contains("ResetProfileEditorFields();", apply, StringComparison.Ordinal);
        Assert.True(
            apply.IndexOf("ResetProfileEditorFields();", StringComparison.Ordinal)
                < apply.IndexOf("FilmProfileName =", StringComparison.Ordinal));
        Assert.Contains("SelectedCalibrationChannel = channel;", apply, StringComparison.Ordinal);
        Assert.Contains("if (draft.ChannelProfiles.TryGetValue(channel, out var profile))", apply, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo3ResetThenOverlay_MissingRecipeRoiAndReferences_ResetEveryProfileOwnedEditorDefault()
    {
        var reset = ExtractMethod(ReadViewModelSource(), "ResetProfileEditorFields");

        foreach (var field in new[]
        {
            "var defaultAcquisitionSettings = ScanFilmAcquisitionSettings.CreateDefault();",
            "ApplyProfileAcquisitionSettings(defaultAcquisitionSettings);",
            "SelectedCalibrationChannel = string.Empty;",
            "ExposureTicks = string.Empty;",
            "Adc1Offset = string.Empty;",
            "Adc1Gain = string.Empty;",
            "Adc2Offset = string.Empty;",
            "Adc2Gain = string.Empty;",
            "SysClockKhz = string.Empty;",
            "_roiSettings = ScanCalibrationRoiSettings.CreateDefault();",
            "IsChannel1Reversed = false;",
            "IsChannel2Reversed = false;",
            "IsChannel3Reversed = false;",
            "IsChannel4Reversed = false;",
            "IsScanRecipeColorManagementEnabled = defaultColorManagement.IsEnabled;",
            "ScanRecipeRedWavelengthNm = FormatColorDouble(defaultColorManagement.RedWavelengthNm);",
            "ScanRecipeGreenWavelengthNm = FormatColorDouble(defaultColorManagement.GreenWavelengthNm);",
            "ScanRecipeBlueWavelengthNm = FormatColorDouble(defaultColorManagement.BlueWavelengthNm);",
            "ScanRecipeOutputGamma = FormatColorDouble(defaultColorManagement.OutputGamma);",
            "SelectedScanRecipeTargetWhitePointMode = defaultColorManagement.TargetWhitePointMode.ToString();",
            "ScanRecipeManualWhitePointColorTemperatureK = FormatColorDouble(defaultColorManagement.ManualWhitePointColorTemperatureK);",
            "SelectedProfileAlignmentMode = AlignmentModeOptions[0];",
            "SelectedProfileDngExportMode = DngExportModeOptions[0];"
        })
        {
            Assert.Contains(field, reset, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Todo3ResetThenOverlay_ChannelResolution_UsesRequestedThenFirstKnownThenNoSelection()
    {
        var resolve = ExtractMethod(ReadViewModelSource(), "ResolveProfileChannelToLoad");

        Assert.Contains("return selectedChannel;", resolve, StringComparison.Ordinal);
        Assert.Contains("return firstKnown;", resolve, StringComparison.Ordinal);
        Assert.Contains("return string.Empty;", resolve, StringComparison.Ordinal);
        Assert.DoesNotContain("return SelectedCalibrationChannel;", resolve, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo3ResetThenOverlay_RepeatedAtoBtoA_RefreshesProjectionWithoutDeviceOrLibraryMutation()
    {
        var apply = ExtractMethod(ReadViewModelSource(), "ApplyDraftToFields");

        Assert.Contains("ResetProfileEditorFields();", apply, StringComparison.Ordinal);
        Assert.Contains("RefreshRoiStatus();", apply, StringComparison.Ordinal);
        Assert.Contains("RefreshColumnSampleStatus();", apply, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection();", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("_calibrationProfiles.", apply, StringComparison.Ordinal);
    }

    private static string ReadViewModelSource()
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PrismUtility.Core.Tests", "Fixtures", "FilmProfile", name));

    private static string ReadTodo9AnalysisEvidence()
        => File.ReadAllText(Path.Combine(Directory.GetParent(FindHostSoftwareRoot())!.FullName, ".omo", "evidence", "task-9-scan-profile-editor-architecture-analysis.txt"));

    private static string ReadTodo13BaselineEvidence()
        => File.ReadAllText(Path.Combine(Directory.GetParent(FindHostSoftwareRoot())!.FullName, ".omo", "evidence", "task-13-scandebug-baseline.txt"));

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }

    internal static string ExtractMethod(string source, string methodName)
    {
        var start = FindMethodStart(source, methodName);
        var openBrace = FindCodeOpenBrace(source, start, methodName);
        var closeBrace = FindMatchingCodeBrace(source, openBrace, methodName);
        return source[start..(closeBrace + 1)];
    }

    private static string ExtractCallback(string source, string methodName)
    {
        var start = FindMethodStart(source, methodName);
        var end = source.IndexOf("\n    partial ", start + 1, StringComparison.Ordinal);
        return source[start..(end >= 0 ? end : source.Length)];
    }

    private static int FindMethodStart(string source, string methodName)
    {
        var searchStart = 0;
        while (true)
        {
            var candidate = source.IndexOf($" {methodName}(", searchStart, StringComparison.Ordinal);
            Assert.True(candidate >= 0, $"Could not find {methodName}.");

            var lineStart = source.LastIndexOf('\n', candidate) + 1;
            var declaration = source[lineStart..candidate];
            if (declaration.Contains("private ", StringComparison.Ordinal)
                || declaration.Contains("internal ", StringComparison.Ordinal)
                || declaration.Contains("public ", StringComparison.Ordinal)
                || declaration.Contains("partial ", StringComparison.Ordinal))
            {
                return candidate;
            }

            searchStart = candidate + methodName.Length + 1;
        }
    }

    private static int FindCodeOpenBrace(string source, int start, string methodName)
    {
        var state = LexicalState.Code;
        var rawDelimiterQuotes = 0;
        for (var index = start; index < source.Length; index++)
        {
            if (AdvanceLexicalState(source, ref index, ref state, ref rawDelimiterQuotes))
                continue;
            if (state == LexicalState.Code && source[index] == '{')
                return index;
        }

        throw new InvalidOperationException($"Could not find opening brace for '{methodName}'.");
    }

    private static int FindMatchingCodeBrace(string source, int openBrace, string methodName)
    {
        var depth = 0;
        var state = LexicalState.Code;
        var rawDelimiterQuotes = 0;
        for (var index = openBrace; index < source.Length; index++)
        {
            if (AdvanceLexicalState(source, ref index, ref state, ref rawDelimiterQuotes))
                continue;
            if (state != LexicalState.Code)
                continue;
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return index;
        }

        throw new InvalidOperationException($"Could not find closing brace for '{methodName}'.");
    }

    private static bool AdvanceLexicalState(string source, ref int index, ref LexicalState state, ref int rawDelimiterQuotes)
    {
        var current = source[index];
        var next = index + 1 < source.Length ? source[index + 1] : '\0';
        if (state == LexicalState.LineComment)
        {
            if (current is '\r' or '\n') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.BlockComment)
        {
            if (current == '*' && next == '/') { index++; state = LexicalState.Code; }
            return true;
        }
        if (state == LexicalState.Character)
        {
            if (current == '\\') index++;
            else if (current == '\'') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.String)
        {
            if (current == '\\') index++;
            else if (current == '"') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.VerbatimString)
        {
            if (current == '"' && next == '"') index++;
            else if (current == '"') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.RawString)
        {
            if (current == '"' && CountRun(source, index, '"') >= rawDelimiterQuotes)
            {
                index += rawDelimiterQuotes - 1;
                rawDelimiterQuotes = 0;
                state = LexicalState.Code;
            }
            return true;
        }
        if (current == '/' && next == '/') { index++; state = LexicalState.LineComment; return true; }
        if (current == '/' && next == '*') { index++; state = LexicalState.BlockComment; return true; }
        if (current == '\'') { state = LexicalState.Character; return true; }
        var dollarCount = CountRun(source, index, '$');
        var quoteStart = index + dollarCount;
        var quoteCount = quoteStart < source.Length ? CountRun(source, quoteStart, '"') : 0;
        if (quoteCount >= 3)
        {
            index = quoteStart + quoteCount - 1;
            rawDelimiterQuotes = quoteCount;
            state = LexicalState.RawString;
            return true;
        }
        if (current == '"') { state = LexicalState.String; return true; }
        if (current == '@' && next == '"') { index++; state = LexicalState.VerbatimString; return true; }
        if (current == '$' && next == '@' && index + 2 < source.Length && source[index + 2] == '"') { index += 2; state = LexicalState.VerbatimString; return true; }
        if (current == '@' && next == '$' && index + 2 < source.Length && source[index + 2] == '"') { index += 2; state = LexicalState.VerbatimString; return true; }
        if (current == '$' && next == '"') { index++; state = LexicalState.String; return true; }
        return false;
    }

    private static int CountRun(string source, int start, char character)
    {
        var count = 0;
        while (start + count < source.Length && source[start + count] == character)
            count++;
        return count;
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var start = 0;
        while ((start = source.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }
        return count;
    }

    private static void AssertNoExactSymbol(string source, string symbol)
        => Assert.DoesNotMatch($@"\b{System.Text.RegularExpressions.Regex.Escape(symbol)}\b", source);

    private enum LexicalState { Code, LineComment, BlockComment, Character, String, VerbatimString, RawString }
}
