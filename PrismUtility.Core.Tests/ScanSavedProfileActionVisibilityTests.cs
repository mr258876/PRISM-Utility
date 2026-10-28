using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    [Fact]
    public async Task SavedProfileActionDisappearsAfterSuccessfulLoadAndEquivalentDocumentRebuild()
    {
        var harness = CreateHarness();
        var changes = new List<string?>();
        harness.ViewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.True(harness.ViewModel.ShowSavedFilmProfileAction);

        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.ShowSavedFilmProfileAction);
        Assert.Contains(nameof(harness.ViewModel.ShowSavedFilmProfileAction), changes);
        harness.Workspace.MarkExported(harness.Document with
        {
            ChannelProfiles = new Dictionary<string, ScanChannelCalibrationProfile>(harness.Document.ChannelProfiles)
        });
        Assert.False(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task SameNameAndTimestampWithChangedAcquisitionStillOffersSavedProfile()
    {
        var harness = CreateHarness();
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);

        harness.Workspace.MarkExported(harness.Document with
        {
            AcquisitionSettings = harness.Document.AcquisitionSettings! with { Rows = 1_024 }
        });

        Assert.True(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        Assert.Equal("1024", harness.ViewModel.SelectedRows);
        Assert.False(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task SameNameAndTimestampWithChangedChannelCalibrationStillOffersSavedProfile()
    {
        var harness = CreateHarness();
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        var channels = new Dictionary<string, ScanChannelCalibrationProfile>(harness.Document.ChannelProfiles);
        var first = channels.First();
        channels[first.Key] = first.Value with { BlackLevel = (ushort)((first.Value.BlackLevel ?? 0) + 1) };

        harness.Workspace.MarkExported(harness.Document with { ChannelProfiles = channels });

        Assert.True(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task NewSavedRevisionWithSameNameRemainsAvailable()
    {
        var harness = CreateHarness();
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);

        harness.Workspace.MarkExported(harness.Document with { SavedAtUtc = harness.Document.SavedAtUtc.AddMinutes(1) });

        Assert.True(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task UnsavedEditorChangesDoNotReofferAnAlreadyLoadedSavedProfile()
    {
        var harness = CreateHarness();
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        var edited = harness.Document with { AcquisitionSettings = harness.Document.AcquisitionSettings! with { Rows = 1_024 } };

        harness.Workspace.SetCurrentDraft(ScanFilmProfileDraft.FromDocument(edited).Draft);

        Assert.True(harness.Workspace.Snapshot.IsDirty);
        Assert.False(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task FailedHandoffDoesNotHideTheSavedProfileAction()
    {
        var harness = CreateHarness();
        harness.ColorSettings.NextInitialization = Task.FromException(new IOException("settings unavailable"));

        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsConfigProfileLoaded);
        Assert.True(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task SaveArrivingDuringHandoffIsNotMarkedAsAlreadyLoaded()
    {
        var harness = CreateHarness();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.ColorSettings.NextInitialization = release.Task;
        var load = harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        harness.Workspace.MarkExported(harness.Document with
        {
            AcquisitionSettings = harness.Document.AcquisitionSettings! with { Rows = 1_024 }
        });

        release.SetResult();
        await load;

        Assert.Equal(harness.Document.AcquisitionSettings!.Rows.ToString(), harness.ViewModel.SelectedRows);
        Assert.True(harness.ViewModel.ShowSavedFilmProfileAction);
        await harness.ViewModel.CleanupAsync();
    }
}
