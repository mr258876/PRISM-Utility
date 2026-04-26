using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PRISM_Utility.Services;

public sealed class ScanChannelParameterProfileService : IScanChannelParameterProfileService
{
    private const string ProfilesKey = "ScanChannelParameterProfiles";
    private const string SelectedCalibrationChannelKey = "ScanCalibrationSelectedChannel";
    private const int ExchangeSchemaVersion = 1;

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;
    private string? _selectedCalibrationChannel;

    private readonly Dictionary<string, ScanChannelCalibrationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ScanChannelCalibrationProfile> Profiles => _profiles;

    public ScanChannelParameterProfileService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _initializeGate.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            var saved = await _localSettingsService.ReadSettingAsync<Dictionary<string, ScanChannelCalibrationProfile>>(ProfilesKey);
            _profiles.Clear();
            if (saved is not null)
            {
                foreach (var pair in saved)
                {
                    var role = NormalizeRole(pair.Key);
                    if (!string.IsNullOrEmpty(role) && TryNormalizeProfile(pair.Value, out var normalized))
                        _profiles[role] = normalized;
                }
            }
            else
            {
                var legacySaved = await _localSettingsService.ReadSettingAsync<Dictionary<string, ScanParameterSnapshot>>(ProfilesKey);
                if (legacySaved is not null)
                {
                    foreach (var pair in legacySaved)
                    {
                        var role = NormalizeRole(pair.Key);
                        if (!string.IsNullOrEmpty(role) && TryNormalizeSnapshot(pair.Value, out var normalizedSnapshot))
                        {
                            _profiles[role] = new ScanChannelCalibrationProfile(
                                normalizedSnapshot,
                                ScanCalibrationRoiSettings.CreateDefault().Normalize());
                        }
                    }

                    if (_profiles.Count > 0)
                        await _localSettingsService.SaveSettingAsync(ProfilesKey, _profiles);
                }
            }

            if (saved is not null)
                await _localSettingsService.SaveSettingAsync(ProfilesKey, _profiles);

