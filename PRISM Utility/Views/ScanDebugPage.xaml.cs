using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PRISM_Utility.ViewModels;
using Windows.Foundation;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage : Page
{
    private const double AxisMarginLeft = 48;
    private const double AxisMarginTop = 28;
    private const double AxisMarginRight = 16;
    private const double AxisMarginBottom = 36;
    private static readonly float[] ZoomLevels = { 0.1f, 0.125f, 0.2f, 0.25f, 0.5f, 0.75f, 1f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 20f };
    private bool _updatingZoomScaleComboBox;
    private bool _pendingInitialFitZoom;
    private int _lastPreviewImageWidth = -1;
    private int _lastPreviewImageHeight = -1;
    private bool _isPanning;
    private uint _activePanPointerId;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

    public ScanDebugViewModel ViewModel
    {
        get;
    }

    public ScanDebugPage()
    {
        ViewModel = App.GetService<ScanDebugViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.CalibrationPromptRequested += OnCalibrationPromptRequested;
        InitializeZoomScaleComboBox();
        RefreshPreviewLayout();
    }

    private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.CalibrationPromptRequested -= OnCalibrationPromptRequested;
        await ViewModel.CleanupAsync();
    }

    private async void OnCalibrationPromptRequested(object? sender, ScanCalibrationPromptRequest e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = e.Prompt.Title,
                Content = e.Prompt.Content,
                PrimaryButtonText = e.Prompt.PrimaryButtonText,
                CloseButtonText = e.Prompt.CloseButtonText,
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            e.CompletionSource.TrySetResult(result == ContentDialogResult.Primary);
        }
        catch (Exception ex)
        {
            e.CompletionSource.TrySetException(ex);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanDebugViewModel.PreviewImage))
            _ = DispatcherQueue.TryEnqueue(RefreshPreviewLayout);
    }

    private void RefreshPreviewLayout()
    {
        var bitmap = ViewModel.PreviewImage;
        if (bitmap is null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            PreviewImageElement.Width = 0;
            PreviewImageElement.Height = 0;
            PreviewCanvas.Width = 0;
            PreviewCanvas.Height = 0;
            AxisCanvas.Children.Clear();
            CursorPositionTextBlock.Text = "Cursor: (-, -) px";
            CursorIntensityTextBlock.Text = "Intensity16: -";
            _pendingInitialFitZoom = false;
            _lastPreviewImageWidth = -1;
            _lastPreviewImageHeight = -1;
            UpdateZoomScaleComboBoxSelection();
            return;
        }

        var imageWidth = bitmap.PixelWidth;
        var imageHeight = bitmap.PixelHeight;
        var imageChanged = imageWidth != _lastPreviewImageWidth || imageHeight != _lastPreviewImageHeight;
        if (imageChanged)
        {
            _lastPreviewImageWidth = imageWidth;
            _lastPreviewImageHeight = imageHeight;
            _pendingInitialFitZoom = true;
        }

        PreviewImageElement.Width = imageWidth;
        PreviewImageElement.Height = imageHeight;
        Canvas.SetLeft(PreviewImageElement, AxisMarginLeft);
        Canvas.SetTop(PreviewImageElement, AxisMarginTop);

        PreviewCanvas.Width = AxisMarginLeft + imageWidth + AxisMarginRight;
        PreviewCanvas.Height = AxisMarginTop + imageHeight + AxisMarginBottom;
        AxisCanvas.Width = PreviewCanvas.Width;
        AxisCanvas.Height = PreviewCanvas.Height;

        DrawAxes(imageWidth, imageHeight);
        UpdateZoomScaleComboBoxSelection();

        if (_pendingInitialFitZoom)
            _ = DispatcherQueue.TryEnqueue(ApplyInitialFitZoom);
    }

    private void PreviewScrollViewer_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (_pendingInitialFitZoom)
            ApplyInitialFitZoom();
    }

    private void ApplyInitialFitZoom()
    {
        if (!_pendingInitialFitZoom)
            return;

        var viewportWidth = PreviewScrollViewer.ViewportWidth;
        var viewportHeight = PreviewScrollViewer.ViewportHeight;
        var contentWidth = PreviewCanvas.Width;
        var contentHeight = PreviewCanvas.Height;
        if (viewportWidth <= 1 || viewportHeight <= 1 || contentWidth <= 0 || contentHeight <= 0)
            return;

        var fitZoom = Math.Min(viewportWidth / contentWidth, viewportHeight / contentHeight);
        var targetZoom = Math.Clamp((float)fitZoom, PreviewScrollViewer.MinZoomFactor, PreviewScrollViewer.MaxZoomFactor);

        var offsetX = Math.Max((contentWidth * targetZoom - viewportWidth) / 2.0, 0);
        var offsetY = Math.Max((contentHeight * targetZoom - viewportHeight) / 2.0, 0);

        _pendingInitialFitZoom = false;
        _ = PreviewScrollViewer.ChangeView(offsetX, offsetY, targetZoom, true);
        UpdateZoomScaleComboBoxSelection();
    }

    private void PreviewScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.PreviewImage is null)
            return;

        var wheelDelta = e.GetCurrentPoint(PreviewScrollViewer).Properties.MouseWheelDelta;
        if (wheelDelta == 0)
            return;

        var anchor = e.GetCurrentPoint(PreviewScrollViewer).Position;
        StepZoom(wheelDelta > 0, anchor);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.PreviewImage is null)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _isPanning = true;
        _activePanPointerId = point.PointerId;
        _panStartPoint = point.Position;
        _panStartHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = PreviewScrollViewer.VerticalOffset;
        PreviewScrollViewer.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (point.PointerId != _activePanPointerId)
            return;

        var deltaX = point.Position.X - _panStartPoint.X;
        var deltaY = point.Position.Y - _panStartPoint.Y;
        var newHorizontalOffset = Math.Max(0, _panStartHorizontalOffset - deltaX);
        var newVerticalOffset = Math.Max(0, _panStartVerticalOffset - deltaY);

        _ = PreviewScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, null, true);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (point.PointerId != _activePanPointerId)
            return;

        EndPanning();
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning)
            return;

        var point = e.GetCurrentPoint(PreviewScrollViewer);
        if (point.PointerId != _activePanPointerId)
            return;

        EndPanning();
        e.Handled = true;
    }

    private void PreviewScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        => UpdateZoomScaleComboBoxSelection();

    private void ZoomInButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => StepZoom(true, new Point(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2));

    private void ZoomOutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => StepZoom(false, new Point(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2));

    private void ZoomScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingZoomScaleComboBox || ZoomScaleComboBox.SelectedIndex < 0)
            return;

        var selectedZoom = ZoomLevels[ZoomScaleComboBox.SelectedIndex];
        ApplyZoom(selectedZoom, new Point(PreviewScrollViewer.ViewportWidth / 2, PreviewScrollViewer.ViewportHeight / 2));
    }

    private void StepZoom(bool zoomIn, Point anchorInViewport)
    {
        var oldZoom = PreviewScrollViewer.ZoomFactor;
        float? nextZoom = null;

        if (zoomIn)
        {
            foreach (var level in ZoomLevels)
            {
                if (level > oldZoom + 0.001f)
                {
                    nextZoom = level;
                    break;
                }
            }
        }
        else
        {
            for (var i = ZoomLevels.Length - 1; i >= 0; i--)
            {
                var level = ZoomLevels[i];
                if (level < oldZoom - 0.001f)
                {
                    nextZoom = level;
                    break;
                }
            }
        }

        if (nextZoom is null)
            return;

        ApplyZoom(nextZoom.Value, anchorInViewport);
    }

    private void ApplyZoom(float targetZoom, Point anchorInViewport)
    {
        var oldZoom = PreviewScrollViewer.ZoomFactor;
        var newZoom = Math.Clamp(targetZoom, PreviewScrollViewer.MinZoomFactor, PreviewScrollViewer.MaxZoomFactor);
        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
        {
            UpdateZoomScaleComboBoxSelection();
            return;
        }

        var contentX = (PreviewScrollViewer.HorizontalOffset + anchorInViewport.X) / oldZoom;
        var contentY = (PreviewScrollViewer.VerticalOffset + anchorInViewport.Y) / oldZoom;

        var newHorizontalOffset = (contentX * newZoom) - anchorInViewport.X;
        var newVerticalOffset = (contentY * newZoom) - anchorInViewport.Y;

        _ = PreviewScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, newZoom, true);
        UpdateZoomScaleComboBoxSelection();
    }

    private void InitializeZoomScaleComboBox()
    {
        if (ZoomScaleComboBox.Items.Count > 0)
            return;

        foreach (var zoom in ZoomLevels)
            ZoomScaleComboBox.Items.Add(FormatZoomLabel(zoom));

        UpdateZoomScaleComboBoxSelection();
    }

    private void UpdateZoomScaleComboBoxSelection()
    {
        if (ZoomScaleComboBox.Items.Count == 0)
            return;

        var currentZoom = PreviewScrollViewer.ZoomFactor;
        var nearestIndex = 0;
        var nearestDelta = float.MaxValue;
        for (var i = 0; i < ZoomLevels.Length; i++)
        {
            var delta = Math.Abs(ZoomLevels[i] - currentZoom);
            if (delta < nearestDelta)
            {
                nearestDelta = delta;
                nearestIndex = i;
            }
        }

        if (ZoomScaleComboBox.SelectedIndex == nearestIndex)
            return;

        _updatingZoomScaleComboBox = true;
        ZoomScaleComboBox.SelectedIndex = nearestIndex;
        _updatingZoomScaleComboBox = false;
    }

    private static string FormatZoomLabel(float zoom)
        => $"{zoom * 100:0.###}%";

    private void EndPanning()
    {
        _isPanning = false;
        _activePanPointerId = 0;
        PreviewScrollViewer.ReleasePointerCaptures();
    }

    private void PreviewImageElement_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var bitmap = ViewModel.PreviewImage;
        if (bitmap is null)
            return;

        var point = e.GetCurrentPoint(PreviewImageElement).Position;
        var x = (int)Math.Floor(point.X);
        var y = (int)Math.Floor(point.Y);

        if (x < 0 || y < 0 || x >= bitmap.PixelWidth || y >= bitmap.PixelHeight)
        {
            CursorPositionTextBlock.Text = "Cursor: (-, -) px";
            CursorIntensityTextBlock.Text = "Intensity16: -";
            return;
        }

        CursorPositionTextBlock.Text = $"Cursor: ({x}, {y}) px";
        if (ViewModel.TryGetPreviewSample16(x, y, out var sample16))
            CursorIntensityTextBlock.Text = $"Intensity16: {sample16}";
        else
            CursorIntensityTextBlock.Text = "Intensity16: -";
    }

    private void PreviewImageElement_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        CursorPositionTextBlock.Text = "Cursor: (-, -) px";
        CursorIntensityTextBlock.Text = "Intensity16: -";
    }

    private void DrawAxes(int imageWidth, int imageHeight)
    {
        AxisCanvas.Children.Clear();

        var axisBrush = new SolidColorBrush(Colors.Gray);
        var textBrush = new SolidColorBrush(Colors.DarkGray);
        var originX = AxisMarginLeft;
        var originY = AxisMarginTop;
        var xEnd = originX + imageWidth;
        var yEnd = originY + imageHeight;

        AxisCanvas.Children.Add(new Line
        {
            X1 = originX,
            Y1 = originY,
            X2 = xEnd,
            Y2 = originY,
            Stroke = axisBrush,
            StrokeThickness = 1
        });

        AxisCanvas.Children.Add(new Line
        {
            X1 = originX,
            Y1 = originY,
            X2 = originX,
            Y2 = yEnd,
            Stroke = axisBrush,
            StrokeThickness = 1
        });

        var xStep = GetTickStep(imageWidth);
        for (var x = 0; x <= imageWidth; x += xStep)
        {
            var tickX = originX + x;
            AxisCanvas.Children.Add(new Line
            {
                X1 = tickX,
                Y1 = originY - 4,
                X2 = tickX,
                Y2 = originY,
                Stroke = axisBrush,
                StrokeThickness = 1
            });

            var label = new TextBlock { Text = x.ToString(), Foreground = textBrush, FontSize = 11 };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, tickX - (label.DesiredSize.Width / 2));
            Canvas.SetTop(label, 4);
            AxisCanvas.Children.Add(label);
        }

        var yStep = GetTickStep(imageHeight);
        for (var y = yStep; y <= imageHeight; y += yStep)
        {
            var tickY = originY + y;
            AxisCanvas.Children.Add(new Line
            {
                X1 = originX - 4,
                Y1 = tickY,
                X2 = originX,
                Y2 = tickY,
                Stroke = axisBrush,
                StrokeThickness = 1
            });

            var label = new TextBlock { Text = y.ToString(), Foreground = textBrush, FontSize = 11 };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Max(0, originX - 8 - label.DesiredSize.Width));
            Canvas.SetTop(label, tickY - (label.DesiredSize.Height / 2));
            AxisCanvas.Children.Add(label);
        }
    }

    private static int GetTickStep(int span)
    {
        if (span <= 0)
            return 1;

        var rough = Math.Max(1.0, span / 12.0);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var normalized = rough / magnitude;

        var nice = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };

        return (int)(nice * magnitude);
    }
}
