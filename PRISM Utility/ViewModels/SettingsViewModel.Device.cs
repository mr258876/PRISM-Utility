using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class SettingsViewModel
{
    public const string DeviceChannelsNavigationParameter = AppNavigationTarget.DeviceSettingsParameter;
    private const string ScopeDevice = "device";
    private const string ScopeDngGeometry = "dng-geometry";
    private readonly IScanDeviceSettingsService? _deviceSettingsService;
    private readonly IScanDngGeometrySettingsService? _dngGeometrySettingsService;
    private ScanDeviceSettings? _loadedDeviceSettings;
    private ScanDngGeometrySettings? _loadedDngGeometrySettings;
    private bool _isLoadingDeviceSettings;

    public IReadOnlyList<string> DeviceMotorOptions { get; } = ["Motor1", "Motor2", "Motor3"];
    public IReadOnlyList<string> DeviceDirectionOptions { get; } = ["Dir0", "Dir1"];
    public IReadOnlyList<string> DeviceChannelRoleOptions { get; } = ["Red", "Green", "Blue", "White", "IR", "Unused"];
    public ObservableCollection<DeviceMotorMechanicalInput> DeviceMotorMechanics { get; } = [];
    public IAsyncRelayCommand SaveDeviceSettingsCommand { get; private set; } = null!;
    public IRelayCommand RevertDeviceSettingsCommand { get; private set; } = null!;
    public IRelayCommand RestoreDeviceSettingsDefaultsCommand { get; private set; } = null!;
    public IRelayCommand RestoreDngGeometryDefaultsCommand { get; private set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveDeviceSettings))]
    public partial bool IsDeviceSettingsLoaded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveDeviceSettings))]
    public partial bool IsSavingDeviceSettings { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveDeviceSettings))]
    public partial bool HasDeviceSettingsChanges { get; set; }

    public bool CanSaveDeviceSettings => IsDeviceSettingsLoaded && HasDeviceSettingsChanges && !IsSavingDeviceSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeviceSettingsValidationError))]
    public partial string DeviceSettingsValidationMessage { get; set; } = string.Empty;

    public bool HasDeviceSettingsValidationError => !string.IsNullOrEmpty(DeviceSettingsValidationMessage);

    [ObservableProperty]
    public partial string DeviceSettingsStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceDefaultScanMotor { get; set; } = "Motor2";

    [ObservableProperty]
    public partial string DeviceLeftFocusMotor { get; set; } = "Motor1";

    [ObservableProperty]
    public partial string DeviceRightFocusMotor { get; set; } = "Motor3";

    [ObservableProperty]
    public partial string DeviceZPositiveDirection { get; set; } = "Dir0";

    [ObservableProperty]
    public partial string DeviceTiltPositiveDirection { get; set; } = "Dir0";

    [ObservableProperty]
    public partial string DeviceChannel1Role { get; set; } = "Blue";

    [ObservableProperty]
    public partial string DeviceChannel2Role { get; set; } = "White";

    [ObservableProperty]
    public partial string DeviceChannel3Role { get; set; } = "Red";

    [ObservableProperty]
    public partial string DeviceChannel4Role { get; set; } = "Green";

    [ObservableProperty]
    public partial string DngActiveStart { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngActiveEndInclusive { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngMaskedRanges { get; set; } = string.Empty;

    partial void OnDeviceDefaultScanMotorChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceLeftFocusMotorChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceRightFocusMotorChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceZPositiveDirectionChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceTiltPositiveDirectionChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceChannel1RoleChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceChannel2RoleChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceChannel3RoleChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDeviceChannel4RoleChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDngActiveStartChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDngActiveEndInclusiveChanged(string value) => MarkDeviceSettingsEdited();
    partial void OnDngMaskedRangesChanged(string value) => MarkDeviceSettingsEdited();

    private void InitializeDeviceSettingsEditor()
    {
        for (var motorId = 0; motorId < 3; motorId++)
        {
            var input = new DeviceMotorMechanicalInput(motorId);
            input.PropertyChanged += (_, _) => MarkDeviceSettingsEdited();
            DeviceMotorMechanics.Add(input);
        }

        SaveDeviceSettingsCommand = new AsyncRelayCommand(SaveDeviceSettingsAsync);
        RevertDeviceSettingsCommand = new RelayCommand(RevertDeviceSettings);
        RestoreDeviceSettingsDefaultsCommand = new RelayCommand(RestoreDeviceSettingsDefaults);
        RestoreDngGeometryDefaultsCommand = new RelayCommand(RestoreDngGeometryDefaults);
        if (_deviceSettingsService is not null)
            QueueSettingsOperation(ScopeDevice, "Settings_PersistenceScopeDevice", LoadDeviceSettingsAsync);
    }

    private async Task LoadDeviceSettingsAsync(CancellationToken cancellationToken)
    {
        if (_deviceSettingsService is null)
            return;

        await _deviceSettingsService.InitializeAsync();
        if (_dngGeometrySettingsService is not null)
            await _dngGeometrySettingsService.InitializeAsync();
        await RunOnUiAsync(() =>
        {
            _loadedDeviceSettings = _deviceSettingsService.Settings.Normalize();
            _loadedDngGeometrySettings = _dngGeometrySettingsService?.Settings;
            ApplyDeviceSettings(_loadedDeviceSettings);
            if (_loadedDngGeometrySettings is not null)
                ApplyDngGeometrySettings(_loadedDngGeometrySettings);
            IsDeviceSettingsLoaded = true;
        }, cancellationToken);
    }

    private void ApplyDeviceSettings(ScanDeviceSettings settings)
    {
        _isLoadingDeviceSettings = true;
        try
        {
            var normalized = settings.Normalize();
            var mapping = normalized.FocusMotorMapping!;
            DeviceDefaultScanMotor = GetDeviceMotorOption(normalized.DefaultScanMotorId!.Value);
            DeviceLeftFocusMotor = GetDeviceMotorOption(mapping.LeftMotorId);
            DeviceRightFocusMotor = GetDeviceMotorOption(mapping.RightMotorId);
            DeviceZPositiveDirection = mapping.ZPositiveDirection ? "Dir1" : "Dir0";
            DeviceTiltPositiveDirection = mapping.TiltPositiveDirection ? "Dir1" : "Dir0";
            DeviceChannel1Role = normalized.Channel1Role;
            DeviceChannel2Role = normalized.Channel2Role;
            DeviceChannel3Role = normalized.Channel3Role;
            DeviceChannel4Role = normalized.Channel4Role;
            for (byte motor = 0; motor < DeviceMotorMechanics.Count; motor++)
                DeviceMotorMechanics[motor].Apply(normalized.GetMotorSettings(motor));

            DeviceSettingsValidationMessage = string.Empty;
            DeviceSettingsStatusMessage = string.Empty;
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private string GetDeviceMotorOption(byte motorId)
        => motorId < DeviceMotorOptions.Count ? DeviceMotorOptions[motorId] : string.Empty;

    private void MarkDeviceSettingsEdited()
    {
        if (_isLoadingDeviceSettings || !IsDeviceSettingsLoaded)
            return;

        DeviceSettingsValidationMessage = string.Empty;
        HasDeviceSettingsChanges = !TryBuildDeviceSettings(out var settings, out _) || settings != _loadedDeviceSettings
            || (_dngGeometrySettingsService is not null
                && (!TryBuildDngGeometrySettings(out var geometry, out _) || !GeometryEquals(geometry, _loadedDngGeometrySettings)));
        DeviceSettingsStatusMessage = HasDeviceSettingsChanges
            ? "Settings_DeviceUnsaved".GetLocalizedOrFallback("Changes have not been saved.")
            : string.Empty;
    }

    private async Task SaveDeviceSettingsAsync()
    {
        if (!CanSaveDeviceSettings || _deviceSettingsService is null)
            return;

        if (!TryBuildDeviceSettings(out var settings, out var error))
        {
            DeviceSettingsValidationMessage = error;
            return;
        }

        ScanDngGeometrySettings? geometry = null;
        if (_dngGeometrySettingsService is not null && !TryBuildDngGeometrySettings(out geometry, out error))
        {
            DeviceSettingsValidationMessage = error;
            return;
        }

        IsSavingDeviceSettings = true;
        try
        {
            if (settings != _loadedDeviceSettings)
                QueueSettingsOperation(ScopeDevice, "Settings_PersistenceScopeDevice", async cancellationToken =>
                {
                    await _deviceSettingsService.SetSettingsAsync(settings);
                    await RunOnUiAsync(() =>
                    {
                        // Preserve edits made while the save was in flight.
                        _loadedDeviceSettings = settings;
                        MarkDeviceSettingsEdited();
                    }, cancellationToken);
                });
            if (_dngGeometrySettingsService is not null && geometry is not null && !GeometryEquals(geometry, _loadedDngGeometrySettings))
            {
                QueueSettingsOperation(ScopeDngGeometry, "Settings_PersistenceScopeDngGeometry", async cancellationToken =>
                {
                    await _dngGeometrySettingsService.SetSettingsAsync(geometry);
                    await RunOnUiAsync(() =>
                    {
                        _loadedDngGeometrySettings = geometry;
                        MarkDeviceSettingsEdited();
                    }, cancellationToken);
                });
            }
            await FlushSettingsPersistenceAsync();
            if (!HasDeviceSettingsChanges)
                DeviceSettingsStatusMessage = "Settings_DeviceSaved".GetLocalizedOrFallback("Saved to this computer.");
        }
        finally
        {
            IsSavingDeviceSettings = false;
        }
    }

    private void RevertDeviceSettings()
    {
        if (!CanSaveDeviceSettings || _loadedDeviceSettings is null)
            return;

        ApplyDeviceSettings(_loadedDeviceSettings);
        if (_loadedDngGeometrySettings is not null)
            ApplyDngGeometrySettings(_loadedDngGeometrySettings);
        MarkDeviceSettingsEdited();
        ClearSettingsPersistenceError(ScopeDevice);
        ClearSettingsPersistenceError(ScopeDngGeometry);
    }

    private void RestoreDeviceSettingsDefaults()
    {
        if (!IsDeviceSettingsLoaded || IsSavingDeviceSettings || _deviceSettingsService is null)
            return;
        ApplyDeviceSettings(_deviceSettingsService.DefaultSettings);
        MarkDeviceSettingsEdited();
    }

    private void RestoreDngGeometryDefaults()
    {
        if (!IsDeviceSettingsLoaded || IsSavingDeviceSettings || _dngGeometrySettingsService is null)
            return;
        ApplyDngGeometrySettings(_dngGeometrySettingsService.DefaultSettings);
        MarkDeviceSettingsEdited();
    }

    private void ApplyDngGeometrySettings(ScanDngGeometrySettings settings)
    {
        _isLoadingDeviceSettings = true;
        try
        {
            DngActiveStart = settings.ActiveRange.Start.ToString(CultureInfo.InvariantCulture);
            DngActiveEndInclusive = settings.ActiveRange.EndInclusive.ToString(CultureInfo.InvariantCulture);
            DngMaskedRanges = string.Join(", ", settings.MaskedBlackRanges.Select(range => $"{range.Start}-{range.EndInclusive}"));
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private static bool GeometryEquals(ScanDngGeometrySettings? first, ScanDngGeometrySettings? second)
        => first is not null && second is not null
           && first.ActiveRange == second.ActiveRange
           && first.MaskedBlackRanges.SequenceEqual(second.MaskedBlackRanges);

    private bool TryBuildDngGeometrySettings(out ScanDngGeometrySettings settings, out string error)
    {
        settings = ScanDngGeometrySettings.CreateDefault();
        error = string.Empty;
        var lastColumn = ScanDebugConstants.DecodedPixelsPerLine - 1;
        if (!int.TryParse(DngActiveStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || start < 0 || start > lastColumn)
        {
            error = "DeviceConfiguration_DngStartInvalid".GetLocalizedFormatOrFallback("Active start column must be an integer from 0 to {0}.", lastColumn);
            return false;
        }
        if (!int.TryParse(DngActiveEndInclusive, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end)
            || end < 0 || end > lastColumn)
        {
            error = "DeviceConfiguration_DngEndInvalid".GetLocalizedFormatOrFallback("Active end column must be an integer from 0 to {0}.", lastColumn);
            return false;
        }
        if (end < start)
        {
            error = "DeviceConfiguration_DngActiveOrderInvalid".GetLocalizedOrFallback("Active end column must be at least the start column.");
            return false;
        }

        var parts = DngMaskedRanges.Split(',', StringSplitOptions.TrimEntries);
        var ranges = new List<ScanColumnRange>();
        foreach (var part in parts)
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length != 2
                || !int.TryParse(bounds[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rangeStart)
                || !int.TryParse(bounds[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rangeEnd)
                || rangeStart < 0 || rangeEnd > lastColumn || rangeEnd < rangeStart)
                break;
            ranges.Add(new ScanColumnRange(rangeStart, rangeEnd));
        }
        if (ranges.Count != parts.Length || ranges.Count is < 1 or > ScanDngGeometrySettings.MaxMaskedBlackRangeCount)
        {
            error = "DeviceConfiguration_DngMaskedInvalid".GetLocalizedFormatOrFallback("Enter 1–4 comma-separated start-end ranges, within 0–{0}.", lastColumn);
            return false;
        }
        if (ranges.Any(range => range.Start <= end && start <= range.EndInclusive))
        {
            error = "DeviceConfiguration_DngMaskedOverlap".GetLocalizedOrFallback("Masked black ranges must be outside the active columns.");
            return false;
        }
        settings = new ScanDngGeometrySettings(new ScanColumnRange(start, end), ranges.ToArray()).Normalize();
        return true;
    }

    private bool TryBuildDeviceSettings(out ScanDeviceSettings settings, out string error)
    {
        settings = ScanDeviceSettings.CreateDefault();
        error = string.Empty;
        var scanMotor = IndexOfDeviceOption(DeviceMotorOptions, DeviceDefaultScanMotor);
        var leftMotor = IndexOfDeviceOption(DeviceMotorOptions, DeviceLeftFocusMotor);
        var rightMotor = IndexOfDeviceOption(DeviceMotorOptions, DeviceRightFocusMotor);
        if (scanMotor < 0 || leftMotor < 0 || rightMotor < 0
            || !DeviceDirectionOptions.Contains(DeviceZPositiveDirection)
            || !DeviceDirectionOptions.Contains(DeviceTiltPositiveDirection))
        {
            error = "Settings_DeviceSelectMotors".GetLocalizedOrFallback("Select a motor and direction for each function.");
            return false;
        }

        var mechanics = new ScanMotorMechanicalSettings[3];
        for (var motor = 0; motor < mechanics.Length; motor++)
        {
            if (motor >= DeviceMotorMechanics.Count || !DeviceMotorMechanics[motor].TryBuild(out mechanics[motor]))
            {
                error = "Settings_DeviceInvalidMechanics".GetLocalizedOrFallback("Steps, microsteps and lead must be positive numbers. Steps and microsteps must be whole numbers.");
                return false;
            }
        }

        var roles = new[] { DeviceChannel1Role, DeviceChannel2Role, DeviceChannel3Role, DeviceChannel4Role };
        if (roles.Any(role => !DeviceChannelRoleOptions.Contains(role)))
        {
            error = "Settings_DeviceSelectRoles".GetLocalizedOrFallback("Select a role for each illumination channel.");
            return false;
        }

        settings = new ScanDeviceSettings(mechanics[0], mechanics[1], mechanics[2],
            roles[0], roles[1], roles[2], roles[3],
            new ScanFocusMotorMapping((byte)leftMotor, (byte)rightMotor, DeviceZPositiveDirection == "Dir1", DeviceTiltPositiveDirection == "Dir1"),
            (byte)scanMotor);
        var validation = settings.ValidateMappings();
        if (!validation.IsValid)
        {
            error = validation.Error == ScanDeviceSettingsValidationError.DuplicateChannelRoles
                ? "Settings_DeviceDuplicateRoles".GetLocalizedOrFallback("Each illumination role can be assigned once. Use Unused for spare channels.")
                : "Settings_DeviceDuplicateMotors".GetLocalizedOrFallback("Film transport, left focus and right focus must use different motors.");
            return false;
        }

        return true;
    }

    private static int IndexOfDeviceOption(IReadOnlyList<string> options, string selected)
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (options[index] == selected)
                return index;
        }

        return -1;
    }
}

public sealed class DeviceMotorMechanicalInput(int motorId) : ObservableObject
{
    private string _stepsPerRevolution = "200";
    private string _microsteps = "16";
    private string _leadLengthMm = "8";

    public string DisplayName => $"Motor{motorId + 1}";
    public string StepsPerRevolution { get => _stepsPerRevolution; set => SetProperty(ref _stepsPerRevolution, value); }
    public string Microsteps { get => _microsteps; set => SetProperty(ref _microsteps, value); }
    public string LeadLengthMm { get => _leadLengthMm; set => SetProperty(ref _leadLengthMm, value); }

    public void Apply(ScanMotorMechanicalSettings settings)
    {
        StepsPerRevolution = settings.StepsPerRevolution.ToString(CultureInfo.InvariantCulture);
        Microsteps = settings.Microsteps.ToString(CultureInfo.InvariantCulture);
        LeadLengthMm = settings.LeadLengthMm.ToString("R", CultureInfo.InvariantCulture);
    }

    public bool TryBuild(out ScanMotorMechanicalSettings settings)
    {
        settings = ScanMotorMechanicalSettings.CreateDefault();
        if (!uint.TryParse(StepsPerRevolution, out var steps) || steps == 0
            || !uint.TryParse(Microsteps, out var microsteps) || microsteps == 0
            || !(double.TryParse(LeadLengthMm, NumberStyles.Float, CultureInfo.CurrentCulture, out var lead)
                 || double.TryParse(LeadLengthMm, NumberStyles.Float, CultureInfo.InvariantCulture, out lead))
            || !double.IsFinite(lead) || lead <= 0)
            return false;

        settings = new ScanMotorMechanicalSettings(steps, microsteps, lead);
        return true;
    }
}
