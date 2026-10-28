using System.Globalization;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;

namespace PRISM_Utility.ViewModels;

public partial class ScanDebugViewModel
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _filmProfileChannelEditorInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _filmProfileProjectedEditorChannels = new(StringComparer.OrdinalIgnoreCase);
    private ScanFilmProfileEditorInputs? _filmProfileEditorInputBaseline;
    private ScanFilmProfileDraft? _filmProfileEditorInputBaselineDraft;
    private ScanFilmProfileDraft? _lastEditorAuthoredFilmProfileDraft;
    private string? _filmProfileDisplayedEditorChannel;

    private (string Name, Func<string> Read, Action<string> Write)[] GetFilmProfileGlobalInputBindings()
        =>
        [
            (nameof(FilmProfileName), () => FilmProfileName, value => FilmProfileName = value),
            (nameof(SelectedRows), () => SelectedRows, value => SelectedRows = value),
            (nameof(SelectedCaptureMode), () => SelectedCaptureMode.ToString(), value => SelectedCaptureMode = Enum.Parse<ScanDebugCaptureMode>(value)),
            (nameof(IsWarmUpEnabled), () => IsWarmUpEnabled.ToString(), value => IsWarmUpEnabled = bool.Parse(value)),
            (nameof(IsAlternateMotorDirectionEnabled), () => IsAlternateMotorDirectionEnabled.ToString(), value => IsAlternateMotorDirectionEnabled = bool.Parse(value)),
            (nameof(SelectedStartingDirection), () => SelectedStartingDirection, value => SelectedStartingDirection = value),
            (nameof(MotorDistancePerLineValue), () => MotorDistancePerLineValue, value => MotorDistancePerLineValue = value),
            (nameof(MotorDistancePerLineUnit), () => MotorDistancePerLineUnit, value => MotorDistancePerLineUnit = value),
            (nameof(MotorIntervalUs), () => MotorIntervalUs, value => MotorIntervalUs = value),
            (nameof(Motor1SpeedValue), () => Motor1SpeedValue, value => Motor1SpeedValue = value),
            (nameof(Motor1SpeedUnit), () => Motor1SpeedUnit, value => Motor1SpeedUnit = value),
            (nameof(Motor1IntervalNs), () => Motor1IntervalNs, value => Motor1IntervalNs = value),
            (nameof(Motor2SpeedValue), () => Motor2SpeedValue, value => Motor2SpeedValue = value),
            (nameof(Motor2SpeedUnit), () => Motor2SpeedUnit, value => Motor2SpeedUnit = value),
            (nameof(Motor2IntervalNs), () => Motor2IntervalNs, value => Motor2IntervalNs = value),
            (nameof(Motor3SpeedValue), () => Motor3SpeedValue, value => Motor3SpeedValue = value),
            (nameof(Motor3SpeedUnit), () => Motor3SpeedUnit, value => Motor3SpeedUnit = value),
            (nameof(Motor3IntervalNs), () => Motor3IntervalNs, value => Motor3IntervalNs = value),
            (nameof(Led1Level), () => Led1Level, value => Led1Level = value),
            (nameof(Led2Level), () => Led2Level, value => Led2Level = value),
            (nameof(Led3Level), () => Led3Level, value => Led3Level = value),
            (nameof(Led4Level), () => Led4Level, value => Led4Level = value),
            (nameof(Led1PulseClock), () => Led1PulseClock, value => Led1PulseClock = value),
            (nameof(Led2PulseClock), () => Led2PulseClock, value => Led2PulseClock = value),
            (nameof(Led3PulseClock), () => Led3PulseClock, value => Led3PulseClock = value),
            (nameof(Led4PulseClock), () => Led4PulseClock, value => Led4PulseClock = value),
            (nameof(IsLed1SteadyEnabled), () => IsLed1SteadyEnabled.ToString(), value => IsLed1SteadyEnabled = bool.Parse(value)),
            (nameof(IsLed2SteadyEnabled), () => IsLed2SteadyEnabled.ToString(), value => IsLed2SteadyEnabled = bool.Parse(value)),
            (nameof(IsLed3SteadyEnabled), () => IsLed3SteadyEnabled.ToString(), value => IsLed3SteadyEnabled = bool.Parse(value)),
            (nameof(IsLed4SteadyEnabled), () => IsLed4SteadyEnabled.ToString(), value => IsLed4SteadyEnabled = bool.Parse(value)),
            (nameof(IsLed1SyncEnabled), () => IsLed1SyncEnabled.ToString(), value => IsLed1SyncEnabled = bool.Parse(value)),
            (nameof(IsLed2SyncEnabled), () => IsLed2SyncEnabled.ToString(), value => IsLed2SyncEnabled = bool.Parse(value)),
            (nameof(IsLed3SyncEnabled), () => IsLed3SyncEnabled.ToString(), value => IsLed3SyncEnabled = bool.Parse(value)),
            (nameof(IsLed4SyncEnabled), () => IsLed4SyncEnabled.ToString(), value => IsLed4SyncEnabled = bool.Parse(value)),
            (nameof(IsChannel1Reversed), () => IsChannel1Reversed.ToString(), value => IsChannel1Reversed = bool.Parse(value)),
            (nameof(IsChannel2Reversed), () => IsChannel2Reversed.ToString(), value => IsChannel2Reversed = bool.Parse(value)),
            (nameof(IsChannel3Reversed), () => IsChannel3Reversed.ToString(), value => IsChannel3Reversed = bool.Parse(value)),
            (nameof(IsChannel4Reversed), () => IsChannel4Reversed.ToString(), value => IsChannel4Reversed = bool.Parse(value)),
            (nameof(IsScanRecipeColorManagementEnabled), () => IsScanRecipeColorManagementEnabled.ToString(), value => IsScanRecipeColorManagementEnabled = bool.Parse(value)),
            (nameof(ScanRecipeRedWavelengthNm), () => ScanRecipeRedWavelengthNm, value => ScanRecipeRedWavelengthNm = value),
            (nameof(ScanRecipeGreenWavelengthNm), () => ScanRecipeGreenWavelengthNm, value => ScanRecipeGreenWavelengthNm = value),
            (nameof(ScanRecipeBlueWavelengthNm), () => ScanRecipeBlueWavelengthNm, value => ScanRecipeBlueWavelengthNm = value),
            (nameof(ScanRecipeOutputGamma), () => ScanRecipeOutputGamma, value => ScanRecipeOutputGamma = value),
            (nameof(SelectedScanRecipeTargetWhitePointMode), () => SelectedScanRecipeTargetWhitePointMode, value => SelectedScanRecipeTargetWhitePointMode = value),
            (nameof(ScanRecipeManualWhitePointColorTemperatureK), () => ScanRecipeManualWhitePointColorTemperatureK, value => ScanRecipeManualWhitePointColorTemperatureK = value),
            (nameof(SelectedProfileAlignmentMode), () => SelectedProfileAlignmentMode.ToString(), value => SelectedProfileAlignmentMode = Enum.Parse<ScanChannelAlignmentMode>(value)),
            (nameof(SelectedProfileDngExportMode), () => SelectedProfileDngExportMode.ToString(), value => SelectedProfileDngExportMode = Enum.Parse<ScanDngExportMode>(value))
        ];

    private ScanFilmProfileEditorInputs CaptureFilmProfileEditorInputs()
    {
        RememberCurrentFilmProfileChannelInputs(_filmProfileDisplayedEditorChannel ?? SelectedCalibrationChannel);
        var global = GetFilmProfileGlobalInputBindings().ToDictionary(binding => binding.Name, binding => binding.Read() ?? string.Empty, StringComparer.Ordinal);
        // Keep this read-only context for motion-field comparisons and older draft
        // files. Restoring a draft must never restore its hardware motor selection.
        global[nameof(SelectedScanMotor)] = SelectedScanMotor;
        global["$MotorDistanceDerived"] = _isMotorDistanceDerivedFromInterval.ToString();
        global["$Motor1SpeedDerived"] = _isMotor1SpeedDerivedFromInterval.ToString();
        global["$Motor2SpeedDerived"] = _isMotor2SpeedDerivedFromInterval.ToString();
        global["$Motor3SpeedDerived"] = _isMotor3SpeedDerivedFromInterval.ToString();
        global["$ManualBlackLevelInput"] = ManualBlackLevelInput;
        global["$ManualWhiteLevelInput"] = ManualWhiteLevelInput;
        global["$ManualReferenceHasLocalEdit"] = _manualReferenceHasLocalEdit.ToString();
        global["$WhiteChannelSettings"] = Newtonsoft.Json.JsonConvert.SerializeObject(_whiteChannelSettings);
        global["VirtualWhiteChannelReversed"] = _isVirtualWhiteChannelReversed.ToString();
        CaptureWhiteLightingEditorInputs(global);
        for (var index = 0; index < 5; index++)
            global[$"AcquisitionLed{index}"] = (AcquisitionChannels.FirstOrDefault(channel => channel.LedIndex == index)?.IsSelected ?? false).ToString();
        return new ScanFilmProfileEditorInputs(global,
            _filmProfileChannelEditorInputs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase), SelectedCalibrationChannel);
    }

    private void AcceptFilmProfileEditorInputBaseline(ScanFilmProfileEditorInputs? savedInputs = null)
    {
        if (savedInputs is null)
        {
            _filmProfileEditorInputBaseline = CaptureFilmProfileEditorInputs();
            _filmProfileEditorInputBaselineDraft = _filmProfileWorkspace.Snapshot.CurrentDraft;
            _lastEditorAuthoredFilmProfileDraft = null;
            return;
        }
        // A picker may stay open while another channel is visited. Keep that channel's
        // original projection so edits made after the captured save stay unsaved.
        var channels = savedInputs.ChannelInputs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (_filmProfileEditorInputBaseline is { } previous)
            foreach (var pair in previous.ChannelInputs)
                channels.TryAdd(pair.Key, pair.Value);
        _filmProfileEditorInputBaseline = savedInputs with { ChannelInputs = channels };
        _filmProfileEditorInputBaselineDraft = _filmProfileWorkspace.Snapshot.BaselineDraft;
    }

    private void ResetFilmProfileEditorInputBaseline()
    {
        _filmProfileChannelEditorInputs.Clear();
        _filmProfileProjectedEditorChannels.Clear();
        _filmProfileDisplayedEditorChannel = SelectedCalibrationChannel;
        if (_filmProfileWorkspace.Snapshot.CurrentDraft.ChannelProfiles.ContainsKey(SelectedCalibrationChannel))
            _filmProfileProjectedEditorChannels.Add(SelectedCalibrationChannel);
        AcceptFilmProfileEditorInputBaseline();
    }

    private void RememberCurrentFilmProfileChannelInputs(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)
            || _filmProfileDisplayedEditorChannel is not null
                && !string.Equals(role, _filmProfileDisplayedEditorChannel, StringComparison.OrdinalIgnoreCase))
            return;
        _filmProfileDisplayedEditorChannel = role;
        _filmProfileChannelEditorInputs[role] = CaptureCurrentFilmProfileChannelInputs(role);
    }

    private IReadOnlyDictionary<string, string> CaptureCurrentFilmProfileChannelInputs(string role)
    {
        var input = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(ExposureMicroseconds)] = ExposureMicroseconds ?? string.Empty,
            [nameof(Adc1Offset)] = Adc1Offset ?? string.Empty,
            [nameof(Adc1Gain)] = Adc1Gain ?? string.Empty,
            [nameof(Adc2Offset)] = Adc2Offset ?? string.Empty,
            [nameof(Adc2Gain)] = Adc2Gain ?? string.Empty,
            [nameof(SysClockMhz)] = SysClockMhz ?? string.Empty
        };
        foreach (var selection in RoiSelectionLabels)
        {
            var range = GetRoiRange(_roiSettings, selection);
            input[$"Roi.{selection}.Start"] = range.Start.ToString(CultureInfo.InvariantCulture);
            input[$"Roi.{selection}.End"] = range.EndInclusive.ToString(CultureInfo.InvariantCulture);
            if (!_roiNumericDrafts.TryGetValue((role.ToUpperInvariant(), selection), out var pending)
                || pending.Start == pending.AppliedRange.Start.ToString(CultureInfo.InvariantCulture)
                    && pending.End == pending.AppliedRange.EndInclusive.ToString(CultureInfo.InvariantCulture))
                continue;
            input[$"PendingRoi.{selection}.Start"] = pending.Start;
            input[$"PendingRoi.{selection}.End"] = pending.End;
            input[$"PendingRoi.{selection}.AppliedStart"] = pending.AppliedRange.Start.ToString(CultureInfo.InvariantCulture);
            input[$"PendingRoi.{selection}.AppliedEnd"] = pending.AppliedRange.EndInclusive.ToString(CultureInfo.InvariantCulture);
        }
        return input;
    }

    private bool HasFilmProfileEditorInputChanges()
    {
        if (_filmProfileEditorInputBaseline is not { } baseline)
            return false;
        var current = CaptureFilmProfileEditorInputs();
        if (!HaveSameFilmProfileInputValues(current.GlobalInputs, baseline.GlobalInputs, global: true))
            return true;
        return current.ChannelInputs.Any(pair => baseline.ChannelInputs.TryGetValue(pair.Key, out var original)
            && !HaveSameFilmProfileInputValues(pair.Value, original));
    }

    private void TryRestoreFilmProfileEditorInputBaselineDraft()
    {
        var current = _filmProfileWorkspace.Snapshot.CurrentDraft;
        if (_filmProfileEditorInputBaselineDraft is not { } baseline
            || !ReferenceEquals(current, _lastEditorAuthoredFilmProfileDraft)
            || HasFilmProfileEditorInputChanges()
            || current.HasSameContentAs(baseline))
            return;
        // Undo only content authored by this editor. A workspace may already contain
        // an external, unsaved profile when its visible input baseline is established.
        _lastProjectedFilmProfileDraft = baseline;
        _lastEditorAuthoredFilmProfileDraft = null;
        _filmProfileWorkspace.SetCurrentDraft(baseline);
    }

    private static bool HaveSameFilmProfileInputValues(IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string> baseline, bool global = false)
    {
        foreach (var key in current.Keys.Concat(baseline.Keys).Distinct(StringComparer.Ordinal))
        {
            if (key.StartsWith('$'))
                continue;
            // Session mode and device-owned transport mapping do not modify
            // the reusable film configuration.
            if (global && key is nameof(SelectedCaptureMode) or nameof(SelectedScanMotor))
                continue;
            if (global && (key is nameof(MotorDistancePerLineValue) or nameof(MotorDistancePerLineUnit))
                && current.GetValueOrDefault("$MotorDistanceDerived") == bool.TrueString
                && baseline.GetValueOrDefault("$MotorDistanceDerived") == bool.TrueString)
                continue;
            if (global && TryGetMotorInputIndex(key, out var motorIndex)
                && (key.EndsWith("IntervalNs", StringComparison.Ordinal)
                    || !string.Equals(current.GetValueOrDefault(nameof(SelectedScanMotor)), $"Motor{motorIndex}", StringComparison.OrdinalIgnoreCase)
                    || current.GetValueOrDefault($"$Motor{motorIndex}SpeedDerived") == bool.TrueString
                        && baseline.GetValueOrDefault($"$Motor{motorIndex}SpeedDerived") == bool.TrueString))
                continue;
            var value = current.GetValueOrDefault(key) ?? string.Empty;
            var original = baseline.GetValueOrDefault(key) ?? string.Empty;
            if (string.Equals(value.Trim(), original.Trim(), StringComparison.Ordinal))
                continue;
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && decimal.TryParse(original, NumberStyles.Float, CultureInfo.InvariantCulture, out var originalNumber)
                && number == originalNumber)
                continue;
            return false;
        }
        return true;
    }

    private static bool TryGetMotorInputIndex(string key, out int index)
    {
        index = key.Length > 6 && key.StartsWith("Motor", StringComparison.Ordinal) && key[5] is >= '1' and <= '3' ? key[5] - '0' : 0;
        return index > 0;
    }

    private void TrackFilmProfileChannelProjection(string role)
    {
        _filmProfileDisplayedEditorChannel = role;
        _filmProfileProjectedEditorChannels.Add(role);
        RememberCurrentFilmProfileChannelInputs(role);
        if (_filmProfileEditorInputBaseline is not { } baseline || baseline.ChannelInputs.ContainsKey(role))
            return;
        var channels = baseline.ChannelInputs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        channels[role] = _filmProfileChannelEditorInputs[role];
        _filmProfileEditorInputBaseline = baseline with { ChannelInputs = channels };
    }

    private bool TryRestoreFilmProfileChannelInputs(string role)
    {
        if (!_filmProfileProjectedEditorChannels.Contains(role)
            || !_filmProfileChannelEditorInputs.TryGetValue(role, out var inputs))
            return false;
        if (!_filmProfileWorkspace.Snapshot.CurrentDraft.ChannelProfiles.ContainsKey(role)
            && TryGetSavedCalibrationProfile(role, out var persisted))
        {
            _selectedCalibrationEditorReferenceRole = role;
            _selectedCalibrationEditorReferenceProfile = persisted;
        }
        ApplyFilmProfileChannelInputs(role, inputs);
        _hasInvalidFilmProfileInput = !TryBuildCurrentFilmProfileDraft(out _, out var validation);
        SetCurrentFilmProfileValidation(validation);
        RefreshFilmProfileWorkspaceProjection();
        return true;
    }

    private void RestoreFilmProfileEditorInputs(ScanFilmProfileEditorInputs inputs)
    {
        if (!TryValidateFilmProfileEditorInputs(inputs, out var error))
            throw new InvalidDataException(error);
        var wasSynchronizing = _isSynchronizingFilmProfileWorkspace;
        _isSynchronizingFilmProfileWorkspace = true;
        try
        {
            _filmProfileChannelEditorInputs.Clear();
            _filmProfileProjectedEditorChannels.Clear();
            foreach (var pair in inputs.ChannelInputs)
            {
                _filmProfileChannelEditorInputs[pair.Key] = pair.Value;
                _filmProfileProjectedEditorChannels.Add(pair.Key);
            }
            var wasApplyingDistance = _isApplyingDerivedMotorDistance;
            var wasApplyingSpeed = _isApplyingDerivedMotorSpeed;
            _isApplyingDerivedMotorDistance = true;
            _isApplyingDerivedMotorSpeed = true;
            try
            {
                foreach (var binding in GetFilmProfileGlobalInputBindings())
                    if (inputs.GlobalInputs.TryGetValue(binding.Name, out var value))
                        binding.Write(value);
                // Interval mirroring may run while the other fields are restored.
                // Reapply the saved display text after those projections have finished.
                if (inputs.GlobalInputs.TryGetValue(nameof(MotorDistancePerLineValue), out var distance))
                    MotorDistancePerLineValue = distance;
            }
            finally
            {
                _isApplyingDerivedMotorDistance = wasApplyingDistance;
                _isApplyingDerivedMotorSpeed = wasApplyingSpeed;
            }
            _isMotorDistanceDerivedFromInterval = ReadDerived("$MotorDistanceDerived");
            _isMotor1SpeedDerivedFromInterval = ReadDerived("$Motor1SpeedDerived");
            _isMotor2SpeedDerivedFromInterval = ReadDerived("$Motor2SpeedDerived");
            _isMotor3SpeedDerivedFromInterval = ReadDerived("$Motor3SpeedDerived");
            // Older editing drafts stored physical motor fields. Preserve their
            // sampling interval when the device now uses a different transport motor.
            if (inputs.GlobalInputs.TryGetValue(nameof(SelectedScanMotor), out var savedMotor)
                && !string.Equals(savedMotor, SelectedScanMotor, StringComparison.Ordinal)
                && inputs.GlobalInputs.TryGetValue(nameof(MotorIntervalUs), out var savedInterval)
                && ScanMotorIntervalText.TryParseMicroseconds(savedInterval, out var intervalNs)
                && intervalNs >= ScanDebugConstants.MotionMinIntervalNs
                && TryParseSelectedScanMotor(out var motorId, out _))
                ApplyMotorSpeedFromIntervalNs(motorId, intervalNs);
            foreach (var channel in AcquisitionChannels)
                if (inputs.GlobalInputs.TryGetValue($"AcquisitionLed{channel.LedIndex}", out var selected))
                    channel.SetSelectedFromOwner(bool.Parse(selected));
            if (!string.IsNullOrWhiteSpace(inputs.SelectedCalibrationChannel) && CalibrationChannelOptions.Contains(inputs.SelectedCalibrationChannel, StringComparer.OrdinalIgnoreCase))
                SelectedCalibrationChannel = CalibrationChannelOptions.First(role => string.Equals(role, inputs.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase));
            if (_filmProfileChannelEditorInputs.TryGetValue(SelectedCalibrationChannel, out var channelInputs))
            {
                ApplyFilmProfileChannelInputs(SelectedCalibrationChannel, channelInputs);
                // Restoring a file is a local editor projection. It needs no repository
                // selection write or hardware read to make the numeric editors available.
                var loadVersion = ++_profileLoadVersion;
                _selectedCalibrationChannelLoad = new CalibrationChannelSelectionLoad(
                    SelectedCalibrationChannel, loadVersion, Volatile.Read(ref _calibrationProjectionVersion), Task.FromResult(true));
            }
            if (inputs.GlobalInputs.TryGetValue("VirtualWhiteChannelReversed", out var whiteReversed))
                _isVirtualWhiteChannelReversed = bool.Parse(whiteReversed);
        }
        finally
        {
            _isSynchronizingFilmProfileWorkspace = wasSynchronizing;
        }
        RestoreWhiteLightingEditorInputs(inputs.GlobalInputs);
        OnPropertyChanged(nameof(CurrentCalibrationChannelReversed));
        NotifyAcquisitionPlanChanged();
        NotifyRoiNumericDraftsChanged();
        RefreshActiveIlluminationChannelBindings();
        UpdateTimingProjectionFromInputs(markEdited: true);
        RestoreFilmProfileManualReferenceInputs(inputs.GlobalInputs);

        bool ReadDerived(string key) => inputs.GlobalInputs.TryGetValue(key, out var value) && bool.TryParse(value, out var derived) && derived;
    }

    private bool TryValidateFilmProfileEditorInputs(ScanFilmProfileEditorInputs? inputs, out string error)
    {
        error = "Saved editor inputs are incomplete or contain an invalid typed value.";
        if (inputs?.GlobalInputs is null || inputs.ChannelInputs is null
            || !string.IsNullOrWhiteSpace(inputs.SelectedCalibrationChannel)
                && !CalibrationChannelOptions.Contains(inputs.SelectedCalibrationChannel, StringComparer.OrdinalIgnoreCase))
            return false;
        if (GetFilmProfileGlobalInputBindings().Any(binding => !inputs.GlobalInputs.ContainsKey(binding.Name)))
            return false;
        string[] metadataKeys = ["$MotorDistanceDerived", "$Motor1SpeedDerived", "$Motor2SpeedDerived", "$Motor3SpeedDerived",
            "$ManualBlackLevelInput", "$ManualWhiteLevelInput", "$ManualReferenceHasLocalEdit"];
        if (metadataKeys.Any(key => !inputs.GlobalInputs.ContainsKey(key))
            || Enumerable.Range(0, ScanDebugConstants.IlluminationChannelCount).Any(index => !inputs.GlobalInputs.ContainsKey($"AcquisitionLed{index}")))
            return false;
        foreach (var pair in inputs.GlobalInputs)
        {
            if (pair.Value is null)
                return false;
            if ((pair.Key.StartsWith("Is", StringComparison.Ordinal)
                    || pair.Key.StartsWith("AcquisitionLed", StringComparison.Ordinal)
                    || pair.Key is "$MotorDistanceDerived" or "$Motor1SpeedDerived" or "$Motor2SpeedDerived" or "$Motor3SpeedDerived" or "$ManualReferenceHasLocalEdit" or "VirtualWhiteChannelReversed")
                && !bool.TryParse(pair.Value, out _))
                return false;
            if (pair.Key == nameof(SelectedProfileAlignmentMode) && !IsDefinedEnum<ScanChannelAlignmentMode>(pair.Value)
                || pair.Key == nameof(SelectedProfileDngExportMode) && !IsDefinedEnum<ScanDngExportMode>(pair.Value)
                || pair.Key == nameof(SelectedCaptureMode) && !IsDefinedEnum<ScanDebugCaptureMode>(pair.Value)
                || pair.Key == "WhiteLighting.Source" && !IsDefinedEnum<WhiteLightSource>(pair.Value))
                return false;
        }
        foreach (var channel in inputs.ChannelInputs)
        {
            if (channel.Value is null || !CalibrationChannelOptions.Contains(channel.Key, StringComparer.OrdinalIgnoreCase))
                return false;
            string[] parameterKeys = [nameof(ExposureMicroseconds), nameof(Adc1Offset), nameof(Adc1Gain), nameof(Adc2Offset), nameof(Adc2Gain), nameof(SysClockMhz)];
            if (parameterKeys.Any(key => !channel.Value.ContainsKey(key)))
                return false;
            foreach (var pair in channel.Value)
                if (pair.Value is null)
                    return false;
            foreach (var selection in RoiSelectionLabels)
            {
                if (!HasInteger($"Roi.{selection}.Start") || !HasInteger($"Roi.{selection}.End"))
                    return false;
                var hasStart = channel.Value.ContainsKey($"PendingRoi.{selection}.Start");
                var hasEnd = channel.Value.ContainsKey($"PendingRoi.{selection}.End");
                if (hasStart != hasEnd || hasStart && (!HasInteger($"PendingRoi.{selection}.AppliedStart") || !HasInteger($"PendingRoi.{selection}.AppliedEnd")))
                    return false;
            }
            bool HasInteger(string key) => channel.Value.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }
        if (!string.IsNullOrWhiteSpace(inputs.SelectedCalibrationChannel)
            && !inputs.ChannelInputs.Keys.Contains(inputs.SelectedCalibrationChannel, StringComparer.OrdinalIgnoreCase))
            return false;
        error = string.Empty;
        return true;

        static bool IsDefinedEnum<T>(string value) where T : struct, Enum
            => Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed);
    }

    private void CaptureWhiteLightingEditorInputs(IDictionary<string, string> inputs)
    {
        inputs["WhiteLighting.Source"] = SelectedWhiteLightSource.ToString();
        foreach (var editor in WhiteLightComponents)
        {
            var prefix = $"WhiteLighting.{editor.Role}.";
            inputs[prefix + "Level"] = editor.Level ?? string.Empty;
            inputs[prefix + "PulseClock"] = editor.PulseClock ?? string.Empty;
            var modeIndex = Array.IndexOf(editor.ModeOptions.ToArray(), editor.Mode);
            inputs[prefix + "Mode"] = modeIndex >= 0
                ? modeIndex.ToString(CultureInfo.InvariantCulture) : editor.Mode ?? string.Empty;
        }
    }

    private void RestoreWhiteLightingEditorInputs(IReadOnlyDictionary<string, string> inputs)
    {
        // These optional keys were not present in earlier editing files.
        if (!inputs.TryGetValue("WhiteLighting.Source", out var sourceText))
            return;
        var source = Enum.Parse<WhiteLightSource>(sourceText);
        if (source != SelectedWhiteLightSource)
        {
            // An unfinished new draft may have saved its selected source only in
            // raw inputs. Use the normal source switch to keep calibration isolated.
            var switched = _filmProfileWorkspace.Snapshot.CurrentDraft.SwitchWhiteLightSource(source);
            _whiteChannelSettings = switched.WhiteChannelSettings;
            _selectedFilmAcquisitionSettings = switched.AcquisitionSettings;
            _lastProjectedFilmProfileDraft = switched;
            _lastEditorAuthoredFilmProfileDraft = switched;
            _filmProfileWorkspace.SetCurrentDraft(switched);
        }
        RefreshWhiteChannelEditors(rebuildLighting: true);
        _isUpdatingWhiteChannel = true;
        try
        {
            foreach (var editor in WhiteLightComponents)
            {
                var prefix = $"WhiteLighting.{editor.Role}.";
                if (inputs.TryGetValue(prefix + "Level", out var level)) editor.Level = level;
                if (inputs.TryGetValue(prefix + "PulseClock", out var pulse)) editor.PulseClock = pulse;
                if (inputs.TryGetValue(prefix + "Mode", out var mode))
                    editor.Mode = int.TryParse(mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                        && index >= 0 && index < editor.ModeOptions.Count ? editor.ModeOptions[index] : mode;
            }
        }
        finally
        {
            _isUpdatingWhiteChannel = false;
        }
        _whiteLightingValidation = WhiteLightComponents.Any(editor => !editor.TryBuild(out _))
            ? "ScanDebug_WhiteLightingInvalid".GetLocalizedOrFallback("Enter a level from 0 to 65535 and a pulse of at least 2 clocks.")
            : string.Empty;
        RefreshWhiteChannelEditors();
        RefreshFilmProfileWorkspaceProjection();
    }

    private void RestoreFilmProfileManualReferenceInputs(IReadOnlyDictionary<string, string> inputs)
    {
        if (!inputs.TryGetValue("$ManualBlackLevelInput", out var black) || !inputs.TryGetValue("$ManualWhiteLevelInput", out var white))
            return;
        _manualReferenceExpectedSnapshot = _filmProfileWorkspace.Snapshot;
        _manualReferenceRole = SelectedCalibrationChannel;
        _manualReferenceSampleSource = null;
        _manualReferenceActionFeedback = false;
        _isLoadingManualReference = true;
        try
        {
            ManualBlackLevelInput = black;
            ManualWhiteLevelInput = white;
        }
        finally
        {
            _isLoadingManualReference = false;
        }
        _manualReferenceHasLocalEdit = inputs.TryGetValue("$ManualReferenceHasLocalEdit", out var edited) && bool.Parse(edited);
        ManualReferenceStatusText = (_manualReferenceHasLocalEdit ? "ScanDebug_ManualReferenceEdited" : "ScanDebug_ManualReferenceLoaded").GetLocalized();
        OnPropertyChanged(nameof(ManualReferenceSourceText));
        OnPropertyChanged(nameof(ManualReferenceLocalEditVisibility));
        OnPropertyChanged(nameof(ManualReferenceFeedbackVisibility));
        NotifyManualReferenceAvailabilityChanged();
    }

    private void ApplyFilmProfileChannelInputs(string role, IReadOnlyDictionary<string, string> input)
    {
        var wasSynchronizing = _isSynchronizingFilmProfileWorkspace;
        _isSynchronizingFilmProfileWorkspace = true;
        try
        {
            _filmProfileDisplayedEditorChannel = role;
            SysClockMhz = Read(nameof(SysClockMhz));
            ExposureMicroseconds = Read(nameof(ExposureMicroseconds));
            Adc1Offset = Read(nameof(Adc1Offset));
            Adc1Gain = Read(nameof(Adc1Gain));
            Adc2Offset = Read(nameof(Adc2Offset));
            Adc2Gain = Read(nameof(Adc2Gain));
            _roiSettings = new ScanCalibrationRoiSettings(
                ReadRange(RoiSelectionBwActive), ReadRange(RoiSelectionBwShield),
                ReadRange(RoiSelectionFocusLeft), ReadRange(RoiSelectionFocusRight), ReadRange(RoiSelectionFocusOverall));
            foreach (var selection in RoiSelectionLabels)
            {
                var key = (role.ToUpperInvariant(), selection);
                _roiNumericDrafts.Remove(key);
                if (input.TryGetValue($"PendingRoi.{selection}.Start", out var start)
                    && input.TryGetValue($"PendingRoi.{selection}.End", out var end))
                    _roiNumericDrafts[key] = new RoiNumericDraft(start, end,
                        new ScanColumnRange(int.Parse(Read($"PendingRoi.{selection}.AppliedStart"), CultureInfo.InvariantCulture), int.Parse(Read($"PendingRoi.{selection}.AppliedEnd"), CultureInfo.InvariantCulture)));
            }
            RefreshRoiStatus();
            UpdateComputedParameterDisplays();
        }
        finally
        {
            _isSynchronizingFilmProfileWorkspace = wasSynchronizing;
        }
        string Read(string key) => input.GetValueOrDefault(key) ?? string.Empty;
        ScanColumnRange ReadRange(string selection) => new(int.Parse(Read($"Roi.{selection}.Start"), CultureInfo.InvariantCulture), int.Parse(Read($"Roi.{selection}.End"), CultureInfo.InvariantCulture));
    }
}
