using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage
{
    private const float MinimumPreviewZoom = 0.001f;
    private const float MaximumPreviewZoom = 20f;
    private const float PreviewZoomEpsilon = 0.000001f;
    private bool _isPreviewFitMode = true;
    private bool _isChangingPreviewContentScale;
    private float _previewContentScale = 1;
    private float? _lastAppliedPreviewFitZoom;

    private float ActualPreviewZoom => PreviewScrollViewer.ZoomFactor * _previewContentScale;

    private void SetPreviewContentScale(float actualZoom)
    {
        // Keep the canvas and all hit-testing in original image pixels. Only the
        // surrounding Viewbox changes size to get below ScrollViewer's 10% limit.
        var targetZoom = Math.Clamp(actualZoom, MinimumPreviewZoom, MaximumPreviewZoom);
        _isChangingPreviewContentScale = true;
        try
        {
            _previewContentScale = Math.Min(1, targetZoom / PreviewScrollViewer.MinZoomFactor);
            PreviewScrollViewer.MaxZoomFactor = MaximumPreviewZoom / _previewContentScale;
            RefreshPreviewContentScaleDimensions();
            PreviewScrollViewer.UpdateLayout();
        }
        finally
        {
            _isChangingPreviewContentScale = false;
        }
    }

    private void RefreshPreviewContentScaleDimensions()
    {
        var width = double.IsFinite(PreviewCanvas.Width) ? Math.Max(0, PreviewCanvas.Width) : 0;
        var height = double.IsFinite(PreviewCanvas.Height) ? Math.Max(0, PreviewCanvas.Height) : 0;
        PreviewZoomContainer.Width = width * _previewContentScale;
        PreviewZoomContainer.Height = height * _previewContentScale;
    }

    private void FitPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HasValidPreviewFrame())
            return;

        _isPreviewFitMode = true;
        _pendingInitialFitZoom = true;
        ApplyInitialFitZoom();
        UpdatePrecisePreviewZoomControls();
    }

    private void ActualSizePreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HasValidPreviewFrame())
            return;

        MarkPreviewZoomAsManual();
        ApplyZoom(1, new Point(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2));
        UpdatePrecisePreviewZoomControls();
    }

    private void MarkPreviewZoomAsManual()
    {
        _isPreviewFitMode = false;
        _pendingInitialFitZoom = false;
    }

    private void RememberPreviewFitZoom(float zoom)
        => _lastAppliedPreviewFitZoom = zoom;

    private void ObservePreviewZoomViewChange(ScrollViewerViewChangedEventArgs e)
    {
        // Touch pinch bypasses ApplyZoom. It should keep the resulting view on the
        // next frame, just like the explicit zoom buttons and preset selector.
        if (HasValidPreviewFrame() && !e.IsIntermediate && !_isChangingPreviewContentScale
            && !_pendingInitialFitZoom && _isPreviewFitMode
            && _lastAppliedPreviewFitZoom is { } fitZoom
            && Math.Abs(ActualPreviewZoom - fitZoom) > Math.Max(PreviewZoomEpsilon, fitZoom * 0.0001f))
            MarkPreviewZoomAsManual();

        UpdatePrecisePreviewZoomControls();
    }

    private void UpdatePrecisePreviewZoomControls()
    {
        var hasImage = HasValidPreviewFrame();
        var currentZoom = ActualPreviewZoom;
        FitPreviewButton.IsEnabled = hasImage;
        ActualSizePreviewButton.IsEnabled = hasImage;
        ZoomScaleComboBox.IsEnabled = hasImage;
        ZoomOutButton.IsEnabled = hasImage && currentZoom > MinimumPreviewZoom + PreviewZoomEpsilon;
        ZoomInButton.IsEnabled = hasImage && currentZoom < MaximumPreviewZoom - PreviewZoomEpsilon;
        if ((Control)FitPreviewButton is ToggleButton fitToggle)
            fitToggle.IsChecked = _isPreviewFitMode;

        var exactPresetIndex = -1;
        if (hasImage)
        {
            for (var index = 0; index < ZoomLevels.Length; index++)
            {
                if (Math.Abs(ZoomLevels[index] - currentZoom) < PreviewZoomEpsilon)
                {
                    exactPresetIndex = index;
                    break;
                }
            }
        }

        // An automatically fitted percentage often falls between the presets.
        // Show the actual value as the placeholder rather than a nearby preset.
        var wasUpdating = _updatingZoomScaleComboBox;
        _updatingZoomScaleComboBox = true;
        try
        {
            ZoomScaleComboBox.PlaceholderText = hasImage ? FormatZoomLabel(currentZoom) : "—";
            ZoomScaleComboBox.SelectedIndex = exactPresetIndex < ZoomScaleComboBox.Items.Count ? exactPresetIndex : -1;
        }
        finally
        {
            _updatingZoomScaleComboBox = wasUpdating;
        }
    }
}
