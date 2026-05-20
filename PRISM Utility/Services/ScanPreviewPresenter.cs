using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Services;

public sealed class ScanPreviewPresenter : IScanPreviewPresenter
{
    private readonly IScanImageDecoder _imageDecoder;
    private byte[] _waterfallPixels = Array.Empty<byte>();
    private byte[] _waterfallFramePixels = Array.Empty<byte>();
    private byte[] _waterfallStripPixels = Array.Empty<byte>();
    private int _nextFrameVersion;

    public ScanPreviewPresenter(IScanImageDecoder imageDecoder)
    {
        _imageDecoder = imageDecoder;
    }

    public bool TryRender(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, ScanPreviewFrame? reusableFrame, out ScanPreviewFrame? frame, out string error)
    {
        frame = reusableFrame;
        error = string.Empty;

        if (!TryGetGamma(options, out var gamma, out error))
            return false;

        frame = options.IsWaterfallEnabled
            ? RenderWaterfallPreviewFrame(lineBuffer, rows, options, gamma)
            : RenderFramePreviewFrame(lineBuffer, rows, options, gamma, reusableFrame);
        return true;
    }

    public bool TryRender(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error)
    {
        bitmap = currentBitmap;
        error = string.Empty;

        if (!TryGetGamma(options, out var gamma, out error))
            return false;

        bitmap = options.IsWaterfallEnabled
            ? RenderWaterfallPreview(lineBuffer, rows, options, bitmap, gamma)
            : RenderFramePreview(lineBuffer, rows, options, bitmap, gamma);
        return true;
    }

    public void Reset()
    {
        _waterfallPixels = Array.Empty<byte>();
        _waterfallFramePixels = Array.Empty<byte>();
        _waterfallStripPixels = Array.Empty<byte>();
    }

    private ScanPreviewFrame RenderFramePreviewFrame(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, double gamma, ScanPreviewFrame? reusableFrame)
    {
        var previewWidth = _imageDecoder.GetDecodedPixelsPerLine();
        var rowBytes = previewWidth * 4;
        var totalBytes = rowBytes * rows;
        var pixels = CanReuseFramePixels(reusableFrame, previewWidth, rows, rowBytes, totalBytes)
            ? reusableFrame!.Pixels
            : new byte[totalBytes];

        _imageDecoder.DecodeToBgra(lineBuffer, rows, pixels, options.IsGammaCorrectionEnabled, gamma, options.IsWhiteLevelEnabled, options.WhiteLevel);
        return new ScanPreviewFrame(pixels, previewWidth, rows, rowBytes, ScanPreviewPixelFormat.Bgra8, GetNextFrameVersion());
    }

    private WriteableBitmap RenderFramePreview(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? bitmap, double gamma)
    {
        var previewWidth = _imageDecoder.GetDecodedPixelsPerLine();
        if (bitmap is null || bitmap.PixelWidth != previewWidth || bitmap.PixelHeight != rows)
            bitmap = new WriteableBitmap(previewWidth, rows);

        using var stream = bitmap.PixelBuffer.AsStream();
        _imageDecoder.DecodeToBgra(lineBuffer, rows, stream, options.IsGammaCorrectionEnabled, gamma, options.IsWhiteLevelEnabled, options.WhiteLevel);
        bitmap.Invalidate();
        return bitmap;
    }

    private ScanPreviewFrame RenderWaterfallPreviewFrame(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, double gamma)
    {
        var previewWidth = _imageDecoder.GetDecodedPixelsPerLine();
        var previewHeight = ScanDebugConstants.WaterfallPreviewHeight;
        var rowBytes = previewWidth * 4;
        var totalBytes = rowBytes * previewHeight;

        EnsureWaterfallBuffers(totalBytes, rowBytes);
        UpdateWaterfallPixels(lineBuffer, rows, options, gamma, previewHeight, rowBytes);
        return new ScanPreviewFrame(_waterfallPixels, previewWidth, previewHeight, rowBytes, ScanPreviewPixelFormat.Bgra8, GetNextFrameVersion());
    }

