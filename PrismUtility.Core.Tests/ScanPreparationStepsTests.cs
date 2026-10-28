using System.Reflection;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    private static Harness CreatePreparationHarness()
    {
        var harness = CreateHarness();
        // A complete configuration also needs matching real device wiring.
        harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
        {
            Channel1Role = "IR", Channel2Role = "Blue", Channel3Role = "White", Channel4Role = "Red"
        }).GetAwaiter().GetResult();
        // The serialization fixture has only IR and Red calibration profiles.
        // Readiness tests need a complete saved configuration for every active role.
        var profiles = harness.Document.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var role in harness.Document.ScanRecipeSettings!.ChannelAssignment!.Roles)
            profiles.TryAdd(role, harness.Document.ChannelProfiles["IR"]);
        var completeDocument = harness.Document with { ChannelProfiles = profiles };
        harness.Workspace.MarkExported(completeDocument);
        return harness with { Document = completeDocument };
    }

    [Fact]
    public async Task PreparationSteps_FollowsSavedConfigurationAvailabilityWithoutReloadingPage()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            harness.Workspace.ResetToDefaultDraft();
            var configuration = Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration);
            Assert.Equal(ScanPreparationAction.LoadConfiguration, configuration.Action);
            Assert.False(configuration.IsStatusOnly);
            Assert.True(configuration.IsActionVisible);
            Assert.True(configuration.IsEnabled);
            Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Device).IsActionVisible);

            harness.Workspace.MarkExported(harness.Document);
            configuration = Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration);
            Assert.Equal(ScanPreparationAction.UseSavedConfiguration, configuration.Action);
            Assert.False(configuration.IsStatusOnly);
            Assert.True(configuration.IsActionVisible);
            Assert.True(configuration.IsEnabled);

            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            Assert.DoesNotContain(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration);
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task ConfigurationEditorEntry_DistinguishesNewDraftExistingDraftAndLoadedConfiguration()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            harness.Workspace.ResetToDefaultDraft();
            Assert.Equal("New configuration", vm.FilmProfileEditorActionText);
            var changes = new List<string?>();
            vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            var draft = PRISM_Utility.Core.Models.ScanFilmProfileDraft.FromDocument(harness.Document).Draft;
            harness.Workspace.RestoreEditingDraft(draft);
            harness.Workspace.MarkEditingDraftSaved(draft);
            Assert.False(vm.IsConfigProfileLoaded);
            Assert.False(harness.Workspace.Snapshot.IsDirty);
            Assert.Equal("Continue editing configuration", vm.FilmProfileEditorActionText);
            Assert.Contains(nameof(vm.FilmProfileEditorActionText), changes);

            harness.Workspace.MarkExported(harness.Document);
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            Assert.True(vm.IsConfigProfileLoaded);
            Assert.Equal("Edit configuration", vm.FilmProfileEditorActionText);
            Assert.Equal(draft.ChannelProfiles["IR"].Parameters,
                harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["IR"].Parameters);
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_MissingCalibrationOffersReplacementInsteadOfOpeningUnrelatedEditor()
    {
        var harness = CreateHarness();
        await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
        {
            Channel1Role = "IR", Channel2Role = "Blue", Channel3Role = "White", Channel4Role = "Red"
        });
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);

            Assert.True(vm.IsConfigProfileLoaded);
            Assert.True(vm.NeedsConfigProfile);
            var step = Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration);
            Assert.Equal(ScanPreparationAction.ReplaceConfiguration, step.Action);
            Assert.True(step.IsActionVisible);
            Assert.True(step.IsEnabled);
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_UnmappedLoadedChannelOffersDeviceMappingsInsteadOfReplacingTheConfiguration()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with { Channel1Role = "Green" });
            await vm.RefreshDeviceSettingsBindingsAsync();

            var step = Assert.Single(vm.PreparationSteps, step => step.Action == ScanPreparationAction.ReviewDeviceMappings);
            Assert.True(step.IsActionVisible);
            Assert.True(step.IsEnabled);
            Assert.DoesNotContain(vm.PreparationSteps, step => step.Action == ScanPreparationAction.ReplaceConfiguration);
            Assert.False(vm.StartScanCommand.CanExecute(null));
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_LoadingSavedConfigurationRemovesOnlyConfigurationRequirement()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            Assert.Equal(2, vm.PreparationSteps.Count);
            Assert.Contains(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Device);
            Assert.Contains(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration);
            Assert.All(vm.PreparationSteps, step => Assert.True(step.IsEnabled));
            var changes = new List<string?>();
            vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);

            Assert.True(vm.IsConfigProfileLoaded);
            Assert.Equal(ScanWorkflowBlockerIds.Device, Assert.Single(vm.PreparationSteps).CardId);
            Assert.Contains(nameof(vm.PreparationSteps), changes);
            Assert.False(vm.StartScanCommand.CanExecute(null));
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_InvalidRowsAddExecutionRequirementUntilCorrected()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            var validRows = vm.SelectedRows;

            vm.SelectedRows = "unfinished rows";

            Assert.Equal(2, vm.PreparationSteps.Count);
            Assert.Contains(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Device);
            var execution = Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution);
            Assert.True(execution.IsEnabled);
            Assert.False(vm.StartScanCommand.CanExecute(null));

            vm.SelectedRows = validRows;

            Assert.Equal(ScanWorkflowBlockerIds.Device, Assert.Single(vm.PreparationSteps).CardId);
            Assert.True(vm.IsConfigProfileLoaded);
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationSteps_ConnectingOrExportingDisablesSetupActionsWithoutHidingRequirements(bool outputBusy)
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            vm.SelectedRows = "unfinished rows";
            vm.IsConnecting = !outputBusy;
            vm.IsOutputOperationRunning = outputBusy;

            Assert.False(vm.IsSetupEditingEnabled);
            Assert.False(vm.UseSavedFilmProfileCommand.CanExecute(null));
            Assert.Equal(outputBusy ? 4 : 3, vm.PreparationSteps.Count);
            Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Device).IsEnabled);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration).IsEnabled);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution).IsEnabled);
            if (outputBusy)
                Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Output).IsEnabled);

            vm.IsConnecting = false;
            vm.IsOutputOperationRunning = false;

            Assert.True(vm.IsSetupEditingEnabled);
            Assert.True(vm.UseSavedFilmProfileCommand.CanExecute(null));
            Assert.Equal(3, vm.PreparationSteps.Count);
            Assert.DoesNotContain(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Output);
            Assert.All(vm.PreparationSteps, step => Assert.True(step.IsEnabled));
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_ReturningToPagePreservesLoadedConfigurationInputsAndUnresolvedRequirements()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            var profileName = vm.SelectedConfigProfileName;
            vm.SelectedRows = "unfinished rows";
            vm.SelectedStartingDirection = "Forward";
            vm.IsWarmUpEnabled = false;

            vm.Deactivate();
            vm.Activate();
            await vm.RefreshDeviceSettingsBindingsAsync();

            Assert.True(vm.IsConfigProfileLoaded);
            Assert.Equal(profileName, vm.SelectedConfigProfileName);
            Assert.Equal("unfinished rows", vm.SelectedRows);
            Assert.Equal("Forward", vm.SelectedStartingDirection);
            Assert.False(vm.IsWarmUpEnabled);
            Assert.Equal(2, vm.PreparationSteps.Count);
            Assert.Contains(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Device);
            Assert.Contains(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution);
            Assert.All(vm.PreparationSteps, step => Assert.True(step.IsEnabled));
            Assert.False(vm.StartScanCommand.CanExecute(null));
        }
        finally
        {
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_SuccessfulLoadImmediatelyEnablesRemainingExecutionRequirement()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? load = null;
        try
        {
            // The saved fixture's fractional-microsecond interval still needs user
            // correction when there is no explicit line pitch to determine motion.
            harness.Workspace.MarkExported(harness.Document with
            {
                AcquisitionSettings = harness.Document.AcquisitionSettings! with { TargetLinePitchMicrometers = null }
            });
            Assert.NotNull(harness.Workspace.LastSavedDocument);
            var parameters = harness.Document.ChannelProfiles["IR"].Parameters;
            typeof(ScanViewModel).GetField("_loadedSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, parameters);
            typeof(ScanViewModel).GetField("_loadedSysClockKhz", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, parameters.SysClockKhz);
            typeof(ScanViewModel).GetField("_loadedExposureTicks", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, parameters.ExposureTicks);
            vm.SelectedRows = "unfinished rows";
            harness.ColorSettings.NextInitialization = release.Task;

            load = vm.UseSavedFilmProfileCommand.ExecuteAsync(null);

            Assert.False(load.IsCompleted);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration).IsEnabled);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution).IsEnabled);

            release.SetResult();
            await load;

            Assert.True(vm.IsConfigProfileLoaded);
            Assert.True(vm.IsSetupEditingEnabled);
            Assert.DoesNotContain(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration);
            Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution).IsEnabled);
            Assert.False(vm.StartScanCommand.CanExecute(null));
        }
        finally
        {
            release.TrySetResult();
            if (load is not null)
                await load;
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_CancelingLoadImmediatelyRestoresUnresolvedConfigurationActions()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? load = null;
        try
        {
            vm.SelectedRows = "unfinished rows";
            harness.ColorSettings.NextInitialization = release.Task;

            load = vm.UseSavedFilmProfileCommand.ExecuteAsync(null);

            Assert.False(load.IsCompleted);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration).IsEnabled);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution).IsEnabled);

            vm.Deactivate();
            release.SetResult();
            await load;

            Assert.False(vm.IsConfigProfileLoaded);
            Assert.Equal(0, harness.Repository.ReplaceCount);
            Assert.True(vm.IsSetupEditingEnabled);
            Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration).IsEnabled);
            Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution).IsEnabled);
        }
        finally
        {
            release.TrySetResult();
            if (load is not null)
                await load;
            await vm.CleanupAsync();
        }
    }

    [Fact]
    public async Task PreparationSteps_FailedLoadImmediatelyRestoresUnresolvedConfigurationActions()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? load = null;
        try
        {
            vm.SelectedRows = "unfinished rows";
            harness.ColorSettings.NextInitialization = release.Task;

            load = vm.UseSavedFilmProfileCommand.ExecuteAsync(null);

            Assert.False(load.IsCompleted);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration).IsEnabled);
            Assert.False(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution).IsEnabled);

            release.SetException(new InvalidOperationException("Color settings are unavailable."));
            await load;

            Assert.False(vm.IsConfigProfileLoaded);
            Assert.Equal(0, harness.Repository.ReplaceCount);
            Assert.True(vm.IsSetupEditingEnabled);
            Assert.True(vm.UseSavedFilmProfileCommand.CanExecute(null));
            Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Configuration).IsEnabled);
            Assert.True(Assert.Single(vm.PreparationSteps, step => step.CardId == ScanWorkflowBlockerIds.Execution).IsEnabled);
        }
        finally
        {
            release.TrySetResult();
            if (load is not null)
                await load;
            await vm.CleanupAsync();
        }
    }
}
