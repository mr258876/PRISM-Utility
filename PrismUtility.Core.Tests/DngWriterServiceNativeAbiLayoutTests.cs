using System.Runtime.InteropServices;

using PRISM_Utility.Core.Services;

using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "DNG001")]
[Trait("Category", "NativeAbiLayout")]
public sealed class DngWriterServiceNativeAbiLayoutTests
{
    [Fact]
    public void NativeAbiV2_X64LayoutMatchesPrismDngApiHeaderContract()
    {
        Assert.Equal(8, IntPtr.Size);

        Assert.Equal(664, Marshal.SizeOf<NativePrismDngWriteRequestV2>());
        Assert.Equal(0, Offset<NativePrismDngWriteRequestV2>("StructSize"));
        Assert.Equal(4, Offset<NativePrismDngWriteRequestV2>("AbiVersion"));
        Assert.Equal(16, Offset<NativePrismDngWriteRequestV2>("OutputPath"));
        Assert.Equal(24, Offset<NativePrismDngWriteRequestV2>("Image"));
        Assert.Equal(64, Offset<NativePrismDngWriteRequestV2>("Metadata"));
        Assert.Equal(616, Offset<NativePrismDngWriteRequestV2>("Reserved"));

        Assert.Equal(40, Marshal.SizeOf<NativePrismDngImageBuffer>());
        Assert.Equal(0, Offset<NativePrismDngImageBuffer>("Data"));
        Assert.Equal(8, Offset<NativePrismDngImageBuffer>("DataBytes"));
        Assert.Equal(16, Offset<NativePrismDngImageBuffer>("Width"));
        Assert.Equal(20, Offset<NativePrismDngImageBuffer>("Height"));
        Assert.Equal(24, Offset<NativePrismDngImageBuffer>("RowStrideBytes"));
        Assert.Equal(28, Offset<NativePrismDngImageBuffer>("BitsPerSample"));
        Assert.Equal(30, Offset<NativePrismDngImageBuffer>("SamplesPerPixel"));
        Assert.Equal(32, Offset<NativePrismDngImageBuffer>("PixelLayout"));
        Assert.Equal(36, Offset<NativePrismDngImageBuffer>("CfaPattern"));

        Assert.Equal(552, Marshal.SizeOf<NativePrismDngMetadata>());
        Assert.Equal(0, Offset<NativePrismDngMetadata>("Make"));
        Assert.Equal(8, Offset<NativePrismDngMetadata>("Model"));
        Assert.Equal(16, Offset<NativePrismDngMetadata>("Software"));
        Assert.Equal(24, Offset<NativePrismDngMetadata>("UniqueCameraModel"));
        Assert.Equal(32, Offset<NativePrismDngMetadata>("IsoSpeed"));
        Assert.Equal(36, Offset<NativePrismDngMetadata>("ExposureTime"));
        Assert.Equal(44, Offset<NativePrismDngMetadata>("FrameRate"));
        Assert.Equal(52, Offset<NativePrismDngMetadata>("BlackLevel"));
        Assert.Equal(56, Offset<NativePrismDngMetadata>("WhiteLevel"));
        Assert.Equal(60, Offset<NativePrismDngMetadata>("CaptureTime"));
        Assert.Equal(92, Offset<NativePrismDngMetadata>("ActiveArea"));
        Assert.Equal(108, Offset<NativePrismDngMetadata>("DefaultCrop"));
        Assert.Equal(124, Offset<NativePrismDngMetadata>("MaskedAreas"));
        Assert.Equal(188, Offset<NativePrismDngMetadata>("MaskedAreaCount"));
        Assert.Equal(192, Offset<NativePrismDngMetadata>("BlackLevelPlanes"));
        Assert.Equal(320, Offset<NativePrismDngMetadata>("BlackLevelPlaneCount"));
        Assert.Equal(328, Offset<NativePrismDngMetadata>("Color"));
        Assert.Equal(528, Offset<NativePrismDngMetadata>("ChannelColors"));
        Assert.Equal(544, Offset<NativePrismDngMetadata>("HasChannelColors"));
    }

    private static int Offset<T>(string fieldName)
        => checked((int)Marshal.OffsetOf<T>(fieldName));
}
