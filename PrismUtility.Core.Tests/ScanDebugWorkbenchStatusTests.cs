using Microsoft.UI.Xaml;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Fact]
    public async Task WorkbenchStatus_MissingReferencesRemainDistinctFromValidSaveableParameters()
    {
        var source = CreateIntervalBasedTransportDraft(100_000);
        var profiles = source.ChannelProfiles.ToDictionary(pair => pair.Key,
            pair => pair.Value with { BlackLevel = null, WhiteLevel = null });
        var draft = new ScanFilmProfileDraft(source.ProfileName, source.SavedAtUtc, profiles,
            source.SelectedCalibrationChannel, source.AcquisitionSettings, source.ScanRecipeSettings);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true,
            deviceSettings: WorkbenchStatusDeviceSettings());
        var vm = harness.ViewModel;
        var selected = vm.AcquisitionChannels.Where(channel => channel.IsSelected).ToArray();
        var snapshot = harness.Workspace.Snapshot;

        Assert.NotEmpty(selected);
        Assert.Equal("ScanDebug_WorkbenchParametersValid".GetLocalizedOrFallback("Parameter checks passed"),
            vm.WorkbenchParameterStatusText);
        Assert.Equal("ScanDebug_WorkbenchReferencesCoverage".GetLocalizedFormatOrFallback(
            "References configured · {0}/{1} channels", 0, selected.Length), vm.WorkbenchCalibrationStatusText);
        foreach (var channel in selected)
            Assert.Contains(channel.DisplayName, vm.WorkbenchCalibrationDetailText);
        Assert.Equal(ScanDebugWorkflowGuideStep.Calibration, vm.WorkflowGuideStep);
        Assert.False(vm.IsConnected);
        Assert.True(vm.CanSaveFilmProfileFile);
        Assert.True(vm.CanUseEditingDraftFiles);
        Assert.Same(snapshot.CurrentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Empty(harness.Repository.SavedProfileRoles);

        harness.FilmProfileFiles.ExportSucceeds = true;
        await vm.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(1, vm.FilmProfileExportVersion);
        Assert.Equal(0, harness.Workspace.LastSavedDocument!.ChannelProfiles.Values.Count(
            profile => profile.BlackLevel.HasValue || profile.WhiteLevel.HasValue));
    }

    [Fact]
    public async Task WorkbenchStatus_PendingInputsAndInvalidValuesHaveDifferentRepairGuidance()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, null);
        var vm = harness.ViewModel;

        Assert.Equal("ScanDebug_WorkbenchParametersIncomplete".GetLocalizedFormatOrFallback(
            "Parameters incomplete · {0}", 6), vm.WorkbenchParameterStatusText);
        Assert.False(vm.CanSaveFilmProfileFile);
        Assert.True(vm.CanUseEditingDraftFiles);

        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        vm.SysClockMhz = "invalid";
        await harness.FlushAsync();

        Assert.Equal("ScanDebug_WorkbenchParametersInvalid".GetLocalizedFormatOrFallback(
            "Parameters need repair · {0}", 1), vm.WorkbenchParameterStatusText);
        Assert.Contains(nameof(ScanDebugViewModel.WorkbenchParameterStatusText), notifications);
        Assert.False(vm.CanSaveFilmProfileFile);
    }

    [Fact]
    public async Task WorkbenchStatus_ReferenceCoverageTracksSelectionAndWorkspaceReplacement()
    {
        var source = CreateIntervalBasedTransportDraft(100_000);
        var profiles = source.ChannelProfiles.ToDictionary(pair => pair.Key,
            pair => pair.Value with { BlackLevel = (ushort)10, WhiteLevel = (ushort)60_000 });
        profiles["Blue"] = profiles["Red"];
        var draft = new ScanFilmProfileDraft(source.ProfileName, source.SavedAtUtc, profiles,
            source.SelectedCalibrationChannel, source.AcquisitionSettings, source.ScanRecipeSettings);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true,
            deviceSettings: WorkbenchStatusDeviceSettings());
        var vm = harness.ViewModel;
        var selectedCount = vm.AcquisitionChannels.Count(channel => channel.IsSelected);
        Assert.True(selectedCount > 1);
        Assert.Equal("ScanDebug_WorkbenchReferencesCoverage".GetLocalizedFormatOrFallback(
            "References configured · {0}/{1} channels", selectedCount, selectedCount), vm.WorkbenchCalibrationStatusText);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        vm.AcquisitionChannels.First(channel => channel.IsSelected).IsSelected = false;
        await harness.FlushAsync();

        Assert.Equal("ScanDebug_WorkbenchReferencesCoverage".GetLocalizedFormatOrFallback(
            "References configured · {0}/{1} channels", selectedCount - 1, selectedCount - 1), vm.WorkbenchCalibrationStatusText);
        Assert.Contains(nameof(ScanDebugViewModel.WorkbenchCalibrationStatusText), notifications);
        Assert.Contains(nameof(ScanDebugViewModel.WorkbenchCalibrationDetailText), notifications);

        var current = harness.Workspace.Snapshot.CurrentDraft;
        var remainingRole = vm.AcquisitionChannels.First(channel => channel.IsSelected).Role;
        profiles = current.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        profiles[remainingRole] = profiles[remainingRole] with { BlackLevel = null, WhiteLevel = null };
        notifications.Clear();
        harness.Workspace.SetCurrentDraft(new ScanFilmProfileDraft(current.ProfileName, current.SavedAtUtc, profiles,
            current.SelectedCalibrationChannel, current.AcquisitionSettings, current.ScanRecipeSettings));
        await harness.FlushAsync();

        Assert.Equal("ScanDebug_WorkbenchReferencesCoverage".GetLocalizedFormatOrFallback(
            "References configured · {0}/{1} channels", selectedCount - 2, selectedCount - 1), vm.WorkbenchCalibrationStatusText);
        Assert.Contains(nameof(ScanDebugViewModel.WorkbenchCalibrationStatusText), notifications);
        Assert.True(vm.CanSaveFilmProfileFile);
    }

    [Fact]
    public async Task WorkbenchStatus_DisconnectionUpdatesIdleMotorAndFocusPresentationWithoutChangingStoredStatus()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true);
        var vm = harness.ViewModel;
        vm.Motor1StatusText = "last reported motor state";
        vm.AutofocusSummaryText = "last focus result";
        Assert.Equal(vm.Motor1StatusText, vm.Motor1DisplayStatusText);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        vm.IsConnected = false;

        var offline = "ScanDebug_WorkbenchDeviceOffline".GetLocalizedOrFallback("Device not connected");
        Assert.Equal(offline, vm.WorkbenchDeviceStatusText);
        Assert.Equal(offline, vm.Motor1DisplayStatusText);
        Assert.Equal(offline, vm.Motor2DisplayStatusText);
        Assert.Equal(offline, vm.Motor3DisplayStatusText);
        Assert.Equal(offline, vm.AutofocusDisplayStatusText);
        Assert.Equal("last reported motor state", vm.Motor1StatusText);
        Assert.Equal("last focus result", vm.AutofocusSummaryText);
        Assert.Equal(Visibility.Visible, vm.WorkbenchOfflineGuidanceVisibility);
        foreach (var name in new[]
        {
            nameof(vm.WorkbenchDeviceStatusText), nameof(vm.WorkbenchOfflineGuidanceVisibility),
            nameof(vm.Motor1DisplayStatusText), nameof(vm.AutofocusDisplayStatusText)
        })
            Assert.Contains(name, notifications);
    }

    [Theory]
    [InlineData(nameof(ScanDebugViewModel.IsRunning))]
    [InlineData(nameof(ScanDebugViewModel.IsAutoFocusing))]
    [InlineData(nameof(ScanDebugViewModel.IsManualFocusing))]
    [InlineData(nameof(ScanDebugViewModel.IsAutoCalibrating))]
    public async Task WorkbenchStatus_LocalWorkRemainsVisibleAfterDisconnection(string activity)
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true);
        var vm = harness.ViewModel;
        typeof(ScanDebugViewModel).GetProperty(activity)!.SetValue(vm, true);
        vm.AutofocusSummaryText = "focus operation still stopping";

        vm.IsConnected = false;

        Assert.NotEmpty(vm.RunActivityText);
        Assert.Equal(vm.RunActivityText, vm.WorkbenchDeviceStatusText);
        Assert.Equal(vm.DeviceStateText, vm.RunContextText);
        Assert.Equal(Visibility.Collapsed, vm.WorkbenchOfflineGuidanceVisibility);
        if (vm.IsAutoFocusing)
            Assert.Equal(vm.AutofocusSummaryText, vm.AutofocusDisplayStatusText);

        typeof(ScanDebugViewModel).GetProperty(activity)!.SetValue(vm, false);
        Assert.Equal(Visibility.Visible, vm.WorkbenchOfflineGuidanceVisibility);
    }

    [Fact]
    public async Task WorkbenchStatus_UnresolvedFailureRemainsVisibleUntilAcknowledged()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        var vm = harness.ViewModel;
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        await vm.SaveColumnSampleAsBlackLevelCommand.ExecuteAsync(null);

        var notice = Assert.Single(vm.OperationNotices);
        Assert.Equal(notice.Message, vm.WorkbenchDeviceStatusText);
        Assert.Contains(nameof(ScanDebugViewModel.WorkbenchDeviceStatusText), notifications);
        vm.IsAutoFocusing = true;
        Assert.Equal(notice.Message, vm.WorkbenchDeviceStatusText);
        Assert.Contains(vm.RunActivityText, vm.RunContextText);

        notifications.Clear();
        vm.AcknowledgeOperationNoticeCommand.Execute(notice.Id);
        Assert.Empty(vm.OperationNotices);
        Assert.Equal(vm.RunActivityText, vm.WorkbenchDeviceStatusText);
        Assert.Contains(nameof(ScanDebugViewModel.WorkbenchDeviceStatusText), notifications);
    }

    private static StatusDeviceSettings WorkbenchStatusDeviceSettings()
        => new(ScanDeviceSettings.CreateDefault() with
        {
            Channel1Role = "IR", Channel2Role = "Blue", Channel3Role = "White", Channel4Role = "Red"
        });
}
