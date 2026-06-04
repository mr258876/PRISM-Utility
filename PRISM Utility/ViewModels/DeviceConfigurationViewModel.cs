using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.ViewModels;

public partial class DeviceConfigurationViewModel : ObservableRecipient
{
    private readonly IScanDeviceSettingsService _deviceSettingsService;
    private readonly IScanDngGeometrySettingsService _dngGeometrySettingsService;
    private bool _isLoadingDeviceSettings;
    private bool _isLoadingDngGeometrySettings;

    [ObservableProperty]
    public partial string Motor1StepsPerRevolution { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor1Microsteps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor1LeadLengthMm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor2StepsPerRevolution { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor2Microsteps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor2LeadLengthMm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor3StepsPerRevolution { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor3Microsteps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motor3LeadLengthMm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel1Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel2Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel3Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeviceChannel4Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngActiveStart { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngActiveEndInclusive { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DngMaskedRanges { get; set; } = string.Empty;

    public ObservableCollection<string> ChannelRoleOptions { get; } = new() { "Red", "Green", "Blue", "White", "IR", "Unused" };

    public IAsyncRelayCommand RestoreDeviceSettingsDefaultsCommand { get; }

    public IAsyncRelayCommand RestoreDngGeometryDefaultsCommand { get; }

    public DeviceConfigurationViewModel(IScanDeviceSettingsService deviceSettingsService, IScanDngGeometrySettingsService dngGeometrySettingsService)
    {
        _deviceSettingsService = deviceSettingsService;
        _dngGeometrySettingsService = dngGeometrySettingsService;
        RestoreDeviceSettingsDefaultsCommand = new AsyncRelayCommand(RestoreDeviceSettingsDefaultsAsync);
        RestoreDngGeometryDefaultsCommand = new AsyncRelayCommand(RestoreDngGeometryDefaultsAsync);

        _ = LoadDeviceSettingsAsync();
        _ = LoadDngGeometrySettingsAsync();
    }

    partial void OnMotor1StepsPerRevolutionChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor1MicrostepsChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor1LeadLengthMmChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor2StepsPerRevolutionChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor2MicrostepsChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor2LeadLengthMmChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor3StepsPerRevolutionChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor3MicrostepsChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnMotor3LeadLengthMmChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel1RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel2RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel3RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDeviceChannel4RoleChanged(string value)
        => OnDeviceSettingsInputChanged();

    partial void OnDngActiveStartChanged(string value)
    {
        if (_isLoadingDngGeometrySettings)
            return;

        _ = SaveDngGeometrySettingsFromInputsAsync();
    }

    partial void OnDngActiveEndInclusiveChanged(string value)
    {
        if (_isLoadingDngGeometrySettings)
            return;

        _ = SaveDngGeometrySettingsFromInputsAsync();
    }

    partial void OnDngMaskedRangesChanged(string value)
    {
        if (_isLoadingDngGeometrySettings)
            return;

        _ = SaveDngGeometrySettingsFromInputsAsync();
    }

    private async Task LoadDeviceSettingsAsync()
    {
        _isLoadingDeviceSettings = true;
        try
        {
            await _deviceSettingsService.InitializeAsync();
            ApplyDeviceSettings(_deviceSettingsService.Settings);
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private void ApplyDeviceSettings(ScanDeviceSettings settings)
    {
        var normalized = settings.Normalize();
        ApplyMotorSettings(normalized.Motor1 ?? ScanMotorMechanicalSettings.CreateDefault(),
            value => Motor1StepsPerRevolution = value,
            value => Motor1Microsteps = value,
            value => Motor1LeadLengthMm = value);
        ApplyMotorSettings(normalized.Motor2 ?? ScanMotorMechanicalSettings.CreateDefault(),
            value => Motor2StepsPerRevolution = value,
            value => Motor2Microsteps = value,
            value => Motor2LeadLengthMm = value);
        ApplyMotorSettings(normalized.Motor3 ?? ScanMotorMechanicalSettings.CreateDefault(),
            value => Motor3StepsPerRevolution = value,
            value => Motor3Microsteps = value,
            value => Motor3LeadLengthMm = value);
        DeviceChannel1Role = NormalizeChannelRoleSelection(normalized.Channel1Role, "Blue");
        DeviceChannel2Role = NormalizeChannelRoleSelection(normalized.Channel2Role, "White");
        DeviceChannel3Role = NormalizeChannelRoleSelection(normalized.Channel3Role, "Red");
        DeviceChannel4Role = NormalizeChannelRoleSelection(normalized.Channel4Role, "Green");
    }

    private string NormalizeChannelRoleSelection(string? role, string fallback)
        => ChannelRoleOptions.FirstOrDefault(option => string.Equals(option, role, StringComparison.OrdinalIgnoreCase)) ?? fallback;

    private static void ApplyMotorSettings(ScanMotorMechanicalSettings settings, Action<string> setStepsPerRevolution, Action<string> setMicrosteps, Action<string> setLeadLengthMm)
    {
        var normalized = settings.Normalize();
        setStepsPerRevolution(normalized.StepsPerRevolution.ToString(CultureInfo.InvariantCulture));
        setMicrosteps(normalized.Microsteps.ToString(CultureInfo.InvariantCulture));
        setLeadLengthMm(FormatDeviceDouble(normalized.LeadLengthMm));
    }

    private void OnDeviceSettingsInputChanged()
    {
        if (_isLoadingDeviceSettings)
            return;

        _ = SaveDeviceSettingsFromInputsAsync();
    }

    private async Task SaveDeviceSettingsFromInputsAsync()
    {
        if (!TryBuildDeviceSettingsFromInputs(out var settings))
            return;

        await _deviceSettingsService.InitializeAsync();
        await _deviceSettingsService.SetSettingsAsync(settings);
        _isLoadingDeviceSettings = true;
        try
        {
            ApplyDeviceSettings(_deviceSettingsService.Settings);
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private async Task RestoreDeviceSettingsDefaultsAsync()
    {
        _isLoadingDeviceSettings = true;
        try
        {
            await _deviceSettingsService.InitializeAsync();
            var defaults = _deviceSettingsService.DefaultSettings;
            ApplyDeviceSettings(defaults);
            await _deviceSettingsService.SetSettingsAsync(defaults);
        }
        finally
        {
            _isLoadingDeviceSettings = false;
        }
    }

    private bool TryBuildDeviceSettingsFromInputs(out ScanDeviceSettings settings)
    {
        settings = _deviceSettingsService.DefaultSettings;
        if (!TryBuildMotorMechanicalSettings(Motor1StepsPerRevolution, Motor1Microsteps, Motor1LeadLengthMm, out var motor1)
            || !TryBuildMotorMechanicalSettings(Motor2StepsPerRevolution, Motor2Microsteps, Motor2LeadLengthMm, out var motor2)
            || !TryBuildMotorMechanicalSettings(Motor3StepsPerRevolution, Motor3Microsteps, Motor3LeadLengthMm, out var motor3))
        {
            return false;
        }

        settings = new ScanDeviceSettings(
            motor1,
            motor2,
            motor3,
            DeviceChannel1Role,
            DeviceChannel2Role,
            DeviceChannel3Role,
            DeviceChannel4Role).Normalize();
        return true;
    }

    private static bool TryBuildMotorMechanicalSettings(string stepsPerRevolutionText, string microstepsText, string leadLengthMmText, out ScanMotorMechanicalSettings settings)
    {
        settings = ScanMotorMechanicalSettings.CreateDefault();
        if (!uint.TryParse(stepsPerRevolutionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stepsPerRevolution)
            || stepsPerRevolution == 0
            || !uint.TryParse(microstepsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microsteps)
            || microsteps == 0
            || !double.TryParse(leadLengthMmText, NumberStyles.Float, CultureInfo.InvariantCulture, out var leadLengthMm)
            || !double.IsFinite(leadLengthMm)
            || leadLengthMm <= 0.0)
        {
            return false;
        }

        settings = new ScanMotorMechanicalSettings(stepsPerRevolution, microsteps, leadLengthMm).Normalize();
        return true;
    }

    private async Task LoadDngGeometrySettingsAsync()
    {
        _isLoadingDngGeometrySettings = true;
        try
        {
            await _dngGeometrySettingsService.InitializeAsync();
            ApplyDngGeometrySettings(_dngGeometrySettingsService.Settings);
        }
        finally
        {
            _isLoadingDngGeometrySettings = false;
        }
    }

    private void ApplyDngGeometrySettings(ScanDngGeometrySettings settings)
    {
        DngActiveStart = settings.ActiveRange.Start.ToString(CultureInfo.InvariantCulture);
        DngActiveEndInclusive = settings.ActiveRange.EndInclusive.ToString(CultureInfo.InvariantCulture);
        DngMaskedRanges = string.Join(", ",
            settings.MaskedBlackRanges.Select(range => $"{range.Start}-{range.EndInclusive}"));
    }

    private async Task SaveDngGeometrySettingsFromInputsAsync()
    {
        if (!TryBuildDngGeometrySettingsFromInputs(out var settings))
            return;

        await _dngGeometrySettingsService.InitializeAsync();
        await _dngGeometrySettingsService.SetSettingsAsync(settings);
        _isLoadingDngGeometrySettings = true;
        try
        {
            ApplyDngGeometrySettings(_dngGeometrySettingsService.Settings);
        }
        finally
        {
            _isLoadingDngGeometrySettings = false;
        }
    }

    private async Task RestoreDngGeometryDefaultsAsync()
    {
        _isLoadingDngGeometrySettings = true;
        try
        {
            await _dngGeometrySettingsService.InitializeAsync();
            var defaults = _dngGeometrySettingsService.DefaultSettings;
            ApplyDngGeometrySettings(defaults);
            await _dngGeometrySettingsService.SetSettingsAsync(defaults);
        }
        finally
        {
            _isLoadingDngGeometrySettings = false;
        }
    }

    private bool TryBuildDngGeometrySettingsFromInputs(out ScanDngGeometrySettings settings)
    {
        settings = _dngGeometrySettingsService.DefaultSettings;
        if (!int.TryParse(DngActiveStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(DngActiveEndInclusive, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            return false;
        }

        if (!TryParseMaskedRanges(DngMaskedRanges, out var ranges))
            return false;

        settings = new ScanDngGeometrySettings(new ScanColumnRange(start, end), ranges);
        return true;
    }

    private static bool TryParseMaskedRanges(string value, out ScanColumnRange[] ranges)
    {
        ranges = [];
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var parsedRanges = new List<ScanColumnRange>(parts.Length);
        foreach (var part in parts)
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (bounds.Length != 2
                || !int.TryParse(bounds[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(bounds[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
            {
                return false;
            }

            parsedRanges.Add(new ScanColumnRange(start, end));
        }

        ranges = parsedRanges.ToArray();
        return parsedRanges.Count is > 0 and <= ScanDngGeometrySettings.MaxMaskedBlackRangeCount;
    }

    private static string FormatDeviceDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
