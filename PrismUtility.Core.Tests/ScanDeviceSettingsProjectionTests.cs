using System.Reflection;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Fact]
    public async Task DeviceSettingsMapping_BlankWorkbenchReadsLatestMappingOnReentry()
    {
        var settings = new StatusDeviceSettings();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, attachRuntimeBindings: true, deviceSettings: settings);
        var originalDraft = harness.Workspace.Snapshot.CurrentDraft;
        await settings.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, true, true),
            DefaultScanMotorId = 0,
            Channel1Role = "Red", Channel2Role = "Green", Channel3Role = "Blue", Channel4Role = "White"
        });

        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();

        Assert.Equal("Motor1", harness.ViewModel.SelectedScanMotor);
        Assert.Equal("Motor2", harness.ViewModel.FocusMappingLeftMotor);
        Assert.Equal("Motor3", harness.ViewModel.FocusMappingRightMotor);
        Assert.Equal("Dir1", harness.ViewModel.FocusMappingZPositiveDirection);
        Assert.Equal(new[] { "Red", "Green", "Blue", "White" }, harness.ViewModel.AcquisitionChannels.OrderBy(channel => channel.LedIndex).Select(channel => channel.Role));
        Assert.Equal(originalDraft.AcquisitionSettings, harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings);
        Assert.Equal(originalDraft.ScanRecipeSettings, harness.Workspace.Snapshot.CurrentDraft.ScanRecipeSettings);
        Assert.False(harness.Workspace.Snapshot.IsDirty);
    }

    [Fact]
    public async Task DeviceSettingsMapping_InitialDeviceDefaultsSurviveBlankWorkspaceProjection()
    {
        var settings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2), DefaultScanMotorId = 0
        });
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, attachRuntimeBindings: true, deviceSettings: settings);

        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();

        Assert.Equal("Motor1", harness.ViewModel.SelectedScanMotor);
        Assert.False(harness.Workspace.Snapshot.IsDirty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeviceSettingsMapping_LoadedWorkbenchUsesCurrentPhysicalMappingAndPreservesFilmDraft(bool legacyWithoutRecipe)
    {
        var document = new ScanFilmProfileDocumentService().Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", "full-v6.json"))).Document!;
        if (legacyWithoutRecipe)
            document = document with { ScanRecipeSettings = null };
        var draft = ScanFilmProfileDraft.FromDocument(document).Draft;
        var settings = new StatusDeviceSettings();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true, deviceSettings: settings);
        var originalDraft = harness.Workspace.Snapshot.CurrentDraft;
        await settings.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, true, false), DefaultScanMotorId = 0,
            Channel1Role = "Red", Channel2Role = "Green", Channel3Role = "Blue", Channel4Role = "White"
        });

        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();

        Assert.Equal("Motor1", harness.ViewModel.SelectedScanMotor);
        Assert.Equal("Motor2", harness.ViewModel.FocusMappingLeftMotor);
        Assert.Equal(new[] { "Red", "Green", "Blue", "White" }, harness.ViewModel.AcquisitionChannels.Where(channel => channel.LedIndex < 4).OrderBy(channel => channel.LedIndex).Select(channel => channel.Role));
        Assert.Equal("ScanDebug_ChannelLedBindingUnassigned".GetLocalized(), harness.ViewModel.GetBoundLedName("IR"));
        Assert.True(harness.ViewModel.AcquisitionChannels.Single(channel => channel.Role == "Blue").IsSelected);
        Assert.False(harness.ViewModel.AcquisitionChannels.Single(channel => channel.Role == "White").IsSelected);
        var missingIr = Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.Role == "IR");
        Assert.True(missingIr.IsSelected);
        Assert.True(missingIr.LedIndex >= 5);
        Assert.Same(originalDraft, harness.Workspace.Snapshot.CurrentDraft);
    }

    [Fact]
    public async Task DeviceSettingsMapping_RefreshPreservesCurrentEditedDraftAssignment()
    {
        var document = new ScanFilmProfileDocumentService().Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", "full-v6.json"))).Document!;
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, ScanFilmProfileDraft.FromDocument(document).Draft,
            attachRuntimeBindings: true);
        var current = harness.Workspace.Snapshot.CurrentDraft;
        var editedAssignment = new ScanChannelAssignment("White", "Red", "Blue", "IR", false, true, true, false);
        var edited = new ScanFilmProfileDraft(current.ProfileName, current.SavedAtUtc, current.ChannelProfiles,
            current.SelectedCalibrationChannel, current.AcquisitionSettings,
            current.ScanRecipeSettings! with { ChannelAssignment = editedAssignment });
        harness.Workspace.SetCurrentDraft(edited);
        await harness.FlushAsync();

        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();

        Assert.Same(edited, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Equal(editedAssignment, harness.Workspace.Snapshot.CurrentDraft.ScanRecipeSettings!.ChannelAssignment);
        Assert.Equal(ScanDeviceSettings.CreateDefault().ChannelRoles, harness.ViewModel.AcquisitionChannels.Where(channel => channel.LedIndex < 4).OrderBy(channel => channel.LedIndex).Select(channel => channel.Role));
        Assert.Equal("Motor2", harness.ViewModel.SelectedScanMotor);
    }

    [Fact]
    public async Task DeviceSettingsMapping_RefreshMovesSelectedRoleAndReversalWithoutReplacingPendingInput()
    {
        var settings = new StatusDeviceSettings();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, attachRuntimeBindings: true, deviceSettings: settings);
        // Page activation hydrates the workspace before the user edits it.
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();
        foreach (var channel in harness.ViewModel.AcquisitionChannels)
            channel.IsSelected = channel.Role == "Blue";
        harness.ViewModel.IsChannel1Reversed = true;
        harness.ViewModel.SelectedRows = "unfinished rows";
        Assert.Equal("Blue", Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.IsSelected).Role);
        var originalDraft = harness.Workspace.Snapshot.CurrentDraft;
        await settings.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with
        {
            Channel1Role = "Red", Channel2Role = "White", Channel3Role = "Blue", Channel4Role = "Green"
        });

        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();

        var selected = Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.IsSelected);
        Assert.Equal("Blue", selected.Role);
        Assert.Equal(2, selected.LedIndex);
        Assert.False(harness.ViewModel.IsChannel1Reversed);
        Assert.True(harness.ViewModel.IsChannel3Reversed);
        Assert.Equal("unfinished rows", harness.ViewModel.SelectedRows);
        Assert.True(harness.ViewModel.HasUnsavedProfileChanges);
        Assert.Same(originalDraft, harness.Workspace.Snapshot.CurrentDraft);
    }

    [Fact]
    public async Task DeviceSettingsMapping_MissingSelectedRoleRemainsVisibleAndRestoresWhenRemapped()
    {
        var settings = new StatusDeviceSettings();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, attachRuntimeBindings: true, connected: true, deviceSettings: settings);
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();
        foreach (var channel in harness.ViewModel.AcquisitionChannels)
            channel.IsSelected = channel.Role == "Blue";
        harness.ViewModel.IsChannel1Reversed = true;
        Assert.Equal("Blue", Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.IsSelected).Role);
        var originalDraft = harness.Workspace.Snapshot.CurrentDraft;
        await settings.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with { Channel1Role = "IR" });
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();

        var missing = Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.IsSelected);
        Assert.Equal("Blue", missing.Role);
        Assert.True(missing.LedIndex >= 5);
        Assert.Equal("ScanDebug_ChannelLedBindingUnassigned".GetLocalized(), harness.ViewModel.GetBoundLedName("Blue"));
        Assert.False(harness.ViewModel.StartScanCommand.CanExecute(null));
        Assert.Contains(ScanDebugViewModel.GetCalibrationChannelDisplayName("Blue"), harness.ViewModel.StartDisabledReasonText);
        Assert.Same(originalDraft, harness.Workspace.Snapshot.CurrentDraft);

        await settings.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with
        {
            Channel1Role = "Red", Channel2Role = "White", Channel3Role = "Blue", Channel4Role = "Green"
        });
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();

        var remapped = Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.IsSelected);
        Assert.Equal("Blue", remapped.Role);
        Assert.Equal(2, remapped.LedIndex);
        Assert.True(harness.ViewModel.IsChannel3Reversed);
        Assert.DoesNotContain(harness.ViewModel.AcquisitionChannels, channel => channel.LedIndex >= 5);
        Assert.Same(originalDraft, harness.Workspace.Snapshot.CurrentDraft);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeviceSettingsMapping_SaveUsesCurrentMotorWithoutDirtyingImportedFileOnCheckOrCancel(bool saveSucceeds)
    {
        var draft = CreateFixtureDraft("full-v6.json");
        var settings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with
        {
            Channel1Role = "IR", Channel2Role = "Blue", Channel3Role = "White", Channel4Role = "Red"
        });
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true, deviceSettings: settings);
        harness.Workspace.MarkExported(harness.Workspace.BuildExportDocument().Document.Document!);
        await harness.FlushAsync();
        var original = harness.Workspace.Snapshot.CurrentDraft;
        Assert.Equal((byte)2, original.AcquisitionSettings!.ScanMotorId);

        harness.ViewModel.SelectedScanMotor = "Motor3";
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();
        harness.ViewModel.ValidateFilmProfileCommand.Execute(null);
        await harness.FlushAsync();
        Assert.Equal("Motor2", harness.ViewModel.SelectedScanMotor);
        Assert.Same(original, harness.Workspace.Snapshot.CurrentDraft);
        Assert.False(harness.ViewModel.HasUnsavedProfileChanges);

        harness.FilmProfileFiles.ExportSucceeds = saveSucceeds;
        await harness.ViewModel.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var exported = Assert.IsType<ScanFilmParameterProfileSet>(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(original.AcquisitionSettings with { ScanMotorId = 1 }, exported.AcquisitionSettings);
        Assert.Equal(original.ScanRecipeSettings, exported.ScanRecipeSettings);
        Assert.Equal(original.ChannelProfiles.OrderBy(pair => pair.Key), exported.ChannelProfiles.OrderBy(pair => pair.Key));
        if (!saveSucceeds)
            Assert.Same(original, harness.Workspace.Snapshot.CurrentDraft);
        else
            Assert.Equal((byte)1, harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings!.ScanMotorId);
        Assert.False(harness.ViewModel.HasUnsavedProfileChanges);
    }

    [Fact]
    public async Task DeviceSettingsMapping_OldEditingDraftCannotRestoreMotorAndRetainsSamplingInterval()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, CreateIntervalBasedTransportDraft(7_000), attachRuntimeBindings: true);
        var vm = harness.ViewModel;
        var capture = typeof(ScanDebugViewModel).GetMethod("CaptureFilmProfileEditorInputs", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var restore = typeof(ScanDebugViewModel).GetMethod("RestoreFilmProfileEditorInputs", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var inputs = (ScanFilmProfileEditorInputs)capture.Invoke(vm, null)!;
        var oldInputs = inputs.GlobalInputs.ToDictionary(pair => pair.Key, pair => pair.Value);
        oldInputs[nameof(vm.SelectedScanMotor)] = "Motor3";
        oldInputs[nameof(vm.MotorIntervalUs)] = "7";
        oldInputs[nameof(vm.Motor2SpeedValue)] = "1";

        restore.Invoke(vm, [inputs with { GlobalInputs = oldInputs }]);

        Assert.Equal("Motor2", vm.SelectedScanMotor);
        Assert.True(ScanMotorIntervalText.TryParseMicroseconds(vm.MotorIntervalUs, out var restoredInterval));
        Assert.Equal(7_000u, restoredInterval);
        Assert.Equal("7000", vm.Motor2IntervalNs);
        Assert.Equal(inputs.GlobalInputs[nameof(vm.SelectedRows)], vm.SelectedRows);
        Assert.Equal(inputs.GlobalInputs[nameof(vm.MotorDistancePerLineValue)], vm.MotorDistancePerLineValue);
    }

    [Fact]
    public async Task DeviceSettingsMapping_NewWorkbenchRequestsUseCurrentMotorAndExistingRequestStaysUnchanged()
    {
        var profiles = CreateKnownRoleProfiles().ToDictionary(pair => pair.Key, pair => pair.Value);
        profiles["White"] = profiles["Blue"];
        var settings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with
        {
            Channel1Role = "IR", Channel2Role = "Blue", Channel3Role = "White", Channel4Role = "Red"
        });
        var harness = await StatusHarness.CreateAsync(profiles, CreateIntervalBasedTransportDraft(100_000), attachRuntimeBindings: true, deviceSettings: settings);
        await SetValidParameterInputsAsync(harness, "48");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, []));
        var original = BuildMappedDebugRequest(harness.ViewModel);
        Assert.Equal((byte)1, original.ScanMotorId);
        Assert.Equal(original.ScanMotorId, original.AcquisitionSettings!.ScanMotorId);
        await settings.SetSettingsAsync(settings.Settings with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2), DefaultScanMotorId = 0
        });
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();
        var next = BuildMappedDebugRequest(harness.ViewModel);

        Assert.Equal((byte)0, next.ScanMotorId);
        Assert.Equal(next.ScanMotorId, next.AcquisitionSettings!.ScanMotorId);
        Assert.Equal((byte)1, original.ScanMotorId);
        Assert.Equal((byte)1, original.AcquisitionSettings!.ScanMotorId);
        Assert.Equal(original.PassChannelRoles, next.PassChannelRoles);
        Assert.Equal(original.PassParameterProfiles, next.PassParameterProfiles);
    }

    private static ScanWorkflowRequest BuildMappedDebugRequest(ScanDebugViewModel viewModel)
    {
        object?[] args = [256, null, string.Empty];
        var built = (bool)typeof(ScanDebugViewModel).GetMethod("TryBuildDebugWorkflowRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(viewModel, args)!;
        Assert.True(built, args[2]?.ToString());
        return Assert.IsType<ScanWorkflowRequest>(args[1]);
    }

    private static ScanFilmProfileDraft CreateIntervalBasedTransportDraft(uint intervalNs)
    {
        var source = CreateFixtureDraft("full-v6.json");
        // The serialization fixture has a fractional-microsecond interval and an
        // explicit pitch. Request tests need a valid whole-microsecond interval.
        return new(source.ProfileName, source.SavedAtUtc, source.ChannelProfiles, source.SelectedCalibrationChannel,
            source.AcquisitionSettings! with { TargetLinePitchMicrometers = null, MotorIntervalNs = intervalNs },
            source.ScanRecipeSettings);
    }
}

