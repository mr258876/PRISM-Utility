using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PRISM_Utility.Core.Contracts.Services;

namespace PrismUtility.Core.Tests;

internal class Set003RecordingLocalSettings : ILocalSettingsService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _readFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _saveFailures = new(StringComparer.Ordinal);

    public List<string> SavedKeys { get; } = [];

    public string Hash
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_values.OrderBy(pair => pair.Key)))));

    public void Seed<T>(string key, T value)
        => _values[key] = JsonConvert.SerializeObject(value);

    public T? Get<T>(string key)
        => _values.TryGetValue(key, out var serialized) ? JsonConvert.DeserializeObject<T>(serialized) : default;

    public bool Contains(string key)
        => _values.ContainsKey(key);

    public void FailRead(string key, Exception exception)
        => _readFailures[key] = exception;

    public void FailSave(string key, Exception exception)
        => _saveFailures[key] = exception;

    public void ClearSaveFailure(string key)
        => _saveFailures.Remove(key);

    public Task<T?> ReadSettingAsync<T>(string key)
    {
        if (_readFailures.TryGetValue(key, out var exception))
            return Task.FromException<T?>(exception);

        return Task.FromResult(Get<T>(key));
    }

    public Task SaveSettingAsync<T>(string key, T value)
    {
        SavedKeys.Add(key);
        if (_saveFailures.TryGetValue(key, out var exception))
            return Task.FromException(exception);

        Seed(key, value);
        return Task.CompletedTask;
    }

    protected void Remove(string key)
        => _values.Remove(key);
}

internal sealed class Set003CleanupRecordingLocalSettings : Set003RecordingLocalSettings, ILocalSettingsLegacyCleanupService
{
    public Exception? CleanupFailure { get; set; }
    public int CleanupCount { get; private set; }

    public Task RemoveSettingsAsync(IReadOnlyCollection<string> keys)
    {
        CleanupCount++;
        if (CleanupFailure is not null)
            return Task.FromException(CleanupFailure);

        foreach (var key in keys)
            Remove(key);

        return Task.CompletedTask;
    }
}
