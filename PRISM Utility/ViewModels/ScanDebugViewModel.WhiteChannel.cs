using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public sealed record WhiteLightSourceOption(WhiteLightSource Source, string DisplayName);

public sealed class WhiteLightComponentEditor : ObservableObject
{
    private readonly ScanDebugViewModel _owner;
    private string _level;
    private string _pulseClock;
    private string _mode;

    public WhiteLightComponentEditor(ScanDebugViewModel owner, string role, WhiteLightComponentSettings component)
    {
        _owner = owner;
        Role = role;
        _level = component.Level.ToString(CultureInfo.InvariantCulture);
        _pulseClock = component.PulseClock.ToString(CultureInfo.InvariantCulture);
        _mode = owner.IlluminationWorkModeOptions[(int)component.Mode];
    }

    public string Role { get; }
    public string DisplayName => $"{ScanDebugViewModel.GetCalibrationChannelDisplayName(Role)} · {_owner.GetPhysicalLedName(Role)}";
    public string LevelLabel => "ScanDebug_IlluminationLevelHeader".GetLocalized();
    public string PulseLabel => "ScanDebug_IlluminationPulseClockHeader".GetLocalized();
    public string ModeLabel => "ScanDebug_IlluminationWorkModeHeader".GetLocalized();
    public IReadOnlyList<string> ModeOptions => _owner.IlluminationWorkModeOptions;
    public string Level { get => _level; set { if (SetProperty(ref _level, value)) _owner.UpdateWhiteLighting(); } }
    public string PulseClock { get => _pulseClock; set { if (SetProperty(ref _pulseClock, value)) _owner.UpdateWhiteLighting(); } }
    public string Mode { get => _mode; set { if (SetProperty(ref _mode, value)) _owner.UpdateWhiteLighting(); } }

    public bool TryBuild(out WhiteLightComponentSettings settings)
    {
        settings = new();
        if (!ushort.TryParse(Level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
            || !uint.TryParse(PulseClock, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pulse)
            || pulse < ScanDebugConstants.IlluminationMinSyncPulseClock)
            return false;
        var mode = Array.IndexOf(ModeOptions.ToArray(), Mode);
        if (mode < 0) return false;
        settings = new(level, pulse, (WhiteLightDriveMode)mode);
        return true;
    }
}

public partial class ScanDebugViewModel
{
    private WhiteChannelSettings? _whiteChannelSettings;
    private bool _isUpdatingWhiteChannel;
    private string _whiteLightingValidation = string.Empty;

    public ObservableCollection<WhiteLightComponentEditor> WhiteLightComponents { get; } = new();
    public WhiteLightSource SelectedWhiteLightSource => _whiteChannelSettings?.Source ?? WhiteLightSource.DedicatedLed;
    public bool IsWhiteCalibrationChannel => string.Equals(SelectedCalibrationChannel, "White", StringComparison.OrdinalIgnoreCase);
    public Visibility WhiteChannelVisibility => IsWhiteCalibrationChannel ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SingleChannelLightingVisibility => IsWhiteCalibrationChannel ? Visibility.Collapsed : Visibility.Visible;
    public string WhiteLightingValidation => _whiteLightingValidation;
    public bool CanEditWhiteSource => AreScanAcquisitionSettingsEditable && !IsCalibrationRepositoryOperationRunning && !IsFilmProfileOperationRunning;
    public Visibility WhiteLightingValidationVisibility => string.IsNullOrEmpty(_whiteLightingValidation) ? Visibility.Collapsed : Visibility.Visible;
    public IReadOnlyList<WhiteLightSourceOption> WhiteLightSourceOptions => Enum.GetValues<WhiteLightSource>()
        .Where(source => IsWhiteSourceAvailable(source) || source == SelectedWhiteLightSource)
        .Select(source => new WhiteLightSourceOption(source, GetWhiteSourceName(source))).ToArray();
    public WhiteLightSourceOption? SelectedWhiteLightSourceOption => WhiteLightSourceOptions.FirstOrDefault(option => option.Source == SelectedWhiteLightSource);
    public Visibility WhiteSourceSelectorVisibility => IsWhiteCalibrationChannel && WhiteLightSourceOptions.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WhiteSourceTextVisibility => IsWhiteCalibrationChannel && WhiteLightSourceOptions.Count <= 1 ? Visibility.Visible : Visibility.Collapsed;
    public string WhiteSourceSummary => GetWhiteSourceName(SelectedWhiteLightSource);
    public string WhiteSourceAvailabilityText => IsWhiteSourceAvailable(SelectedWhiteLightSource) ? string.Empty
        : "ScanDebug_WhiteSourceUnavailable".GetLocalizedOrFallback("The configured light source is not mapped on this device.");
    public Visibility WhiteSourceAvailabilityVisibility => IsWhiteCalibrationChannel && !IsWhiteSourceAvailable(SelectedWhiteLightSource) ? Visibility.Visible : Visibility.Collapsed;

