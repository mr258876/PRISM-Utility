using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage
{
    private readonly Dictionary<bool, (double Editor, double Compact)> _channelRouteScrollPositions = [];
    private EventHandler<object>? _channelRouteRestoreLayoutHandler;

    private void RememberChannelRouteScrollPosition()
    {
        if (_activeWorkbenchSectionIndex != 2 || _isCandidateReviewOpen
            || ChannelCalibrationIssuePanel.Visibility == Visibility.Visible)
            return;

        var isDetails = ChannelCalibrationDetailsGrid.Visibility == Visibility.Visible;
        _channelRouteScrollPositions[isDetails] = (
            ChannelCalibrationScrollViewer.VerticalOffset,
            ChannelCalibrationCompactScrollViewer.VerticalOffset);
    }

    private void RestoreChannelRouteScrollPositionAfterLayout(bool isDetails)
    {
        CancelChannelRouteScrollRestore();
        var offsets = _channelRouteScrollPositions.GetValueOrDefault(isDetails);
        var activationEpoch = _activationEpoch;
        var focusRequestVersion = _issueTargetFocusRequestVersion;
        _channelRouteRestoreLayoutHandler = (_, _) =>
        {
            CancelChannelRouteScrollRestore();
            // Explicit issue navigation owns scrolling and focus when it follows a route change.
            if (!IsCurrentActivation(activationEpoch) || _activeWorkbenchSectionIndex != 2
                || _isAuxiliaryPanelOpen != isDetails || focusRequestVersion != _issueTargetFocusRequestVersion)
                return;

            var scroller = _bwCompactMode ? ChannelCalibrationCompactScrollViewer : ChannelCalibrationScrollViewer;
            _ = scroller.ChangeView(null, _bwCompactMode ? offsets.Compact : offsets.Editor, null, true);
        };
        ChannelCalibrationRail.LayoutUpdated += _channelRouteRestoreLayoutHandler;
    }

    private void CancelChannelRouteScrollRestore()
    {
        if (_channelRouteRestoreLayoutHandler is not null)
            ChannelCalibrationRail.LayoutUpdated -= _channelRouteRestoreLayoutHandler;
        _channelRouteRestoreLayoutHandler = null;
    }

    private async void SaveCurrentConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SaveFilmProfileJsonCommand.CanExecute(null))
            await ViewModel.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        else if (ViewModel.CanUseEditingDraftFiles)
            await ViewModel.SaveEditingDraftAsync(App.GetService<IScanFilmProfileFileGateway>());
    }

    private void OpenDeviceChannelsSettings_Click(object sender, RoutedEventArgs e)
        => App.GetService<INavigationService>().NavigateTo(AppRoute.Settings,
            SettingsViewModel.DeviceChannelsNavigationParameter);

    private async void CalibrationChannelStatusListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ScanDebugCalibrationChannelItemViewModel channel)
            await ViewModel.TrySelectCalibrationChannelAsync(channel.Role);
    }
}
