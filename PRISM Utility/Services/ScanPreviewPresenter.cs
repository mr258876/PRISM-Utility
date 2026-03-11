using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public sealed class ScanPreviewPresenter : IScanPreviewPresenter
{
    private readonly IScanImageDecoder _imageDecoder;
    private byte[] _waterfallPixels = Array.Empty<byte>();
    private byte[] _waterfallFramePixels = Array.Empty<byte>();
    private byte[] _waterfallStripPixels = Array.Empty<byte>();

    public ScanPreviewPresenter(IScanImageDecoder imageDecoder)
    {
        _imageDecoder = imageDecoder;
    }

    public bool TryRender(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error)
    {
        bitmap = currentBitmap;
        error = string.Empty;

        var gamma = 1.0;
        if (options.IsGammaCorrectionEnabled)
        {
            gamma = options.Gamma;
            if (double.IsNaN(gamma) || double.IsInfinity(gamma) || gamma <= 0)
            {
                error = "Gamma must be a number greater than 0.";
                return false;
            }
        }

        if (options.IsWaterfallEnabled)
        {
            bitmap = RenderWaterfallPreview(lineBuffer, rows, options, bitmap, gamma);
            return true;
        }

        bitmap = RenderFramePreview(lineBuffer, rows, options, bitmap, gamma);
        return true;
    }

    public void Reset()
    {
        _waterfallPixels = Array.Empty<byte>();
        _waterfallFramePixels = Array.Empty<byte>();
        _waterfallStripPixels = Array.Empty<byte>();
    }

    private WriteableBitmap RenderFramePreview(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? bitmap, double gamma)
    {
        var previewWidth = _imageDecoder.GetDecodedPixelsPerLine();
        if (bitmap is null || bitmap.PixelWidth != previewWidth || bitmap.PixelHeight != rows)
            bitmap = new WriteableBitmap(previewWidth, rows);

        using var stream = bitmap.PixelBuffer.AsStream();
        _imageDecoder.DecodeToBgra(lineBuffer, rows, stream, options.IsGammaCorrectionEnabled, gamma);
        bitmap.Invalidate();
        return bitmap;
    }

    private WriteableBitmap RenderWaterfallPreview(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? bitmap, double gamma)
    {
        var previewWidth = _imageDecoder.GetDecodedPixelsPerLine();
        var previewHeight = ScanDebugConstants.WaterfallPreviewHeight;
        var rowBytes = previewWidth * 4;
        var totalBytes = rowBytes * previewHeight;

        if (bitmap is null || bitmap.PixelWidth != previewWidth || bitmap.PixelHeight != previewHeight)
            bitmap = new WriteableBitmap(previewWidth, previewHeight);

        if (_waterfallPixels.Length != totalBytes)
        {
            _waterfallPixels = new byte[totalBytes];
            InitializeWaterfallAlpha(_waterfallPixels);
        }

        if (_waterfallStripPixels.Length != rowBytes)
            _waterfallStripPixels = new byte[rowBytes];

        if (options.IsWaterfallCompressedEnabled)
        {
            _imageDecoder.DecodeWaterfallStripToBgra(lineBuffer, rows, _waterfallStripPixels, options.IsGammaCorrectionEnabled, gamma);
            if (_waterfallPixels.Length > rowBytes)
                Buffer.BlockCopy(_waterfallPixels, 0, _waterfallPixels, rowBytes, _waterfallPixels.Length - rowBytes);

            Buffer.BlockCopy(_waterfallStripPixels, 0, _waterfallPixels, 0, rowBytes);
        }
        else
        {
            var insertRows = Math.Min(rows, previewHeight);
            var insertBytes = insertRows * rowBytes;
            if (_waterfallFramePixels.Length != insertBytes)
                _waterfallFramePixels = new byte[insertBytes];

            using (var frameStream = new MemoryStream(_waterfallFramePixels, 0, insertBytes, true, true))
                _imageDecoder.DecodeToBgra(lineBuffer, insertRows, frameStream, options.IsGammaCorrectionEnabled, gamma);

            var moveBytes = _waterfallPixels.Length - insertBytes;
            if (moveBytes > 0)
                Buffer.BlockCopy(_waterfallPixels, 0, _waterfallPixels, insertBytes, moveBytes);

            Buffer.BlockCopy(_waterfallFramePixels, 0, _waterfallPixels, 0, insertBytes);
        }

        using var stream = bitmap.PixelBuffer.AsStream();
        stream.Position = 0;
        stream.Write(_waterfallPixels, 0, _waterfallPixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    private static void InitializeWaterfallAlpha(byte[] pixels)
    {
        for (var i = 3; i < pixels.Length; i += 4)
            pixels[i] = 255;
    }
}
