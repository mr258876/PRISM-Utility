using System.Text.RegularExpressions;

namespace PRISM_Utility.Helpers;

internal static partial class ScanRuntimeMessageLocalizer
{
    private const string ScanPrefix = "Scan_Runtime_";
    private const string ScanDebugPrefix = "ScanDebug_Runtime_";

    public static string LocalizeScanViewStatus(string? message)
        => Localize(message, ScanPrefix);

    public static string LocalizeScanDebugStatus(string? message)
        => Localize(message, ScanDebugPrefix);

    public static string LocalizeScanWorkflowStage(string? stage)
        => stage switch
        {
            "Preparing" => "Scan_Runtime_ProgressStagePreparing".GetLocalized(),
            "Scanning" => "Scan_Runtime_ProgressStageScanning".GetLocalized(),
            "Waiting for motor" => "Scan_Runtime_ProgressStageWaitingForMotor".GetLocalized(),
            "Returning" => "Scan_Runtime_ProgressStageReturning".GetLocalized(),
            "Completed" => "Scan_Runtime_ProgressStageCompleted".GetLocalized(),
            _ => stage ?? string.Empty
        };

    public static string GetLocalizedDirection(bool directionPositive)
        => directionPositive
            ? "Scan_Runtime_DirectionForward".GetLocalized()
            : "Scan_Runtime_DirectionReverse".GetLocalized();

    private static string Localize(string? message, string prefix)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var text = message.Trim();

        var segmentPrefixMatch = SegmentPrefixRegex().Match(text);
        if (segmentPrefixMatch.Success)
        {
            return Key(prefix, "StatusSegmentPrefix").GetLocalizedFormat(
                segmentPrefixMatch.Groups["segment"].Value,
                segmentPrefixMatch.Groups["count"].Value,
                Localize(segmentPrefixMatch.Groups["message"].Value, prefix));
        }

        var passFailedMatch = WorkflowPassFailedRegex().Match(text);
        if (passFailedMatch.Success)
        {
            return Key(prefix, "WorkflowPassFailed").GetLocalizedFormat(
                passFailedMatch.Groups["pass"].Value,
                Localize(passFailedMatch.Groups["message"].Value, prefix));
        }

        switch (text)
        {
            case "619C/619D not detected.":
                return Key(prefix, "ServiceDevicesNotDetected").GetLocalized();
            case "Device disappeared during connect.":
                return Key(prefix, "ServiceDeviceDisappearedDuringConnect").GetLocalized();
            case "Endpoint check failed. Need 619C IN 0x82 and 619D OUT 0x01/IN 0x81.":
                return Key(prefix, "ServiceEndpointCheckFailed").GetLocalized();
            case "Endpoint check failed: 619D OUT 0x01 and IN 0x81 must be on same interface.":
                return Key(prefix, "ServiceEndpointInterfaceMismatch").GetLocalized();
            case "Scanner not connected. Click Connect Devices first.":
                return Key(prefix, "ServiceScannerNotConnected").GetLocalized();
            case "Scanner not connected.":
                return Key(prefix, "ServiceScannerNotConnectedShort").GetLocalized();
            case "Scan lines configured. 619C IN opened. Starting scan...":
                return Key(prefix, "StatusScanLinesConfigured").GetLocalized();
            case "619C read started. Sending START_SCAN...":
                return Key(prefix, "StatusReadStartedSendingStartScan").GetLocalized();
            case "Scan command sent, receiving image lines...":
                return Key(prefix, "StatusScanCommandSentReceiving").GetLocalized();
            case "START_SCAN ACK monitor timed out.":
                return Key(prefix, "StatusAckMonitorTimedOut").GetLocalized();
            case "Device stayed busy for too long.":
                return Key(prefix, "StatusDeviceBusyTooLong").GetLocalized();
            case "START_SCAN ACK received.":
                return Key(prefix, "StatusStartScanAckReceived").GetLocalized();
            case "START_SCAN done ACK received.":
                return Key(prefix, "StatusStartScanDoneAckReceived").GetLocalized();
            case "START_SCAN done ACK not received in time.":
                return Key(prefix, "StatusStartScanDoneAckTimeout").GetLocalized();
            case "Scan completed.":
                return Key(prefix, "StatusScanCompleted").GetLocalized();
            case "Scan stopped.":
                return Key(prefix, "StatusScanStopped").GetLocalized();
            case "Four-channel scan workflow completed.":
                return Key(prefix, "WorkflowCompleted").GetLocalized();
            case "Warm-up disabled." when prefix == ScanDebugPrefix:
                return "ScanDebug_Runtime_StatusWarmUpDisabled".GetLocalized();
        }

