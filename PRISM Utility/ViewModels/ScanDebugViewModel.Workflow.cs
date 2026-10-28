using System.Linq;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public enum ScanDebugWorkflowGuideStep
{
    Configuration,
    Calibration,
    Review,
    Scan
}

public partial class ScanDebugViewModel
{
    public ScanDebugWorkflowGuideStep WorkflowGuideStep
    {
        get
        {
            var draft = _filmProfileWorkspace.Snapshot.CurrentDraft;
            var selectedChannels = AcquisitionChannels.Where(channel => channel.IsSelected).ToArray();
            if (selectedChannels.Length == 0 || draft.ChannelProfiles.Count == 0)
                return ScanDebugWorkflowGuideStep.Configuration;

            if (_hasInvalidFilmProfileInput || CurrentFilmProfileValidationIssues.Any(
                issue => issue.Severity == ScanFilmProfileValidationSeverity.Error))
                return ScanDebugWorkflowGuideStep.Review;

            // Missing references are a recommendation, not an additional save requirement.
            if (selectedChannels.Any(channel => !draft.ChannelProfiles.TryGetValue(channel.Role, out var profile)
                || profile.BlackLevel is not ushort black
                || profile.WhiteLevel is not ushort white
                || white <= black))
                return ScanDebugWorkflowGuideStep.Calibration;

            return ScanDebugWorkflowGuideStep.Scan;
        }
    }

    public string WorkflowGuideTitle => WorkflowGuideStep switch
    {
        ScanDebugWorkflowGuideStep.Configuration => "ScanDebug_WorkflowSetupTitle".GetLocalizedOrFallback("Suggested: prepare configuration"),
        ScanDebugWorkflowGuideStep.Calibration => "ScanDebug_WorkflowCalibrationTitle".GetLocalizedOrFallback("Suggested: review channel references"),
        ScanDebugWorkflowGuideStep.Review => "ScanDebug_WorkflowReviewTitle".GetLocalizedOrFallback("Suggested: check configuration"),
        _ => "ScanDebug_WorkflowSaveTitle".GetLocalizedOrFallback("Suggested: save and use for scanning")
    };

    public string WorkflowGuideDetail => WorkflowGuideStep switch
    {
        ScanDebugWorkflowGuideStep.Configuration => "ScanDebug_WorkflowSetupDetail".GetLocalizedOrFallback(
            "Choose channels and acquisition settings. Save a draft at any stage."),
        ScanDebugWorkflowGuideStep.Calibration => "ScanDebug_WorkflowCalibrationDetail".GetLocalizedOrFallback(
            "Review selected-channel parameters and black/white references. Connect hardware to acquire calibration samples. You can still validate and save the full configuration."),
        ScanDebugWorkflowGuideStep.Review => "ScanDebug_WorkflowReviewDetail".GetLocalizedFormatOrFallback(
            "There are {0} configuration issue(s). Locate them here, or save a draft.",
            Math.Max(1, CurrentFilmProfileValidationIssues.Count)),
        _ => "ScanDebug_WorkflowSaveDetail".GetLocalizedOrFallback(
            "The configuration passed structural checks. Save the full configuration to use it on the scan page; scanning does not start automatically.")
    };

    public string WorkflowGuideActionText => WorkflowGuideStep switch
    {
        ScanDebugWorkflowGuideStep.Configuration => "ScanDebug_WorkflowSetupAction".GetLocalizedOrFallback("Prepare configuration"),
        ScanDebugWorkflowGuideStep.Calibration => "ScanDebug_WorkflowCalibrationAction".GetLocalizedOrFallback("Review channels"),
        ScanDebugWorkflowGuideStep.Review => "ScanDebug_WorkflowReviewAction".GetLocalizedOrFallback("Validate and fix"),
        _ => "ScanDebug_WorkflowSaveAction".GetLocalizedOrFallback("Go to save and scan")
    };

    // Navigation remains available while hardware is busy; each operation retains its own gate.
    public bool WorkflowGuideActionEnabled => true;

    private void NotifyWorkflowGuideChanged()
    {
        NotifyWorkbenchStatusChanged();
        OnPropertyChanged(nameof(WorkflowGuideStep));
        OnPropertyChanged(nameof(WorkflowGuideTitle));
        OnPropertyChanged(nameof(WorkflowGuideDetail));
        OnPropertyChanged(nameof(WorkflowGuideActionText));
        OnPropertyChanged(nameof(WorkflowGuideActionEnabled));
    }
}
