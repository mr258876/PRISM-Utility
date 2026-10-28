using System.Text.Json;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanDeviceSettingsEditorTests
{
    private const string SettingsKey = "ScanDeviceSettings";
    private const string GeometryKey = "ScanDngGeometrySettings";

    [Fact]
    public async Task LegacyFocusMappingChoosesRemainingTransportMotorWithoutChangingStoredMapping()
    {
        var defaults = ScanDeviceSettings.CreateDefault();
        var focus = new ScanFocusMotorMapping(1, 2, true, false);
        var legacy = JsonSerializer.Deserialize<ScanDeviceSettings>(JsonSerializer.Serialize(new
        {
            defaults.Motor1, defaults.Motor2, defaults.Motor3,
            defaults.Channel1Role, defaults.Channel2Role, defaults.Channel3Role, defaults.Channel4Role,
            FocusMotorMapping = focus
        }))!;
        var store = new Set003RecordingLocalSettings();
        store.Seed(SettingsKey, legacy);
        var service = new ScanDeviceSettingsService(store);

        await service.InitializeAsync();

        Assert.Equal((byte)0, service.Settings.DefaultScanMotorId);
        Assert.Equal(focus, service.Settings.FocusMotorMapping);
        Assert.True(service.Settings.ValidateMappings().IsValid);
        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task DuplicateAssignmentsAreRejectedBeforePersisting()
    {
        var store = new Set003RecordingLocalSettings();
        var service = new ScanDeviceSettingsService(store);
        await service.InitializeAsync();
        var previous = service.Settings;

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetSettingsAsync(previous with { DefaultScanMotorId = 0 }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetSettingsAsync(previous with { Channel1Role = "Red" }));

        Assert.Same(previous, service.Settings);
        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task MultipleUnusedChannelsAreAllowedAndExplicitTransportMotorRoundTrips()
    {
        var store = new Set003RecordingLocalSettings();
        var service = new ScanDeviceSettingsService(store);
        var settings = ScanDeviceSettings.CreateDefault() with
        {
            Channel1Role = "Unused",
            Channel2Role = "Unused",
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, true, true),
            DefaultScanMotorId = 0,
            Motor1 = new ScanMotorMechanicalSettings(400, 32, 4.5)
        };

        await service.SetSettingsAsync(settings);
        var reopened = new ScanDeviceSettingsService(store);
        await reopened.InitializeAsync();

        Assert.Equal(settings.Normalize(), reopened.Settings);
    }

    [Fact]
    public async Task FailedPersistenceLeavesEffectiveDeviceSettingsUnchanged()
    {
        var store = new Set003RecordingLocalSettings();
        var service = new ScanDeviceSettingsService(store);
        await service.InitializeAsync();
        var previous = service.Settings;
        store.FailSave(SettingsKey, new IOException("disk unavailable"));

        await Assert.ThrowsAsync<IOException>(() => service.SetSettingsAsync(previous with { Channel2Role = "IR" }));

        Assert.Same(previous, service.Settings);
        Assert.False(store.Contains(SettingsKey));
    }

    [Fact]
    public async Task EditorSavesSwappedMotorAndChannelAssignmentsTogetherAndReopensLatestValues()
    {
        var store = new Set003RecordingLocalSettings();
        var service = new ScanDeviceSettingsService(store);
        var editor = await CreateEditorAsync(service);

        editor.DeviceDefaultScanMotor = "Motor1";
        editor.DeviceLeftFocusMotor = "Motor2";
        editor.DeviceChannel1Role = "Red";
        editor.DeviceChannel3Role = "Blue";
        editor.DeviceZPositiveDirection = "Dir1";
        editor.DeviceMotorMechanics[0].LeadLengthMm = "4.5";
        Assert.Empty(store.SavedKeys);

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.Single(store.SavedKeys);
        Assert.False(editor.HasDeviceSettingsChanges);
        Assert.False(editor.HasDeviceSettingsValidationError);
        Assert.Equal((byte)0, service.Settings.DefaultScanMotorId);
        Assert.Equal((byte)1, service.Settings.FocusMotorMapping!.LeftMotorId);
        Assert.True(service.Settings.FocusMotorMapping.ZPositiveDirection);
        Assert.Equal("Red", service.Settings.Channel1Role);
        Assert.Equal("Blue", service.Settings.Channel3Role);
        Assert.Equal(4.5, service.Settings.Motor1!.LeadLengthMm);

        var reopened = await CreateEditorAsync(service);
        Assert.Equal("Motor1", reopened.DeviceDefaultScanMotor);
        Assert.Equal("Motor2", reopened.DeviceLeftFocusMotor);
        Assert.Equal("Red", reopened.DeviceChannel1Role);
        Assert.Equal("4.5", reopened.DeviceMotorMechanics[0].LeadLengthMm);
        Assert.False(reopened.HasDeviceSettingsChanges);
    }

    [Fact]
    public async Task EditorReportsInvalidAssignmentsWithoutWritingOrDiscardingInputs()
    {
        var store = new Set003RecordingLocalSettings();
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store));
        editor.DeviceDefaultScanMotor = "Motor1";

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.True(editor.HasDeviceSettingsValidationError);
        Assert.Equal("Motor1", editor.DeviceDefaultScanMotor);
        Assert.Empty(store.SavedKeys);

        editor.DeviceDefaultScanMotor = "Motor2";
        editor.DeviceChannel1Role = "Red";
        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);
        Assert.True(editor.HasDeviceSettingsValidationError);
        Assert.Empty(store.SavedKeys);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public async Task EditorRejectsInvalidMechanicalInputs(string lead)
    {
        var store = new Set003RecordingLocalSettings();
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store));
        editor.DeviceMotorMechanics[1].LeadLengthMm = lead;

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.True(editor.HasDeviceSettingsValidationError);
        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task EditorRetainsUnsavedChangesAfterStorageFailureAndCanRetry()
    {
        var store = new Set003RecordingLocalSettings();
        var service = new ScanDeviceSettingsService(store);
        var editor = await CreateEditorAsync(service);
        store.FailSave(SettingsKey, new IOException("disk unavailable"));
        editor.DeviceChannel2Role = "IR";

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.True(editor.HasSettingsPersistenceError);
        Assert.True(editor.HasDeviceSettingsChanges);
        Assert.Equal("IR", editor.DeviceChannel2Role);
        Assert.Equal("White", service.Settings.Channel2Role);

        store.ClearSaveFailure(SettingsKey);
        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);
        Assert.False(editor.HasSettingsPersistenceError);
        Assert.False(editor.HasDeviceSettingsChanges);
        Assert.Equal("IR", service.Settings.Channel2Role);
    }

    [Fact]
    public async Task DeviceAndGeometryEditsShareOneExplicitSaveAndReloadWithoutBecomingDirty()
    {
        var store = new Set003RecordingLocalSettings();
        var device = new ScanDeviceSettingsService(store);
        var geometry = new ScanDngGeometrySettingsService(store);
        var editor = await CreateEditorAsync(device, geometry);

        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "140";
        editor.DngMaskedRanges = "80-100, 20-40";
        Assert.True(editor.CanSaveDeviceSettings);
        Assert.Empty(store.SavedKeys);

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.Equal(new[] { SettingsKey, GeometryKey }, store.SavedKeys);
        Assert.Equal("IR", device.Settings.Channel2Role);
        Assert.Equal(140, geometry.Settings.ActiveRange.Start);
        Assert.Equal([new ScanColumnRange(20, 40), new ScanColumnRange(80, 100)], geometry.Settings.MaskedBlackRanges);
        Assert.False(editor.HasDeviceSettingsChanges);

        var reopened = await CreateEditorAsync(device, geometry);
        Assert.Equal("140", reopened.DngActiveStart);
        Assert.Equal("20-40, 80-100", reopened.DngMaskedRanges);
        Assert.False(reopened.HasDeviceSettingsChanges);
    }

    [Fact]
    public async Task InvalidGeometryPreventsSavingBothGroupsAndKeepsInputsForCorrection()
    {
        var store = new Set003RecordingLocalSettings();
        var device = new ScanDeviceSettingsService(store);
        var geometry = new ScanDngGeometrySettingsService(store);
        var editor = await CreateEditorAsync(device, geometry);
        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "20"; // Overlaps the default black reference range.

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.Empty(store.SavedKeys);
        Assert.True(editor.HasDeviceSettingsValidationError);
        Assert.True(editor.HasDeviceSettingsChanges);
        Assert.Equal("20", editor.DngActiveStart);
        Assert.Equal("IR", editor.DeviceChannel2Role);
        Assert.Equal("White", device.Settings.Channel2Role);
    }

    [Theory]
    [InlineData("26-115,")]
    [InlineData("26-115,,10-20")]
    [InlineData("")]
    [InlineData("115-26")]
    public async Task MalformedGeometryRangesAreRejectedBeforePersistence(string ranges)
    {
        var store = new Set003RecordingLocalSettings();
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store), new ScanDngGeometrySettingsService(store));
        editor.DngMaskedRanges = ranges;

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.True(editor.HasDeviceSettingsValidationError);
        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task RestoreDefaultsChangesOnlyTheDraftAndRevertRestoresBothSavedGroups()
    {
        var store = new Set003RecordingLocalSettings();
        var device = new ScanDeviceSettingsService(store);
        var geometry = new ScanDngGeometrySettingsService(store);
        await device.SetSettingsAsync(ScanDeviceSettings.CreateDefault() with { Channel2Role = "IR" });
        await geometry.SetSettingsAsync(ScanDngGeometrySettings.CreateDefault() with { ActiveRange = new ScanColumnRange(160, 7400) });
        store.SavedKeys.Clear();
        var editor = await CreateEditorAsync(device, geometry);

        editor.RestoreDeviceSettingsDefaultsCommand.Execute(null);
        editor.RestoreDngGeometryDefaultsCommand.Execute(null);

        Assert.True(editor.HasDeviceSettingsChanges);
        Assert.Equal("White", editor.DeviceChannel2Role);
        Assert.Equal("128", editor.DngActiveStart);
        Assert.Empty(store.SavedKeys);
        Assert.Equal("IR", device.Settings.Channel2Role);
        Assert.Equal(160, geometry.Settings.ActiveRange.Start);

        editor.RevertDeviceSettingsCommand.Execute(null);

        Assert.Equal("IR", editor.DeviceChannel2Role);
        Assert.Equal("160", editor.DngActiveStart);
        Assert.False(editor.HasDeviceSettingsChanges);
        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task GeometryStorageFailureKeepsTheEffectiveValueAndRetriesOnlyTheFailedGroup()
    {
        var store = new Set003RecordingLocalSettings();
        var device = new ScanDeviceSettingsService(store);
        var geometry = new ScanDngGeometrySettingsService(store);
        var editor = await CreateEditorAsync(device, geometry);
        var previousGeometry = geometry.Settings;
        store.FailSave(GeometryKey, new IOException("disk unavailable"));
        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "160";

        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.Equal("IR", device.Settings.Channel2Role);
        Assert.Same(previousGeometry, geometry.Settings);
        Assert.True(editor.HasSettingsPersistenceError);
        Assert.True(editor.HasDeviceSettingsChanges);
        Assert.Equal("160", editor.DngActiveStart);

        store.SavedKeys.Clear();
        store.ClearSaveFailure(GeometryKey);
        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        Assert.Equal([GeometryKey], store.SavedKeys);
        Assert.Equal(160, geometry.Settings.ActiveRange.Start);
        Assert.False(editor.HasSettingsPersistenceError);
        Assert.False(editor.HasDeviceSettingsChanges);
    }

    [Fact]
    public async Task RevertAfterPartialFailureKeepsSuccessfulSavesAndRestoresFailedGroup()
    {
        var store = new Set003RecordingLocalSettings();
        var device = new ScanDeviceSettingsService(store);
        var geometry = new ScanDngGeometrySettingsService(store);
        var editor = await CreateEditorAsync(device, geometry);
        store.FailSave(GeometryKey, new IOException("disk unavailable"));
        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "160";
        await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);

        editor.RevertDeviceSettingsCommand.Execute(null);

        Assert.Equal("IR", editor.DeviceChannel2Role);
        Assert.Equal("128", editor.DngActiveStart);
        Assert.False(editor.HasDeviceSettingsChanges);
        Assert.False(editor.HasSettingsPersistenceError);
    }

    [Fact]
    public async Task TeardownDoesNotImplicitlyPersistDeviceOrGeometryDrafts()
    {
        var store = new Set003RecordingLocalSettings();
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store), new ScanDngGeometrySettingsService(store));
        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "160";

        await editor.TeardownSettingsPersistenceAsync();

        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task CancelingLanguageChangeKeepsTheLanguageAndBothDeviceDrafts()
    {
        var store = new Set003RecordingLocalSettings();
        var harness = SettingsViewModelHarness.Create();
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store), new ScanDngGeometrySettingsService(store), harness);
        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "160";
        var confirmationCount = 0;
        editor.ConfirmDeviceSettingsLeaveAsync = () =>
        {
            confirmationCount++;
            return Task.FromResult(false);
        };

        editor.SelectedLanguage = "zh-CN";
        await editor.ChangeLanguageCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, confirmationCount);
        Assert.Equal("en-US", editor.SelectedLanguage);
        Assert.Equal("en-US", harness.Language.CurrentLanguage);
        Assert.Equal("IR", editor.DeviceChannel2Role);
        Assert.Equal("160", editor.DngActiveStart);
        Assert.True(editor.HasDeviceSettingsChanges);
        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task CancelingLanguageChangeRestoresTheVisibleSelectionAfterTheSelectionEventCompletes()
    {
        var store = new Set003RecordingLocalSettings();
        var harness = SettingsViewModelHarness.Create();
        await harness.Language.SetLanguageAsync("system");
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store), new ScanDngGeometrySettingsService(store), harness);
        editor.DeviceMotorMechanics[0].StepsPerRevolution = "201";
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        editor.ConfirmDeviceSettingsLeaveAsync = () => decision.Task;
        var displayedLanguage = editor.SelectedLanguage;
        editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.SelectedLanguage))
                displayedLanguage = editor.SelectedLanguage;
        };

        editor.SelectedLanguage = "en-US";
        displayedLanguage = "en-US"; // ComboBox finishes its own selection after the binding setter returns.
        decision.SetResult(false);
        await editor.ChangeLanguageCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("system", harness.Language.CurrentLanguage);
        Assert.Equal("system", editor.SelectedLanguage);
        Assert.Equal("system", displayedLanguage);
        Assert.Equal("201", editor.DeviceMotorMechanics[0].StepsPerRevolution);
        Assert.True(editor.HasDeviceSettingsChanges);
        Assert.Empty(store.SavedKeys);
    }

    [Fact]
    public async Task LanguageChangeWaitsForExplicitDeviceSaveBeforeRefreshingTheShell()
    {
        var store = new Set003RecordingLocalSettings();
        var harness = SettingsViewModelHarness.Create();
        var device = new ScanDeviceSettingsService(store);
        var geometry = new ScanDngGeometrySettingsService(store);
        var editor = await CreateEditorAsync(device, geometry, harness);
        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "160";
        editor.ConfirmDeviceSettingsLeaveAsync = async () =>
        {
            await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);
            return !editor.HasDeviceSettingsChanges;
        };
        harness.Language.BeforeSetLanguageAsync = _ =>
        {
            Assert.Equal("IR", device.Settings.Channel2Role);
            Assert.Equal(160, geometry.Settings.ActiveRange.Start);
            Assert.False(editor.HasDeviceSettingsChanges);
            return Task.CompletedTask;
        };

        editor.SelectedLanguage = "zh-CN";
        await editor.ChangeLanguageCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("zh-CN", harness.Language.CurrentLanguage);
        Assert.Equal("zh-CN", editor.SelectedLanguage);
        Assert.False(editor.HasDeviceSettingsChanges);
    }

    [Fact]
    public async Task FailedDeviceSaveDuringLanguageChangeKeepsTheCurrentShellAndDraft()
    {
        var store = new Set003RecordingLocalSettings();
        var harness = SettingsViewModelHarness.Create();
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store), new ScanDngGeometrySettingsService(store), harness);
        editor.DngActiveStart = "160";
        store.FailSave(GeometryKey, new IOException("disk unavailable"));
        editor.ConfirmDeviceSettingsLeaveAsync = async () =>
        {
            await editor.SaveDeviceSettingsCommand.ExecuteAsync(null);
            return !editor.HasDeviceSettingsChanges;
        };

        editor.SelectedLanguage = "zh-CN";
        await editor.ChangeLanguageCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("en-US", harness.Language.CurrentLanguage);
        Assert.Equal("en-US", editor.SelectedLanguage);
        Assert.Equal("160", editor.DngActiveStart);
        Assert.True(editor.HasDeviceSettingsChanges);
        Assert.True(editor.HasSettingsPersistenceError);
    }

    [Fact]
    public async Task ExplicitlyDiscardingDeviceDraftAllowsLanguageChangeWithoutSavingIt()
    {
        var store = new Set003RecordingLocalSettings();
        var harness = SettingsViewModelHarness.Create();
        var editor = await CreateEditorAsync(new ScanDeviceSettingsService(store), new ScanDngGeometrySettingsService(store), harness);
        editor.DeviceChannel2Role = "IR";
        editor.DngActiveStart = "160";
        editor.ConfirmDeviceSettingsLeaveAsync = () =>
        {
            editor.RevertDeviceSettingsCommand.Execute(null);
            return Task.FromResult(true);
        };

        editor.SelectedLanguage = "zh-CN";
        await editor.ChangeLanguageCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("zh-CN", harness.Language.CurrentLanguage);
        Assert.Equal("White", editor.DeviceChannel2Role);
        Assert.Equal("128", editor.DngActiveStart);
        Assert.False(editor.HasDeviceSettingsChanges);
        Assert.Empty(store.SavedKeys);
    }

    private static async Task<SettingsViewModel> CreateEditorAsync(ScanDeviceSettingsService service, ScanDngGeometrySettingsService? geometry = null, SettingsViewModelHarness? harness = null)
    {
        harness ??= SettingsViewModelHarness.Create();
        var editor = new SettingsViewModel(harness.Theme, harness.Language, harness.Debug, harness.Mirror,
            harness.Transfer, harness.Color, harness.Coordinator, harness.Dispatcher, service, geometry);
        await editor.FlushSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(5));
        return editor;
    }
}
