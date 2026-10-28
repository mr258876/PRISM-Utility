using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileEditingCheckpointTests
{
    [Fact]
    public async Task SaveCompletion_PreservesEditsMadeWhileTheWriteWasPending()
    {
        var repository = new RecordingCalibrationProfileRepository();
        IScanFilmProfileWorkspace workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        var runnable = ReadDocument();
        workspace.MarkExported(runnable);
        var capturedForFile = WithName(workspace.Snapshot.CurrentDraft, "Draft at save time");
        workspace.SetCurrentDraft(capturedForFile);
        Assert.True(workspace.StageImport(runnable with { ProfileName = "Pending import" }).Staged);
        var import = workspace.Snapshot.ImportResult;
        var pendingWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpoint = CompleteSaveAsync();
        var laterEdit = WithName(capturedForFile, "Changed during save");
        workspace.SetCurrentDraft(laterEdit);
        var notifications = new List<ScanFilmProfileWorkspaceSnapshot>();
        workspace.SnapshotChanged += notifications.Add;

        pendingWrite.SetResult();
        await checkpoint;

        Assert.Same(laterEdit, workspace.Snapshot.CurrentDraft);
        Assert.Same(capturedForFile, workspace.Snapshot.BaselineDraft);
        Assert.Same(import, workspace.Snapshot.ImportResult);
        Assert.True(workspace.Snapshot.IsDirty);
        Assert.Equal(runnable.ProfileName, workspace.LastSavedDocument!.ProfileName);
        Assert.Equal(runnable.AcquisitionSettings, workspace.LastSavedDocument.AcquisitionSettings);
        Assert.Same(workspace.Snapshot, Assert.Single(notifications));
        Assert.Empty(repository.CallLog);

        async Task CompleteSaveAsync()
        {
            await pendingWrite.Task;
            workspace.MarkEditingDraftSaved(capturedForFile);
        }
    }

    [Fact]
    public void SavingIncompleteEditingDraft_SetsCleanCheckpointWithoutCreatingRunnableFile()
    {
        var repository = new RecordingCalibrationProfileRepository();
        IScanFilmProfileWorkspace workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        var draft = new ScanFilmProfileDraft(
            string.Empty, DateTimeOffset.UnixEpoch, null, null,
            ScanFilmAcquisitionSettings.CreateDefault() with { Rows = 0 }, null);
        workspace.SetCurrentDraft(draft);
        Assert.True(workspace.Snapshot.IsDirty);

        workspace.MarkEditingDraftSaved(draft);

        Assert.Same(draft, workspace.Snapshot.CurrentDraft);
        Assert.Same(draft, workspace.Snapshot.BaselineDraft);
        Assert.False(workspace.Snapshot.IsDirty);
        Assert.Null(workspace.LastSavedDocument);
        Assert.False(workspace.BuildExportDocument().Document.CanApply);
        Assert.Empty(repository.CallLog);
    }

    [Fact]
    public void OpeningEditingDraft_ClearsPreviousRunnableFileAndStagedImport()
    {
        var repository = new RecordingCalibrationProfileRepository();
        IScanFilmProfileWorkspace workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        var runnable = ReadDocument();
        workspace.MarkExported(runnable);
        Assert.True(workspace.StageImport(runnable with { ProfileName = "Staged runnable file" }).Staged);
        var draft = WithName(ScanFilmProfileDraft.FromDocument(runnable).Draft, "Editing file only");
        var notifications = new List<ScanFilmProfileWorkspaceSnapshot>();
        workspace.SnapshotChanged += notifications.Add;

        workspace.RestoreEditingDraft(draft);

        Assert.Same(draft, workspace.Snapshot.CurrentDraft);
        Assert.Same(draft, workspace.Snapshot.BaselineDraft);
        Assert.False(workspace.Snapshot.IsDirty);
        Assert.Same(ScanFilmProfileImportResult.None, workspace.Snapshot.ImportResult);
        Assert.Null(workspace.Snapshot.StagedImport);
        Assert.Null(workspace.LastSavedDocument);
        Assert.True(workspace.BuildExportDocument().Document.CanApply);
        Assert.Same(workspace.Snapshot, Assert.Single(notifications));
        Assert.Empty(repository.CallLog);
    }

    [Fact]
    public void OpeningIncompleteEditingDraft_DoesNotRequireRunnableValidation()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        workspace.MarkExported(ReadDocument());
        var draft = ScanFilmProfileDraft.CreateDefault();

        workspace.RestoreEditingDraft(draft);

        Assert.Same(draft, workspace.Snapshot.CurrentDraft);
        Assert.Same(draft, workspace.Snapshot.BaselineDraft);
        Assert.False(workspace.Snapshot.IsDirty);
        Assert.Null(workspace.LastSavedDocument);
        Assert.False(workspace.BuildExportDocument().Document.CanApply);
        Assert.Empty(repository.CallLog);
    }

    [Fact]
    public void NullCheckpointArguments_DoNotChangeEditingOrRunnableState()
    {
        var workspace = new ScanFilmProfileWorkspace(new RecordingCalibrationProfileRepository(), new ScanFilmProfileDocumentService());
        workspace.MarkExported(ReadDocument());
        var before = workspace.Snapshot;
        var savedName = workspace.LastSavedDocument!.ProfileName;

        Assert.Throws<ArgumentNullException>(() => workspace.MarkEditingDraftSaved(null!));
        Assert.Throws<ArgumentNullException>(() => workspace.RestoreEditingDraft(null!));

        Assert.Same(before, workspace.Snapshot);
        Assert.Equal(savedName, workspace.LastSavedDocument!.ProfileName);
    }

    private static ScanFilmProfileDraft WithName(ScanFilmProfileDraft draft, string name)
        => new(name, draft.SavedAtUtc, draft.ChannelProfiles, draft.SelectedCalibrationChannel, draft.AcquisitionSettings, draft.ScanRecipeSettings);

    private static ScanFilmParameterProfileSet ReadDocument()
        => new ScanFilmProfileDocumentService().Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", "full-v6.json"))).Document!;
}