        var connectFailedMatch = ConnectFailedRegex().Match(text);
        if (connectFailedMatch.Success)
            return Key(prefix, "StatusConnectFailed").GetLocalizedFormat(Localize(connectFailedMatch.Groups["message"].Value, prefix));

        var workflowEnableMotorMatch = WorkflowEnableMotorRegex().Match(text);
        if (workflowEnableMotorMatch.Success)
            return Key(prefix, "WorkflowEnableMotor").GetLocalizedFormat(workflowEnableMotorMatch.Groups["motor"].Value);

        var workflowApplyProfileMatch = WorkflowApplyProfileRegex().Match(text);
        if (workflowApplyProfileMatch.Success)
        {
            return Key(prefix, "WorkflowApplyCcdProfile").GetLocalizedFormat(
                workflowApplyProfileMatch.Groups["pass"].Value,
                workflowApplyProfileMatch.Groups["total"].Value,
                LocalizeChannelRole(workflowApplyProfileMatch.Groups["role"].Value));
        }

        var workflowPrepareMotorMatch = WorkflowPrepareMotorRegex().Match(text);
        if (workflowPrepareMotorMatch.Success)
        {
            return Key(prefix, "WorkflowPrepareMotorOnExposureSync").GetLocalizedFormat(
                workflowPrepareMotorMatch.Groups["pass"].Value,
                workflowPrepareMotorMatch.Groups["total"].Value,
                workflowPrepareMotorMatch.Groups["motor"].Value,
                LocalizeDirection(workflowPrepareMotorMatch.Groups["direction"].Value),
                workflowPrepareMotorMatch.Groups["steps"].Value);
        }

        var workflowCaptureRowsMatch = WorkflowCaptureRowsRegex().Match(text);
        if (workflowCaptureRowsMatch.Success)
        {
            return Key(prefix, "WorkflowCaptureRows").GetLocalizedFormat(
                workflowCaptureRowsMatch.Groups["pass"].Value,
                workflowCaptureRowsMatch.Groups["total"].Value,
                workflowCaptureRowsMatch.Groups["led"].Value,
                workflowCaptureRowsMatch.Groups["rows"].Value);
        }

        var workflowReturnMotorMatch = WorkflowReturnMotorRegex().Match(text);
        if (workflowReturnMotorMatch.Success)
        {
            return Key(prefix, "WorkflowReturnMotor").GetLocalizedFormat(
                workflowReturnMotorMatch.Groups["pass"].Value,
                workflowReturnMotorMatch.Groups["total"].Value,
                workflowReturnMotorMatch.Groups["motor"].Value);
        }

        var workflowPassCompleteMatch = WorkflowPassCompleteRegex().Match(text);
        if (workflowPassCompleteMatch.Success)
        {
            return Key(prefix, "WorkflowPassComplete").GetLocalizedFormat(
                workflowPassCompleteMatch.Groups["pass"].Value,
                workflowPassCompleteMatch.Groups["total"].Value);
        }

        var deviceBusyMatch = DeviceBusyRegex().Match(text);
        if (deviceBusyMatch.Success)
            return Key(prefix, "StatusDeviceBusy").GetLocalizedFormat(deviceBusyMatch.Groups["command"].Value);

        var startScanAckStatusMatch = StartScanAckStatusRegex().Match(text);
        if (startScanAckStatusMatch.Success)
        {
            return Key(prefix, "StatusStartScanAckStatus").GetLocalizedFormat(
                startScanAckStatusMatch.Groups["status"].Value,
                startScanAckStatusMatch.Groups["code"].Value);
        }

