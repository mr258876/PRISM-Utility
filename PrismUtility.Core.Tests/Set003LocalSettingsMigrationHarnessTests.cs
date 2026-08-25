using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PRISM_Utility.Core.Configuration;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Models;
using PRISM_Utility.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set003")]
[Trait("Category", "Set003Harness")]
public sealed class Set003LocalSettingsMigrationHarnessTests : IDisposable
{
    private readonly List<string> _createdFolders = [];

    [Fact]
    public async Task Set003FilesystemHarness_MigratesLegacyGroupsIdempotentlyAndPreservesDurableStateOnFailures()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var settingsPath = GetSettingsPath(applicationDataFolder);
        var durableSettings = CreateSettings(applicationDataFolder, new AtomicFileWriter());
        await SeedLegacySettingsAsync(durableSettings);

        var transfer = new ScanTransferSettingsService(durableSettings);
        var color = new ScanColorManagementSettingsService(durableSettings);
        var calibration = new ScanCalibrationProfileRepository(new LocalSettingsScanCalibrationProfileStorage(durableSettings));
        await Task.WhenAll(
            transfer.InitializeAsync(),
            color.InitializeAsync(),
            calibration.InitializeAsync(CancellationToken.None));

        Assert.Equal(32 * 1024, transfer.Settings.RequestBytes);
        Assert.False(color.Settings.IsEnabled);
        Assert.Equal("Blue", calibration.Snapshot.SelectedChannel);
        Assert.True(calibration.Snapshot.Profiles.ContainsKey("Blue"));
        Assert.Equal(1, (await durableSettings.ReadSettingAsync<VersionedSettingsDocument<ScanBulkInTransferOptions>>("ScanTransferSettingsDocument"))?.SchemaVersion);
        Assert.Equal(1, (await durableSettings.ReadSettingAsync<VersionedSettingsDocument<ScanColorManagementOptions>>("ScanColorManagementSettingsDocument"))?.SchemaVersion);
        Assert.Equal(1, (await durableSettings.ReadSettingAsync<VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>>("ScanCalibrationProfileSettingsDocument"))?.SchemaVersion);
        Assert.Equal(32 * 1024, await durableSettings.ReadSettingAsync<int?>("ScanBulkInRequestBytes"));
        Assert.False((await durableSettings.ReadSettingAsync<bool?>("ScanColorManagementEnabled")) ?? true);
        var migratedHash = HashFile(settingsPath);

        var repeatSettings = CreateSettings(applicationDataFolder, new AtomicFileWriter());
        await Task.WhenAll(
            new ScanTransferSettingsService(repeatSettings).InitializeAsync(),
            new ScanColorManagementSettingsService(repeatSettings).InitializeAsync(),
            new ScanCalibrationProfileRepository(new LocalSettingsScanCalibrationProfileStorage(repeatSettings)).InitializeAsync(CancellationToken.None));

        Assert.Equal(migratedHash, HashFile(settingsPath));
        Console.WriteLine("SET003_MIGRATION_IDEMPOTENT");

        var failingSettings = CreateSettings(applicationDataFolder, new FailAfterTempWriteAtomicFileWriter());
        var failedTransfer = new ScanTransferSettingsService(failingSettings);
        var failedColor = new ScanColorManagementSettingsService(failingSettings);
        var failedCalibration = new ScanCalibrationProfileRepository(new LocalSettingsScanCalibrationProfileStorage(failingSettings));
        await Task.WhenAll(
            failedTransfer.InitializeAsync(),
            failedColor.InitializeAsync(),
            failedCalibration.InitializeAsync(CancellationToken.None));
        var originalTransfer = failedTransfer.Settings;
        var originalColor = failedColor.Settings;
        var originalCalibration = failedCalibration.Snapshot;

        await Assert.ThrowsAsync<IOException>(() => failedTransfer.SetSettingsAsync(originalTransfer with { RequestBytes = 64 * 1024 }));
        await Assert.ThrowsAsync<IOException>(() => failedColor.SetSettingsAsync(originalColor with { OutputGamma = 1.7 }));
        await Assert.ThrowsAsync<IOException>(() => failedCalibration.ReplaceAsync(
            new ScanCalibrationProfileRepositorySnapshot(
                new Dictionary<string, ScanChannelCalibrationProfile> { ["Green"] = Profile() },
                "Green"),
            CancellationToken.None));

        Assert.Equal(migratedHash, HashFile(settingsPath));
        Assert.Equal(originalTransfer, failedTransfer.Settings);
        Assert.Equal(originalColor, failedColor.Settings);
        var persistedCalibration = failedCalibration.Snapshot;
        Assert.Equal(originalCalibration.SelectedChannel, persistedCalibration.SelectedChannel);
        Assert.Equal(originalCalibration.Profiles.OrderBy(pair => pair.Key), persistedCalibration.Profiles.OrderBy(pair => pair.Key));
        Console.WriteLine("SET003_ORIGINAL_HASH_AND_STATE_PRESERVED");
    }

    public void Dispose()
    {
        foreach (var folder in _createdFolders)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private async Task SeedLegacySettingsAsync(ILocalSettingsService settings)
    {
        await settings.SaveSettingAsync("ScanBulkInReadMode", ScanBulkInReadMode.MultiBuffered);
        await settings.SaveSettingAsync("ScanBulkInRequestBytes", 32 * 1024);
        await settings.SaveSettingAsync("ScanColorManagementEnabled", false);
        await settings.SaveSettingAsync("ScanColorManagementRedWavelengthNm", 700.0);
        await settings.SaveSettingAsync("ScanChannelParameterProfiles", new Dictionary<string, ScanParameterSnapshot> { ["Blue"] = Snapshot() });
        await settings.SaveSettingAsync("ScanCalibrationSelectedChannel", "Blue");
    }

    private string CreateApplicationDataFolder()
    {
        var relativeFolder = Path.Combine("PRISM Utility Tests", "Set003", Guid.NewGuid().ToString("N"));
        _createdFolders.Add(ApplicationDataPathResolver.Resolve(relativeFolder).RootPath);
        return relativeFolder;
    }

    private static LocalSettingsService CreateSettings(string applicationDataFolder, IAtomicFileWriter writer)
        => new(
            new FileService(),
            writer,
            Options.Create(new LocalSettingsOptions
            {
                ApplicationDataFolder = applicationDataFolder,
                LocalSettingsFile = "LocalSettings.json"
            }));

    private static string GetSettingsPath(string applicationDataFolder)
        => ApplicationDataPathResolver.Resolve(applicationDataFolder, "LocalSettings.json").LocalSettingsPath;

    private static string HashFile(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static ScanParameterSnapshot Snapshot()
        => new(10, -2, 3, 4, 5, 30_000);

    private static ScanChannelCalibrationProfile Profile()
        => new(Snapshot(), ScanCalibrationRoiSettings.CreateDefault());

    private sealed class FailAfterTempWriteAtomicFileWriter : IAtomicFileWriter
    {
        public void Write(string folderPath, string fileName, string content)
        {
            Directory.CreateDirectory(folderPath);
            var tempPath = Path.Combine(folderPath, $".{fileName}.{Guid.NewGuid():N}.tmp");
            try
            {
                using var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(content[..Math.Max(1, content.Length / 2)]);
                writer.Flush();
                stream.Flush(flushToDisk: true);
                throw new IOException("Injected SET-003 mid-write failure.");
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