            var selected = await _localSettingsService.ReadSettingAsync<string>(SelectedCalibrationChannelKey);
            _selectedCalibrationChannel = NormalizeRole(selected);
            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public bool TryGetProfile(string channelRole, out ScanChannelCalibrationProfile profile)
    {
        profile = new ScanChannelCalibrationProfile(
            new ScanParameterSnapshot(ScanDebugConstants.MinExposureTicks, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
            ScanCalibrationRoiSettings.CreateDefault());
        if (!_isInitialized)
            return false;

        var role = NormalizeRole(channelRole);
        if (string.IsNullOrWhiteSpace(role)
            || !_profiles.TryGetValue(role, out var loaded)
            || loaded is null)
        {
            return false;
        }

        if (!TryNormalizeProfile(loaded, out profile))
            return false;

        return true;
    }

    public async Task SaveProfileAsync(string channelRole, ScanChannelCalibrationProfile profile)
    {
        await InitializeAsync();

        var role = NormalizeRole(channelRole);
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Channel role cannot be empty.", nameof(channelRole));

        if (!TryNormalizeProfile(profile, out var normalized))
            throw new ArgumentOutOfRangeException(nameof(profile), "Calibration profile contains unsupported scan parameters.");

        _profiles[role] = normalized;
        await _localSettingsService.SaveSettingAsync(ProfilesKey, _profiles);
    }

    public async Task<bool> ClearProfileAsync(string channelRole)
    {
        await InitializeAsync();

        var role = NormalizeRole(channelRole);
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Channel role cannot be empty.", nameof(channelRole));

        var removed = _profiles.Remove(role);
        if (!removed)
            return false;

        await _localSettingsService.SaveSettingAsync(ProfilesKey, _profiles);
        return true;
    }

    public async Task ExportProfilesAsync(ScanFilmParameterProfileSet profileSet)
    {
        await InitializeAsync();

        var normalized = NormalizeImportedProfileSet(profileSet);
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("JSON file", new List<string> { ".json" });
        picker.SuggestedFileName = BuildSuggestedFileName(normalized.ProfileName);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return;

        var json = await PRISM_Utility.Core.Helpers.Json.StringifyAsync(normalized);
        await FileIO.WriteTextAsync(file, json);
    }

    public async Task<ScanFilmParameterProfileSet?> ImportProfilesAsync()
    {
        await InitializeAsync();

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return null;

        var json = await FileIO.ReadTextAsync(file);
        var loaded = await PRISM_Utility.Core.Helpers.Json.ToObjectAsync<ScanFilmParameterProfileSet>(json);
        return loaded is null ? null : NormalizeImportedProfileSet(loaded);
    }

    public async Task ReplaceProfilesAsync(ScanFilmParameterProfileSet profileSet)
    {
        await InitializeAsync();

        var normalized = NormalizeImportedProfileSet(profileSet);
        _profiles.Clear();
        foreach (var pair in normalized.ChannelProfiles)
        {
            _profiles[pair.Key] = pair.Value;
        }

        _selectedCalibrationChannel = NormalizeRole(normalized.SelectedCalibrationChannel);
        await _localSettingsService.SaveSettingAsync(ProfilesKey, _profiles);
        if (!string.IsNullOrWhiteSpace(_selectedCalibrationChannel))
            await _localSettingsService.SaveSettingAsync(SelectedCalibrationChannelKey, _selectedCalibrationChannel);
    }

    public async Task<string?> GetSelectedCalibrationChannelAsync()
    {
        await InitializeAsync();
        return _selectedCalibrationChannel;
    }

    public async Task SetSelectedCalibrationChannelAsync(string channelRole)
    {
        await InitializeAsync();

        var role = NormalizeRole(channelRole);
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Channel role cannot be empty.", nameof(channelRole));

        _selectedCalibrationChannel = role;
        await _localSettingsService.SaveSettingAsync(SelectedCalibrationChannelKey, role);
    }

    private static string NormalizeRole(string? channelRole)
        => string.IsNullOrWhiteSpace(channelRole) ? string.Empty : channelRole.Trim();

    private static string BuildSuggestedFileName(string profileName)
    {
        var safeName = string.IsNullOrWhiteSpace(profileName)
            ? "film_profile"
            : string.Concat(profileName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? "film_profile" : safeName;
    }

    private static ScanFilmParameterProfileSet NormalizeImportedProfileSet(ScanFilmParameterProfileSet profileSet)
    {
        var normalizedProfiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        if (profileSet.ChannelProfiles is not null)
        {
            foreach (var pair in profileSet.ChannelProfiles)
            {
                var role = NormalizeRole(pair.Key);
                if (!string.IsNullOrEmpty(role) && TryNormalizeProfile(pair.Value, out var normalized))
                    normalizedProfiles[role] = normalized;
            }
        }

        return new ScanFilmParameterProfileSet(
            ExchangeSchemaVersion,
            string.IsNullOrWhiteSpace(profileSet.ProfileName) ? "Untitled Film Profile" : profileSet.ProfileName.Trim(),
            profileSet.SavedAtUtc == default ? DateTimeOffset.Now : profileSet.SavedAtUtc,
            normalizedProfiles,
            NormalizeRole(profileSet.SelectedCalibrationChannel));
    }

    private static bool TryNormalizeProfile(ScanChannelCalibrationProfile? profile, out ScanChannelCalibrationProfile normalized)
    {
        if (profile is null)
        {
            normalized = new ScanChannelCalibrationProfile(
                new ScanParameterSnapshot(ScanDebugConstants.MinExposureTicks, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz),
                ScanCalibrationRoiSettings.CreateDefault().Normalize());
            return false;
        }

        var isSnapshotValid = TryNormalizeSnapshot(profile.Parameters, out var normalizedSnapshot);
        var roiSettings = profile.RoiSettings;
        var isRoiValid = true;
        if (roiSettings is null)
        {
            roiSettings = ScanCalibrationRoiSettings.CreateDefault();
            isRoiValid = false;
        }

        normalized = new ScanChannelCalibrationProfile(normalizedSnapshot, roiSettings.Normalize());
        return isSnapshotValid && isRoiValid;
    }

    private static bool TryNormalizeSnapshot(ScanParameterSnapshot? snapshot, out ScanParameterSnapshot normalized)
    {
        if (snapshot is null)
        {
            normalized = new ScanParameterSnapshot(ScanDebugConstants.MinExposureTicks, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);
            return false;
        }

        return ScanDebugValidation.TryNormalizeSnapshot(snapshot, out normalized);
    }
}
