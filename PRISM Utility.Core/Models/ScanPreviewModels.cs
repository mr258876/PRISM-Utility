namespace PRISM_Utility.Core.Models;

public sealed record ScanPreviewRenderOptions(
    bool IsWaterfallEnabled,
    bool IsWaterfallCompressedEnabled,
    bool IsGammaCorrectionEnabled,
    double Gamma);
