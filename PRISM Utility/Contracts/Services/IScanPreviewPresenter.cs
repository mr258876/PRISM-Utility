using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Models;

namespace PRISM_Utility.Contracts.Services;

public interface IScanPreviewPresenter
{
    bool TryRender(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error);

    void Reset();
}
