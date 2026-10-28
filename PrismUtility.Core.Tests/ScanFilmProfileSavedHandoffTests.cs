using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileSavedHandoffTests
{
    [Fact]
    public async Task HydratedChannelLibraryAndUnwrittenExportAreNotSavedConfigurations()
    {
        var document = ReadDocument();
        var repository = new RecordingCalibrationProfileRepository
        {
            PersistedSnapshot = new(document.ChannelProfiles, document.SelectedCalibrationChannel)
        };
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());

        await workspace.InitializeAsync(CancellationToken.None);
        Assert.False(workspace.Snapshot.IsDirty);
        Assert.True(workspace.BuildExportDocument().Document.CanApply);
        Assert.Null(workspace.LastSavedDocument);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public void SuccessfullySavedConfigurationIsIsolatedFromUnsavedEditsAndCallerMutation()
    {
        var workspace = CreateWorkspace();
        var document = ReadDocument();
        workspace.MarkExported(document);

        var savedChannelCount = document.ChannelProfiles.Count;
        document.ChannelProfiles.Clear();
        var saved = Assert.IsType<ScanFilmParameterProfileSet>(workspace.LastSavedDocument);
        Assert.Equal(savedChannelCount, saved.ChannelProfiles.Count);

        workspace.SetCurrentDraft(new ScanFilmProfileDraft(
            "Unsaved rename", saved.SavedAtUtc, saved.ChannelProfiles, saved.SelectedCalibrationChannel,
            saved.AcquisitionSettings! with { Rows = 512 }, saved.ScanRecipeSettings));
        saved.ChannelProfiles.Clear();

        var handoff = Assert.IsType<ScanFilmParameterProfileSet>(workspace.LastSavedDocument);
        Assert.NotEqual("Unsaved rename", handoff.ProfileName);
        Assert.NotEqual(512, handoff.AcquisitionSettings!.Rows);
        Assert.Equal(savedChannelCount, handoff.ChannelProfiles.Count);
        Assert.True(workspace.Snapshot.IsDirty);
    }

    [Fact]
    public async Task OpenedFileBecomesAvailableOnlyAfterApplyAndCanceledImportPreservesPreviousSave()
    {
        var workspace = CreateWorkspace();
        var previous = ReadDocument() with { ProfileName = "Previously saved" };
        workspace.MarkExported(previous);
        Assert.True(workspace.StageImport(ReadDocument() with { ProfileName = "Opened file" }).Staged);
        Assert.Equal("Previously saved", workspace.LastSavedDocument!.ProfileName);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(ScanFilmProfileApplyStatus.Canceled, (await workspace.ApplyStagedImportAsync(canceled.Token)).Status);
        Assert.Equal("Previously saved", workspace.LastSavedDocument!.ProfileName);

        Assert.Equal(ScanFilmProfileApplyStatus.Applied, (await workspace.ApplyStagedImportAsync(CancellationToken.None)).Status);
        Assert.Equal("Opened file", workspace.LastSavedDocument!.ProfileName);
        workspace.ResetToDefaultDraft();
        Assert.Null(workspace.LastSavedDocument);
    }

    [Fact]
    public void StructurallyValidConfigurationCanBeSavedBeforeReferenceLevelCalibration()
    {
        var document = ReadDocument();
        var profiles = document.ChannelProfiles.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with { BlackLevel = null, WhiteLevel = null },
            StringComparer.OrdinalIgnoreCase);
        var workspace = CreateWorkspace();
        workspace.SetCurrentDraft(ScanFilmProfileDraft.FromDocument(document with { ChannelProfiles = profiles }).Draft);

        var export = workspace.BuildExportDocument();

        Assert.True(export.Document.CanApply);
        workspace.MarkExported(export.Document.Document!);
        Assert.All(workspace.LastSavedDocument!.ChannelProfiles.Values, profile =>
        {
            Assert.Null(profile.BlackLevel);
            Assert.Null(profile.WhiteLevel);
        });
    }

    private static ScanFilmProfileWorkspace CreateWorkspace()
        => new(new RecordingCalibrationProfileRepository(), new ScanFilmProfileDocumentService());

    private static ScanFilmParameterProfileSet ReadDocument()
        => new ScanFilmProfileDocumentService().Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", "full-v6.json"))).Document!;
}
