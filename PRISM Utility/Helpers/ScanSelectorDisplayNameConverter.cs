using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Data;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;

namespace PRISM_Utility.Helpers;

public sealed class ScanMotorDirectionOption(string token) : ObservableObject
{
    private string _displayName = token;

    public string Token { get; } = token;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}

public sealed class ScanSelectorDisplayNameConverter : IValueConverter
{
    public static string GetMappedMotorDirectionDisplayName(
        string token, byte motorId, ScanFocusMotorMapping? focusMapping, byte? transportMotorId)
    {
        if (token is not ("Dir0" or "Dir1"))
            return token;

        var direction = token == "Dir1";
        var labels = new List<string>();
        if (focusMapping is { } mapping && mapping.Validate().IsValid
            && (mapping.LeftMotorId == motorId || mapping.RightMotorId == motorId))
        {
            labels.Add(direction == mapping.ZPositiveDirection
                ? "ScanDebug_Runtime_MotorMoveFocusPositive".GetLocalizedOrFallback("Focus positive (Z+)")
                : "ScanDebug_Runtime_MotorMoveFocusNegative".GetLocalizedOrFallback("Focus negative (Z−)"));
        }

        if (transportMotorId == motorId)
        {
            labels.Add(direction
                ? "ScanDebug_Runtime_MotorMoveTransportForward".GetLocalizedOrFallback("Film forward")
                : "ScanDebug_Runtime_MotorMoveTransportReverse".GetLocalizedOrFallback("Film reverse"));
        }

        return labels.Count > 0
            ? string.Join(" · ", labels)
            : direction
                ? "ScanDebug_Runtime_MotorMoveDirection1".GetLocalizedOrFallback("Direction 1")
                : "ScanDebug_Runtime_MotorMoveDirection0".GetLocalizedOrFallback("Direction 0");
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter as string == "DngExportMode" && value is ScanDngExportMode exportMode)
            return GetDngExportModeDisplayName(exportMode);

        if (parameter as string == "ProfileDngExportMode" && value is ScanDngExportMode profileExportMode)
            return GetProfileDngExportModeDisplayName(profileExportMode);

        if (parameter as string == "AlignmentMode" && value is ScanChannelAlignmentMode alignmentMode)
            return GetAlignmentModeDisplayName(alignmentMode);

        if (parameter as string == "AutofocusPreset" && value is ScanAutofocusPresetKind autofocusPreset)
            return GetAutofocusPresetDisplayName(autofocusPreset);

        if (parameter as string == "TargetWhitePointMode" && value is ScanTargetWhitePointMode targetWhitePointMode)
            return GetTargetWhitePointModeDisplayName(targetWhitePointMode);

        if (parameter as string == "CaptureMode" && value is ScanDebugCaptureMode captureMode)
            return GetCaptureModeDisplayName(captureMode);

        if (value is not string token || string.IsNullOrWhiteSpace(token))
            return value?.ToString() ?? string.Empty;

