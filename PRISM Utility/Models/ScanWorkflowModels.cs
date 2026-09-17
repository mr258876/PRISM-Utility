using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Models;

public enum ScanDebugCaptureMode
{
    Single,
    Continuous,
    Transport
}

public sealed record ScanCompositeFrame(ScanCompositePixelBuffer Buffer, WriteableBitmap Bitmap)
{
    public byte[] Pixels => Buffer.Pixels;
    public int Width => Buffer.Width;
    public int Height => Buffer.Height;
}
