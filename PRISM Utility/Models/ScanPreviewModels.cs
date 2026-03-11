namespace PRISM_Utility.Models;

public sealed record ScanPreviewRenderOptions(
    bool IsWaterfallEnabled,
    bool IsWaterfallCompressedEnabled,
    bool IsGammaCorrectionEnabled,
    double Gamma);
