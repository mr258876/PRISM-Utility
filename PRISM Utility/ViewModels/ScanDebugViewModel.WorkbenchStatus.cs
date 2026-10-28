using Microsoft.UI.Xaml;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class ScanDebugViewModel
{
    // These summaries describe separate facts. In particular, absent reference
    // values are not a new validation error or an additional save requirement.
    public string WorkbenchParameterStatusText
    {
        get
        {
            var issues = CurrentFilmProfileValidationIssues;
            if (!_hasCurrentFilmProfileValidation && issues.Count == 0)
                return "ScanDebug_WorkbenchParametersNotChecked".GetLocalizedOrFallback("Parameters not checked");

            var errors = issues.Count(issue => issue.Severity == ScanFilmProfileValidationSeverity.Error
                && !FilmProfileValidationTextPresenter.IsPendingInput(issue));
            if (errors > 0)
                return "ScanDebug_WorkbenchParametersInvalid".GetLocalizedFormatOrFallback(
                    "Parameters need repair · {0}", errors);

            var pending = issues.Count(FilmProfileValidationTextPresenter.IsPendingInput);
            if (pending > 0)
                return "ScanDebug_WorkbenchParametersIncomplete".GetLocalizedFormatOrFallback(
                    "Parameters incomplete · {0}", pending);

            if (_hasInvalidFilmProfileInput)
                return "ScanDebug_WorkbenchParametersReview".GetLocalizedOrFallback("Parameters need review");

            return issues.Count > 0
                ? "ScanDebug_WorkbenchParametersWithNotices".GetLocalizedFormatOrFallback(
                    "Parameters checked · notices: {0}", issues.Count)
                : "ScanDebug_WorkbenchParametersValid".GetLocalizedOrFallback("Parameter checks passed");
        }
    }

    public string WorkbenchParameterDetailText => "ScanDebug_WorkbenchParameterDetail".GetLocalizedOrFallback(
        "Checks cover configuration structure and input values. Passing does not confirm complete calibration references, calibration quality, or device readiness.");

    public int WorkbenchSelectedChannelCount => AcquisitionChannels.Count(channel => channel.IsSelected);

    public int WorkbenchConfiguredReferenceCount => AcquisitionChannels.Count(
        channel => channel.IsSelected && HasWorkbenchChannelReferences(channel.Role));

    public string WorkbenchCalibrationStatusText
    {
        get
        {
            var channels = GetSelectedAcquisitionChannelsForSummary();
            return channels.Length == 0
                ? "ScanDebug_WorkbenchReferencesNoChannels".GetLocalizedOrFallback("No acquisition channels selected")
                : "ScanDebug_WorkbenchReferencesCoverage".GetLocalizedFormatOrFallback(
                    "References configured · {0}/{1} channels",
                    channels.Count(channel => HasWorkbenchChannelReferences(channel.Role)), channels.Length);
        }
    }

    public string WorkbenchCalibrationDetailText
    {
        get
        {
            var channels = GetSelectedAcquisitionChannelsForSummary();
            if (channels.Length == 0)
                return "ScanDebug_WorkbenchReferencesSelectChannels".GetLocalizedOrFallback(
                    "Choose channels in Sampling to review their black/white references.");

            var missing = channels.Where(channel => !HasWorkbenchChannelReferences(channel.Role))
                .Select(channel => channel.DisplayName).ToArray();
            return missing.Length == 0
                ? "ScanDebug_WorkbenchReferencesComplete".GetLocalizedOrFallback(
                    "All selected channels have black/white references. This describes configuration data, not measured calibration quality.")
                : "ScanDebug_WorkbenchReferencesMissing".GetLocalizedFormatOrFallback(
                    "Missing black/white references: {0}. A valid configuration can still be saved; review these references before using calibrated output.",
                    string.Join(", ", missing));
        }
    }

    private bool HasWorkbenchChannelReferences(string role)
        => _filmProfileWorkspace.Snapshot.CurrentDraft.ChannelProfiles.TryGetValue(role, out var profile)
            && profile.BlackLevel is ushort black
            && profile.WhiteLevel is ushort white
            && white > black;

    public string WorkbenchDeviceStatusText
    {
        get
        {
            // Retain unresolved failures and local work that is still stopping
            // after disconnection, following the existing run-status priority.
            if (LatestRuntimeOperationNotice is not null || !string.IsNullOrEmpty(RunActivityText))
                return RunPresentationText;
            if (IsConnecting)
                return DeviceStateText;
            if (!IsConnected)
                return "ScanDebug_WorkbenchDeviceOffline".GetLocalizedOrFallback("Device not connected");
            return CanStartScan()
                ? "ScanDebug_WorkbenchDeviceReady".GetLocalizedOrFallback("Ready to sample")
                : BuildStartDisabledReason();
        }
    }

    public Visibility WorkbenchOfflineGuidanceVisibility =>
        !IsConnected && !IsConnecting && string.IsNullOrEmpty(RunActivityText)
            ? Visibility.Visible : Visibility.Collapsed;

    public string Motor1DisplayStatusText => GetWorkbenchMotorStatus(Motor1StatusText);
    public string Motor2DisplayStatusText => GetWorkbenchMotorStatus(Motor2StatusText);
    public string Motor3DisplayStatusText => GetWorkbenchMotorStatus(Motor3StatusText);

    private string GetWorkbenchMotorStatus(string connectedStatus)
        => !IsConnected
            ? "ScanDebug_WorkbenchDeviceOffline".GetLocalizedOrFallback("Device not connected")
            : connectedStatus;

    public string AutofocusDisplayStatusText => IsAutoFocusing
        ? AutofocusSummaryText
        : IsManualFocusing
            ? RunActivityText
            : !IsConnected
                ? "ScanDebug_WorkbenchDeviceOffline".GetLocalizedOrFallback("Device not connected")
                : AutofocusSummaryText;

    private void NotifyWorkbenchStatusChanged()
    {
        OnPropertyChanged(nameof(WorkbenchParameterStatusText));
        OnPropertyChanged(nameof(WorkbenchSelectedChannelCount));
        OnPropertyChanged(nameof(WorkbenchConfiguredReferenceCount));
        OnPropertyChanged(nameof(WorkbenchCalibrationStatusText));
        OnPropertyChanged(nameof(WorkbenchCalibrationDetailText));
        NotifyWorkbenchRuntimeStatusChanged();
    }

    private void NotifyWorkbenchRuntimeStatusChanged()
    {
        OnPropertyChanged(nameof(WorkbenchDeviceStatusText));
        OnPropertyChanged(nameof(WorkbenchOfflineGuidanceVisibility));
        OnPropertyChanged(nameof(Motor1DisplayStatusText));
        OnPropertyChanged(nameof(Motor2DisplayStatusText));
        OnPropertyChanged(nameof(Motor3DisplayStatusText));
        OnPropertyChanged(nameof(AutofocusDisplayStatusText));
    }
}
