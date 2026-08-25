using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;

using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "DNG001")]
public sealed class DngWriterServiceValidationTests
{
    [Theory]
    [InlineData(1, 1, 0, 1, 2, 2)]
    [InlineData(0, 2, 2, 1, 2, 2)]
    [InlineData(0, 0, 3, 2, 2, 2)]
    [InlineData(0, 0, 2, 3, 2, 2)]
    public void WriteRawDng_InvalidActiveArea_DoesNotInvokeNativeApi(uint top, uint left, uint bottom, uint right, uint width, uint height)
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);

        var request = DngWriterServiceTestData.CreateRequest(width, height) with
        {
            ActiveArea = new DngRectangle(top, left, bottom, right)
        };

        var exception = Assert.Throws<ArgumentException>(() => service.WriteRawDng(request));

        Assert.Contains("ActiveArea", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, nativeApi.WriteCallCount);
    }

    [Theory]
    [InlineData(1, 1, 0, 1, 2, 2)]
    [InlineData(0, 2, 2, 1, 2, 2)]
    [InlineData(0, 0, 3, 2, 2, 2)]
    [InlineData(0, 0, 2, 3, 2, 2)]
    public void WriteRawDng_InvalidDefaultCrop_DoesNotInvokeNativeApi(uint top, uint left, uint bottom, uint right, uint width, uint height)
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);

        var request = DngWriterServiceTestData.CreateRequest(width, height) with
        {
            DefaultCrop = new DngRectangle(top, left, bottom, right)
        };

        var exception = Assert.Throws<ArgumentException>(() => service.WriteRawDng(request));

        Assert.Contains("DefaultCrop", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, nativeApi.WriteCallCount);
    }

    [Theory]
    [InlineData(1, 1, 0, 1)]
    [InlineData(0, 2, 2, 1)]
    [InlineData(0, 0, 3, 2)]
    [InlineData(0, 0, 2, 3)]
    [InlineData(0, 0, 0, 0)]
    public void WriteRawDng_InvalidMaskedArea_DoesNotInvokeNativeApi(uint top, uint left, uint bottom, uint right)
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);

        var request = DngWriterServiceTestData.CreateRequest() with
        {
            MaskedAreas = [new DngRectangle(top, left, bottom, right)]
        };

        var exception = Assert.Throws<ArgumentException>(() => service.WriteRawDng(request));

        Assert.Contains("MaskedAreas", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, nativeApi.WriteCallCount);
    }

    [Fact]
    public void WriteRawDng_EmptyActiveAndDefaultAreas_AreAllowed()
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);

        service.WriteRawDng(DngWriterServiceTestData.CreateRequest() with
        {
            ActiveArea = DngRectangle.Empty,
            DefaultCrop = DngRectangle.Empty
        });

        Assert.Equal(1, nativeApi.WriteCallCount);
    }

    [Fact]
    public void WriteRawDng_NonEmptyAreasWithinBounds_AreAllowed()
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);

        service.WriteRawDng(DngWriterServiceTestData.CreateRequest() with
        {
            ActiveArea = new DngRectangle(0, 0, 2, 2),
            DefaultCrop = new DngRectangle(0, 0, 2, 2),
            MaskedAreas = [new DngRectangle(1, 1, 2, 2)]
        });

        Assert.Equal(1, nativeApi.WriteCallCount);
    }

    [Fact]
    public void WriteRawDng_PackedRowWidthOverflow_IsRejectedBeforeNativeApi()
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);
        var request = new DngWriteRequest(
            OutputPath: "output.dng",
            PixelData: [0],
            Width: uint.MaxValue,
            Height: 1,
            RowStrideBytes: uint.MaxValue,
            BitsPerSample: 8,
            SamplesPerPixel: 3,
            PixelLayout: DngPixelLayout.LinearRgb,
            CfaPattern: DngCfaPattern.Unknown);

        Assert.Throws<ArgumentException>(() => service.WriteRawDng(request));
        Assert.Equal(0, nativeApi.WriteCallCount);
    }

    [Fact]
    public void WriteRawDng_BlackLevelPlaneCountMustMatchSamplesPerPixel_IsRejectedBeforeNativeApi()
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);
        var request = DngWriterServiceTestData.CreateRequest() with
        {
            BlackLevelPlanes =
            [
                new DngBlackLevelPlane(0, 0, 0, 0),
                new DngBlackLevelPlane(0, 0, 0, 0)
            ]
        };

        Assert.Throws<ArgumentException>(() => service.WriteRawDng(request));
        Assert.Equal(0, nativeApi.WriteCallCount);
    }
}