        var startScanAckTargetMismatchMatch = StartScanAckTargetMismatchRegex().Match(text);
        if (startScanAckTargetMismatchMatch.Success)
        {
            return Key(prefix, "StatusStartScanAckTargetMismatch").GetLocalizedFormat(
                startScanAckTargetMismatchMatch.Groups["target"].Value,
                startScanAckTargetMismatchMatch.Groups["expected"].Value);
        }

        var scanCompletedInSegmentsMatch = ScanCompletedInSegmentsRegex().Match(text);
        if (scanCompletedInSegmentsMatch.Success)
            return Key(prefix, "StatusScanCompletedInSegments").GetLocalizedFormat(scanCompletedInSegmentsMatch.Groups["count"].Value);

        var scanFailedAfterMatch = ScanFailedAfterRegex().Match(text);
        if (scanFailedAfterMatch.Success)
        {
            return Key(prefix, "StatusScanFailedAfterMs").GetLocalizedFormat(
                scanFailedAfterMatch.Groups["elapsedMs"].Value,
                Localize(scanFailedAfterMatch.Groups["message"].Value, prefix));
        }

        var segmentDoneAckTimeoutMatch = SegmentDoneAckTimeoutRegex().Match(text);
        if (segmentDoneAckTimeoutMatch.Success)
        {
            return Key(prefix, "StatusSegmentDoneAckTimeout").GetLocalizedFormat(
                segmentDoneAckTimeoutMatch.Groups["segment"].Value,
                segmentDoneAckTimeoutMatch.Groups["count"].Value);
        }

        var unexpectedStopAckMatch = UnexpectedStopAckRegex().Match(text);
        if (unexpectedStopAckMatch.Success)
            return Key(prefix, "StatusUnexpectedStopAck").GetLocalizedFormat(unexpectedStopAckMatch.Groups["command"].Value);

        var stopAckReceivedMatch = StopAckReceivedRegex().Match(text);
        if (stopAckReceivedMatch.Success)
        {
            return Key(prefix, "StatusStopAckReceived").GetLocalizedFormat(
                stopAckReceivedMatch.Groups["target"].Value,
                stopAckReceivedMatch.Groups["completed"].Value);
        }

        var stopAckStatusMatch = StopAckStatusRegex().Match(text);
        if (stopAckStatusMatch.Success)
        {
            return Key(prefix, "StatusStopAckStatus").GetLocalizedFormat(
                stopAckStatusMatch.Groups["status"].Value,
                stopAckStatusMatch.Groups["code"].Value);
        }

        var stopSendFailedMatch = StopSendFailedRegex().Match(text);
        if (stopSendFailedMatch.Success)
            return Key(prefix, "StatusStopSendFailed").GetLocalizedFormat(Localize(stopSendFailedMatch.Groups["message"].Value, prefix));

        if (prefix == ScanDebugPrefix)
        {
            var warmUpUnexpectedAckMatch = WarmUpUnexpectedAckRegex().Match(text);
            if (warmUpUnexpectedAckMatch.Success)
                return "ScanDebug_Runtime_StatusUnexpectedWarmUpAck".GetLocalizedFormat(warmUpUnexpectedAckMatch.Groups["command"].Value);

            var warmUpAckStatusMatch = WarmUpAckStatusRegex().Match(text);
            if (warmUpAckStatusMatch.Success)
            {
                return "ScanDebug_Runtime_StatusWarmUpAckStatus".GetLocalizedFormat(
                    warmUpAckStatusMatch.Groups["status"].Value,
                    warmUpAckStatusMatch.Groups["code"].Value);
            }

            var warmUpEnabledMatch = WarmUpEnabledRegex().Match(text);
            if (warmUpEnabledMatch.Success)
            {
                return "ScanDebug_Runtime_StatusWarmUpEnabledDetailed".GetLocalizedFormat(
                    warmUpEnabledMatch.Groups["target"].Value,
                    warmUpEnabledMatch.Groups["completed"].Value);
            }

            var warmUpCommandFailedMatch = WarmUpCommandFailedRegex().Match(text);
            if (warmUpCommandFailedMatch.Success)
                return "ScanDebug_Runtime_StatusWarmUpCommandFailed".GetLocalizedFormat(Localize(warmUpCommandFailedMatch.Groups["message"].Value, prefix));
        }

