using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage
{
    private void UpdateConfigurationHandoffPresentation()
    {
        var action = "ScanDebug_SaveAndOpenScanButton/Content".GetLocalized();
        SaveAndOpenScanButton.Content = action;
        AutomationProperties.SetName(SaveAndOpenScanButton, action);
        AutomationProperties.SetHelpText(SaveAndOpenScanButton, ViewModel.FilmProfileSaveHintText);
        ToolTipService.SetToolTip(SaveAndOpenScanButton, ViewModel.FilmProfileSaveHintText);
    }

    private void FilmProfileWorkflowNextButton_Click(object sender, RoutedEventArgs e)
    {
        FilmProfileWorkflowFlyout.ShowAt(FilmProfileWorkflowNextButton);
    }

    private void FilmProfileWorkflowStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string stepName }
            && Enum.TryParse<ScanDebugWorkflowGuideStep>(stepName, out var step))
            NavigateFilmProfileWorkflowStep(step);
    }

    private void NavigateFilmProfileWorkflowStep(ScanDebugWorkflowGuideStep step)
    {
        FilmProfileWorkflowFlyout.Hide();
        ViewModel.CancelPendingCurrentFilmProfileIssueNavigation();
        CloseCandidateReview(returnFocus: false);
        ExitBwIssueView(returnFocus: false);

        switch (step)
        {
            case ScanDebugWorkflowGuideStep.Configuration:
                SelectWorkbenchTask(0);
                BeginIssueTargetFocus(1, AcquisitionPlanScrollViewer, AcquisitionRowsComboBox);
                break;
            case ScanDebugWorkflowGuideStep.Calibration:
                OpenBlackWhiteTaskButton_Click(this, new RoutedEventArgs());
                break;
            case ScanDebugWorkflowGuideStep.Review:
                NavigateCalibrationGuideStep(ScanDebugCalibrationGuideStep.ReviewConfiguration);
                break;
            case ScanDebugWorkflowGuideStep.Scan:
                FilmProfileLifecycleHeader.StartBringIntoView();
                EnqueueForCurrentActivation(() =>
                {
                    if (!SaveAndOpenScanButton.Focus(FocusState.Programmatic))
                        WorkbenchValidationButton.Focus(FocusState.Programmatic);
                });
                break;
        }
    }
}
