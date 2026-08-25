using Newtonsoft.Json;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set003")]
[Trait("Category", "Settings")]
public sealed class Set003ColorManagementSettingsTests
{
    private const string DocumentKey = "ScanColorManagementSettingsDocument";

    [Fact]
    public async Task InitializeAsync_MigratesPartialLegacySettingsWithExistingNormalization()
    {
        var settings = new Set003RecordingLocalSettings();
        settings.Seed("ScanColorManagementEnabled", false);
        settings.Seed("ScanColorManagementRedWavelengthNm", 700.0);
        settings.Seed("ScanColorManagementGreenWavelengthNm", 900.0);
        settings.Seed("ScanColorManagementOutputGamma", 1.8);
        settings.Seed("ScanColorManagementTargetWhitePointMode", (ScanTargetWhitePointMode)999);
        settings.Seed("ScanColorManagementManualWhitePointColorTemperatureK", 30_000.0);
        var service = new ScanColorManagementSettingsService(settings);

        await service.InitializeAsync();
        await service.InitializeAsync();

        var expected = ScanColorManagementOptions.CreateDefault() with
        {
            IsEnabled = false,
            RedWavelengthNm = 700.0,
            OutputGamma = 1.8
        };
        Assert.Equal(expected, service.Settings);
        Assert.Equal(new VersionedSettingsDocument<ScanColorManagementOptions>(1, expected), settings.Get<VersionedSettingsDocument<ScanColorManagementOptions>>(DocumentKey));
        Assert.Equal([DocumentKey], settings.SavedKeys);
        Assert.True(settings.Contains("ScanColorManagementEnabled"));
    }

    [Fact]
    public async Task InitializeAsync_FutureOrMalformedDocumentLeavesDefaultsWithoutPartialLegacyPublication()
    {
        var future = new Set003RecordingLocalSettings();
        future.Seed(DocumentKey, new VersionedSettingsDocument<ScanColorManagementOptions>(2, ScanColorManagementOptions.CreateDefault() with { IsEnabled = false }));
        future.Seed("ScanColorManagementEnabled", false);
        var futureService = new ScanColorManagementSettingsService(future);

        await futureService.InitializeAsync();

        Assert.Equal(ScanColorManagementOptions.CreateDefault(), futureService.Settings);
        Assert.Empty(future.SavedKeys);

        var invalid = new Set003RecordingLocalSettings();
        invalid.Seed(DocumentKey, new VersionedSettingsDocument<ScanColorManagementOptions>(1, null));
        invalid.Seed("ScanColorManagementEnabled", false);
        var invalidService = new ScanColorManagementSettingsService(invalid);

        await invalidService.InitializeAsync();

        Assert.Equal(ScanColorManagementOptions.CreateDefault(), invalidService.Settings);
        Assert.Empty(invalid.SavedKeys);

        var settings = new Set003RecordingLocalSettings();
        settings.FailRead(DocumentKey, new JsonReaderException("Malformed color document."));
        settings.Seed("ScanColorManagementEnabled", false);
        var service = new ScanColorManagementSettingsService(settings);

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => service.InitializeAsync()));

        Assert.Equal(ScanColorManagementOptions.CreateDefault(), service.Settings);
        Assert.Empty(settings.SavedKeys);
    }

    [Fact]
    public async Task InitializeAsync_MigrationSaveFailureLeavesLegacyPersistenceAndMemoryUnchanged()
    {
        var settings = new Set003RecordingLocalSettings();
        settings.Seed("ScanColorManagementEnabled", false);
        var originalHash = settings.Hash;
        settings.FailSave(DocumentKey, new IOException("Injected color migration failure."));
        var service = new ScanColorManagementSettingsService(settings);

        await Assert.ThrowsAsync<IOException>(() => service.InitializeAsync());

        Assert.Equal(ScanColorManagementOptions.CreateDefault(), service.Settings);
        Assert.Equal(originalHash, settings.Hash);
    }

    [Fact]
    public async Task SetSettingsAsync_SaveFailureAndConcurrentCallsDoNotPublishAnUnpersistedValue()
    {
        var settings = new Set003RecordingLocalSettings();
        var current = ScanColorManagementOptions.CreateDefault();
        settings.Seed(DocumentKey, new VersionedSettingsDocument<ScanColorManagementOptions>(1, current));
        var service = new ScanColorManagementSettingsService(settings);
        await service.InitializeAsync();
        var originalHash = settings.Hash;
        settings.FailSave(DocumentKey, new IOException("Injected color write failure."));

        await Assert.ThrowsAsync<IOException>(() => service.SetSettingsAsync(current with { OutputGamma = 1.8 }));

        Assert.Equal(current, service.Settings);
        Assert.Equal(originalHash, settings.Hash);
    }

    [Fact]
    public async Task InitializeAndSaveAsync_AreSerializedIntoOneColorDocument()
    {
        var settings = new Set003RecordingLocalSettings();
        settings.Seed("ScanColorManagementEnabled", false);
        var service = new ScanColorManagementSettingsService(settings);
        var updated = ScanColorManagementOptions.CreateDefault() with { OutputGamma = 1.7 };

        await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => service.InitializeAsync()).Append(service.SetSettingsAsync(updated)));

        Assert.Equal(updated, service.Settings);
        Assert.Equal(new VersionedSettingsDocument<ScanColorManagementOptions>(1, updated), settings.Get<VersionedSettingsDocument<ScanColorManagementOptions>>(DocumentKey));
        Assert.All(settings.SavedKeys, key => Assert.Equal(DocumentKey, key));
    }
}
