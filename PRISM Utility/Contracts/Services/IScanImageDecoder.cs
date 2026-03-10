namespace PRISM_Utility.Contracts.Services;

public interface IScanImageDecoder
{
    int GetDecodedPixelsPerLine();
    void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma);
    bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample);
}
