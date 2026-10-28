using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PRISM_Utility.Helpers;
using Windows.System;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage
{
    private const string LineProfileOverlayTag = "LineProfileSamplingLine";
    private bool _isLineProfileDragging;
    private uint _lineProfilePointerId;
    private Guid _lineProfileCaptureId;

    private bool IsLineProfileVisible => RawSignalProfileScrollViewer.Visibility == Visibility.Visible;

    private bool TryBeginLineProfileSelection(PointerRoutedEventArgs e)
    {
        if (!IsLineProfileVisible || !ViewModel.CanSelectRawSignalRowFromPreview
            || ViewModel.CanMutateRoiFromPreview || ViewModel.CanMutateColumnSampleFromPreview || ViewModel.CanMutateObservationFromPreview
            || ViewModel.ObservationFrame is not { } frame)
            return false;
        var point = e.GetCurrentPoint(PreviewCanvasControl);
        if (!point.Properties.IsLeftButtonPressed || point.Position.X < 0 || point.Position.X >= frame.Width
            || point.Position.Y < 0 || point.Position.Y >= frame.Height
            || !ViewModel.SelectRawSignalPreviewRow((int)Math.Floor(point.Position.Y)))
            return false;

        // Sampling a row and editing a column ROI are separate gestures. Profile mode takes
        // the left drag; the existing preview tools still provide precise ROI editing.
        _isLineProfileDragging = true;
        _lineProfilePointerId = point.PointerId;
        _lineProfileCaptureId = ViewModel.RawSignalCaptureIdentity;
        PreviewScrollViewer.Focus(FocusState.Pointer);
        PreviewCanvasControl.CapturePointer(e.Pointer);
        DrawLineProfileOverlay();
        e.Handled = true;
        return true;
    }

    private bool TryMoveLineProfileSelection(PointerRoutedEventArgs e)
    {
        if (!_isLineProfileDragging)
            return false;
        var point = e.GetCurrentPoint(PreviewCanvasControl);
        if (point.PointerId != _lineProfilePointerId)
            return false;
        if (!IsLineProfileVisible || !ViewModel.CanSelectRawSignalRowFromPreview
            || ViewModel.RawSignalCaptureIdentity != _lineProfileCaptureId
            || ViewModel.ObservationFrame is not { } frame)
        {
            CancelLineProfileSelection();
            e.Handled = true;
            return true;
        }

        var row = (int)Math.Clamp(Math.Floor(point.Position.Y), 0, frame.Height - 1);
        ViewModel.SelectRawSignalPreviewRow(row);
        DrawLineProfileOverlay();
        e.Handled = true;
        return true;
    }

    private bool TryEndLineProfileSelection(PointerRoutedEventArgs e)
    {
        if (!_isLineProfileDragging || e.GetCurrentPoint(PreviewCanvasControl).PointerId != _lineProfilePointerId)
            return false;
        CancelLineProfileSelection();
        e.Handled = true;
        return true;
    }

    private void CancelLineProfileSelection()
    {
        _isLineProfileDragging = false;
        _lineProfilePointerId = 0;
        _lineProfileCaptureId = Guid.Empty;
        PreviewCanvasControl.ReleasePointerCaptures();
    }

    private void LineProfilePreview_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsLineProfileVisible || e.Key is not (VirtualKey.Up or VirtualKey.Down))
            return;
        if (ViewModel.MoveRawSignalRow(e.Key == VirtualKey.Up ? -1 : 1))
        {
            DrawLineProfileOverlay();
            e.Handled = true;
        }
    }

    private void DrawLineProfileOverlay()
    {
        foreach (var previous in RoiCanvas.Children.OfType<FrameworkElement>()
                     .Where(element => Equals(element.Tag, LineProfileOverlayTag)).ToArray())
            RoiCanvas.Children.Remove(previous);
        if (!IsLineProfileVisible || ViewModel.ObservationFrame is not { } frame
            || !ViewModel.TryGetRawSignalPreviewRow(out var row))
            return;

        var zoom = Math.Max(MinimumPreviewZoom, ActualPreviewZoom);
        var brush = new SolidColorBrush(Colors.Cyan);
        // The overlay shares image coordinates, but its line and label must remain legible
        // when the full sensor width is fitted into a small preview.
        RoiCanvas.Children.Add(new Line
        {
            X1 = AxisMarginLeft,
            X2 = AxisMarginLeft + frame.Width,
            Y1 = AxisMarginTop + row + 0.5,
            Y2 = AxisMarginTop + row + 0.5,
            Stroke = new SolidColorBrush(Colors.Black),
            StrokeThickness = 3.5 / zoom,
            Opacity = 0.75,
            Tag = LineProfileOverlayTag
        });
        var line = new Line
        {
            X1 = AxisMarginLeft,
            X2 = AxisMarginLeft + frame.Width,
            Y1 = AxisMarginTop + row + 0.5,
            Y2 = AxisMarginTop + row + 0.5,
            Stroke = brush,
            StrokeThickness = 1.5 / zoom,
            Tag = LineProfileOverlayTag
        };
        RoiCanvas.Children.Add(line);
        if (frame.Height * zoom < 60)
            return;
        var label = new Border
        {
            Background = new SolidColorBrush(Colors.Black),
            Opacity = 0.9,
            Padding = new Thickness(4 / zoom, 1 / zoom, 4 / zoom, 1 / zoom),
            Child = new TextBlock
            {
                Text = $"{"ScanDebug_RawSignalSamplingLine".GetLocalized()} · {ViewModel.RawSignalPositionText}",
                Foreground = brush,
                FontSize = 11 / zoom
            },
            Tag = LineProfileOverlayTag
        };
        Canvas.SetLeft(label, AxisMarginLeft + 6 / zoom);
        Canvas.SetTop(label, AxisMarginTop + Math.Max(0, row - 22 / zoom));
        RoiCanvas.Children.Add(label);
    }
}
