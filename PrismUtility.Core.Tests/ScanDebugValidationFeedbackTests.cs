using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Fact]
    public async Task FilmProfileCheck_EmptyWorkbenchImmediatelyShowsPendingInputsBeforeAnExplicitCheck()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, null);
        var vm = harness.ViewModel;
        var snapshot = harness.Workspace.Snapshot;

        Assert.True(string.IsNullOrWhiteSpace(vm.SysClockMhz));
        Assert.Equal(new[] { "Adc1Gain", "Adc1Offset", "Adc2Gain", "Adc2Offset", "ExposureMicroseconds", "SysClockMhz" },
            vm.CurrentFilmProfileValidationIssues.Select(issue => issue.FieldPath.Split('.').Last()).OrderBy(field => field));
        Assert.All(vm.CurrentFilmProfileValidationIssues, issue => Assert.True(FilmProfileValidationTextPresenter.IsPendingInput(issue)));
        Assert.All(vm.CurrentFilmProfileValidationIssueDisplays, display => Assert.True(display.CanNavigate));
        var inputOrder = new[] { "SysClockMhz", "ExposureMicroseconds", "Adc1Offset", "Adc1Gain", "Adc2Offset", "Adc2Gain" };
        Assert.Equal(inputOrder, vm.CurrentFilmProfileValidationIssueDisplays.Select(display => display.FieldPath.Split('.').Last()));
        Assert.Equal(inputOrder, vm.ChannelCalibrationCurrentFilmProfileValidationIssueDisplays.Select(display => display.FieldPath.Split('.').Last()));
        Assert.Equal(InfoBarSeverity.Informational, vm.CurrentFilmProfileValidationSeverity);
        Assert.Equal("FilmProfile_Validation_SaveHintPending".GetLocalizedFormatOrFallback(
            "Fill in {0} setting(s) before saving a complete configuration. You can save a draft now.", 6), vm.FilmProfileSaveHintText);
        Assert.False(vm.CanSaveFilmProfileFile);
        var initialPresentation = (vm.WorkbenchValidationActionText, vm.CurrentFilmProfileValidationHeadline,
            vm.CurrentFilmProfileValidationSeverity, vm.FilmProfileSaveHintText);

        vm.ValidateFilmProfileCommand.Execute(null);
        await harness.FlushAsync();

        Assert.Equal(initialPresentation, (vm.WorkbenchValidationActionText, vm.CurrentFilmProfileValidationHeadline,
            vm.CurrentFilmProfileValidationSeverity, vm.FilmProfileSaveHintText));
        Assert.Same(snapshot.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(snapshot.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Empty(vm.OperationNotices);
    }

    [Fact]
    public async Task FilmProfileCheck_ParameterDisplayOrderPreservesUnrelatedIssuesAndBusinessCollection()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, null);
        var vm = harness.ViewModel;
        var parameters = vm.CurrentFilmProfileValidationIssues.ToArray();
        var first = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput,
            "AcquisitionSettings.Rows", "FilmProfile.Validation.AcquisitionInputInvalid");
        var second = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidRoiInput,
            "ChannelProfiles.Selected.RoiSettings", "FilmProfile.Validation.RoiInputInvalid");
        IReadOnlyList<ScanFilmProfileValidationIssue> source = [first, parameters[0], second, .. parameters.Skip(1)];
        vm.CurrentFilmProfileValidationIssues = source;

        var displayed = vm.CurrentFilmProfileValidationIssueDisplays.Select(display => display.Issue).ToArray();

        Assert.Same(first, displayed[0]);
        Assert.Same(second, displayed[2]);
        Assert.Equal(new[] { "SysClockMhz", "ExposureMicroseconds", "Adc1Offset", "Adc1Gain", "Adc2Offset", "Adc2Gain" },
            displayed.Where(FilmProfileValidationTextPresenter.IsPendingInput).Select(issue => issue.FieldPath.Split('.').Last()));
        Assert.Same(source, vm.CurrentFilmProfileValidationIssues);
        Assert.Equal(parameters, source.Where(FilmProfileValidationTextPresenter.IsPendingInput));
        Assert.False(vm.IsCurrentFilmProfileValidationValid);
        Assert.False(vm.CanSaveFilmProfileFile);
    }

    [Fact]
    public async Task FilmProfileCheck_CompletingBlankParametersRemovesOnlyTheirOwnPendingEntry()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, attachRuntimeBindings: true,
            parameters: new ScanParameterService(new ScanProtocolService()));
        var vm = harness.ViewModel;
        var remaining = vm.CurrentFilmProfileValidationIssues.Select(issue => issue.FieldPath).ToHashSet();
        Assert.Equal(6, remaining.Count);
        var edits = new (string Field, Action Fill)[]
        {
            (nameof(vm.SysClockMhz), () => vm.SysClockMhz = "52"),
            (nameof(vm.ExposureMicroseconds), () => vm.ExposureMicroseconds = "1000"),
            (nameof(vm.Adc1Offset), () => vm.Adc1Offset = "0"),
            (nameof(vm.Adc1Gain), () => vm.Adc1Gain = "1"),
            (nameof(vm.Adc2Offset), () => vm.Adc2Offset = "0")
        };
        foreach (var (field, fill) in edits)
        {
            fill();
            await harness.FlushAsync();
            Assert.True(remaining.Remove($"ChannelProfiles.Selected.Parameters.{field}"));
            Assert.Equal(remaining.OrderBy(path => path), vm.CurrentFilmProfileValidationIssues.Select(issue => issue.FieldPath).OrderBy(path => path));
            Assert.All(vm.CurrentFilmProfileValidationIssues, issue => Assert.True(FilmProfileValidationTextPresenter.IsPendingInput(issue)));
            Assert.False(vm.CanSaveFilmProfileFile);
            Assert.False(vm.ApplyParametersCommand.CanExecute(null));
        }
        vm.Adc2Gain = "64";
        await harness.FlushAsync();
        var invalid = Assert.Single(vm.CurrentFilmProfileValidationIssues);
        Assert.Equal("FilmProfile.Validation.AdcGainInputInvalid", invalid.MessageKey);
        Assert.False(FilmProfileValidationTextPresenter.IsPendingInput(invalid));
        Assert.Equal(InfoBarSeverity.Error, vm.CurrentFilmProfileValidationSeverity);

        vm.Adc2Gain = "1";
        await harness.FlushAsync();
        Assert.DoesNotContain(vm.CurrentFilmProfileValidationIssues, issue => issue.Code == ScanFilmProfileValidationCode.InvalidChannelParameters);
    }

    [Theory]
    [InlineData(256, 1)]
    [InlineData(0, 64)]
    public async Task FilmProfileCheck_StoredInvalidAdcParametersRemainErrors(int offset, ushort gain)
    {
        var draft = CreateIntervalBasedTransportDraft(100_000);
        var profiles = draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        var channel = draft.SelectedCalibrationChannel!;
        profiles[channel] = profiles[channel] with { Parameters = profiles[channel].Parameters with { Adc1Offset = offset, Adc1Gain = gain } };
        var invalid = new ScanFilmProfileDraft(draft.ProfileName, draft.SavedAtUtc, profiles,
            channel, draft.AcquisitionSettings, draft.ScanRecipeSettings);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, invalid, attachRuntimeBindings: true,
            parameters: new ScanParameterService(new ScanProtocolService()));

        Assert.Contains(harness.ViewModel.CurrentFilmProfileValidationIssues, issue => issue.Code == ScanFilmProfileValidationCode.InvalidChannelParameters);
        Assert.DoesNotContain(harness.ViewModel.CurrentFilmProfileValidationIssues, FilmProfileValidationTextPresenter.IsPendingInput);
        Assert.Equal(InfoBarSeverity.Error, harness.ViewModel.CurrentFilmProfileValidationSeverity);
        Assert.False(harness.ViewModel.CanSaveFilmProfileFile);
    }

    [Fact]
    public async Task FilmProfileCheck_ExistingInvalidChannelClockRetainsDocumentErrorOnInitialProjection()
    {
        var draft = CreateIntervalBasedTransportDraft(100_000);
        var profiles = draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        var channel = draft.SelectedCalibrationChannel!;
        profiles[channel] = profiles[channel] with { Parameters = profiles[channel].Parameters with { SysClockKhz = 0 } };
        var invalidDraft = new ScanFilmProfileDraft(draft.ProfileName, draft.SavedAtUtc, profiles,
            draft.SelectedCalibrationChannel, draft.AcquisitionSettings, draft.ScanRecipeSettings);

        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, invalidDraft);
        var vm = harness.ViewModel;

        Assert.Contains(vm.CurrentFilmProfileValidationIssues, issue => issue.Code == ScanFilmProfileValidationCode.InvalidChannelParameters);
        Assert.DoesNotContain(vm.CurrentFilmProfileValidationIssues, FilmProfileValidationTextPresenter.IsPendingInput);
        Assert.Equal(InfoBarSeverity.Error, vm.CurrentFilmProfileValidationSeverity);
        Assert.False(vm.CanSaveFilmProfileFile);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("29", false)]
    [InlineData("unfinished", false)]
    public async Task FilmProfileCheck_ClockInputPresentationDistinguishesPendingWhileKeepingDraftAndSafetyRules(string clockInput, bool pending)
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateIntervalBasedTransportDraft(100_000),
            deviceSettings: CreateFullV6DeviceSettings());
        var vm = harness.ViewModel;
        vm.SysClockMhz = clockInput;

        vm.ValidateFilmProfileCommand.Execute(null);
        await harness.FlushAsync();

        var issue = Assert.Single(vm.CurrentFilmProfileValidationIssues);
        Assert.Equal(pending, FilmProfileValidationTextPresenter.IsPendingInput(issue));
        Assert.Equal(pending ? InfoBarSeverity.Informational : InfoBarSeverity.Error, vm.CurrentFilmProfileValidationSeverity);
        Assert.Equal(pending ? InfoBarSeverity.Informational : InfoBarSeverity.Error, vm.ChannelCalibrationCurrentFilmProfileValidationSeverity);
        Assert.False(vm.IsCurrentFilmProfileValidationValid);
        Assert.False(vm.CanSaveFilmProfileFile);
        Assert.False(vm.ApplyDeviceClockCommand.CanExecute(null));
        Assert.False(vm.ApplyParametersCommand.CanExecute(null));
        var display = Assert.Single(vm.CurrentFilmProfileValidationIssueDisplays);
        Assert.True(display.CanNavigate);
        if (pending)
        {
            Assert.Equal("FilmProfile_Validation_PendingCount".GetLocalizedFormatOrFallback("{0} to fill in", 1), vm.CurrentFilmProfileValidationHeadline);
            Assert.Equal(vm.CurrentFilmProfileValidationHeadline, vm.BwValidationSummaryText);
            Assert.Contains("FilmProfile_Validation_PendingLabel".GetLocalizedOrFallback("To fill in"), display.SourceSeverityText);
            Assert.Equal("FilmProfile_Validation_SaveHintPending".GetLocalizedFormatOrFallback(
                "Fill in {0} setting(s) before saving a complete configuration. You can save a draft now.", 1), vm.FilmProfileSaveHintText);
        }
        Assert.Equal(FilmProfileValidationTextPresenter.GetValidationIssueText(issue), vm.ChannelClockValidationMessage);
        Assert.Empty(vm.OperationNotices);

        var gateway = new EditingDraftMemoryGateway();
        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();

        var saved = Assert.IsType<ScanFilmProfileDraftFile>(new ScanFilmProfileDraftFileService().Parse(gateway.SavedText).DraftFile);
        Assert.Equal(clockInput, saved.ChannelRawFields[vm.SelectedCalibrationChannel][nameof(vm.SysClockMhz)]);
        Assert.False(vm.CanSaveFilmProfileFile);
        AssertEditingDraftDidNotWriteLibrary(harness);

        vm.SysClockMhz = "52";
        vm.ValidateFilmProfileCommand.Execute(null);
        await harness.FlushAsync();

        Assert.DoesNotContain(vm.CurrentFilmProfileValidationIssues, FilmProfileValidationTextPresenter.IsSystemClockInputIssue);
        Assert.True(vm.IsCurrentFilmProfileValidationValid);
        Assert.Equal(InfoBarSeverity.Success, vm.CurrentFilmProfileValidationSeverity);
    }

    [Fact]
    public async Task FilmProfileCheck_PendingClockDoesNotDowngradeAnInvalidAcquisitionSetting()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateIntervalBasedTransportDraft(100_000));
        var vm = harness.ViewModel;
        vm.SysClockMhz = string.Empty;
        vm.SelectedRows = "unfinished rows";

        vm.ValidateFilmProfileCommand.Execute(null);
        await harness.FlushAsync();

        Assert.Contains(vm.CurrentFilmProfileValidationIssues, FilmProfileValidationTextPresenter.IsPendingInput);
        Assert.Contains(vm.CurrentFilmProfileValidationIssues, issue => issue.Code == ScanFilmProfileValidationCode.InvalidAcquisitionInput);
        Assert.Equal(InfoBarSeverity.Error, vm.CurrentFilmProfileValidationSeverity);
        Assert.Equal(InfoBarSeverity.Error, vm.AcquisitionCurrentFilmProfileValidationSeverity);
        Assert.False(vm.CanSaveFilmProfileFile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilmProfileCheck_RequestsOneReviewWithoutPublishingDuplicateOperationFeedback(bool invalid)
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"),
            deviceSettings: CreateFullV6DeviceSettings());
        var vm = harness.ViewModel;
        if (invalid)
            vm.SelectedRows = "unfinished rows";
        var snapshot = harness.Workspace.Snapshot;
        var feedback = (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen);
        var runText = vm.RunPresentationText;
        var reviews = 0;
        vm.FilmProfileValidationReviewRequested += (_, _) => reviews++;

        vm.ValidateFilmProfileCommand.Execute(null);
        await harness.FlushAsync();

        Assert.Equal(1, reviews);
        Assert.Equal(!invalid, vm.IsCurrentFilmProfileValidationValid);
        Assert.Equal(feedback, (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen));
        Assert.Empty(vm.OperationNotices);
        Assert.Equal(runText, vm.RunPresentationText);
        Assert.Same(snapshot.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(snapshot.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Null(harness.FilmProfileFiles.ExportedProfile);
        if (invalid)
            Assert.Contains(vm.CurrentFilmProfileValidationIssues, issue => issue.Severity == ScanFilmProfileValidationSeverity.Error);
    }

    [Fact]
    public async Task FilmProfileCheck_PreservesARealFileFailureWhileShowingInputIssuesInReview()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        await vm.SaveEditingDraftAsync(new EditingDraftMemoryGateway { WriteFailure = new IOException("disk rejected draft") });
        var notice = Assert.Single(vm.OperationNotices);
        Assert.Equal(InfoBarSeverity.Error, notice.Severity);
        var feedback = (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen);
        vm.SelectedRows = "unfinished rows";
        var reviews = 0;
        vm.FilmProfileValidationReviewRequested += (_, _) => reviews++;

        vm.ValidateFilmProfileCommand.Execute(null);
        await harness.FlushAsync();

        Assert.Equal(1, reviews);
        Assert.NotEmpty(vm.CurrentFilmProfileValidationIssues);
        Assert.Same(notice, Assert.Single(vm.OperationNotices));
        Assert.Equal(feedback, (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen));
        Assert.NotEqual(notice.Message, vm.RunPresentationText);
        Assert.Empty(vm.RunContextText);
    }

    [Fact]
    public async Task FilmProfileSave_RechecksInvalidInputAndRequestsReviewWithoutExportOrDuplicateFailure()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"));
        var vm = harness.ViewModel;
        vm.SelectedRows = "unfinished rows";
        var reviews = 0;
        vm.FilmProfileValidationReviewRequested += (_, _) => reviews++;
        Assert.False(vm.CanSaveFilmProfileFile);

        // Exercise the command's final validation gate even if a stale UI invocation reaches it.
        await vm.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(1, reviews);
        Assert.NotEmpty(vm.CurrentFilmProfileValidationIssues);
        Assert.Empty(vm.OperationNotices);
        Assert.False(vm.FilmProfileOperationIsOpen);
        Assert.Null(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(0, vm.FilmProfileExportVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilmProfilePicker_CancellationKeepsCurrentDraftAndRealFailureFeedback(bool save)
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        await vm.SaveEditingDraftAsync(new EditingDraftMemoryGateway { WriteFailure = new IOException("disk rejected draft") });
        var feedback = (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen);
        var notice = Assert.Single(vm.OperationNotices);
        var snapshot = harness.Workspace.Snapshot;
        var fileName = vm.FilmProfileFileName;
        var exportVersion = vm.FilmProfileExportVersion;
        harness.FilmProfileFiles.ImportResult = new ScanFilmProfileFileImportResult(true, null, null);

        if (save)
            await vm.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        else
            await vm.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(feedback, (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen));
        Assert.Same(notice, Assert.Single(vm.OperationNotices));
        Assert.Same(snapshot.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(snapshot.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Equal(fileName, vm.FilmProfileFileName);
        Assert.Equal(exportVersion, vm.FilmProfileExportVersion);
        Assert.False(vm.IsFilmProfileOperationRunning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilmProfileImport_UsesStagedReviewWithoutRepeatingResultInRunBarOrNotification(bool damaged)
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var document = harness.Workspace.BuildExportDocument().Document.Document!;
        vm.SelectedRows = "unfinished rows";
        var snapshot = harness.Workspace.Snapshot;
        var runText = vm.RunPresentationText;
        harness.FilmProfileFiles.ImportResult = damaged
            ? new ScanFilmProfileFileImportResult(false, null, new ScanFilmProfileValidationResult([
                CreateFilmProfileIssue(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson")]), "Damaged.json")
            : new ScanFilmProfileFileImportResult(false, document, null, "Loaded.json");

        await vm.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.True(vm.HasPendingFilmProfileImportResult);
        Assert.Equal(!damaged, vm.HasStagedFilmProfileImport);
        Assert.Equal(damaged ? InfoBarSeverity.Error : InfoBarSeverity.Success, vm.FilmProfileOperationSeverity);
        Assert.False(vm.FilmProfileOperationIsOpen);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.FilmProfileOperationVisibility);
        Assert.Equal(runText, vm.RunPresentationText);
        Assert.Empty(vm.RunContextText);
        Assert.Equal("unfinished rows", vm.SelectedRows);
        Assert.Same(snapshot.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(snapshot.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        if (damaged)
        {
            var notice = Assert.Single(vm.OperationNotices);
            Assert.Equal("FilmProfile", notice.Target);
            Assert.Equal(vm.FilmProfileOperationMessage, notice.Message);
            Assert.Contains("1", vm.OperationFeedbackActionText, StringComparison.Ordinal);
        }
        else
            Assert.Empty(vm.OperationNotices);
    }

    [Fact]
    public async Task FilmProfileImport_IoFailureRemainsVisibleWithoutReplacingRunStatus()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var snapshot = harness.Workspace.Snapshot;
        var runText = vm.RunPresentationText;
        harness.FilmProfileFiles.PendingImport = Task.FromException<ScanFilmProfileFileImportResult>(new IOException("file read failed"));

        await vm.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.True(vm.FilmProfileOperationIsOpen);
        Assert.Equal(InfoBarSeverity.Error, vm.FilmProfileOperationSeverity);
        Assert.Equal("ScanDebug_Runtime_StatusLoadFilmProfileFailed".GetLocalizedFormat("file read failed"), vm.FilmProfileOperationMessage);
        Assert.Equal("FilmProfile", Assert.Single(vm.OperationNotices).Target);
        Assert.Equal(runText, vm.RunPresentationText);
        Assert.Empty(vm.RunContextText);
        Assert.Same(snapshot.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(snapshot.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
    }

    [Fact]
    public async Task RunNotice_FileFailureRetainsHistoryWithoutMaskingPreviousRuntimeFailure()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        await vm.SaveColumnSampleAsBlackLevelCommand.ExecuteAsync(null);
        var runtimeNotice = Assert.Single(vm.OperationNotices);
        Assert.Equal("ColumnSample", runtimeNotice.Target);
        var context = vm.RunContextText;

        await vm.SaveEditingDraftAsync(new EditingDraftMemoryGateway { WriteFailure = new IOException("file write failed") });

        Assert.Equal(2, vm.OperationNotices.Count);
        Assert.Equal("FilmProfile", vm.OperationNotices[^1].Target);
        Assert.Equal(runtimeNotice.Message, vm.RunPresentationText);
        Assert.Equal(context, vm.RunContextText);
        Assert.Contains("2", vm.OperationFeedbackActionText, StringComparison.Ordinal);
        vm.AcknowledgeOperationNoticeCommand.Execute(runtimeNotice.Id);
        Assert.Equal("FilmProfile", Assert.Single(vm.OperationNotices).Target);
        Assert.NotEqual(vm.OperationNotices[0].Message, vm.RunPresentationText);
        Assert.Empty(vm.RunContextText);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FilmProfileImport_SelectAnotherFileNeedsNoConfirmationAndPreservesEditor(bool pendingError, bool cancel)
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var replacement = harness.Workspace.BuildExportDocument().Document.Document! with { ProfileName = "Replacement profile" };
        if (pendingError)
            harness.Workspace.SetImportError(new ScanFilmProfileValidationResult([
                CreateFilmProfileIssue(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson")]));
        else
            Assert.True(harness.Workspace.StageImport(replacement with { ProfileName = "Original staged profile" }).Staged);
        vm.SelectedRows = "unfinished rows";
        await harness.FlushAsync();
        var before = harness.Workspace.Snapshot;
        var saved = harness.Workspace.LastSavedDocument;
        var fileName = vm.FilmProfileFileName;
        var importIssues = vm.StagedFilmProfileImportValidationIssues;
        var confirmations = 0;
        vm.FilmProfileDiscardConfirmationRequested += (_, _) => confirmations++;
        harness.FilmProfileFiles.ImportResult = cancel
            ? new ScanFilmProfileFileImportResult(true, null, null)
            : new ScanFilmProfileFileImportResult(false, replacement, null, "Replacement.json");

        await vm.LoadFilmProfileJsonCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(3));
        await harness.FlushAsync();

        Assert.Equal(0, confirmations);
        Assert.Same(before.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(before.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(saved!, harness.Workspace.LastSavedDocument!);
        Assert.Equal(fileName, vm.FilmProfileFileName);
        Assert.Equal("unfinished rows", vm.SelectedRows);
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.True(vm.HasPendingFilmProfileImportResult);
        Assert.False(vm.IsFilmProfileOperationRunning);
        if (cancel)
        {
            Assert.Same(before, harness.Workspace.Snapshot);
            Assert.Same(before.ImportResult, harness.Workspace.Snapshot.ImportResult);
            Assert.Same(importIssues, vm.StagedFilmProfileImportValidationIssues);
        }
        else
        {
            Assert.NotSame(before.ImportResult, harness.Workspace.Snapshot.ImportResult);
            Assert.Equal(ScanFilmProfileImportResultState.ValidStaged, harness.Workspace.Snapshot.ImportResult.State);
            Assert.Equal("Replacement profile", harness.Workspace.Snapshot.StagedImport!.Draft.ProfileName);
        }
    }
}
