using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Fact]
    public async Task EditingDraft_SaveAndOpen_RestoresInvalidRowsAndExposureAsACleanEditingCheckpoint()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var savedRunnable = harness.Workspace.LastSavedDocument!;
        var role = vm.SelectedCalibrationChannel;
        vm.SelectedRows = "unfinished rows";
        vm.ExposureMicroseconds = "  invalid exposure  ";
        var gateway = new EditingDraftMemoryGateway();

        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();

        var persisted = Assert.IsType<ScanFilmProfileDraftFile>(new ScanFilmProfileDraftFileService().Parse(gateway.SavedText).DraftFile);
        Assert.Equal("unfinished rows", persisted.RawFields[nameof(ScanDebugViewModel.SelectedRows)]);
        Assert.Equal("  invalid exposure  ", persisted.ChannelRawFields[role][nameof(ScanDebugViewModel.ExposureMicroseconds)]);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.False(harness.Workspace.Snapshot.IsDirty);
        Assert.Equal(gateway.FileName, vm.EditingDraftFileName);
        Assert.Equal("ScanDebug_ProfileStateDraftSaved".GetLocalized(), vm.ProfileSaveStateText);
        Assert.Equal("Previously saved.json", vm.FilmProfileFileName);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(savedRunnable, harness.Workspace.LastSavedDocument!);
        Assert.Equal(0, vm.FilmProfileExportVersion);

        vm.SelectedRows = "256";
        vm.ExposureMicroseconds = "100";
        Assert.Equal("ScanDebug_ProfileStateDraftUnsaved".GetLocalized(), vm.ProfileSaveStateText);
        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.Equal("unfinished rows", vm.SelectedRows);
        Assert.Equal("  invalid exposure  ", vm.ExposureMicroseconds);
        Assert.Equal(role, vm.SelectedCalibrationChannel);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.False(harness.Workspace.Snapshot.IsDirty);
        Assert.Null(harness.Workspace.LastSavedDocument);
        Assert.Null(vm.FilmProfileFileName);
        Assert.Equal(gateway.FileName, vm.CurrentProfileNameText);
        Assert.Equal("ScanDebug_ProfileStateDraftSaved".GetLocalized(), vm.ProfileSaveStateText);
        Assert.False(vm.CanSaveFilmProfileFile);
        Assert.Equal(0, vm.FilmProfileExportVersion);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task EditingDraft_UnnamedEmptyConfiguration_CanBeSavedAndReopenedWithoutRunnableDocument()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, null);
        var vm = harness.ViewModel;
        vm.SelectedRows = string.Empty;
        vm.ExposureMicroseconds = string.Empty;
        var gateway = new EditingDraftMemoryGateway();

        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        Assert.NotNull(gateway.SavedText);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.Null(harness.Workspace.LastSavedDocument);

        vm.SelectedRows = "128";
        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.Equal(string.Empty, vm.FilmProfileName);
        Assert.Equal(string.Empty, vm.SelectedRows);
        Assert.Equal(string.Empty, vm.ExposureMicroseconds);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.Null(harness.Workspace.LastSavedDocument);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditingDraft_SaveCancelOrFailure_PreservesDirtyStateIdentityAndBaseline(bool fail)
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        vm.EditingDraftFileName = "Existing editing file.prismdraft";
        vm.SelectedRows = "unsaved invalid rows";
        vm.ExposureMicroseconds = "unsaved invalid exposure";
        var before = harness.Workspace.Snapshot;
        var savedRunnable = harness.Workspace.LastSavedDocument!;
        var previousFeedback = (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility);
        var gateway = new EditingDraftMemoryGateway
        {
            CancelWrite = !fail,
            WriteFailure = fail ? new IOException("injected write failure") : null
        };

        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.Equal("unsaved invalid rows", vm.SelectedRows);
        Assert.Equal("unsaved invalid exposure", vm.ExposureMicroseconds);
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Same(before.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(before.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Equal("Existing editing file.prismdraft", vm.EditingDraftFileName);
        Assert.Equal("Previously saved.json", vm.FilmProfileFileName);
        Assert.Null(gateway.SavedText);
        Assert.False(vm.IsFilmProfileOperationRunning);
        if (fail)
        {
            Assert.True(vm.FilmProfileOperationIsOpen);
            Assert.Equal(InfoBarSeverity.Error, vm.FilmProfileOperationSeverity);
        }
        else
        {
            Assert.Equal(previousFeedback, (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility));
        }
        FilmProfileRoundTripAssertions.EqualCompleteDocument(savedRunnable, harness.Workspace.LastSavedDocument!);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Theory]
    [InlineData("json syntax")]
    [InlineData("typed snapshot")]
    [InlineData("typed raw input")]
    [InlineData("missing global input")]
    public async Task EditingDraft_OpenDamagedOrIncompleteFile_DoesNotReplaceCurrentEditor(string damage)
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var gateway = new EditingDraftMemoryGateway();
        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        var root = JObject.Parse(gateway.SavedText!);
        switch (damage)
        {
            case "typed snapshot":
                root["CurrentDraft"] = JValue.CreateNull();
                break;
            case "typed raw input":
                root["RawFields"]![nameof(ScanDebugViewModel.IsWarmUpEnabled)] = "not a boolean";
                break;
            case "missing global input":
                ((JObject)root["RawFields"]!).Remove(nameof(ScanDebugViewModel.SelectedRows));
                break;
        }
        gateway.TextToRead = damage == "json syntax" ? "{ broken draft" : root.ToString(Formatting.None);
        vm.SelectedRows = "my unfinished rows";
        vm.ExposureMicroseconds = "my unfinished exposure";
        var before = harness.Workspace.Snapshot;
        var savedRunnable = harness.Workspace.LastSavedDocument!;

        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.Equal("my unfinished rows", vm.SelectedRows);
        Assert.Equal("my unfinished exposure", vm.ExposureMicroseconds);
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Same(before.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(before.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Equal(gateway.FileName, vm.EditingDraftFileName);
        Assert.Equal("Previously saved.json", vm.FilmProfileFileName);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(savedRunnable, harness.Workspace.LastSavedDocument!);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditingDraft_OpenCanceledOrReadFailure_PreservesCurrentEditor(bool fail)
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        vm.SelectedRows = "unfinished";
        var before = harness.Workspace.Snapshot;
        var previousFeedback = (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility);
        var gateway = new EditingDraftMemoryGateway
        {
            CancelRead = !fail,
            ReadFailure = fail ? new IOException("injected read failure") : null
        };

        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.Equal("unfinished", vm.SelectedRows);
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Same(before.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(before.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Null(vm.EditingDraftFileName);
        Assert.Equal("Previously saved.json", vm.FilmProfileFileName);
        Assert.NotNull(harness.Workspace.LastSavedDocument);
        Assert.False(vm.IsFilmProfileOperationRunning);
        if (fail)
        {
            Assert.True(vm.FilmProfileOperationIsOpen);
            Assert.Equal(InfoBarSeverity.Error, vm.FilmProfileOperationSeverity);
        }
        else
        {
            Assert.Equal(previousFeedback, (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility));
        }
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task EditingDraft_DeclinedDiscardConfirmation_KeepsEditorWithoutOpeningPickerOrPublishingFeedback()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"));
        var vm = harness.ViewModel;
        vm.FilmProfileDiscardConfirmationRequested += (_, request) => request.CompletionSource.TrySetResult(false);
        vm.AttachRuntimeBindings();
        vm.SelectedRows = "unfinished rows";
        await harness.FlushAsync();
        Assert.True(vm.HasUnsavedProfileChanges);
        var before = harness.Workspace.Snapshot;
        var previousFeedback = (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility);
        var gateway = new EditingDraftMemoryGateway();

        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.False(gateway.ReadEntered.Task.IsCompleted);
        Assert.Equal("unfinished rows", vm.SelectedRows);
        Assert.Same(before.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(before.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Equal(previousFeedback, (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility));
        Assert.False(vm.IsFilmProfileOperationRunning);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task EditingDraft_CanceledPendingOpen_PreservesLaterEditsAndExistingErrorFeedback()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        await vm.SaveEditingDraftAsync(new EditingDraftMemoryGateway { WriteFailure = new IOException("existing save failure") });
        var previousFeedback = (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility);
        Assert.Equal(InfoBarSeverity.Error, previousFeedback.FilmProfileOperationSeverity);
        var before = harness.Workspace.Snapshot;
        var gateway = new EditingDraftMemoryGateway { BlockRead = true, CancelRead = true };

        var open = vm.LoadEditingDraftAsync(gateway);
        await gateway.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.SelectedRows = "edited while canceling picker";
        gateway.ReleaseRead.SetResult(null);
        await open;
        await harness.FlushAsync();

        Assert.Equal("edited while canceling picker", vm.SelectedRows);
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Same(before.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Same(before.BaselineDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Equal(previousFeedback, (vm.FilmProfileOperationMessage, vm.FilmProfileOperationSeverity, vm.FilmProfileOperationIsOpen, vm.FilmProfileOperationVisibility));
        Assert.False(vm.IsFilmProfileOperationRunning);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task EditingDraft_PendingSave_KeepsLaterRawEditsVisibleAndDirty()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        vm.SelectedRows = "rows captured for file";
        vm.ExposureMicroseconds = "exposure captured for file";
        var savedDraft = harness.Workspace.Snapshot.CurrentDraft;
        var gateway = new EditingDraftMemoryGateway { BlockWrite = true };

        var save = vm.SaveEditingDraftAsync(gateway);
        await gateway.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.SelectedRows = "rows edited during save";
        vm.ExposureMicroseconds = "exposure edited during save";
        gateway.ReleaseWrite.SetResult(null);
        await save;
        await harness.FlushAsync();

        var persisted = new ScanFilmProfileDraftFileService().Parse(gateway.SavedText).DraftFile!;
        Assert.Equal("rows captured for file", persisted.RawFields[nameof(ScanDebugViewModel.SelectedRows)]);
        Assert.Equal("exposure captured for file", persisted.ChannelRawFields[vm.SelectedCalibrationChannel][nameof(ScanDebugViewModel.ExposureMicroseconds)]);
        Assert.Equal("rows edited during save", vm.SelectedRows);
        Assert.Equal("exposure edited during save", vm.ExposureMicroseconds);
        Assert.Same(savedDraft, harness.Workspace.Snapshot.BaselineDraft);
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Equal("ScanDebug_ProfileStateDraftUnsaved".GetLocalized(), vm.ProfileSaveStateText);
        Assert.NotNull(harness.Workspace.LastSavedDocument);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task EditingDraft_PendingOpen_DoesNotOverwriteRawEditsMadeWhilePickerWasOpen()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var gateway = new EditingDraftMemoryGateway();
        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        gateway.FileName = "Different file.prismdraft";
        gateway.BlockRead = true;
        var originalBaseline = harness.Workspace.Snapshot.BaselineDraft;

        var open = vm.LoadEditingDraftAsync(gateway);
        await gateway.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.ExposureMicroseconds = "new unfinished input during picker";
        vm.SelectedRows = "new unfinished rows during picker";
        gateway.ReleaseRead.SetResult(null);
        await open;
        await harness.FlushAsync();

        Assert.Equal("new unfinished input during picker", vm.ExposureMicroseconds);
        Assert.Equal("new unfinished rows during picker", vm.SelectedRows);
        Assert.Equal("ScanDebug_DraftOpenSuperseded".GetLocalizedOrFallback(
            "Your configuration changed while opening the draft. The current input was kept."), vm.FilmProfileOperationMessage);
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Same(originalBaseline, harness.Workspace.Snapshot.BaselineDraft);
        Assert.Equal("Memory draft.prismdraft", vm.EditingDraftFileName);
        Assert.Equal("Previously saved.json", vm.FilmProfileFileName);
        Assert.NotNull(harness.Workspace.LastSavedDocument);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task EditingDraft_TwoChannels_RoundTripsTheirSeparateUnfinishedInputs()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var firstRole = vm.SelectedCalibrationChannel;
        var secondRole = vm.CalibrationChannelOptions.First(role => !string.Equals(role, firstRole, StringComparison.OrdinalIgnoreCase)
            && harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles.ContainsKey(role));
        vm.ExposureMicroseconds = "first channel unfinished exposure";
        vm.Adc1Gain = string.Empty;
        await harness.SelectChannelAsync(secondRole);
        vm.ExposureMicroseconds = "second channel unfinished exposure";
        vm.Adc1Gain = "not a gain";
        var gateway = new EditingDraftMemoryGateway();

        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        var persisted = new ScanFilmProfileDraftFileService().Parse(gateway.SavedText).DraftFile!;
        Assert.Equal("first channel unfinished exposure", persisted.ChannelRawFields[firstRole][nameof(ScanDebugViewModel.ExposureMicroseconds)]);
        Assert.Equal("second channel unfinished exposure", persisted.ChannelRawFields[secondRole][nameof(ScanDebugViewModel.ExposureMicroseconds)]);
        vm.ExposureMicroseconds = "replaced second exposure";
        await harness.SelectChannelAsync(firstRole);
        vm.ExposureMicroseconds = "replaced first exposure";
        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.Equal(secondRole, vm.SelectedCalibrationChannel);
        Assert.Equal("second channel unfinished exposure", vm.ExposureMicroseconds);
        Assert.Equal("not a gain", vm.Adc1Gain);
        Assert.False(vm.HasUnsavedProfileChanges);
        await harness.SelectChannelAsync(firstRole);
        Assert.Equal("first channel unfinished exposure", vm.ExposureMicroseconds);
        Assert.Equal(string.Empty, vm.Adc1Gain);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.Null(harness.Workspace.LastSavedDocument);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task EditingDraft_CompletedRunnableSave_ReplacesDraftIdentityAndPublishesSavedConfiguration()
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        vm.SelectedRows = "256";
        var gateway = new EditingDraftMemoryGateway();
        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        Assert.Equal(gateway.FileName, vm.EditingDraftFileName);
        Assert.Equal(gateway.FileName, vm.CurrentProfileNameText);
        Assert.True(vm.CanSaveFilmProfileFile);
        Assert.Equal("ScanDebug_ProfileStateDraftSaved".GetLocalized(), vm.ProfileSaveStateText);
        Assert.Equal("ScanDebug_SaveHintReady".GetLocalizedOrFallback(
            "Validation passed. Save a complete configuration for use on the scan page."), vm.FilmProfileSaveHintText);
        var stateNotifications = new List<string?>();
        vm.PropertyChanged += (_, args) => stateNotifications.Add(args.PropertyName);
        var exportVersion = vm.FilmProfileExportVersion;
        harness.FilmProfileFiles.ExportSucceeds = true;
        harness.FilmProfileFiles.ExportFileName = "Completed film.json";

        await vm.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Null(vm.EditingDraftFileName);
        Assert.Equal("Completed film.json", vm.FilmProfileFileName);
        Assert.Equal("Completed film.json", vm.CurrentProfileNameText);
        Assert.Equal("ScanDebug_ProfileStateSaved".GetLocalized(), vm.ProfileSaveStateText);
        Assert.Contains(nameof(ScanDebugViewModel.ProfileSaveStateText), stateNotifications);
        var saved = Assert.IsType<ScanFilmParameterProfileSet>(harness.Workspace.LastSavedDocument);
        Assert.Equal("Completed film", saved.ProfileName);
        Assert.Equal(256, saved.AcquisitionSettings!.Rows);
        Assert.Equal(exportVersion + 1, vm.FilmProfileExportVersion);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.False(harness.Workspace.Snapshot.IsDirty);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(harness.FilmProfileFiles.ExportedProfile!, saved);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Theory]
    [InlineData("123", "456")]
    [InlineData("unfinished black", "65536")]
    public async Task EditingDraft_Open_RestoresUnappliedManualReferenceTextWithoutChangingTypedLevels(string blackText, string whiteText)
    {
        var harness = await CreateEditingDraftHarnessAsync();
        var vm = harness.ViewModel;
        var role = vm.SelectedCalibrationChannel;
        var originalProfile = harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles[role];
        vm.ManualBlackLevelInput = blackText;
        vm.ManualWhiteLevelInput = whiteText;
        var gateway = new EditingDraftMemoryGateway();

        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();

        var persisted = new ScanFilmProfileDraftFileService().Parse(gateway.SavedText).DraftFile!;
        Assert.Equal(blackText, persisted.RawFields["$ManualBlackLevelInput"]);
        Assert.Equal(whiteText, persisted.RawFields["$ManualWhiteLevelInput"]);
        Assert.Equal(originalProfile.BlackLevel, persisted.CurrentDraft.ChannelProfiles[role].BlackLevel);
        Assert.Equal(originalProfile.WhiteLevel, persisted.CurrentDraft.ChannelProfiles[role].WhiteLevel);
        vm.ManualBlackLevelInput = "321";
        vm.ManualWhiteLevelInput = "654";

        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();

        Assert.Equal(blackText, vm.ManualBlackLevelInput);
        Assert.Equal(whiteText, vm.ManualWhiteLevelInput);
        Assert.Equal(originalProfile.BlackLevel, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles[role].BlackLevel);
        Assert.Equal(originalProfile.WhiteLevel, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles[role].WhiteLevel);
        Assert.Null(harness.Workspace.LastSavedDocument);
        Assert.False(vm.HasUnsavedProfileChanges);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    private static async Task<StatusHarness> CreateEditingDraftHarnessAsync()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"),
            deviceSettings: CreateFullV6DeviceSettings());
        harness.Workspace.MarkExported(harness.Workspace.BuildExportDocument().Document.Document!);
        harness.ViewModel.FilmProfileFileName = "Previously saved.json";
        await harness.FlushAsync();
        Assert.False(harness.ViewModel.HasUnsavedProfileChanges);
        return harness;
    }

    private static void AssertEditingDraftDidNotWriteLibrary(StatusHarness harness)
    {
        Assert.Equal(0, harness.Repository.SaveProfileCount);
        Assert.Equal(0, harness.Repository.ReplaceCount);
        Assert.Equal(0, harness.Repository.ClearProfileCount);
        Assert.Empty(harness.Repository.SavedProfileRoles);
        Assert.Empty(harness.Repository.ClearedProfileRoles);
    }

    private sealed class EditingDraftMemoryGateway : IScanFilmProfileFileGateway
    {
        public string FileName { get; set; } = "Memory draft.prismdraft";
        public string? SavedText { get; private set; }
        public string? TextToRead { get; set; }
        public bool CancelWrite { get; init; }
        public bool CancelRead { get; init; }
        public Exception? WriteFailure { get; init; }
        public Exception? ReadFailure { get; init; }
        public bool BlockWrite { get; init; }
        public bool BlockRead { get; set; }
        public TaskCompletionSource<object?> WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?> ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?> ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?> ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ScanFilmProfileFileReadResult> ReadJsonAsync()
            => throw new InvalidOperationException("An editing draft must not use runnable JSON import.");

        public Task<ScanFilmProfileFileWriteResult> WriteJsonAsync(string suggestedFileName, Func<string, string> createText)
            => throw new InvalidOperationException("An editing draft must not use runnable JSON export.");

        public async Task<ScanFilmProfileFileWriteResult> WriteDraftAsync(string suggestedFileName, Func<string, string> createText)
        {
            WriteEntered.TrySetResult(null);
            if (BlockWrite)
                await ReleaseWrite.Task;
            if (WriteFailure is not null)
                throw WriteFailure;
            if (CancelWrite)
                return new(true);
            SavedText = createText(FileName);
            return new(false, FileName);
        }

        public async Task<ScanFilmProfileFileReadResult> ReadDraftAsync()
        {
            ReadEntered.TrySetResult(null);
            if (BlockRead)
                await ReleaseRead.Task;
            if (ReadFailure is not null)
                throw ReadFailure;
            return CancelRead ? new(true, null) : new(false, TextToRead ?? SavedText, FileName);
        }
    }
}
