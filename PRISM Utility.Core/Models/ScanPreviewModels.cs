namespace PRISM_Utility.Core.Models;

public enum ScanPreviewPixelFormat
{
    Bgra8,
    Gray16
}

public sealed record ScanPreviewFrame(
    byte[] Pixels,
    int Width,
    int Height,
    int StrideBytes,
    ScanPreviewPixelFormat PixelFormat,
    int Version);

public sealed record ScanPreviewRenderOptions(
    bool IsWaterfallEnabled,
    bool IsWaterfallCompressedEnabled,
    bool IsGammaCorrectionEnabled,
    double Gamma,
    bool IsWhiteLevelEnabled,
    ushort WhiteLevel);
