using System.Diagnostics;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanCalibrationProfileRepository : IScanCalibrationProfileRepository
{
    private const int SchemaVersion = 2;

    private readonly IScanCalibrationProfileStorage _storage;
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private ScanCalibrationProfileRepositorySnapshot _current = CreateSnapshot(
        new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase),
        null);
    private bool _isInitialized;

    public ScanCalibrationProfileRepository(IScanCalibrationProfileStorage storage)
    {
        _storage = storage;
    }

    public ScanCalibrationProfileRepositorySnapshot Snapshot
        => CloneSnapshot(Volatile.Read(ref _current));

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _isInitialized))
            return;

        await _settingsGate.WaitAsync(ct);
        try
        {
            await InitializeUnderGateAsync(ct);
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task<ScanCalibrationProfileRepositorySnapshot> ReadAsync(CancellationToken ct)
    {
        await InitializeAsync(ct);
        return Snapshot;
    }

    public bool TryGetProfile(string channelRole, out ScanChannelCalibrationProfile profile)
    {
        profile = ScanCalibrationProfileSettingsNormalizer.CreateDefaultProfile();
        if (!Volatile.Read(ref _isInitialized))
            return false;

        var role = ScanCalibrationProfileSettingsNormalizer.NormalizeRole(channelRole);
        var current = Volatile.Read(ref _current);
        return !string.IsNullOrEmpty(role) && current.Profiles.TryGetValue(role, out profile!);
    }

    public async Task SaveProfileAsync(string channelRole, ScanChannelCalibrationProfile profile, CancellationToken ct)
    {
        var role = ScanCalibrationProfileSettingsNormalizer.RequireRole(channelRole);
        if (!ScanCalibrationProfileSettingsNormalizer.TryValidateProfile(profile, out var normalized))
            throw new ArgumentOutOfRangeException(nameof(profile), "Calibration profile contains unsupported scan parameters.");

        await _settingsGate.WaitAsync(ct);
        try
        {
            await InitializeUnderGateAsync(ct);
            var current = Volatile.Read(ref _current);
            var nextProfiles = ScanCalibrationProfileSettingsNormalizer.CopyProfiles(current.Profiles);
            nextProfiles[role] = normalized;
            var candidate = CreateSnapshot(nextProfiles, current.SelectedChannel, current.WhiteChannelSettings);
            await SaveDocumentAsync(candidate, ct);
            Volatile.Write(ref _current, candidate);
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task<bool> ClearProfileAsync(string channelRole, CancellationToken ct)
    {
        var role = ScanCalibrationProfileSettingsNormalizer.RequireRole(channelRole);
        await _settingsGate.WaitAsync(ct);
        try
        {
            await InitializeUnderGateAsync(ct);
            var current = Volatile.Read(ref _current);
            if (!current.Profiles.ContainsKey(role))
                return false;

            var nextProfiles = ScanCalibrationProfileSettingsNormalizer.CopyProfiles(current.Profiles);
            nextProfiles.Remove(role);
            var candidate = CreateSnapshot(nextProfiles, current.SelectedChannel, current.WhiteChannelSettings);
            await SaveDocumentAsync(candidate, ct);
            Volatile.Write(ref _current, candidate);
            return true;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task ReplaceAsync(ScanCalibrationProfileRepositorySnapshot snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var nextProfiles = ScanCalibrationProfileSettingsNormalizer.NormalizeReplacementProfiles(snapshot.Profiles);
        var selectedChannel = ScanCalibrationProfileSettingsNormalizer.NormalizeSelectedChannel(snapshot.SelectedChannel);
        if (!WhiteChannelSettingsValidation.TryValidate(snapshot.WhiteChannelSettings, out var field))
            throw new ArgumentOutOfRangeException(nameof(snapshot), $"Invalid White channel settings at {field}.");
        var candidate = CreateSnapshot(nextProfiles, selectedChannel, snapshot.WhiteChannelSettings);

        await _settingsGate.WaitAsync(ct);
        try
        {
            await InitializeUnderGateAsync(ct);
            await SaveDocumentAsync(candidate, ct);
            Volatile.Write(ref _current, candidate);
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task<string?> GetSelectedChannelAsync(CancellationToken ct)
    {
        await InitializeAsync(ct);
        return Volatile.Read(ref _current).SelectedChannel;
    }

    public async Task SetSelectedChannelAsync(string? channelRole, CancellationToken ct)
    {
        var role = ScanCalibrationProfileSettingsNormalizer.RequireRole(channelRole);
        await _settingsGate.WaitAsync(ct);
        try
        {
            await InitializeUnderGateAsync(ct);
            var current = Volatile.Read(ref _current);
            if (string.Equals(current.SelectedChannel, role, StringComparison.OrdinalIgnoreCase))
                return;

            var candidate = CreateSnapshot(current.Profiles, role, current.WhiteChannelSettings);
            await SaveDocumentAsync(candidate, ct);
            Volatile.Write(ref _current, candidate);
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task SetWhiteChannelSettingsAsync(WhiteChannelSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!WhiteChannelSettingsValidation.TryValidate(settings, out var field))
            throw new ArgumentOutOfRangeException(nameof(settings), $"Invalid White channel settings at {field}.");
        await _settingsGate.WaitAsync(ct);
        try
        {
            await InitializeUnderGateAsync(ct);
            var current = Volatile.Read(ref _current);
            var profiles = ScanCalibrationProfileSettingsNormalizer.CopyProfiles(current.Profiles);
            if (settings.ActiveSettings?.Profile is { } profile)
                profiles["White"] = profile;
            else
                profiles.Remove("White");
            var candidate = CreateSnapshot(profiles, current.SelectedChannel, settings);
            await SaveDocumentAsync(candidate, ct);
            Volatile.Write(ref _current, candidate);
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private async Task InitializeUnderGateAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _isInitialized))
            return;

        VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>? document;
        try
        {
            document = await _storage.ReadDocumentAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Failed to read versioned calibration settings: {ex}");
            Volatile.Write(ref _isInitialized, true);
            return;
        }

        if (document is not null)
        {
            if (document.SchemaVersion is not (1 or SchemaVersion)
                || !ScanCalibrationProfileSettingsNormalizer.TryNormalizeDocumentPayload(document.Payload, out var documentProfiles, out var documentSelectedChannel)
                || (document.SchemaVersion == SchemaVersion && !WhiteChannelSettingsValidation.TryValidate(document.Payload?.WhiteChannelSettings, out _)))
            {
                WriteDiagnostic($"Ignoring calibration settings document schema {document.SchemaVersion}.");
                Volatile.Write(ref _isInitialized, true);
                return;
            }

            var whiteSettings = document.SchemaVersion == SchemaVersion ? document.Payload?.WhiteChannelSettings : null;
            if (whiteSettings is not null && whiteSettings.ActiveSettings?.Profile != documentProfiles.GetValueOrDefault("White"))
            {
                WriteDiagnostic("Ignoring calibration settings with mismatched White source profile.");
                Volatile.Write(ref _isInitialized, true);
                return;
            }
            Volatile.Write(ref _current, CreateSnapshot(documentProfiles, documentSelectedChannel, whiteSettings));
            Volatile.Write(ref _isInitialized, true);
            return;
        }

        var currentProfiles = await ReadLegacyAsync(() => _storage.ReadCurrentProfilesAsync(ct), ct, "current calibration profiles");
        var legacyProfiles = currentProfiles is null || ScanCalibrationProfileSettingsNormalizer.LooksLikeLegacySnapshots(currentProfiles)
            ? await ReadLegacyAsync(() => _storage.ReadLegacyProfilesAsync(ct), ct, "legacy calibration profiles")
            : null;
        var selectedChannel = await ReadLegacyAsync(() => _storage.ReadSelectedChannelAsync(ct), ct, "legacy selected calibration channel");
        var profiles = legacyProfiles is not null
            ? ScanCalibrationProfileSettingsNormalizer.MigrateLegacyProfiles(legacyProfiles)
            : ScanCalibrationProfileSettingsNormalizer.NormalizeLoadedProfiles(currentProfiles);
        var normalizedSelectedChannel = ScanCalibrationProfileSettingsNormalizer.NormalizeSelectedChannel(selectedChannel);
        var candidate = CreateSnapshot(profiles, normalizedSelectedChannel);

        await SaveDocumentAsync(candidate, ct);
        Volatile.Write(ref _current, candidate);
        Volatile.Write(ref _isInitialized, true);
    }

    private async Task<T?> ReadLegacyAsync<T>(Func<Task<T?>> read, CancellationToken ct, string description)
    {
        try
        {
            return await read();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"Ignoring malformed {description}: {ex}");
            return default;
        }
    }

    private Task SaveDocumentAsync(ScanCalibrationProfileRepositorySnapshot candidate, CancellationToken ct)
        => _storage.SaveDocumentAsync(
            new VersionedSettingsDocument<ScanCalibrationProfileSettingsPayload>(
                SchemaVersion,
                new ScanCalibrationProfileSettingsPayload(
                    ScanCalibrationProfileSettingsNormalizer.CopyProfiles(candidate.Profiles),
                    candidate.SelectedChannel,
                    candidate.WhiteChannelSettings)),
            ct);

    private static ScanCalibrationProfileRepositorySnapshot CreateSnapshot(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile> profiles,
        string? selectedChannel,
        WhiteChannelSettings? whiteChannelSettings = null)
        => new(ScanCalibrationProfileSettingsNormalizer.CopyProfiles(profiles), selectedChannel,
            whiteChannelSettings is null && !profiles.ContainsKey("White") ? null
                : (whiteChannelSettings ?? WhiteChannelSettings.FromLegacy(profiles.GetValueOrDefault("White")))
                    .WithActiveProfile(profiles.GetValueOrDefault("White")));

    private static ScanCalibrationProfileRepositorySnapshot CloneSnapshot(ScanCalibrationProfileRepositorySnapshot snapshot)
        => CreateSnapshot(snapshot.Profiles, snapshot.SelectedChannel, snapshot.WhiteChannelSettings);

    private static void WriteDiagnostic(string message)
    {
        Debug.WriteLine($"[ScanCalibrationProfileRepository] {message}");
        Trace.WriteLine($"[ScanCalibrationProfileRepository] {message}");
    }
}
