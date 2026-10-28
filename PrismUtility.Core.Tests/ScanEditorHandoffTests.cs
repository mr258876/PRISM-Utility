using System.Reflection;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Fact]
    public async Task ScanEditorEntry_UnfinishedRawInputsReturnAsExistingDraftWithoutChangingWorkspace()
    {
        AppContext.SetSwitch("PRISM.Utility.UseResourceFallbacks", true);
        var editor = await StatusHarness.CreateAsync(EmptyProfiles, null, attachRuntimeBindings: true);
        editor.Workspace.ResetToDefaultDraft();
        await editor.FlushAsync();
        var manager = DispatchProxy.Create<IScannerDeviceSessionManager, ScanSavedProfileHandoffBehaviorTests.OfflineSessionManager>();
        var usb = new UsbUsageCoordinator();
        var scan = new ScanViewModel(manager, new ScanWorkflowSessionCoordinator(usb, manager),
            null!, new RecordingTransferSettingsService(), null!, new RecordingDebugOutputMirrorService(), null!,
            new StatusDeviceSettings(), new RecordingColorManagementSettingsService(), editor.Repository,
            editor.FilmProfileFiles, editor.Workspace, usb, new RecordingUiDispatcher(autoDrain: true));
        try
        {
            Assert.Equal("New configuration", scan.FilmProfileEditorActionText);
            scan.Deactivate();
            var original = editor.Workspace.Snapshot;

            editor.ViewModel.SelectedRows = "unfinished rows";
            editor.ViewModel.ExposureMicroseconds = "unfinished exposure";
            await editor.FlushAsync();

            Assert.True(editor.ViewModel.HasUnsavedProfileChanges);
            Assert.Same(original, editor.Workspace.Snapshot);
            Assert.True(editor.Workspace.Snapshot.CurrentDraft.HasSameContentAs(ScanFilmProfileDraft.CreateDefault()));
            var changes = new List<string?>();
            scan.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
            scan.UpdateFilmProfileEditorSession(editor.ViewModel.HasUnsavedProfileChanges
                || !string.IsNullOrWhiteSpace(editor.ViewModel.EditingDraftFileName));
            scan.Activate();

            Assert.Equal("Continue editing configuration", scan.FilmProfileEditorActionText);
            Assert.Contains(nameof(scan.FilmProfileEditorActionText), changes);
            Assert.Equal("unfinished rows", editor.ViewModel.SelectedRows);
            Assert.Equal("unfinished exposure", editor.ViewModel.ExposureMicroseconds);
            Assert.Same(original, editor.Workspace.Snapshot);
        }
        finally
        {
            await scan.CleanupAsync();
            await editor.ViewModel.CleanupAsync();
        }
    }

    [Fact]
    public async Task ScanEditorHandoff_OpensLoadedDocumentWithWhiteSourceAndFilename()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        harness.ViewModel.AttachRuntimeBindings();
        await harness.FlushAsync();
        var document = CreateScanHandoffDocument();
        harness.ViewModel.FilmProfileDiscardConfirmationRequested += (_, request) => request.CompletionSource.TrySetResult(true);

        Assert.True(await harness.ViewModel.EditLoadedScanProfileAsync(new(document, "loaded-rgb.json")));
        await harness.FlushAsync();

        Assert.Equal("loaded-rgb.json", harness.ViewModel.CurrentProfileNameText);
        Assert.Equal(document.AcquisitionSettings!.WhiteChannelSettings, harness.Workspace.Snapshot.CurrentDraft.WhiteChannelSettings);
        Assert.Equal(WhiteLightSource.RgbComposite, harness.ViewModel.SelectedWhiteLightSource);
        Assert.Equal(document.ChannelProfiles.Keys.Order(), harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles.Keys.Order());
        Assert.False(harness.Workspace.Snapshot.IsDirty);
        Assert.Equal(0, harness.Repository.ReplaceCount);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task ScanEditorHandoff_CancelKeepsDifferentUnsavedDraftAndRawInput()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true));
        harness.ViewModel.AttachRuntimeBindings();
        await harness.FlushAsync();
        harness.ViewModel.ExposureMicroseconds = "unfinished";
        var original = harness.Workspace.Snapshot;
        var confirmationCount = 0;
        harness.ViewModel.FilmProfileDiscardConfirmationRequested += (_, request) =>
        {
            confirmationCount++;
            request.CompletionSource.TrySetResult(false);
        };

        Assert.False(await harness.ViewModel.EditLoadedScanProfileAsync(new(CreateScanHandoffDocument(), "loaded-rgb.json")));
        await harness.FlushAsync();

        Assert.Equal(1, confirmationCount);
        Assert.Same(original, harness.Workspace.Snapshot);
        Assert.Equal("unfinished", harness.ViewModel.ExposureMicroseconds);
        Assert.Equal(WhiteLightSource.DedicatedLed, harness.ViewModel.SelectedWhiteLightSource);
    }

    [Fact]
    public async Task ScanEditorHandoff_ReturningToSameFilePreservesItsUnsavedWhiteInput()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        harness.ViewModel.AttachRuntimeBindings();
        await harness.FlushAsync();
        var request = new ScanFilmProfileEditRequest(CreateScanHandoffDocument(), "loaded-rgb.json");
        harness.ViewModel.FilmProfileDiscardConfirmationRequested += (_, confirmation) => confirmation.CompletionSource.TrySetResult(true);
        Assert.True(await harness.ViewModel.EditLoadedScanProfileAsync(request));
        await harness.FlushAsync();
        harness.ViewModel.WhiteLightComponents[0].Level = "unfinished";
        var original = harness.Workspace.Snapshot;
        var confirmations = 0;
        harness.ViewModel.FilmProfileDiscardConfirmationRequested += (_, _) => confirmations++;

        Assert.True(await harness.ViewModel.EditLoadedScanProfileAsync(request));
        await harness.FlushAsync();

        Assert.Equal(0, confirmations);
        Assert.Same(original, harness.Workspace.Snapshot);
        Assert.Equal("unfinished", harness.ViewModel.WhiteLightComponents[0].Level);
    }

    [Fact]
    public async Task ScanEditorHandoff_ChangesWhileConfirmationIsOpenAreNotOverwritten()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true));
        harness.ViewModel.AttachRuntimeBindings();
        await harness.FlushAsync();
        harness.ViewModel.ExposureMicroseconds = "first edit";
        ScanFilmProfileDiscardConfirmationRequest? pending = null;
        harness.ViewModel.FilmProfileDiscardConfirmationRequested += (_, confirmation) => pending = confirmation;
        var handoff = harness.ViewModel.EditLoadedScanProfileAsync(new(CreateScanHandoffDocument(), "loaded-rgb.json"));
        Assert.NotNull(pending);
        harness.ViewModel.ExposureMicroseconds = "newer edit";
        pending!.CompletionSource.TrySetResult(true);

        Assert.False(await handoff);
        Assert.Equal("newer edit", harness.ViewModel.ExposureMicroseconds);
        Assert.Equal(WhiteLightSource.DedicatedLed, harness.ViewModel.SelectedWhiteLightSource);
    }

    private static ScanFilmParameterProfileSet CreateScanHandoffDocument()
    {
        var source = WhiteTestDraft(includeComposite: true).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var white = source.ChannelProfiles["White"];
        var profiles = new[] { "Red", "Green", "Blue", "White" }.ToDictionary(role => role, _ => white);
        var draft = new ScanFilmProfileDraft("Loaded RGB white", DateTimeOffset.UnixEpoch, profiles, "White",
            source.AcquisitionSettings, source.ScanRecipeSettings);
        var built = new ScanFilmProfileDocumentService().Build(draft);
        Assert.NotNull(built.Document);
        Assert.True(built.Validation.IsValid);
        return built.Document!;
    }
}