    private static string GetWhiteSourceName(WhiteLightSource source) => source == WhiteLightSource.RgbComposite
        ? "ScanDebug_WhiteSourceRgb".GetLocalizedOrFallback("RGB combined light")
        : "ScanDebug_WhiteSourceDedicated".GetLocalizedOrFallback("Dedicated white LED");

    internal string GetPhysicalLedName(string role)
    {
        var roles = _deviceSettings.Settings.Normalize().ChannelRoles.ToArray();
        var indexes = Enumerable.Range(0, Math.Min(4, roles.Length)).Where(i => string.Equals(roles[i], role, StringComparison.OrdinalIgnoreCase)).ToArray();
        return indexes.Length == 1 ? $"LED{indexes[0] + 1}" : "ScanDebug_ChannelLedBindingUnassigned".GetLocalized();
    }

    private bool IsWhiteSourceAvailable(WhiteLightSource source)
    {
        var roles = _deviceSettings.Settings.Normalize().ChannelRoles.ToArray();
        return (source == WhiteLightSource.DedicatedLed ? new[] { "White" } : new[] { "Red", "Green", "Blue" })
            .All(role => roles.Count(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase)) == 1);
    }

    private WhiteChannelLightingSettings CreateWhiteLighting(WhiteLightSource source)
    {
        WhiteLightComponentSettings Read(string role)
        {
            var index = Array.FindIndex(GetEffectiveDeviceChannelRoles(), candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index >= 4) return new();
            _ = ushort.TryParse(GetIlluminationLevelInput(index), out var level);
            _ = uint.TryParse(GetIlluminationPulseClockInput(index), out var pulse);
            return new(level, Math.Max(2u, pulse), GetIlluminationSyncInput(index) ? WhiteLightDriveMode.Synchronized
                : GetIlluminationSteadyInput(index) ? WhiteLightDriveMode.Steady : WhiteLightDriveMode.Off);
        }
        return source == WhiteLightSource.DedicatedLed ? new(DedicatedLed: Read("White"))
            : new(Red: Read("Red"), Green: Read("Green"), Blue: Read("Blue"));
    }

    private void RefreshWhiteChannelEditors(bool rebuildLighting = false)
    {
        if (rebuildLighting)
        {
            _isUpdatingWhiteChannel = true;
            try
            {
                WhiteLightComponents.Clear();
                var lighting = _whiteChannelSettings?.ActiveSettings?.Lighting ?? CreateWhiteLighting(SelectedWhiteLightSource);
                if (_whiteChannelSettings is not null && _whiteChannelSettings.ActiveSettings?.Lighting is null)
                    _whiteChannelSettings = _whiteChannelSettings.WithActiveLighting(lighting);
                if (SelectedWhiteLightSource == WhiteLightSource.DedicatedLed)
                    WhiteLightComponents.Add(new(this, "White", lighting.DedicatedLed ?? new()));
                else
                {
                    WhiteLightComponents.Add(new(this, "Red", lighting.Red ?? new()));
                    WhiteLightComponents.Add(new(this, "Green", lighting.Green ?? new()));
                    WhiteLightComponents.Add(new(this, "Blue", lighting.Blue ?? new()));
                }
                _whiteLightingValidation = string.Empty;
            }
            finally { _isUpdatingWhiteChannel = false; }
        }
        foreach (var property in new[] { nameof(IsWhiteCalibrationChannel), nameof(WhiteChannelVisibility), nameof(SingleChannelLightingVisibility),
            nameof(WhiteLightSourceOptions), nameof(SelectedWhiteLightSourceOption), nameof(WhiteSourceSelectorVisibility), nameof(WhiteSourceTextVisibility),
            nameof(WhiteSourceSummary), nameof(WhiteSourceAvailabilityText), nameof(WhiteSourceAvailabilityVisibility),
            nameof(WhiteLightingValidation), nameof(WhiteLightingValidationVisibility), nameof(CurrentCalibrationLedName) })
            OnPropertyChanged(property);
    }

