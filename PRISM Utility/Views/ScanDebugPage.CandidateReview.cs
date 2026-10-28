using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage
{
    private bool _isCandidateReviewOpen;
    private double _candidateEditorScrollOffset;
    private Control? _candidateReturnFocus;
    private EventHandler<object>? _candidateReturnLayoutHandler;

    private void OpenCandidateReview()
    {
        if (_isCandidateReviewOpen || _isAuxiliaryPanelOpen || _activeWorkbenchSectionIndex != 2
            || ViewModel.PendingCalibrationCandidateReviewVisibility != Visibility.Visible)
            return;

        CancelCandidateReturn();
        ExitBwIssueView(returnFocus: false);
        CancelIssueTargetFocus();
        _candidateEditorScrollOffset = (_bwCompactMode ? ChannelCalibrationCompactScrollViewer : ChannelCalibrationScrollViewer).VerticalOffset;
        _candidateReturnFocus = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as Control : null;
        _isCandidateReviewOpen = true;
        ChannelCalibrationRail.Visibility = Visibility.Collapsed;
        ChannelCalibrationCompactScrollViewer.Visibility = Visibility.Collapsed;
        CalibrationCandidateReviewPanel.Visibility = Visibility.Visible;
        CalibrationCandidateReviewScrollViewer.ChangeView(null, 0, null, true);
        CandidateReturnToParametersButton.Focus(FocusState.Programmatic);
    }

    private void CandidateReturnToParametersButton_Click(object sender, RoutedEventArgs e)
        => CloseCandidateReview(returnFocus: true);

    private void CloseCandidateReview(bool returnFocus)
    {
        CancelCandidateReturn();
        if (!_isCandidateReviewOpen)
            return;

        _isCandidateReviewOpen = false;
        CalibrationCandidateReviewPanel.Visibility = Visibility.Collapsed;
        ChannelCalibrationRail.Visibility = Visibility.Visible;
        ChannelCalibrationCompactScrollViewer.Visibility = ToVisibility(_bwCompactMode);
        UpdateChannelCalibrationRailLayout(ChannelCalibrationSection.ActualHeight);
        var returnTarget = _candidateReturnFocus;
        _candidateReturnFocus = null;
        if (!returnFocus)
            return;

        // Wait for the restored editor to arrange before restoring its focus and scroll owner.
        _candidateReturnLayoutHandler = (_, _) =>
        {
            CancelCandidateReturn();
            var target = returnTarget is { IsEnabled: true } && IsVisibleInTree(returnTarget)
                ? returnTarget : _isAuxiliaryPanelOpen
                    ? CalibrationChannelFallbackComboBox : CalibrationChannelSelectorBar;
            target.Focus(FocusState.Programmatic);
            (_bwCompactMode ? ChannelCalibrationCompactScrollViewer : ChannelCalibrationScrollViewer)
                .ChangeView(null, _candidateEditorScrollOffset, null, true);
        };
        ChannelCalibrationSection.LayoutUpdated += _candidateReturnLayoutHandler;
    }

    private void CancelCandidateReturn()
    {
        if (_candidateReturnLayoutHandler is not null)
            ChannelCalibrationSection.LayoutUpdated -= _candidateReturnLayoutHandler;
        _candidateReturnLayoutHandler = null;
    }

    private void ObservationCompareFlyout_Opening(object sender, object e)
        => ConfigureLocalFlyoutBounds(ObservationCompareFlyout, ObservationCompareScrollViewer, ObservationCompareContent);

    private void ObservationSelectionFlyout_Opening(object sender, object e)
        => ConfigureLocalFlyoutBounds(ObservationSelectionFlyout, ObservationSelectionScrollViewer, ObservationSelectionContent);
}