    private WriteableBitmap RenderWaterfallPreview(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? bitmap, double gamma)
    {
        var previewWidth = _imageDecoder.GetDecodedPixelsPerLine();
        var previewHeight = ScanDebugConstants.WaterfallPreviewHeight;
        var rowBytes = previewWidth * 4;
        var totalBytes = rowBytes * previewHeight;

        if (bitmap is null || bitmap.PixelWidth != previewWidth || bitmap.PixelHeight != previewHeight)
            bitmap = new WriteableBitmap(previewWidth, previewHeight);

        EnsureWaterfallBuffers(totalBytes, rowBytes);
        UpdateWaterfallPixels(lineBuffer, rows, options, gamma, previewHeight, rowBytes);

        using var stream = bitmap.PixelBuffer.AsStream();
        stream.Position = 0;
        stream.Write(_waterfallPixels, 0, _waterfallPixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    private void EnsureWaterfallBuffers(int totalBytes, int rowBytes)
    {
        if (_waterfallPixels.Length != totalBytes)
        {
            _waterfallPixels = new byte[totalBytes];
            InitializeWaterfallAlpha(_waterfallPixels);
        }

        if (_waterfallStripPixels.Length != rowBytes)
            _waterfallStripPixels = new byte[rowBytes];
    }

    private void UpdateWaterfallPixels(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, double gamma, int previewHeight, int rowBytes)
    {
        if (options.IsWaterfallCompressedEnabled)
        {
            _imageDecoder.DecodeWaterfallStripToBgra(lineBuffer, rows, _waterfallStripPixels, options.IsGammaCorrectionEnabled, gamma, options.IsWhiteLevelEnabled, options.WhiteLevel);
            if (_waterfallPixels.Length > rowBytes)
                Buffer.BlockCopy(_waterfallPixels, 0, _waterfallPixels, rowBytes, _waterfallPixels.Length - rowBytes);

            Buffer.BlockCopy(_waterfallStripPixels, 0, _waterfallPixels, 0, rowBytes);
            return;
        }

        var insertRows = Math.Min(rows, previewHeight);
        var insertBytes = insertRows * rowBytes;
        if (_waterfallFramePixels.Length != insertBytes)
            _waterfallFramePixels = new byte[insertBytes];

        _imageDecoder.DecodeToBgra(lineBuffer, insertRows, _waterfallFramePixels, options.IsGammaCorrectionEnabled, gamma, options.IsWhiteLevelEnabled, options.WhiteLevel);

        var moveBytes = _waterfallPixels.Length - insertBytes;
        if (moveBytes > 0)
            Buffer.BlockCopy(_waterfallPixels, 0, _waterfallPixels, insertBytes, moveBytes);

        Buffer.BlockCopy(_waterfallFramePixels, 0, _waterfallPixels, 0, insertBytes);
    }

    private static bool CanReuseFramePixels(ScanPreviewFrame? reusableFrame, int width, int height, int strideBytes, int totalBytes)
        => reusableFrame is { PixelFormat: ScanPreviewPixelFormat.Bgra8 }
            && reusableFrame.Width == width
            && reusableFrame.Height == height
            && reusableFrame.StrideBytes == strideBytes
            && reusableFrame.Pixels.Length == totalBytes;

    private static bool TryGetGamma(ScanPreviewRenderOptions options, out double gamma, out string error)
    {
        gamma = 1.0;
        error = string.Empty;

        if (!options.IsGammaCorrectionEnabled)
            return true;

        gamma = options.Gamma;
        if (!double.IsNaN(gamma) && !double.IsInfinity(gamma) && gamma > 0)
            return true;

        error = "Gamma must be a number greater than 0.";
        return false;
    }

    private int GetNextFrameVersion()
    {
        unchecked
        {
            _nextFrameVersion++;
            return _nextFrameVersion;
        }
    }

    private static void InitializeWaterfallAlpha(byte[] pixels)
    {
        for (var i = 3; i < pixels.Length; i += 4)
            pixels[i] = 255;
    }
}
