namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanImageDecoder
{
    int GetDecodedPixelsPerLine();
    (int Start, int EndInclusive) GetEffectivePixelRange();
    void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel);
    void DecodeToBgra(byte[] lineBuffer, int rows, Span<byte> destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel);
    void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel);
    bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample);
}
