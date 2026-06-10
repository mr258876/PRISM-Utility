namespace PRISM_Utility.Core.Models;

public enum DngPixelLayout
{
    Unknown = 0,
    RawMosaic = 1,
    LinearRgb = 2,
    MonochromeRaw = 3,
    LinearRawMultiChannel = 4
}

public enum ScanDngExportMode
{
    LinearRaw4 = 0,
    LinearRgbIrw = 1
}

public enum DngChannelColor
{
    Red = 0,
    Green = 1,
    Blue = 2,
    Cyan = 3,
    Magenta = 4,
    Yellow = 5,
    White = 6
}

public enum DngCfaPattern
{
    Unknown = 0,
    Rggb = 1,
    Bggr = 2,
    Grbg = 3,
    Gbrg = 4
}

public sealed record DngRectangle(uint Top, uint Left, uint Bottom, uint Right)
{
    public static DngRectangle Empty { get; } = new(0, 0, 0, 0);
}

public sealed record DngRational(uint Numerator, uint Denominator);

public sealed record DngBlackLevelPlane(double TopLeft, double TopRight, double BottomLeft, double BottomRight);

public sealed record DngColorMetadata(
    double[]? AnalogBalance = null,
    double[]? CameraNeutral = null,
    double[]? ColorMatrix1 = null,
    double[]? ColorMatrix2 = null);

public sealed record DngWriteRequest(
    string OutputPath,
    byte[] PixelData,
    uint Width,
    uint Height,
    uint RowStrideBytes,
    ushort BitsPerSample,
    ushort SamplesPerPixel,
    DngPixelLayout PixelLayout,
    DngCfaPattern CfaPattern,
    DngChannelColor[]? ChannelColors = null,
    string? Make = null,
    string? Model = null,
    string? Software = null,
    uint IsoSpeed = 0,
    DngRational? ExposureTime = null,
    uint BlackLevel = 0,
    uint WhiteLevel = 0,
    DateTimeOffset? CaptureTime = null,
    DngRectangle? ActiveArea = null,
    DngRectangle? DefaultCrop = null,
    DngRectangle[]? MaskedAreas = null,
    DngBlackLevelPlane[]? BlackLevelPlanes = null,
    DngColorMetadata? Color = null);