    internal void UpdateWhiteLighting()
    {
        if (_isUpdatingWhiteChannel || _isSynchronizingFilmProfileWorkspace) return;
        var values = new Dictionary<string, WhiteLightComponentSettings>();
        foreach (var editor in WhiteLightComponents)
        {
            if (!editor.TryBuild(out var value))
            {
                _whiteLightingValidation = "ScanDebug_WhiteLightingInvalid".GetLocalizedOrFallback("Enter a level from 0 to 65535 and a pulse of at least 2 clocks.");
                RefreshWhiteChannelEditors();
                RefreshFilmProfileWorkspaceProjection();
                return;
            }
            values[editor.Role] = value;
        }
        _whiteLightingValidation = string.Empty;
        var lighting = new WhiteChannelLightingSettings(values.GetValueOrDefault("White"), values.GetValueOrDefault("Red"), values.GetValueOrDefault("Green"), values.GetValueOrDefault("Blue"));
        _whiteChannelSettings = (_whiteChannelSettings ?? WhiteChannelSettings.FromLegacy(null, _selectedFilmAcquisitionSettings)).WithActiveLighting(lighting);
        CommitWhiteSettingsToDraft();
        SynchronizeFilmProfileDraftFromInputs();
        RefreshWhiteChannelEditors();
    }

    private void CommitWhiteSettingsToDraft()
    {
        var current = _filmProfileWorkspace.Snapshot.CurrentDraft;
        var acquisition = (current.AcquisitionSettings ?? _selectedFilmAcquisitionSettings ?? CreateDeviceDefaultAcquisitionSettings()) with { WhiteChannelSettings = _whiteChannelSettings };
        var draft = new ScanFilmProfileDraft(current.ProfileName, current.SavedAtUtc, current.ChannelProfiles,
            SelectedCalibrationChannel, acquisition, current.ScanRecipeSettings);
        _selectedFilmAcquisitionSettings = acquisition;
        _lastProjectedFilmProfileDraft = draft;
        _lastEditorAuthoredFilmProfileDraft = draft;
        _filmProfileWorkspace.SetCurrentDraft(draft);
        RefreshFilmProfileWorkspaceProjection();
    }

    public async Task ChangeWhiteLightSourceAsync(WhiteLightSource source)
    {
        if (source == SelectedWhiteLightSource || _isUpdatingWhiteChannel) return;
        if (!CanEditWhiteSource)
        {
            RefreshWhiteChannelEditors();
            return;
        }
        if (!string.IsNullOrEmpty(_whiteLightingValidation))
        {
            RefreshWhiteChannelEditors();
            return;
        }
        var synchronized = SynchronizeFilmProfileDraftFromInputs();
        var hasChangedChannelInput = _filmProfileEditorInputBaseline?.ChannelInputs.TryGetValue("White", out var baselineInputs) == true
            && !HaveSameFilmProfileInputValues(CaptureCurrentFilmProfileChannelInputs("White"), baselineInputs!);
        if (!synchronized && hasChangedChannelInput || _manualReferenceHasLocalEdit)
        {
            StatusText = "ScanDebug_WhiteSourcePendingInput".GetLocalizedOrFallback("Finish or revert the current channel edits before changing the light source.");
            RefreshWhiteChannelEditors();
            return;
        }
        var current = _filmProfileWorkspace.Snapshot.CurrentDraft;
        var settings = _whiteChannelSettings ?? WhiteChannelSettings.FromLegacy(current.ChannelProfiles.GetValueOrDefault("White"), current.AcquisitionSettings);
        if (settings.GetSourceSettings(source)?.Lighting is null)
            settings = settings.WithSourceSettings(source, (settings.GetSourceSettings(source) ?? new()) with { Lighting = CreateWhiteLighting(source) });
        _whiteChannelSettings = settings;
        CommitWhiteSettingsToDraft();
        var switched = _filmProfileWorkspace.Snapshot.CurrentDraft.SwitchWhiteLightSource(source);
        _whiteChannelSettings = switched.WhiteChannelSettings;
        _copiedUnverifiedCalibrationProfiles.Remove("White");
        _filmProfileChannelEditorInputs.Remove("White");
        _filmProfileProjectedEditorChannels.Remove("White");
        ClearSelectedCalibrationEditorBaseline();
        InvalidateColumnSampleOwnership();
        AdvanceCalibrationProjectionVersion();
        _filmProfileWorkspace.SetCurrentDraft(switched);
        ApplyDraftToFields(switched);
        SelectedCalibrationChannel = "White";
        RefreshActiveIlluminationChannels();
        RefreshWhiteChannelEditors(rebuildLighting: true);
        await HandleSelectedCalibrationChannelChangedAsync(SelectedCalibrationChannel, Volatile.Read(ref _calibrationProjectionVersion));
        LoadManualReferenceFromCurrentTarget();
        InvalidatePendingCalibrationIfStale();
    }

    private bool TryGetSavedCalibrationProfile(string role, out ScanChannelCalibrationProfile profile)
    {
        if (!string.Equals(role, "White", StringComparison.OrdinalIgnoreCase))
            return _calibrationProfiles.TryGetProfile(role, out profile!);
        var saved = _calibrationProfiles.Snapshot.WhiteChannelSettings;
        if (saved?.GetSourceSettings(SelectedWhiteLightSource)?.Profile is { } sourceProfile)
        {
            profile = sourceProfile;
            return true;
        }
        if (saved is null && SelectedWhiteLightSource == WhiteLightSource.DedicatedLed)
            return _calibrationProfiles.TryGetProfile(role, out profile!);
        profile = null!;
        return false;
    }

