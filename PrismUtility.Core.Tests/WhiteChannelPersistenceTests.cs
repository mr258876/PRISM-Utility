using Newtonsoft.Json.Linq;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class WhiteChannelPersistenceTests
{
    [Fact]
    public void SwitchingSources_RestoresTheirOwnParametersCalibrationAndLighting()
    {
        var dedicated = Profile(30, 100, 3000);
        var composite = Profile(60, 200, 4000);
        var settings = Settings(dedicated, composite);
        var draft = Draft(settings);

        var rgb = draft.SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        Assert.Equal(composite, rgb.ChannelProfiles["White"]);
        Assert.Equal(settings.RgbComposite!.Lighting, rgb.WhiteChannelSettings.ActiveSettings!.Lighting);
        var edited = composite with { BlackLevel = 250 };
        var profiles = rgb.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        profiles["White"] = edited;
        rgb = new(rgb.ProfileName, rgb.SavedAtUtc, profiles, "White", rgb.AcquisitionSettings, rgb.ScanRecipeSettings);
        var restored = rgb.SwitchWhiteLightSource(WhiteLightSource.DedicatedLed);
        Assert.Equal(dedicated, restored.ChannelProfiles["White"]);
        Assert.Equal(settings.DedicatedLed!.Lighting, restored.WhiteChannelSettings.ActiveSettings!.Lighting);
        Assert.Equal(edited, restored.SwitchWhiteLightSource(WhiteLightSource.RgbComposite).ChannelProfiles["White"]);
    }

    [Fact]
    public void SwitchingToUnconfiguredSource_DoesNotCopyCalibration()
    {
        var draft = Draft(new(DedicatedLed: new(Profile(30, 100, 3000))));
        var switched = draft.SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        Assert.False(switched.ChannelProfiles.ContainsKey("White"));
        Assert.Null(switched.WhiteChannelSettings.RgbComposite);
        Assert.Equal(draft.ChannelProfiles["White"], switched.SwitchWhiteLightSource(WhiteLightSource.DedicatedLed).ChannelProfiles["White"]);
    }

    [Fact]
    public void RunnableFile_RoundTripsBothSourcesAndFifthChannel()
    {
        var service = new ScanFilmProfileDocumentService();
        var draft = Draft(Settings(Profile(30, 100, 3000), Profile(60, 200, 4000)))
            .SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var built = service.Build(draft);
        Assert.True(built.CanApply);
        var parsed = service.Parse(service.Serialize(built.Document!));
        Assert.True(parsed.CanApply);
        Assert.Equal(7, parsed.Document!.SchemaVersion);
        Assert.Equal(draft.WhiteChannelSettings, parsed.Document.AcquisitionSettings!.WhiteChannelSettings);
        Assert.Equal("White", parsed.Document.AcquisitionSettings.AcquisitionChannelAssignment!.Channel5Role);
        Assert.True(parsed.Document.ScanRecipeSettings!.ChannelAssignment!.Channel5Reversed);
        var restored = ScanFilmProfileDraft.FromDocument(parsed.Document).Draft.SwitchWhiteLightSource(WhiteLightSource.DedicatedLed);
        Assert.Equal(draft.WhiteChannelSettings.DedicatedLed!.Profile, restored.ChannelProfiles["White"]);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void OldFiles_AlwaysUseDedicatedWhiteEvenOnAnRgbIrDevice(int schema)
    {
        var service = new ScanFilmProfileDocumentService();
        var root = JObject.Parse(service.Serialize(service.Build(Draft(Settings(Profile(30, 100, 3000), Profile(60, 200, 4000)))).Document!));
        root["SchemaVersion"] = schema;
        var acquisition = (JObject)root["AcquisitionSettings"]!;
        acquisition["WhiteChannelSettings"]!["Source"] = (int)WhiteLightSource.RgbComposite;
        var parsed = service.Parse(root.ToString());
        Assert.True(parsed.CanApply);
        var settings = parsed.Document!.AcquisitionSettings!.WhiteChannelSettings!;
        Assert.Equal(WhiteLightSource.DedicatedLed, settings.Source);
        Assert.Null(settings.RgbComposite);
        Assert.Equal(parsed.Document.ChannelProfiles["White"], settings.DedicatedLed!.Profile);
        Assert.Equal("Unused", parsed.Document.AcquisitionSettings.AcquisitionChannelAssignment!.Channel5Role);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void OldFiles_WhiteWithoutEnabledModePreservesAutomaticSteadyIllumination(int schema)
    {
        var service = new ScanFilmProfileDocumentService();
        var root = JObject.Parse(service.Serialize(service.Build(Draft(Settings(Profile(30, 100, 3000), Profile(60, 200, 4000)))).Document!));
        root["SchemaVersion"] = schema;
        var acquisition = (JObject)root["AcquisitionSettings"]!;
        acquisition["Led2ChannelColor"] = "White";
        acquisition["Led2Level"] = 432;
        acquisition["Led2PulseClock"] = 77;
        acquisition["SteadyMask"] = 0;
        acquisition["SyncMask"] = 0;

        var parsed = service.Parse(root.ToString());

        Assert.True(parsed.CanApply);
        var migrated = parsed.Document!.AcquisitionSettings!;
        Assert.Equal(WhiteLightDriveMode.Off, WhiteChannelSettings.FindComponent(migrated, "White")!.Mode);
        Assert.Equal(WhiteLightDriveMode.Steady, migrated.WhiteChannelSettings!.DedicatedLed!.Lighting!.DedicatedLed!.Mode);
        var state = ScanIlluminationService.BuildChannelState(migrated, "White", 1);
        Assert.Equal((byte)0b0010, state.SteadyMask);
        Assert.Equal((byte)0, state.SyncMask);
        Assert.Equal((ushort)432, state.Led2Level);
        Assert.Equal(77u, state.Led2PulseClock);
        Assert.Equal((ushort)0, state.Led1Level);
        Assert.Equal((ushort)0, state.Led3Level);
        Assert.Equal((ushort)0, state.Led4Level);
    }

    [Fact]
    public void RunnableFile_ExplicitWhiteOffRemainsOffAfterRoundTrip()
    {
        var service = new ScanFilmProfileDocumentService();
        var profile = Profile(30, 100, 3000);
        var settings = new WhiteChannelSettings(DedicatedLed: new(profile,
            new(DedicatedLed: new(432, 77, WhiteLightDriveMode.Off))));
        var draft = Draft(settings);
        draft = new(draft.ProfileName, draft.SavedAtUtc, draft.ChannelProfiles, draft.SelectedCalibrationChannel,
            draft.AcquisitionSettings! with { Led2ChannelColor = "White" }, draft.ScanRecipeSettings);
        var built = service.Build(draft);
        Assert.True(built.CanApply);

        var parsed = service.Parse(service.Serialize(built.Document!));

        Assert.True(parsed.CanApply);
        var acquisition = parsed.Document!.AcquisitionSettings!;
        Assert.Equal(WhiteLightDriveMode.Off, acquisition.WhiteChannelSettings!.DedicatedLed!.Lighting!.DedicatedLed!.Mode);
        var state = ScanIlluminationService.BuildChannelState(acquisition, "White", 1);
        Assert.Equal((byte)0, state.SteadyMask);
        Assert.Equal((byte)0, state.SyncMask);
        Assert.Equal((ushort)432, state.Led2Level);
        Assert.Equal(77u, state.Led2PulseClock);
    }

    [Fact]
    public void RunnableFile_RejectsUnknownSourceAndMismatchedSourceCalibration()
    {
        var service = new ScanFilmProfileDocumentService();
        var root = JObject.Parse(service.Serialize(service.Build(Draft(Settings(Profile(30, 100, 3000), Profile(60, 200, 4000)))).Document!));
        var settings = root["AcquisitionSettings"]!["WhiteChannelSettings"]!;
        settings["Source"] = 99;
        Assert.False(service.Parse(root.ToString()).CanApply);
        settings["Source"] = "1";
        Assert.False(service.Parse(root.ToString()).CanApply);
        settings["Source"] = 1;
        Assert.False(service.Parse(root.ToString()).CanApply);
    }

    [Fact]
    public void EditingFile_RoundTripsInactiveSourceAndOpensPreviousShape()
    {
        var draft = Draft(Settings(Profile(30, 100, 3000), Profile(60, 200, 4000)));
        var service = new ScanFilmProfileDraftFileService();
        var text = service.Serialize(new(draft, draft));
        var parsed = service.Parse(text);
        Assert.True(parsed.CanOpen);
        Assert.True(draft.HasSameContentAs(parsed.DraftFile!.CurrentDraft));
        var old = JObject.Parse(text);
        foreach (var name in new[] { "CurrentDraft", "BaselineDraft" })
        {
            ((JObject)old[name]!["AcquisitionSettings"]!).Remove("WhiteChannelSettings");
            foreach (var assignment in ((JObject)old[name]!).Descendants().OfType<JObject>().Where(obj => obj["Channel1Role"] is not null))
            {
                assignment.Remove("Channel5Role");
                assignment.Remove("Channel5Reversed");
            }
        }
        var legacy = service.Parse(old.ToString());
        Assert.True(legacy.CanOpen);
        Assert.Equal(WhiteLightSource.DedicatedLed, legacy.DraftFile!.CurrentDraft.WhiteChannelSettings.Source);
        var invalid = JObject.Parse(text);
        invalid["CurrentDraft"]!["AcquisitionSettings"]!["WhiteChannelSettings"]!["Source"] = 99;
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidShape, service.Parse(invalid.ToString()).Error);
    }

    [Fact]
    public async Task Repository_ReloadsBothSourcesAndClearsOnlyActiveCalibration()
    {
        var storage = new MemoryStorage();
        var repository = new ScanCalibrationProfileRepository(storage);
        var settings = Settings(Profile(30, 100, 3000), Profile(60, 200, 4000));
        await repository.SetWhiteChannelSettingsAsync(settings, default);
        await repository.SetWhiteChannelSettingsAsync(settings with { Source = WhiteLightSource.RgbComposite }, default);
        var changed = Profile(75, 250, 4500);
        await repository.SaveProfileAsync("White", changed, default);
        var reloaded = new ScanCalibrationProfileRepository(storage);
        await reloaded.InitializeAsync(default);
        Assert.Equal(changed, reloaded.Snapshot.Profiles["White"]);
        Assert.Equal(settings.DedicatedLed, reloaded.Snapshot.WhiteChannelSettings!.DedicatedLed);
        await reloaded.ClearProfileAsync("White", default);
        Assert.False(reloaded.TryGetProfile("White", out _));
        Assert.Null(reloaded.Snapshot.WhiteChannelSettings!.RgbComposite!.Profile);
        await reloaded.SetWhiteChannelSettingsAsync(reloaded.Snapshot.WhiteChannelSettings with { Source = WhiteLightSource.DedicatedLed }, default);
        Assert.Equal(settings.DedicatedLed!.Profile, reloaded.Snapshot.Profiles["White"]);
    }

    [Fact]
    public async Task Repository_LegacyWhiteIsNeverTreatedAsComposite()
    {
        var profile = Profile(30, 100, 3000);
        var storage = new MemoryStorage
        {
            Document = new(1, new(new() { ["White"] = profile }, "White",
                Settings(profile, Profile(60, 200, 4000)) with { Source = WhiteLightSource.RgbComposite }))
        };
        var repository = new ScanCalibrationProfileRepository(storage);
        await repository.InitializeAsync(default);
        Assert.Equal(WhiteLightSource.DedicatedLed, repository.Snapshot.WhiteChannelSettings!.Source);
        Assert.Null(repository.Snapshot.WhiteChannelSettings.RgbComposite);
    }

    [Fact]
    public async Task Workspace_ReferencePatchAndExportPreserveInactiveSource()
    {
        var repository = new ScanCalibrationProfileRepository(new MemoryStorage());
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        await workspace.InitializeAsync(default);
        var draft = Draft(Settings(Profile(30, 100, 3000), Profile(60, 200, 4000))).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        workspace.SetCurrentDraft(draft);
        Assert.Equal(ScanFilmProfileReferenceLevelPatchStatus.Applied, workspace.TryApplyReferenceLevels(workspace.Snapshot, "White", 222, 4444).Status);
        var exported = workspace.BuildExportDocument().Document.Document!;
        Assert.Equal((ushort)222, exported.AcquisitionSettings!.WhiteChannelSettings!.RgbComposite!.Profile!.BlackLevel);
        Assert.Equal(draft.WhiteChannelSettings.DedicatedLed, exported.AcquisitionSettings.WhiteChannelSettings.DedicatedLed);
        await workspace.SyncCalibrationLibraryAsync(default);
        Assert.Equal(exported.AcquisitionSettings.WhiteChannelSettings, repository.Snapshot.WhiteChannelSettings);
        var loadedWorkspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        await loadedWorkspace.InitializeAsync(default);
        Assert.Equal(repository.Snapshot.WhiteChannelSettings, loadedWorkspace.Snapshot.CurrentDraft.WhiteChannelSettings);
    }

    [Fact]
    public async Task Workspace_SyncWithoutWhiteLeavesBothSavedWhiteSourcesUntouched()
    {
        var repository = new ScanCalibrationProfileRepository(new MemoryStorage());
        var settings = Settings(Profile(30, 100, 3000), Profile(60, 200, 4000)) with { Source = WhiteLightSource.RgbComposite };
        await repository.SetWhiteChannelSettingsAsync(settings, default);
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        workspace.SetCurrentDraft(new("RGB", DateTimeOffset.UnixEpoch,
            new Dictionary<string, ScanChannelCalibrationProfile> { ["Red"] = Profile(25, 50, 2000) }, "Red", ScanFilmAcquisitionSettings.CreateDefault(), null));
        await workspace.SyncCalibrationLibraryAsync(default);
        Assert.Equal(settings, repository.Snapshot.WhiteChannelSettings);
        Assert.Equal(settings.RgbComposite!.Profile, repository.Snapshot.Profiles["White"]);
    }

    [Fact]
    public async Task Repository_NoLegacyWhiteDoesNotInventAnExplicitSource()
    {
        var repository = new ScanCalibrationProfileRepository(new MemoryStorage());
        await repository.SaveProfileAsync("Red", Profile(25, 50, 2000), default);
        Assert.Null(repository.Snapshot.WhiteChannelSettings);
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        await workspace.InitializeAsync(default);
        Assert.Equal(ScanFilmAcquisitionSettings.CreateDefault(), workspace.Snapshot.CurrentDraft.AcquisitionSettings);
    }

    private static ScanFilmProfileDraft Draft(WhiteChannelSettings settings)
    {
        var assignment = new ScanChannelAssignment("Blue", "IR", "Red", "Green", false, false, false, false, "White", true);
        return new("White test", DateTimeOffset.UnixEpoch,
            new Dictionary<string, ScanChannelCalibrationProfile> { ["White"] = settings.ActiveSettings!.Profile!, ["Red"] = Profile(25, 50, 2000) }, "White",
            ScanFilmAcquisitionSettings.CreateDefault() with { Led2ChannelColor = "IR", AcquisitionChannelAssignment = assignment, WhiteChannelSettings = settings },
            new(ChannelAssignment: assignment));
    }

    private static WhiteChannelSettings Settings(ScanChannelCalibrationProfile dedicated, ScanChannelCalibrationProfile composite)
        => new(DedicatedLed: new(dedicated, new(DedicatedLed: new(111, 22, WhiteLightDriveMode.Synchronized))),
            RgbComposite: new(composite, new(Red: new(101, 11), Green: new(202, 22), Blue: new(303, 33))));

    private static ScanChannelCalibrationProfile Profile(ushort exposure, ushort black, ushort white)
        => new(new(exposure, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz), ScanCalibrationRoiSettings.CreateDefault().Normalize(), black, white);

    private sealed class MemoryStorage : IScanCalibrationProfileStorage
    {
        public VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? Document { get; set; }
        public Task<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>?> ReadDocumentAsync(CancellationToken ct) => Task.FromResult(Document);
        public Task SaveDocumentAsync(VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload> document, CancellationToken ct) { Document = document; return Task.CompletedTask; }
        public Task<Dictionary<string, ScanChannelCalibrationProfile>?> ReadCurrentProfilesAsync(CancellationToken ct) => Task.FromResult<Dictionary<string, ScanChannelCalibrationProfile>?>(null);
        public Task<Dictionary<string, ScanParameterSnapshot>?> ReadLegacyProfilesAsync(CancellationToken ct) => Task.FromResult<Dictionary<string, ScanParameterSnapshot>?>(null);
        public Task<string?> ReadSelectedChannelAsync(CancellationToken ct) => Task.FromResult<string?>(null);
        public Task SaveCurrentProfilesAsync(Dictionary<string, ScanChannelCalibrationProfile> profiles, CancellationToken ct) => Task.CompletedTask;
        public Task SaveSelectedChannelAsync(string? selectedChannel, CancellationToken ct) => Task.CompletedTask;
    }
}
