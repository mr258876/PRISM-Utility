using Newtonsoft.Json;
using PRISM_Utility.Core.Configuration;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set003")]
[Trait("Category", "Settings")]
public sealed class Set003TransferSettingsTests
{
    private const string DocumentKey = "ScanTransferSettingsDocument";

    [Fact]
    public async Task InitializeAsync_MigratesPartialLegacySettingsIntoOneDocumentAndIsIdempotent()
    {
        var settings = new Set003RecordingLocalSettings();
        settings.Seed("ScanBulkInReadMode", (ScanBulkInReadMode)999);
        settings.Seed("ScanBulkInRequestBytes", 32 * 1024);
        settings.Seed("ScanBulkInOutstandingReads", 0);
        var service = new ScanTransferSettingsService(settings);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.InitializeAsync()));
        await service.InitializeAsync();

        var expected = ScanTransferDefaults.Settings with { RequestBytes = 32 * 1024 };
        var document = settings.Get<VersionedSettingsDocument<ScanBulkInTransferOptions>>(DocumentKey);
        Assert.Equal(expected, service.Settings);
        Assert.Equal(new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, expected), document);
        Assert.Equal([DocumentKey], settings.SavedKeys);
        Assert.Equal(32 * 1024, settings.Get<int?>("ScanBulkInRequestBytes"));
    }

    [Fact]
    public async Task InitializeAsync_FutureAndMalformedDocumentsLeaveDefaultsAndDoNotReadLegacy()
    {
        var future = new Set003RecordingLocalSettings();
        future.Seed(DocumentKey, new VersionedSettingsDocument<ScanBulkInTransferOptions>(2, ScanTransferDefaults.Settings with { RequestBytes = 1 }));
        future.Seed("ScanBulkInRequestBytes", 32 * 1024);

        var futureService = new ScanTransferSettingsService(future);
        await futureService.InitializeAsync();

        Assert.Equal(ScanTransferDefaults.Settings, futureService.Settings);
        Assert.Empty(future.SavedKeys);

        var invalid = new Set003RecordingLocalSettings();
        invalid.Seed(DocumentKey, new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, null));
        invalid.Seed("ScanBulkInRequestBytes", 32 * 1024);

        var invalidService = new ScanTransferSettingsService(invalid);
        await invalidService.InitializeAsync();

        Assert.Equal(ScanTransferDefaults.Settings, invalidService.Settings);
        Assert.Empty(invalid.SavedKeys);

        var malformed = new Set003RecordingLocalSettings();
        malformed.FailRead(DocumentKey, new JsonReaderException("Malformed transfer document."));
        malformed.Seed("ScanBulkInRequestBytes", 32 * 1024);

        var malformedService = new ScanTransferSettingsService(malformed);
        await malformedService.InitializeAsync();

        Assert.Equal(ScanTransferDefaults.Settings, malformedService.Settings);
        Assert.Empty(malformed.SavedKeys);
    }

    [Fact]
    public async Task SetSettingsAsync_SaveFailureDoesNotPublishOrChangePersistedDocument()
    {
        var settings = new Set003RecordingLocalSettings();
        settings.Seed(DocumentKey, new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, ScanTransferDefaults.Settings));
        var service = new ScanTransferSettingsService(settings);
        await service.InitializeAsync();
        var originalHash = settings.Hash;
        var expected = service.Settings;
        settings.FailSave(DocumentKey, new IOException("Injected transfer write failure."));

        await Assert.ThrowsAsync<IOException>(() => service.SetSettingsAsync(expected with { RequestBytes = 16 * 1024 }));

        Assert.Equal(expected, service.Settings);
        Assert.Equal(originalHash, settings.Hash);
    }

    [Fact]
    public async Task InitializeAsync_MigrationSaveFailureLeavesLegacyPersistenceAndMemoryUnchanged()
    {
        var settings = new Set003RecordingLocalSettings();
        settings.Seed("ScanBulkInRequestBytes", 32 * 1024);
        var originalHash = settings.Hash;
        settings.FailSave(DocumentKey, new IOException("Injected transfer migration failure."));
        var service = new ScanTransferSettingsService(settings);

        await Assert.ThrowsAsync<IOException>(() => service.InitializeAsync());

        Assert.Equal(ScanTransferDefaults.Settings, service.Settings);
        Assert.Equal(originalHash, settings.Hash);
    }

    [Fact]
    public async Task InitializeAndSaveAsync_AreSerializedAndPublishOnlyTheFinalDocument()
    {
        var settings = new Set003RecordingLocalSettings();
        settings.Seed("ScanBulkInRequestBytes", 32 * 1024);
        var service = new ScanTransferSettingsService(settings);
        var updated = ScanTransferDefaults.Settings with { RequestBytes = 64 * 1024 };

        await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => service.InitializeAsync()).Append(service.SetSettingsAsync(updated)));

        Assert.Equal(updated, service.Settings);
        Assert.Equal(new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, updated), settings.Get<VersionedSettingsDocument<ScanBulkInTransferOptions>>(DocumentKey));
        Assert.All(settings.SavedKeys, key => Assert.Equal(DocumentKey, key));
    }

    [Fact]
    public async Task InitializeAsync_LegacyCleanupFailureRetainsLegacyAfterTheDocumentCommits()
    {
        var settings = new Set003CleanupRecordingLocalSettings
        {
            CleanupFailure = new IOException("Injected legacy cleanup failure.")
        };
        settings.Seed("ScanBulkInRequestBytes", 32 * 1024);
        var service = new ScanTransferSettingsService(settings);

        await service.InitializeAsync();

        Assert.Equal(32 * 1024, service.Settings.RequestBytes);
        Assert.Equal(1, settings.CleanupCount);
        Assert.True(settings.Contains("ScanBulkInRequestBytes"));
        Assert.Equal(new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, service.Settings), settings.Get<VersionedSettingsDocument<ScanBulkInTransferOptions>>(DocumentKey));
    }
}
