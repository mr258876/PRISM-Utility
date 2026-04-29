using Microsoft.UI.Xaml.Data;

namespace PRISM_Utility.Helpers;

public sealed class ScanSelectorDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string token || string.IsNullOrWhiteSpace(token))
            return value?.ToString() ?? string.Empty;

        return (parameter as string) switch
        {
            "Direction" => GetDirectionDisplayName(token),
            "ChannelRole" => GetChannelRoleDisplayName(token),
            "PreviewMode" => GetPreviewModeDisplayName(token),
            "Motor" => GetMotorDisplayName(token),
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
            _ => mode
        };

    private static string GetMotorDisplayName(string motor)
        => motor switch
        {
            "Motor1" => "Scan_Runtime_MotorOption1".GetLocalized(),
            "Motor2" => "Scan_Runtime_MotorOption2".GetLocalized(),
            "Motor3" => "Scan_Runtime_MotorOption3".GetLocalized(),
            _ => motor
        };

    private static string GetCalibrationChannelDisplayName(string channelRole)
        => GetChannelRoleDisplayName(channelRole);

    private static string GetMotorDirectionDisplayName(string direction)
        => direction switch
        {
            "Dir0" => "ScanDebug_Runtime_MotorDirectionDir0".GetLocalized(),
            "Dir1" => "ScanDebug_Runtime_MotorDirectionDir1".GetLocalized(),
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
}
