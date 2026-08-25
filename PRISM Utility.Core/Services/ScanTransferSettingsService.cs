using System.Diagnostics;
using PRISM_Utility.Core.Configuration;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanTransferSettingsService : IScanTransferSettingsService
{
    private const int SchemaVersion = 1;
    private const string DocumentKey = "ScanTransferSettingsDocument";
    private const string BulkInReadModeKey = "ScanBulkInReadMode";
    private const string RequestBytesKey = "ScanBulkInRequestBytes";
    private const string OutstandingReadsKey = "ScanBulkInOutstandingReads";
    private const string TimeoutMsKey = "ScanBulkInTimeoutMs";
    private const string RawIoEnabledKey = "ScanBulkInRawIoEnabled";

    private static readonly string[] LegacyKeys = [BulkInReadModeKey, RequestBytesKey, OutstandingReadsKey, TimeoutMsKey, RawIoEnabledKey];

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private bool _isInitialized;

    public event EventHandler? BulkInReadModeChanged;

    public ScanBulkInReadMode BulkInReadMode => Settings.ReadMode;

    public ScanBulkInTransferOptions DefaultSettings => ScanTransferDefaults.Settings;

    public ScanBulkInTransferOptions Settings { get; private set; } = ScanTransferDefaults.Settings;

    public ScanTransferSettingsService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _settingsGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InitializeUnderGateAsync();
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task SetBulkInReadModeAsync(ScanBulkInReadMode mode, CancellationToken cancellationToken = default)
    {
        var changed = false;
        await _settingsGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InitializeUnderGateAsync();
            var next = NormalizeOrDefault(Settings with { ReadMode = mode });
            if (Settings == next)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            await SaveDocumentAsync(next);
            Settings = next;
            changed = true;
        }
        finally
        {
            _settingsGate.Release();
        }

        if (changed)
            BulkInReadModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetSettingsAsync(ScanBulkInTransferOptions settings, CancellationToken cancellationToken = default)
    {
        var changed = false;
        await _settingsGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InitializeUnderGateAsync();
            var normalized = NormalizeOrDefault(settings);
            if (Settings == normalized)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            await SaveDocumentAsync(normalized);
            Settings = normalized;
            changed = true;
        }
        finally
        {
            _settingsGate.Release();
        }

        if (changed)
            BulkInReadModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task InitializeUnderGateAsync()
    {
        if (_isInitialized)
            return;

        VersionedSettingsDocument<ScanBulkInTransferOptions>? document;
        try
        {
            document = await _localSettingsService.ReadSettingAsync<VersionedSettingsDocument<ScanBulkInTransferOptions>>(DocumentKey);
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Failed to read versioned transfer settings: {ex}");
            _isInitialized = true;
            return;
        }

        if (document is not null)
        {
            var payload = document.Payload;
            if (document.SchemaVersion != SchemaVersion || payload is null || !IsValidDocumentPayload(payload))
            {
                WriteDiagnostic($"Ignoring transfer settings document schema {document.SchemaVersion}.");
                _isInitialized = true;
                return;
            }

            Settings = payload;
            _isInitialized = true;
            return;
        }

        var legacy = await ReadLegacySettingsAsync();
        await SaveDocumentAsync(legacy.Settings);
        if (legacy.HasValues)
            await CleanupLegacySettingsAsync();

        Settings = legacy.Settings;
        _isInitialized = true;
    }

    private async Task<(ScanBulkInTransferOptions Settings, bool HasValues)> ReadLegacySettingsAsync()
    {
        var mode = await ReadLegacyAsync<ScanBulkInReadMode?>(BulkInReadModeKey);
        var requestBytes = await ReadLegacyAsync<int?>(RequestBytesKey);
        var outstandingReads = await ReadLegacyAsync<int?>(OutstandingReadsKey);
        var timeoutMs = await ReadLegacyAsync<int?>(TimeoutMsKey);
        var rawIoEnabled = await ReadLegacyAsync<bool?>(RawIoEnabledKey);
        var hasValues = mode.HasValue || requestBytes.HasValue || outstandingReads.HasValue || timeoutMs.HasValue || rawIoEnabled.HasValue;
        return (NormalizeOrDefault(new ScanBulkInTransferOptions(
            mode ?? DefaultSettings.ReadMode,
            requestBytes ?? DefaultSettings.RequestBytes,
            outstandingReads ?? DefaultSettings.OutstandingReads,
            timeoutMs ?? DefaultSettings.TimeoutMs,
            rawIoEnabled ?? DefaultSettings.RawIoEnabled)), hasValues);
    }

    private async Task<T?> ReadLegacyAsync<T>(string key)
    {
        try
        {
            return await _localSettingsService.ReadSettingAsync<T>(key);
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Ignoring malformed transfer legacy setting '{key}': {ex}");
            return default;
        }
    }

    private Task SaveDocumentAsync(ScanBulkInTransferOptions settings)
        => _localSettingsService.SaveSettingAsync(DocumentKey, new VersionedSettingsDocument<ScanBulkInTransferOptions>(SchemaVersion, settings));

    private async Task CleanupLegacySettingsAsync()
    {
        if (_localSettingsService is not ILocalSettingsLegacyCleanupService cleaner)
            return;

        try
        {
            await cleaner.RemoveSettingsAsync(LegacyKeys);
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Transfer legacy cleanup failed after document commit: {ex}");
        }
    }

    private ScanBulkInTransferOptions NormalizeOrDefault(ScanBulkInTransferOptions? settings)
    {
        var candidate = settings ?? DefaultSettings;
        return new ScanBulkInTransferOptions(
            candidate.ReadMode is ScanBulkInReadMode.SingleRequest or ScanBulkInReadMode.MultiBuffered ? candidate.ReadMode : DefaultSettings.ReadMode,
            candidate.RequestBytes > 0 ? candidate.RequestBytes : DefaultSettings.RequestBytes,
            candidate.OutstandingReads > 0 ? candidate.OutstandingReads : DefaultSettings.OutstandingReads,
            candidate.TimeoutMs > 0 ? candidate.TimeoutMs : DefaultSettings.TimeoutMs,
            candidate.RawIoEnabled);
    }

    private static bool IsValidDocumentPayload(ScanBulkInTransferOptions? settings)
        => settings is not null
            && settings.ReadMode is ScanBulkInReadMode.SingleRequest or ScanBulkInReadMode.MultiBuffered
            && settings.RequestBytes > 0
            && settings.OutstandingReads > 0
            && settings.TimeoutMs > 0;

    private static void WriteDiagnostic(string message)
    {
        Debug.WriteLine($"[ScanTransferSettingsService] {message}");
        Trace.WriteLine($"[ScanTransferSettingsService] {message}");
    }
}