public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    [Fact]
    public async Task DeviceSettingsMapping_ScanPageWithoutConfigurationUsesLatestDeviceDefaultsOnReentry()
    {
        var harness = CreateHarness();
        harness.ViewModel.Deactivate();
        await harness.DeviceSettings.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2), DefaultScanMotorId = 0,
            Channel1Role = "Red", Channel2Role = "Green", Channel3Role = "Blue", Channel4Role = "IR"
        });

        harness.ViewModel.Activate();
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();

        Assert.Equal("Motor1", harness.ViewModel.SelectedScanMotor);
        Assert.Equal(new[] { "Red", "Green", "Blue", "IR" }, GetProjectedScanAssignment(harness.ViewModel).Roles);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task DeviceSettingsMapping_LoadedScanPageProjectsFilmRolesAndReversalOntoCurrentDeviceWiring()
    {
        var harness = CreateHarness();
        await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
        {
            Channel1Role = "IR", Channel2Role = "Blue", Channel3Role = "White", Channel4Role = "Red"
        });
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        var originalAssignment = GetProjectedScanAssignment(harness.ViewModel);
        Assert.Equal("Motor2", harness.ViewModel.SelectedScanMotor);
        harness.ViewModel.Deactivate();
        await harness.DeviceSettings.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2), DefaultScanMotorId = 0,
            Channel1Role = "Red", Channel2Role = "IR", Channel3Role = "Blue", Channel4Role = "White"
        });

        harness.ViewModel.Activate();
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();

        Assert.Equal("Motor1", harness.ViewModel.SelectedScanMotor);
        Assert.Equal(harness.Document.ScanRecipeSettings!.ChannelAssignment, originalAssignment);
        Assert.Equal(new ScanChannelAssignment("Red", "IR", "Blue", "White", true, false, true, false), GetProjectedScanAssignment(harness.ViewModel));
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task DeviceSettingsMapping_SavedProfileRequestsUseDeviceMotorAndRetainExistingSnapshot()
    {
        var harness = CreateHarness();
        await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
        {
            Channel1Role = "IR", Channel2Role = "Blue", Channel3Role = "White", Channel4Role = "Red"
        });
        var profile = harness.Document.ChannelProfiles["IR"];
        var assignment = Assert.IsType<ScanChannelAssignment>(harness.Document.ScanRecipeSettings!.ChannelAssignment);
        var profiles = assignment.Roles
            .Distinct().ToDictionary(role => role, _ => profile);
        harness.Workspace.MarkExported(harness.Document with
        {
            ChannelProfiles = profiles,
            AcquisitionSettings = harness.Document.AcquisitionSettings! with
            {
                TargetLinePitchMicrometers = null, MotorIntervalNs = 100_000
            }
        });
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        var vm = harness.ViewModel;
        typeof(ScanViewModel).GetField("_loadedSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, profile.Parameters);
        typeof(ScanViewModel).GetField("_loadedExposureTicks", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, profile.Parameters.ExposureTicks);
        typeof(ScanViewModel).GetField("_loadedSysClockKhz", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, profile.Parameters.SysClockKhz);
        vm.SelectedScanMotor = "Motor3";
        var original = BuildMappedScanRequest(vm);
        Assert.Equal("Motor2", vm.SelectedScanMotor);
        Assert.Equal((byte)1, original.ScanMotorId);
        Assert.Equal(original.ScanMotorId, original.AcquisitionSettings!.ScanMotorId);

        await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2), DefaultScanMotorId = 0
        });
        await vm.RefreshDeviceSettingsBindingsAsync();
        var next = BuildMappedScanRequest(vm);

        Assert.Equal((byte)0, next.ScanMotorId);
        Assert.Equal(next.ScanMotorId, next.AcquisitionSettings!.ScanMotorId);
        Assert.Equal((byte)1, original.ScanMotorId);
        Assert.Equal((byte)1, original.AcquisitionSettings!.ScanMotorId);
        Assert.Equal(original.PassChannelRoles, next.PassChannelRoles);
        Assert.Equal(original.PassParameterProfiles, next.PassParameterProfiles);
        await vm.CleanupAsync();
    }

    private static ScanWorkflowRequest BuildMappedScanRequest(ScanViewModel viewModel)
    {
        object?[] args = [null, string.Empty];
        var built = (bool)typeof(ScanViewModel).GetMethod("TryBuildWorkflowRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(viewModel, args)!;
        Assert.True(built, args[1]?.ToString());
        return Assert.IsType<ScanWorkflowRequest>(args[0]);
    }

    private static ScanChannelAssignment GetProjectedScanAssignment(ScanViewModel viewModel)
        => (ScanChannelAssignment)typeof(ScanViewModel).GetMethod("BuildChannelAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(viewModel, null)!;
}
