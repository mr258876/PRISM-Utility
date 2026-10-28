using System.Reflection;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    [Fact]
    public async Task WhiteWorkflow_RgbSourceKeepsFifthCaptureAndUsesActualDeviceMapping()
    {
        var harness = await CreateWhiteScanHarnessAsync(WhiteLightSource.RgbComposite);

        var request = BuildMappedScanRequest(harness.ViewModel);

        Assert.Equal(new[] { "Red", "IR", "Green", "Blue", "White" }, request.PassChannelRoles);
        Assert.Equal(new[] { "Red", "IR", "Green", "Blue", "White" }, GetProjectedScanAssignment(harness.ViewModel).Roles);
        Assert.True(GetProjectedScanAssignment(harness.ViewModel).Channel5Reversed);
        Assert.Equal(WhiteLightSource.RgbComposite, request.AcquisitionSettings!.WhiteChannelSettings!.Source);
        Assert.Equal(request.AcquisitionSettings.WhiteChannelSettings, harness.Repository.Snapshot.WhiteChannelSettings);
        Assert.Equal(WhiteLightSource.RgbComposite, harness.ViewModel.LoadedFilmProfileEditRequest!.Profile.AcquisitionSettings!.WhiteChannelSettings!.Source);
        Assert.Equal(new[] { "Red", "IR", "Green", "Blue" }, new[]
        {
            request.AcquisitionSettings.Led1ChannelColor, request.AcquisitionSettings.Led2ChannelColor,
            request.AcquisitionSettings.Led3ChannelColor, request.AcquisitionSettings.Led4ChannelColor
        });
        Assert.Equal(new ushort[] { 300, 400, 200, 100 }, request.LedLevels);
        Assert.Equal(harness.Repository.Snapshot.Profiles["White"].Parameters, request.PassParameterProfiles[4]);
        Assert.Contains(harness.ViewModel.PreviewModeOptions, option => option.Key == "Raw Channel 5"
            && option.DisplayName.Contains("RGB combined light", StringComparison.Ordinal));
        Assert.Equal(5, request.PassParameterProfiles.Length);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task WhiteWorkflow_LegacyDedicatedMappingCannotDriveActualIrLed()
    {
        var harness = await CreateWhiteScanHarnessAsync(WhiteLightSource.DedicatedLed);
        var source = (ScanFilmAcquisitionSettings)typeof(ScanViewModel)
            .GetField("_selectedConfigAcquisitionSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(harness.ViewModel)!;
        typeof(ScanViewModel).GetField("_selectedConfigAcquisitionSettings", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(harness.ViewModel, source with { Led2ChannelColor = "White" });

        object?[] args = [null, string.Empty];
        var built = (bool)typeof(ScanViewModel).GetMethod("TryBuildWorkflowRequest", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, args)!;

        Assert.False(built);
        Assert.Contains("white light source", args[1]!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("White", args[1]!.ToString(), StringComparison.OrdinalIgnoreCase);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task WhiteWorkflow_UnselectedWhiteDoesNotCreateExtraCapture()
    {
        var harness = await CreateWhiteScanHarnessAsync(WhiteLightSource.RgbComposite, includeWhite: false);

        var request = BuildMappedScanRequest(harness.ViewModel);

        Assert.Equal(4, request.PassChannelRoles.Length);
        Assert.DoesNotContain("White", request.PassChannelRoles);
        Assert.DoesNotContain(harness.ViewModel.PreviewModeOptions, option => option.Key == "Raw Channel 5");
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task WhiteOnlyScan_ExposesOnlyRawPreviewAndOneMonochromeDng()
    {
        var harness = await CreateWhiteScanHarnessAsync(WhiteLightSource.RgbComposite, whiteOnly: true);
        var vm = harness.ViewModel;

        var request = BuildMappedScanRequest(vm);

        Assert.Equal(new[] { "Unused", "Unused", "Unused", "Unused", "White" }, request.PassChannelRoles);
        Assert.Equal("Raw Channel 5", Assert.Single(vm.PreviewModeOptions).Key);
        Assert.Equal("Raw Channel 5", vm.SelectedPreviewMode);
        Assert.False(vm.IsDualFileDngExportAvailable);
        Assert.Contains("Exports 1 DNG", vm.OutputFormatDescriptionText);
        Assert.Contains("raw grayscale", vm.OutputFormatDescriptionText);
        vm.IsOutputAvailable = true;
        Assert.False(vm.SaveRgbImageCommand.CanExecute(null));
        Assert.True(vm.ExportDngChannelsCommand.CanExecute(null));
        await vm.CleanupAsync();
    }

    [Fact]
    public async Task WhitePreview_CapturedSourceAndSlotSurviveConfigurationChanges()
    {
        var harness = await CreateWhiteScanHarnessAsync(WhiteLightSource.RgbComposite, whiteOnly: true);
        var vm = harness.ViewModel;
        var assignment = GetProjectedScanAssignment(vm);
        var profile = harness.Repository.Snapshot.Profiles["White"];
        var capture = new ScanPassCapture(1, 4, true, 1, 0, [])
        {
            Provenance = new(Guid.NewGuid(), "White", null, null, profile.Parameters, 1, 1,
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1) { WhiteLightSource = WhiteLightSource.RgbComposite }
        };
        SetScanPrivateField(vm, "_lastResult", new ScanWorkflowResult(1, [capture], 0, 100_000, profile.Parameters.ExposureTicks, profile.Parameters.SysClockKhz));
        SetScanPrivateField(vm, "_lastResultChannelAssignment", assignment);
        SetScanPrivateField(vm, "_lastResultWhiteLightSource", WhiteLightSource.RgbComposite);
        SetScanPrivateField(vm, "_selectedConfigAcquisitionSettings", harness.Document.AcquisitionSettings! with
        {
            WhiteChannelSettings = new(WhiteLightSource.DedicatedLed)
        });
        await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
        {
            Channel1Role = "White", Channel2Role = "Green", Channel3Role = "Red", Channel4Role = "Blue"
        });

        typeof(ScanViewModel).GetMethod("UpdatePreviewModeOptions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null);
        object?[] parseArgs = ["Raw Channel 5", null];
        var parsed = (bool)typeof(ScanViewModel).GetMethod("TryParsePreviewChannelIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, parseArgs)!;

        Assert.True(parsed);
        Assert.Equal(0, parseArgs[1]);
        Assert.Contains("RGB combined light", Assert.Single(vm.PreviewModeOptions).DisplayName);
        Assert.Contains("Exports 1 DNG", vm.OutputFormatDescriptionText);
        await vm.CleanupAsync();
    }

    private static async Task<Harness> CreateWhiteScanHarnessAsync(WhiteLightSource source, bool includeWhite = true, bool whiteOnly = false)
    {
        var harness = CreateHarness();
        var profile = harness.Document.ChannelProfiles["IR"];
        var assignment = whiteOnly
            ? new ScanChannelAssignment("Unused", "Unused", "Unused", "Unused", false, false, false, false, "White", true)
            : new ScanChannelAssignment("Blue", "Green", "Red", "IR", true, false, true, false, includeWhite ? "White" : "Unused", true);
        var whiteSettings = new WhiteChannelSettings(source,
            new(profile, new(DedicatedLed: new(500))),
            new(profile, new(Red: new(300), Green: new(200), Blue: new(100))));
        harness.Workspace.MarkExported(harness.Document with
        {
            ChannelProfiles = new[] { "Red", "Green", "Blue", "IR", "White" }.ToDictionary(role => role, _ => profile),
            AcquisitionSettings = harness.Document.AcquisitionSettings! with
            {
                Led1ChannelColor = "Blue", Led2ChannelColor = "Green", Led3ChannelColor = "Red", Led4ChannelColor = "IR",
                Led1Level = 100, Led2Level = 200, Led3Level = 300, Led4Level = 400,
                AcquisitionChannelAssignment = assignment, WhiteChannelSettings = whiteSettings,
                TargetLinePitchMicrometers = null, MotorIntervalNs = 100_000
            },
            ScanRecipeSettings = harness.Document.ScanRecipeSettings! with { ChannelAssignment = assignment }
        });
        await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
        {
            Channel1Role = "Red", Channel2Role = "IR", Channel3Role = "Green", Channel4Role = "Blue"
        });
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        SetScanPrivateField(harness.ViewModel, "_loadedSnapshot", profile.Parameters);
        SetScanPrivateField(harness.ViewModel, "_loadedExposureTicks", profile.Parameters.ExposureTicks);
        SetScanPrivateField(harness.ViewModel, "_loadedSysClockKhz", profile.Parameters.SysClockKhz);
        return harness;
    }

    private static void SetScanPrivateField(ScanViewModel viewModel, string name, object value)
        => typeof(ScanViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, value);
}
