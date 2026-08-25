using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Models;
using PRISM_Utility.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set001")]
[Trait("Category", "LocalSettings")]
[Trait("Category", "Atomic")]
public sealed class Set001LocalSettingsAtomicPersistenceTests : IDisposable
{
    private readonly List<string> _createdFolders = [];

    [Fact]
    public async Task Set001Baseline_SingleSaveCanBeReadByFreshServiceWithExistingJsonShape()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var settingsPath = GetSettingsPath(applicationDataFolder);
        var writer = new AtomicFileWriter();
        var service = CreateService(applicationDataFolder, writer);

        await service.SaveSettingAsync("answer", 42);

        var document = JObject.Parse(await File.ReadAllTextAsync(settingsPath));
        Assert.Equal("42", document["answer"]?.Value<string>());
        Assert.Equal(42, await CreateService(applicationDataFolder, writer).ReadSettingAsync<int?>("answer"));
    }

    [Fact]
    public async Task Set001Baseline_MalformedExistingDocumentPropagatesCurrentReadFailure()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var settingsPath = GetSettingsPath(applicationDataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await File.WriteAllTextAsync(settingsPath, "{ malformed");

        await Assert.ThrowsAsync<Newtonsoft.Json.JsonReaderException>(
            () => CreateService(applicationDataFolder, new AtomicFileWriter()).ReadSettingAsync<int?>("answer"));
    }

    [Fact]
    public async Task Set001Baseline_SemanticallyInvalidPersistedValuePropagatesCurrentReadFailure()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var settingsPath = GetSettingsPath(applicationDataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await File.WriteAllTextAsync(settingsPath, "{\"answer\":\"\\\"not-an-integer\\\"\"}");

        await Assert.ThrowsAsync<JsonReaderException>(
            () => CreateService(applicationDataFolder, new AtomicFileWriter()).ReadSettingAsync<int?>("answer"));
    }

    [Fact]
    public void Set001DependencyInjection_ResolvesLocalSettingsServiceWithAtomicWriter()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
        services.Configure<LocalSettingsOptions>(options =>
        {
            options.ApplicationDataFolder = applicationDataFolder;
            options.LocalSettingsFile = "LocalSettings.json";
        });

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<ILocalSettingsService>();

        Assert.IsType<LocalSettingsService>(resolved);
        Assert.Same(resolved, provider.GetRequiredService<ILocalSettingsService>());
    }

    [Fact]
    public void Set001AtomicWriter_FirstCreateAndReplaceLeaveOnlyTargetFile()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var settingsPath = GetSettingsPath(applicationDataFolder);
        var writer = new AtomicFileWriter();

        writer.Write(Path.GetDirectoryName(settingsPath)!, Path.GetFileName(settingsPath), "{\"value\":1}");
        writer.Write(Path.GetDirectoryName(settingsPath)!, Path.GetFileName(settingsPath), "{\"value\":2}");

        Assert.Equal("{\"value\":2}", File.ReadAllText(settingsPath));
        Assert.Empty(GetTaskOwnedTemps(settingsPath));
    }

    [Fact]
    public async Task Set001ManualFilesystemHarness_ConcurrentSavesThenWriterFailurePreserveDurableState()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var settingsPath = GetSettingsPath(applicationDataFolder);
        var service = CreateService(applicationDataFolder, new AtomicFileWriter());
        var saves = Enumerable.Range(0, 32)
            .Select(index => service.SaveSettingAsync($"key-{index:D2}", index))
            .ToArray();
        var allSaves = Task.WhenAll(saves);

        var completed = await Task.WhenAny(allSaves, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(allSaves, completed);
        await allSaves;

        var document = JObject.Parse(await File.ReadAllTextAsync(settingsPath));
        Assert.Equal(32, document.Properties().Count());
        foreach (var index in Enumerable.Range(0, 32))
            Assert.Equal(index.ToString(), document[$"key-{index:D2}"]?.Value<string>());

        Console.WriteLine("CONCURRENT_PASS");

        var originalHash = HashFile(settingsPath);
        var failingService = CreateService(applicationDataFolder, new FailAfterTempWriteAtomicFileWriter());

        await Assert.ThrowsAsync<IOException>(() => failingService.SaveSettingAsync("key-00", 999));

        Assert.Equal(originalHash, HashFile(settingsPath));
        Assert.Equal(0, await failingService.ReadSettingAsync<int?>("key-00"));
        Console.WriteLine("ORIGINAL_PRESERVED");

        Assert.Empty(GetTaskOwnedTemps(settingsPath));
        Console.WriteLine("TEMP_CLEAN");
    }

    [Fact]
    public async Task Set001Failure_NewKeyRollbackDoesNotAdvertiseAnUnpersistedValue()
    {
        var applicationDataFolder = CreateApplicationDataFolder();
        var settingsPath = GetSettingsPath(applicationDataFolder);
        var durableService = CreateService(applicationDataFolder, new AtomicFileWriter());
        await durableService.SaveSettingAsync("durable", 7);
        var originalHash = HashFile(settingsPath);
        var failingService = CreateService(applicationDataFolder, new FailAfterTempWriteAtomicFileWriter());

        await Assert.ThrowsAsync<IOException>(() => failingService.SaveSettingAsync("new-key", 99));

        Assert.Equal(originalHash, HashFile(settingsPath));
        Assert.Equal(7, await failingService.ReadSettingAsync<int?>("durable"));
        Assert.Null(await failingService.ReadSettingAsync<int?>("new-key"));
        Assert.Empty(GetTaskOwnedTemps(settingsPath));
    }

    public void Dispose()
    {
        foreach (var folder in _createdFolders)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private string CreateApplicationDataFolder()
    {
        var relativeFolder = Path.Combine("PRISM Utility Tests", "Set001", Guid.NewGuid().ToString("N"));
        _createdFolders.Add(ApplicationDataPathResolver.Resolve(relativeFolder).RootPath);
        return relativeFolder;
    }

    private static LocalSettingsService CreateService(string applicationDataFolder, IAtomicFileWriter writer)
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

    private static IReadOnlyList<string> GetTaskOwnedTemps(string settingsPath)
        => Directory.Exists(Path.GetDirectoryName(settingsPath))
            ? Directory.EnumerateFiles(
                    Path.GetDirectoryName(settingsPath)!,
                    $".{Path.GetFileName(settingsPath)}.*.tmp")
                .ToArray()
            : [];

    private static string HashFile(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

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
                throw new IOException("Injected mid-write failure.");
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
