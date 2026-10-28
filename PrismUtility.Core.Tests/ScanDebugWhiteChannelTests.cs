using System.Reflection;
using System.Globalization;
using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhiteSource_NewRgbIrWorkspaceUsesCompositeEvenAfterPresetHydration(bool hasRedPreset)
    {
        var presets = hasRedPreset
            ? new Dictionary<string, ScanChannelCalibrationProfile> { ["Red"] = WhiteTestProfile(40, 100, 4000) }
            : new Dictionary<string, ScanChannelCalibrationProfile>();
        var harness = await StatusHarness.CreateAsync(presets, null, attachRuntimeBindings: true,
            deviceSettings: new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with { Channel2Role = "IR" }));
        await harness.FlushAsync();

        Assert.Equal(WhiteLightSource.RgbComposite, harness.ViewModel.SelectedWhiteLightSource);
        Assert.Equal("IR", Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.LedIndex == 1).Role);
        Assert.Contains(harness.ViewModel.AcquisitionChannels, channel => channel.LedIndex == 4 && channel.Role == "White");
    }

    [Fact]
    public async Task WhiteSource_OldWhiteConfigurationDoesNotFallbackOnRgbIrDevice()
    {
        var profiles = new Dictionary<string, ScanChannelCalibrationProfile> { ["White"] = WhiteTestProfile(40, 100, 4000) };
        var legacyDraft = new ScanFilmProfileDraft("Legacy white", DateTimeOffset.UnixEpoch, profiles, "White",
            ScanFilmAcquisitionSettings.CreateDefault(), null);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, legacyDraft, attachRuntimeBindings: true,
            deviceSettings: new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with { Channel2Role = "IR" }));
        await harness.SelectChannelAsync("White");

        Assert.Equal(WhiteLightSource.DedicatedLed, harness.ViewModel.SelectedWhiteLightSource);
        Assert.Equal(Visibility.Visible, harness.ViewModel.WhiteSourceAvailabilityVisibility);
        Assert.Equal("IR", Assert.Single(harness.ViewModel.AcquisitionChannels, channel => channel.LedIndex == 1).Role);
    }

    [Fact]
    public async Task WhiteSource_SwitchingRestoresParametersReferencesAndOwnLightValues()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        await harness.SelectChannelAsync("White");
        await harness.ViewModel.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        await harness.FlushAsync();
        Assert.Equal("White", harness.ViewModel.SelectedCalibrationChannel);
        Assert.Equal("60", harness.ViewModel.ExposureTicks);
        Assert.Equal("200", harness.ViewModel.ManualBlackLevelInput);
        Assert.Equal("101", Assert.Single(harness.ViewModel.WhiteLightComponents, component => component.Role == "Red").Level);
        harness.ViewModel.ExposureMicroseconds = ScanTimingMath.ExposureTicksToMicroseconds(75, ScanDebugConstants.MinSysClockKhz).ToString("0.###", CultureInfo.InvariantCulture);
        Assert.Single(harness.ViewModel.WhiteLightComponents, component => component.Role == "Red").Level = "777";

        await harness.ViewModel.ChangeWhiteLightSourceAsync(WhiteLightSource.DedicatedLed);
        await harness.FlushAsync();
        Assert.Equal("30", harness.ViewModel.ExposureTicks);
        Assert.Equal("100", harness.ViewModel.ManualBlackLevelInput);
        Assert.Equal("111", Assert.Single(harness.ViewModel.WhiteLightComponents).Level);

        await harness.ViewModel.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        await harness.FlushAsync();
        Assert.Equal("75", harness.ViewModel.ExposureTicks);
        Assert.Equal("777", Assert.Single(harness.ViewModel.WhiteLightComponents, component => component.Role == "Red").Level);
        Assert.Equal("200", harness.ViewModel.ManualBlackLevelInput);
    }

    [Fact]
    public async Task WhiteSource_UnconfiguredCompositeStaysOnWhiteAndDoesNotReadDedicatedPreset()
    {
        var presets = new Dictionary<string, ScanChannelCalibrationProfile> { ["White"] = WhiteTestProfile(999, 444, 5555) };
        var harness = await StatusHarness.CreateAsync(presets, WhiteTestDraft(includeComposite: false), attachRuntimeBindings: true);
        await harness.SelectChannelAsync("White");
        await harness.ViewModel.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        await harness.FlushAsync();

        Assert.Equal("White", harness.ViewModel.SelectedCalibrationChannel);
        Assert.False(harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles.ContainsKey("White"));
        Assert.Equal(string.Empty, harness.ViewModel.ManualBlackLevelInput);
        Assert.Equal(string.Empty, harness.ViewModel.ManualWhiteLevelInput);
        var arguments = new object?[] { "White", null };
        var found = (bool)typeof(ScanDebugViewModel).GetMethod("TryGetSavedCalibrationProfile", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, arguments)!;
        Assert.False(found);
        Assert.Equal((ushort)999, harness.Repository.Snapshot.Profiles["White"].Parameters.ExposureTicks);
    }

    [Fact]
    public async Task WhiteSource_IncompleteParameterInputIsNotDiscardedBySourceSwitch()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        await harness.SelectChannelAsync("White");
        harness.ViewModel.ExposureMicroseconds = "unfinished";
        await harness.ViewModel.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        await harness.FlushAsync();

        Assert.Equal(WhiteLightSource.DedicatedLed, harness.ViewModel.SelectedWhiteLightSource);
        Assert.Equal("unfinished", harness.ViewModel.ExposureMicroseconds);
    }

    [Fact]
    public async Task WhiteSource_SaveAndClearPresetAffectOnlyTheChosenSource()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        await harness.SelectChannelAsync("White");
        await harness.ViewModel.SaveChannelProfileCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        var dedicated = harness.Repository.Snapshot.WhiteChannelSettings!.DedicatedLed;
        Assert.Equal((ushort)30, dedicated!.Profile!.Parameters.ExposureTicks);

        await harness.ViewModel.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        await harness.ViewModel.SaveChannelProfileCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        Assert.Equal(dedicated, harness.Repository.Snapshot.WhiteChannelSettings!.DedicatedLed);
        Assert.Equal((ushort)60, harness.Repository.Snapshot.WhiteChannelSettings.RgbComposite!.Profile!.Parameters.ExposureTicks);
        Assert.Equal(WhiteLightSource.RgbComposite, harness.Repository.Snapshot.WhiteChannelSettings.Source);

        harness.ViewModel.CalibrationPromptRequested += (_, request) => request.CompletionSource.TrySetResult(true);
        await harness.ViewModel.ClearChannelProfileCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        Assert.Null(harness.Repository.Snapshot.WhiteChannelSettings!.RgbComposite!.Profile);
        Assert.Equal(dedicated, harness.Repository.Snapshot.WhiteChannelSettings.DedicatedLed);
    }

    [Fact]
    public async Task WhiteSource_EditingDraftPreservesInvalidLightingTextAndItsDirtyState()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        var vm = harness.ViewModel;
        await harness.SelectChannelAsync("White");
        await vm.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        var gateway = new EditingDraftMemoryGateway();
        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        Assert.False(vm.HasUnsavedProfileChanges);
        var typedLighting = harness.Workspace.Snapshot.CurrentDraft.WhiteChannelSettings.RgbComposite!.Lighting;

        var red = Assert.Single(vm.WhiteLightComponents, component => component.Role == "Red");
        red.Level = "  70000  ";
        red.PulseClock = "unfinished pulse";
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Equal(Visibility.Visible, vm.WhiteLightingValidationVisibility);
        Assert.False(vm.CanSaveFilmProfileFile);
        await vm.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        var raw = JObject.Parse(gateway.SavedText!)["RawFields"]!;
        Assert.Equal("  70000  ", raw["WhiteLighting.Red.Level"]!.Value<string>());
        Assert.Equal("unfinished pulse", raw["WhiteLighting.Red.PulseClock"]!.Value<string>());
        Assert.False(vm.HasUnsavedProfileChanges);

        red.Level = "303";
        red.PulseClock = "44";
        await vm.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();
        red = Assert.Single(vm.WhiteLightComponents, component => component.Role == "Red");
        Assert.Equal("  70000  ", red.Level);
        Assert.Equal("unfinished pulse", red.PulseClock);
        Assert.Equal(Visibility.Visible, vm.WhiteLightingValidationVisibility);
        Assert.Equal(typedLighting, harness.Workspace.Snapshot.CurrentDraft.WhiteChannelSettings.RgbComposite!.Lighting);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.False(vm.CanSaveFilmProfileFile);
        AssertEditingDraftDidNotWriteLibrary(harness);
    }

    [Fact]
    public async Task WhiteSource_OlderEditingDraftWithoutOptionalWhiteRawKeysStillOpens()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        var gateway = new EditingDraftMemoryGateway();
        await harness.ViewModel.SaveEditingDraftAsync(gateway);
        var old = JObject.Parse(gateway.SavedText!);
        foreach (var property in ((JObject)old["RawFields"]!).Properties().Where(property =>
            property.Name.StartsWith("WhiteLighting.", StringComparison.Ordinal) || property.Name == "VirtualWhiteChannelReversed").ToArray())
            property.Remove();
        gateway.TextToRead = old.ToString();
        harness.ViewModel.SelectedRows = "unfinished rows";
        await harness.ViewModel.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();
        Assert.Equal(gateway.FileName, harness.ViewModel.EditingDraftFileName);
        Assert.NotEqual("unfinished rows", harness.ViewModel.SelectedRows);
        Assert.False(harness.ViewModel.HasUnsavedProfileChanges);
    }

    [Fact]
    public async Task WhiteSource_EditingDraftRestoresVirtualChannelReverse()
    {
        var draft = WhiteTestDraft(includeComposite: true).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true,
            deviceSettings: new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with { Channel2Role = "IR" }));
        await harness.SelectChannelAsync("White");
        harness.ViewModel.CurrentCalibrationChannelReversed = true;
        var gateway = new EditingDraftMemoryGateway();
        await harness.ViewModel.SaveEditingDraftAsync(gateway);
        await harness.FlushAsync();
        Assert.Equal(bool.TrueString, JObject.Parse(gateway.SavedText!)["RawFields"]!["VirtualWhiteChannelReversed"]!.Value<string>());
        harness.ViewModel.CurrentCalibrationChannelReversed = false;
        Assert.True(harness.ViewModel.HasUnsavedProfileChanges);
        await harness.ViewModel.LoadEditingDraftAsync(gateway);
        await harness.FlushAsync();
        Assert.True(harness.ViewModel.CurrentCalibrationChannelReversed);
        Assert.False(harness.ViewModel.HasUnsavedProfileChanges);
    }

    [Fact]
    public async Task WhiteSource_VisitingAnotherChannelPreservesInvalidLightingInput()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        var vm = harness.ViewModel;
        await harness.SelectChannelAsync("White");
        await vm.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        var red = Assert.Single(vm.WhiteLightComponents, component => component.Role == "Red");
        red.Level = "incomplete level";
        red.PulseClock = "  1  ";

        await harness.SelectChannelAsync("Red");
        await harness.SelectChannelAsync("White");

        red = Assert.Single(vm.WhiteLightComponents, component => component.Role == "Red");
        Assert.Equal("incomplete level", red.Level);
        Assert.Equal("  1  ", red.PulseClock);
        Assert.Equal(Visibility.Visible, vm.WhiteLightingValidationVisibility);
        Assert.True(vm.HasUnsavedProfileChanges);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhiteSource_NewUnconfiguredWhiteLightingEditIsSavedEvenWithoutWhiteProfileAndSelectionStaysWhite(bool legacyAcquisition)
    {
        var presets = new Dictionary<string, ScanChannelCalibrationProfile> { ["Red"] = WhiteTestProfile(40, 100, 4000) };
        var draft = legacyAcquisition ? new ScanFilmProfileDraft("Legacy red only", DateTimeOffset.UnixEpoch,
            presets, "Red", ScanFilmAcquisitionSettings.CreateDefault(), null) : null;
        var harness = await StatusHarness.CreateAsync(presets, draft, attachRuntimeBindings: true,
            deviceSettings: new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with { Channel2Role = "IR" }));
        var vm = harness.ViewModel;
        await harness.SelectChannelAsync("White");
        if (legacyAcquisition)
            await vm.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        Assert.Equal(WhiteLightSource.RgbComposite, vm.SelectedWhiteLightSource);
        Assert.False(harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles.ContainsKey("White"));
        Assert.Single(vm.WhiteLightComponents, component => component.Role == "Red").Level = "101";
        Assert.True(vm.HasUnsavedProfileChanges);
        Assert.Equal((ushort)101, harness.Workspace.Snapshot.CurrentDraft.WhiteChannelSettings.RgbComposite!.Lighting!.Red!.Level);

        harness.FilmProfileFiles.ExportSucceeds = true;
        await vm.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var exported = Assert.IsType<ScanFilmParameterProfileSet>(harness.FilmProfileFiles.ExportedProfile);
        Assert.False(exported.ChannelProfiles.ContainsKey("White"));
        Assert.Equal((ushort)101, exported.AcquisitionSettings!.WhiteChannelSettings!.RgbComposite!.Lighting!.Red!.Level);
        Assert.Equal("IR", exported.AcquisitionSettings.Led2ChannelColor);
        Assert.Equal("White", exported.SelectedCalibrationChannel);
        var documents = new ScanFilmProfileDocumentService();
        var reopened = documents.Parse(documents.Serialize(exported));
        Assert.Equal("White", reopened.Document!.SelectedCalibrationChannel);
        Assert.False(reopened.Document.ChannelProfiles.ContainsKey("White"));
        Assert.Equal("White", vm.SelectedCalibrationChannel);
        Assert.Equal("101", Assert.Single(vm.WhiteLightComponents, component => component.Role == "Red").Level);
        Assert.False(vm.HasUnsavedProfileChanges);
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("White"));
    }

    [Fact]
    public async Task WhiteSource_InvalidLightingRejectsSourceChangeAndRestoresSelectorProjection()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        var vm = harness.ViewModel;
        await harness.SelectChannelAsync("White");
        Assert.Single(vm.WhiteLightComponents).Level = "unfinished";
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        await vm.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        await harness.FlushAsync();

        Assert.Equal(WhiteLightSource.DedicatedLed, vm.SelectedWhiteLightSource);
        Assert.Equal(WhiteLightSource.DedicatedLed, vm.SelectedWhiteLightSourceOption!.Source);
        Assert.Equal("unfinished", Assert.Single(vm.WhiteLightComponents).Level);
        Assert.Contains(nameof(vm.SelectedWhiteLightSourceOption), notifications);
        Assert.Equal(Visibility.Visible, vm.WhiteLightingValidationVisibility);
    }

    [Fact]
    public async Task WhiteSource_FixingValidatedLightingInputRestoresCompleteConfigurationSave()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, WhiteTestDraft(includeComposite: true), attachRuntimeBindings: true);
        var vm = harness.ViewModel;
        await harness.SelectChannelAsync("White");
        var white = Assert.Single(vm.WhiteLightComponents);
        white.Level = "unfinished";
        vm.ValidateFilmProfileCommand.Execute(null);
        Assert.False(vm.CanSaveFilmProfileFile);
        Assert.Contains(vm.CurrentFilmProfileValidationIssues, issue => issue.FieldPath == "AcquisitionSettings.WhiteChannelSettings.Lighting");

        white.Level = "101";
        await harness.FlushAsync();

        Assert.Equal(Visibility.Collapsed, vm.WhiteLightingValidationVisibility);
        Assert.DoesNotContain(vm.CurrentFilmProfileValidationIssues, issue => issue.FieldPath == "AcquisitionSettings.WhiteChannelSettings.Lighting");
        Assert.True(vm.CanSaveFilmProfileFile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhiteSource_ChannelStatusUsesCurrentDraftAndChosenSource(bool hasCompositeProfile)
    {
        var presets = new Dictionary<string, ScanChannelCalibrationProfile> { ["White"] = WhiteTestProfile(99, 400, 5000) };
        var harness = await StatusHarness.CreateAsync(presets, WhiteTestDraft(hasCompositeProfile), attachRuntimeBindings: true);
        var vm = harness.ViewModel;
        await harness.SelectChannelAsync("Red");
        vm.CalibrationChannelStatusText = "stale red status";
        await harness.SelectChannelAsync("White");

        Assert.Contains(ScanDebugViewModel.GetCalibrationChannelDisplayName("White"), vm.CalibrationChannelStatusText);
        Assert.Contains("current configuration", vm.CalibrationChannelStatusText);
        Assert.Equal("Channel parameters configured", vm.GetAcquisitionChannelCalibrationStatusText("White"));
        Assert.Equal("Channel parameters configured", vm.GetAcquisitionChannelCalibrationStatusText("Red"));

        await vm.ChangeWhiteLightSourceAsync(WhiteLightSource.RgbComposite);
        await harness.FlushAsync();

        if (hasCompositeProfile)
        {
            Assert.Contains(ScanDebugViewModel.GetCalibrationChannelDisplayName("White"), vm.CalibrationChannelStatusText);
            Assert.Contains("current configuration", vm.CalibrationChannelStatusText);
            Assert.Equal("Channel parameters configured", vm.GetAcquisitionChannelCalibrationStatusText("White"));
        }
        else
        {
            Assert.Equal("ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalized(), vm.CalibrationChannelStatusText);
            Assert.Equal("ScanDebug_ProfileChannelOverviewCalibrationMissing".GetLocalized(), vm.GetAcquisitionChannelCalibrationStatusText("White"));
        }
    }

    private static ScanFilmProfileDraft WhiteTestDraft(bool includeComposite)
    {
        var dedicated = WhiteTestProfile(30, 100, 3000);
        var composite = WhiteTestProfile(60, 200, 4000);
        var settings = new WhiteChannelSettings(DedicatedLed: new(dedicated, new(DedicatedLed: new(111, 22))),
            RgbComposite: includeComposite ? new(composite, new(Red: new(101, 11), Green: new(202, 22), Blue: new(303, 33))) : null);
        return new("White test", DateTimeOffset.UnixEpoch,
            new Dictionary<string, ScanChannelCalibrationProfile> { ["White"] = dedicated, ["Red"] = WhiteTestProfile(25, 50, 2000) },
            "White", ScanFilmAcquisitionSettings.CreateDefault() with { WhiteChannelSettings = settings }, null);
    }

    private static ScanChannelCalibrationProfile WhiteTestProfile(ushort exposure, ushort black, ushort white)
        => new(new(exposure, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz), ScanCalibrationRoiSettings.CreateDefault().Normalize(), black, white);
}
