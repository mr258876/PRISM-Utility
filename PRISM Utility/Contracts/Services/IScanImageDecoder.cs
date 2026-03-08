using PRISM_Utility.Models;

namespace PRISM_Utility.Contracts.Services;

public interface IScanImageDecoder
{
    int GetDecodedPixelsPerLine();
    void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination);
    bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample);
}
