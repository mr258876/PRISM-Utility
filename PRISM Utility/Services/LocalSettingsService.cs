using Microsoft.Extensions.Options;
using Newtonsoft.Json;

using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;
using Windows.Storage;

namespace PRISM_Utility.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private readonly IFileService _fileService;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly LocalSettingsOptions _options;

    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private IDictionary<string, object> _settings;
    private readonly SemaphoreSlim _settingsGate = new(1, 1);

    private bool _isInitialized;

    public LocalSettingsService(
        IFileService fileService,
        IAtomicFileWriter atomicFileWriter,
        IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _atomicFileWriter = atomicFileWriter;
        _options = options.Value;

        var paths = ApplicationDataPathResolver.Resolve(_options.ApplicationDataFolder, _options.LocalSettingsFile);
        _applicationDataFolder = paths.RootPath;
        _localsettingsFile = paths.LocalSettingsFileName;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeUnderGateAsync()
    {
        if (_isInitialized)
            return;

        _settings = await Task.Run(
                () => _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile))
            ?? new Dictionary<string, object>();
        _isInitialized = true;
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                return await Json.ToObjectAsync<T>((string)obj);
            }
        }
        else
        {
            await _settingsGate.WaitAsync();
            try
            {
                await InitializeUnderGateAsync();

                if (_settings.TryGetValue(key, out var obj))
                    return await Json.ToObjectAsync<T>((string)obj);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        return default;
    }

    public async Task SaveSettingAsync<T>(string key, T value)
    {
        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value);
        }
        else
        {
            await _settingsGate.WaitAsync();
            try
            {
                await InitializeUnderGateAsync();

                var serializedValue = await Json.StringifyAsync(value);
                var hadPreviousValue = _settings.TryGetValue(key, out var previousValue);
                _settings[key] = serializedValue;

                try
                {
                    var serializedSnapshot = JsonConvert.SerializeObject(new Dictionary<string, object>(_settings));
                    await Task.Run(
                        () => _atomicFileWriter.Write(_applicationDataFolder, _localsettingsFile, serializedSnapshot));
                }
                catch
                {
                    if (hadPreviousValue)
                        _settings[key] = previousValue!;
                    else
                        _settings.Remove(key);

                    throw;
                }
            }
            finally
            {
                _settingsGate.Release();
            }
        }
    }
}
