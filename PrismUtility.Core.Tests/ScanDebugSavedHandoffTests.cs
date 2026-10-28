using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Fact]
    public async Task ScanHandoff_CleanOpenedConfigurationUsesSavedContentWithoutOpeningSavePicker()
    {
        var harness = await CreateOpenedHandoffHarnessAsync();
        var saved = harness.Workspace.LastSavedDocument!;

        Assert.True(await harness.ViewModel.EnsureSavedFilmProfileForScanAsync());

        Assert.Null(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(0, harness.ViewModel.FilmProfileExportVersion);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(saved, harness.Workspace.LastSavedDocument!);
        Assert.True(harness.Workspace.Snapshot.CurrentDraft.HasSameContentAs(ScanFilmProfileDraft.FromDocument(saved).Draft));
    }

    [Fact]
    public async Task ScanHandoff_ChangedConfigurationSavesCurrentContentBeforeHandoff()
    {
        var harness = await CreateOpenedHandoffHarnessAsync();
        harness.ViewModel.SelectedRows = "1024";
        harness.FilmProfileFiles.ExportSucceeds = true;
        Assert.True(harness.ViewModel.HasUnsavedProfileChanges);

        Assert.True(await harness.ViewModel.EnsureSavedFilmProfileForScanAsync());

        Assert.NotNull(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(1, harness.ViewModel.FilmProfileExportVersion);
        Assert.Equal(1024, harness.Workspace.LastSavedDocument!.AcquisitionSettings!.Rows);
        Assert.False(harness.ViewModel.HasUnsavedProfileChanges);
    }

    [Fact]
    public async Task ScanHandoff_CanceledSaveDoesNotUseTheEarlierSavedConfiguration()
    {
        var harness = await CreateOpenedHandoffHarnessAsync();
        var saved = harness.Workspace.LastSavedDocument!;
        harness.ViewModel.SelectedRows = "1024";

        Assert.False(await harness.ViewModel.EnsureSavedFilmProfileForScanAsync());

        Assert.NotNull(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(0, harness.ViewModel.FilmProfileExportVersion);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(saved, harness.Workspace.LastSavedDocument!);
        Assert.True(harness.ViewModel.HasUnsavedProfileChanges);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScanHandoff_InvalidOrBusyConfigurationCannotBypassTheExistingGate(bool invalid)
    {
        var harness = await CreateOpenedHandoffHarnessAsync();
        if (invalid)
            harness.ViewModel.SelectedRows = "unfinished";
        else
            harness.ViewModel.IsFilmProfileOperationRunning = true;

        Assert.False(await harness.ViewModel.EnsureSavedFilmProfileForScanAsync());

        Assert.Null(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(0, harness.ViewModel.FilmProfileExportVersion);
    }

    [Fact]
    public async Task ScanHandoff_CleanEditingDraftStillRequiresACompleteSavedConfiguration()
    {
        var harness = await CreateOpenedHandoffHarnessAsync();
        harness.Workspace.RestoreEditingDraft(harness.Workspace.Snapshot.CurrentDraft);
        await harness.FlushAsync();
        harness.FilmProfileFiles.ExportSucceeds = true;
        Assert.False(harness.Workspace.Snapshot.IsDirty);
        Assert.Null(harness.Workspace.LastSavedDocument);

        Assert.True(await harness.ViewModel.EnsureSavedFilmProfileForScanAsync());

        Assert.NotNull(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(1, harness.ViewModel.FilmProfileExportVersion);
    }

    [Fact]
    public async Task ScanHandoff_CleanDraftCheckpointCannotReuseDifferentSavedContent()
    {
        var harness = await CreateOpenedHandoffHarnessAsync();
        harness.ViewModel.SelectedRows = "1024";
        harness.Workspace.MarkEditingDraftSaved(harness.Workspace.Snapshot.CurrentDraft);
        await harness.FlushAsync();
        harness.FilmProfileFiles.ExportSucceeds = true;
        Assert.False(harness.Workspace.Snapshot.IsDirty);

        Assert.True(await harness.ViewModel.EnsureSavedFilmProfileForScanAsync());

        Assert.NotNull(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(1, harness.ViewModel.FilmProfileExportVersion);
        Assert.Equal(1024, harness.Workspace.LastSavedDocument!.AcquisitionSettings!.Rows);
    }

    private static async Task<StatusHarness> CreateOpenedHandoffHarnessAsync()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"),
            deviceSettings: CreateFullV6DeviceSettings());
        var document = new ScanFilmProfileDocumentService().Build(CreateFixtureDraft("full-v6.json")).Document!;
        harness.FilmProfileFiles.ImportResult = new(false, document, null, "walkthrough-complete.json");
        await harness.ViewModel.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        await harness.ViewModel.ApplyStagedFilmProfileImportCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        Assert.True(harness.ViewModel.CanSaveFilmProfileFile);
        Assert.False(harness.ViewModel.HasUnsavedProfileChanges);
        return harness;
    }
}
