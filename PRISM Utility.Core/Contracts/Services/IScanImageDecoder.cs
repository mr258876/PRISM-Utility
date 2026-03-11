namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanImageDecoder
{
    int GetDecodedPixelsPerLine();
    void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma);
    void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma);
    bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample);
}