        return text;
    }

    private static string Key(string prefix, string suffix) => prefix + suffix;

    private static string LocalizeDirection(string direction)
        => string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase)
            ? "Scan_Runtime_DirectionForward".GetLocalized()
            : "Scan_Runtime_DirectionReverse".GetLocalized();

    private static string LocalizeChannelRole(string role)
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

    [GeneratedRegex(@"^\[(?<segment>\d+)\/(?<count>\d+)\] (?<message>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPrefixRegex();

    [GeneratedRegex(@"^Pass (?<pass>\d+) failed: (?<message>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowPassFailedRegex();

    [GeneratedRegex(@"^Connect failed: (?<message>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectFailedRegex();

    [GeneratedRegex(@"^Enabling Motor(?<motor>\d+) for scan transport\.\.\.$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowEnableMotorRegex();

    [GeneratedRegex(@"^Pass (?<pass>\d+)\/(?<total>\d+): applying CCD profile for (?<role>.+) channel\.\.\.$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowApplyProfileRegex();

    [GeneratedRegex(@"^Pass (?<pass>\d+)\/(?<total>\d+): preparing Motor(?<motor>\d+) (?<direction>forward|reverse) for (?<steps>\d+) step\(s\), waiting for EXPOSURE_SYNC\.\.\.$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WorkflowPrepareMotorRegex();

    [GeneratedRegex(@"^Pass (?<pass>\d+)\/(?<total>\d+): LED(?<led>\d+) active, capturing (?<rows>\d+) row\(s\)\.\.\.$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowCaptureRowsRegex();

    [GeneratedRegex(@"^Pass (?<pass>\d+)\/(?<total>\d+): returning Motor(?<motor>\d+) to start position before next channel scan\.\.\.$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowReturnMotorRegex();

    [GeneratedRegex(@"^Pass (?<pass>\d+)\/(?<total>\d+) complete\.$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowPassCompleteRegex();

    [GeneratedRegex(@"^Device busy \(cmd=0x(?<command>[0-9A-F]+)\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DeviceBusyRegex();

    [GeneratedRegex(@"^START_SCAN ACK status: (?<status>.+) \(0x(?<code>[0-9A-F]+)\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StartScanAckStatusRegex();

    [GeneratedRegex(@"^START_SCAN ACK target mismatch: (?<target>\d+) \(expected (?<expected>\d+)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex StartScanAckTargetMismatchRegex();

    [GeneratedRegex(@"^Scan completed in (?<count>\d+) segment\(s\)\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ScanCompletedInSegmentsRegex();

    [GeneratedRegex(@"^Scan failed after (?<elapsedMs>\d+) ms: (?<message>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScanFailedAfterRegex();

    [GeneratedRegex(@"^Segment (?<segment>\d+)\/(?<count>\d+) done ACK not received in time\.$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentDoneAckTimeoutRegex();

    [GeneratedRegex(@"^Unexpected ACK cmd while stopping: 0x(?<command>[0-9A-F]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UnexpectedStopAckRegex();

    [GeneratedRegex(@"^STOP_SCAN ACK received \(target=(?<target>\d+), completed=(?<completed>\d+)\)\.$", RegexOptions.CultureInvariant)]
    private static partial Regex StopAckReceivedRegex();

    [GeneratedRegex(@"^STOP_SCAN ACK status: (?<status>.+) \(0x(?<code>[0-9A-F]+)\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StopAckStatusRegex();

    [GeneratedRegex(@"^STOP_SCAN send failed: (?<message>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex StopSendFailedRegex();

    [GeneratedRegex(@"^Unexpected ACK cmd while enabling warm-up: 0x(?<command>[0-9A-F]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WarmUpUnexpectedAckRegex();

    [GeneratedRegex(@"^WARM_UP ACK status: (?<status>.+) \(0x(?<code>[0-9A-F]+)\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WarmUpAckStatusRegex();

    [GeneratedRegex(@"^Warm-up enabled \(target=(?<target>\d+), completed=(?<completed>\d+)\)\.$", RegexOptions.CultureInvariant)]
    private static partial Regex WarmUpEnabledRegex();

    [GeneratedRegex(@"^Warm-up command failed: (?<message>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex WarmUpCommandFailedRegex();
}
