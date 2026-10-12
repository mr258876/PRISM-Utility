namespace PRISM_Utility.Core.Models;

/// <summary>A display-only copy. It never retains a decoder or presenter's reusable buffer.</summary>
public sealed record ScanObservationSnapshot(ScanPreviewFrame Frame, ScanRawSignalResult? Signal)
{
    // One frozen frame and one baseline can coexist. Bound their allocations before copying.
    public const int MaximumFrameBytes = 64 * 1024 * 1024;

    public static bool TryCreate(ScanPreviewFrame? frame, ScanRawSignalResult? signal,
        out ScanObservationSnapshot? snapshot)
    {
        snapshot = null;
        if (frame is not { Width: > 0, Height: > 0, StrideBytes: > 0 })
            return false;
        if (frame.PixelFormat is not (ScanPreviewPixelFormat.Bgra8 or ScanPreviewPixelFormat.Gray16))
            return false;
        var rowBytes = (long)frame.Width * (frame.PixelFormat == ScanPreviewPixelFormat.Bgra8 ? 4 : 2);
        var length = (long)frame.StrideBytes * frame.Height;
        if (frame.StrideBytes < rowBytes || length > MaximumFrameBytes || frame.Pixels.LongLength < length)
            return false;

        var pixels = new byte[(int)length];
        Buffer.BlockCopy(frame.Pixels, 0, pixels, 0, pixels.Length);
        var copiedSignal = signal is null ? null : signal with
        {
            Histogram = Array.AsReadOnly(signal.Histogram.ToArray()),
            Profile = Array.AsReadOnly(signal.Profile.ToArray())
        };
        snapshot = new ScanObservationSnapshot(frame with { Pixels = pixels }, copiedSignal);
        return true;
    }
}