        return (parameter as string) switch
        {
            "Direction" => GetDirectionDisplayName(token),
            "ChannelRole" => GetChannelRoleDisplayName(token),
            "PreviewMode" => GetPreviewModeDisplayName(token),
            "TargetWhitePointMode" => GetTargetWhitePointModeDisplayName(token),
            "Motor" => GetMotorDisplayName(token),
            "MotorUnit" => GetMotorUnitDisplayName(token),
            "MotorRateUnit" => GetMotorRateUnitDisplayName(token),
            "CalibrationChannel" => GetCalibrationChannelDisplayName(token),
            "MotorDirection" => GetMotorDirectionDisplayName(token),
            "RoiSelection" => GetRoiSelectionDisplayName(token),
            _ => token
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    private static string GetDirectionDisplayName(string direction)
        => string.Equals(direction, "Forward", StringComparison.OrdinalIgnoreCase)
            ? "Scan_Runtime_DirectionForward".GetLocalized()
            : string.Equals(direction, "Reverse", StringComparison.OrdinalIgnoreCase)
                ? "Scan_Runtime_DirectionReverse".GetLocalized()
                : direction;

    private static string GetChannelRoleDisplayName(string role)
        => role switch
        {
            "Red" => "Scan_Runtime_ChannelRoleRed".GetLocalized(),
            "Green" => "Scan_Runtime_ChannelRoleGreen".GetLocalized(),
            "Blue" => "Scan_Runtime_ChannelRoleBlue".GetLocalized(),
            "White" => "Scan_Runtime_ChannelRoleWhite".GetLocalized(),
            "IR" => "Scan_Runtime_ChannelRoleIr".GetLocalized(),
            "Unused" => "Scan_Runtime_ChannelRoleUnused".GetLocalized(),
            _ => role
        };

    private static string GetPreviewModeDisplayName(string mode)
        => mode switch
        {
            "RGB Composite" => "Scan_Runtime_PreviewModeRgbComposite".GetLocalized(),
            "Raw Channel 1" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(1),
            "Raw Channel 2" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(2),
            "Raw Channel 3" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(3),
            "Raw Channel 4" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(4),
            "Raw Channel 5" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(5),
            _ => mode
        };

    public static string GetDngExportModeDisplayName(ScanDngExportMode mode)
        => mode switch
        {
            ScanDngExportMode.LinearRaw4 => "Scan_Runtime_DngExportModeLinearRaw4".GetLocalizedOrFallback("Raw channel DNG"),
            ScanDngExportMode.LinearRgbIrw => "Scan_Runtime_DngExportModeLinearRgbIrw".GetLocalizedOrFallback("RGB composite and auxiliary DNG"),
            _ => mode.ToString()
        };

    public static string GetProfileDngExportModeDisplayName(ScanDngExportMode mode)
        => mode switch
        {
            ScanDngExportMode.LinearRaw4 => "Scan_Runtime_ProfileDngExportModeLinearRaw4".GetLocalizedOrFallback("Raw channel DNG"),
            ScanDngExportMode.LinearRgbIrw => "Scan_Runtime_ProfileDngExportModeLinearRgbIrw".GetLocalizedOrFallback("RGB composite and auxiliary DNG"),
            _ => mode.ToString()
        };

    private static string GetAlignmentModeDisplayName(ScanChannelAlignmentMode mode)
        => mode switch
        {
            ScanChannelAlignmentMode.Ecc => "Scan_Runtime_AlignmentModeEcc".GetLocalized(),
            ScanChannelAlignmentMode.MutualInformation => "Scan_Runtime_AlignmentModeMutualInformation".GetLocalized(),
            ScanChannelAlignmentMode.EccThenMutualInformation => "Scan_Runtime_AlignmentModeEccThenMutualInformation".GetLocalized(),
            _ => mode.ToString()
        };

    public static string GetAutofocusPresetDisplayName(ScanAutofocusPresetKind preset)
        => preset switch
        {
            ScanAutofocusPresetKind.Quick => "ScanDebug_Runtime_AutofocusPresetQuick".GetLocalized(),
            ScanAutofocusPresetKind.Standard => "ScanDebug_Runtime_AutofocusPresetStandard".GetLocalized(),
            ScanAutofocusPresetKind.Fine => "ScanDebug_Runtime_AutofocusPresetFine".GetLocalized(),
            ScanAutofocusPresetKind.Custom => "ScanDebug_Runtime_AutofocusPresetCustom".GetLocalized(),
            _ => preset.ToString()
        };

    private static string GetTargetWhitePointModeDisplayName(ScanTargetWhitePointMode mode)
        => mode switch
        {
            ScanTargetWhitePointMode.D65 => "Scan_Runtime_TargetWhitePointModeD65".GetLocalized(),
            ScanTargetWhitePointMode.D50 => "Scan_Runtime_TargetWhitePointModeD50".GetLocalized(),
            ScanTargetWhitePointMode.ManualColorTemperature => "Scan_Runtime_TargetWhitePointModeManualColorTemperature".GetLocalized(),
            _ => mode.ToString()
        };

    private static string GetTargetWhitePointModeDisplayName(string mode)
        => mode switch
        {
            nameof(ScanTargetWhitePointMode.D65) => "Scan_Runtime_TargetWhitePointModeD65".GetLocalized(),
            nameof(ScanTargetWhitePointMode.D50) => "Scan_Runtime_TargetWhitePointModeD50".GetLocalized(),
            nameof(ScanTargetWhitePointMode.ManualColorTemperature) => "Scan_Runtime_TargetWhitePointModeManualColorTemperature".GetLocalized(),
            _ => mode
        };

    private static string GetMotorDisplayName(string motor)
        => motor switch
        {
            "Motor1" => "Scan_Runtime_MotorOption1".GetLocalizedOrFallback("Motor 1 (M1)"),
            "Motor2" => "Scan_Runtime_MotorOption2".GetLocalizedOrFallback("Motor 2 (M2)"),
            "Motor3" => "Scan_Runtime_MotorOption3".GetLocalizedOrFallback("Motor 3 (M3)"),
            _ => motor
        };

    private static string GetMotorUnitDisplayName(string unit)
        => unit switch
        {
            "steps" => "ScanDebug_Runtime_MotorUnitSteps".GetLocalizedOrFallback("steps"),
            "steps/s" => "ScanDebug_Runtime_MotorUnitStepsPerSecond".GetLocalizedOrFallback("steps/s"),
            "um" => "μm",
            "um/s" => "μm/s",
            _ => unit
        };

    private static string GetMotorRateUnitDisplayName(string unit)
        => unit switch
        {
            "steps" => GetMotorUnitDisplayName("steps/s"),
            "um" => "μm/s",
            "mm" => "mm/s",
            _ => GetMotorUnitDisplayName(unit)
        };

    private static string GetCalibrationChannelDisplayName(string channelRole)
        => GetChannelRoleDisplayName(channelRole);

    private static string GetMotorDirectionDisplayName(string direction)
        => direction switch
        {
            "Dir0" => "ScanDebug_Runtime_MotorDirectionDir0".GetLocalizedOrFallback("Direction 0 (Dir0)"),
            "Dir1" => "ScanDebug_Runtime_MotorDirectionDir1".GetLocalizedOrFallback("Direction 1 (Dir1)"),
            _ => direction
        };

    private static string GetRoiSelectionDisplayName(string roiSelection)
        => roiSelection switch
        {
            "BW Active" => "ScanDebug_Runtime_RoiSelectionBwActive".GetLocalized(),
            "BW Shield" => "ScanDebug_Runtime_RoiSelectionBwShield".GetLocalized(),
            "Focus Overall" => "ScanDebug_Runtime_RoiSelectionFocusOverall".GetLocalized(),
            "Focus Left" => "ScanDebug_Runtime_RoiSelectionFocusLeft".GetLocalized(),
            "Focus Right" => "ScanDebug_Runtime_RoiSelectionFocusRight".GetLocalized(),
            _ => roiSelection
        };

    private static string GetCaptureModeDisplayName(ScanDebugCaptureMode mode)
        => mode switch
        {
            ScanDebugCaptureMode.Single => "ScanDebug_Runtime_CaptureModeSingle".GetLocalized(),
            ScanDebugCaptureMode.Continuous => "ScanDebug_Runtime_CaptureModeContinuous".GetLocalized(),
            ScanDebugCaptureMode.Transport => "ScanDebug_Runtime_CaptureModeTransport".GetLocalized(),
            _ => mode.ToString()
        };
}