    private async Task PrepareWhitePresetWriteAsync(string role)
    {
        if (!string.Equals(role, "White", StringComparison.OrdinalIgnoreCase)) return;
        var settings = (_calibrationProfiles.Snapshot.WhiteChannelSettings ?? WhiteChannelSettings.FromLegacy(
            _calibrationProfiles.TryGetProfile("White", out var legacy) ? legacy : null)) with { Source = SelectedWhiteLightSource };
        settings = settings.WithActiveLighting(_whiteChannelSettings?.ActiveSettings?.Lighting ?? CreateWhiteLighting(SelectedWhiteLightSource));
        await _calibrationProfiles.SetWhiteChannelSettingsAsync(settings, CancellationToken.None);
    }

    private ScanFilmAcquisitionSettings BuildCurrentChannelAcquisition()
    {
        if (!string.IsNullOrEmpty(_whiteLightingValidation)) throw new InvalidOperationException(_whiteLightingValidation);
        if (!TryBuildFilmAcquisitionSettings(out var settings, out _))
            throw new InvalidOperationException("ScanDebug_WhiteLightingInvalid".GetLocalized());
        var roles = _deviceSettings.Settings.Normalize().ChannelRoles.ToArray();
        return settings with { Led1ChannelColor = roles[0], Led2ChannelColor = roles[1], Led3ChannelColor = roles[2], Led4ChannelColor = roles[3], WhiteChannelSettings = _whiteChannelSettings };
    }

    private async Task ApplyWhiteCaptureSourceAsync(IScanSessionService session, string role, CancellationToken ct, bool applyParameters = true)
    {
        if (!string.Equals(role, "White", StringComparison.OrdinalIgnoreCase)) return;
        var settings = BuildCurrentChannelAcquisition();
        var index = Array.FindIndex(GetEffectiveDeviceChannelRoles(), candidate => candidate == "White");
        var state = ScanIlluminationService.BuildChannelState(settings, "White", (byte)(index < 0 ? 4 : index));
        ScanChannelCalibrationProfile? profile = null;
        if (applyParameters && !TryGetCalibrationProfile("White", out profile!))
            throw new InvalidOperationException("ScanDebug_WhiteSourceProfileRequired".GetLocalizedFormatOrFallback(
                "Configure the white channel exposure and calibration for {0} first.", GetWhiteSourceName(SelectedWhiteLightSource)));
        if (profile is not null)
            await _parameters.ApplyAsync(session, profile.Parameters, ct);
        await _illumination.ApplyStateWithSafeTransitionAsync(session, state, ct);
    }

    private async Task<T> RunWithWhiteCaptureSourceAsync<T>(IScanSessionService session, string role, CancellationToken ct,
        Func<Task<T>> operation)
    {
        if (!string.Equals(role, "White", StringComparison.OrdinalIgnoreCase)) return await operation();
        var settings = BuildCurrentChannelAcquisition();
        var index = Array.FindIndex(GetEffectiveDeviceChannelRoles(), candidate => candidate == "White");
        _ = ScanIlluminationService.BuildChannelState(settings, "White", (byte)(index < 0 ? 4 : index));
        if (!TryGetCalibrationProfile("White", out _))
            throw new InvalidOperationException("ScanDebug_WhiteSourceProfileRequired".GetLocalizedFormatOrFallback(
                "Configure the white channel exposure and calibration for {0} first.", GetWhiteSourceName(SelectedWhiteLightSource)));
        var original = await _illumination.GetStateAsync(session, ct);
        var originalParameters = await _parameters.LoadAsync(session, ct);
        Exception? primaryFailure = null;
        try
        {
            await ApplyWhiteCaptureSourceAsync(session, role, ct);
            return await operation();
        }
        catch (Exception failure)
        {
            primaryFailure = failure;
            throw;
        }
        finally
        {
            var cleanupFailures = new List<Exception>();
            try { await _illumination.TurnOffAsync(session, CancellationToken.None); }
            catch (Exception failure) { cleanupFailures.Add(failure); }
            try { await _parameters.ApplyAsync(session, originalParameters, CancellationToken.None); }
            catch (Exception failure) { cleanupFailures.Add(failure); }
            try { await _illumination.RestoreStateAsync(session, original, CancellationToken.None); }
            catch (Exception failure) { cleanupFailures.Add(failure); }
            if (cleanupFailures.Count > 0)
                throw new AggregateException("White channel capture state could not be fully restored.",
                    primaryFailure is null ? cleanupFailures : new[] { primaryFailure }.Concat(cleanupFailures));
        }
    }
}
