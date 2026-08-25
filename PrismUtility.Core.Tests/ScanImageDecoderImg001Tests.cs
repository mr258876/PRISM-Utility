using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanImageDecoderImg001Tests
{
    [Fact]
    public void Img001_ValidOneRowWaterfallDecodeProducesExpectedBgraBytes()
    {
        var decoder = new ScanImageDecoder();
        var input = BuildOneRowInput();
        var destination = new byte[decoder.GetDecodedPixelsPerLine() * 4];

        decoder.DecodeWaterfallStripToBgra(input, 1, destination, false, 1.0, false, 0);

        Assert.Equal(BuildExpectedOneRowOutput(destination.Length), destination);
    }

    [Fact]
    public void Img001_ZeroRowsWithEmptyInputRejectsBeforeDivision()
    {
        var decoder = new ScanImageDecoder();
        var destination = new byte[decoder.GetDecodedPixelsPerLine() * 4];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            decoder.DecodeWaterfallStripToBgra(Array.Empty<byte>(), 0, destination, false, 1.0, false, 0));

        Assert.Equal("rows", exception.ParamName);
    }

    [Fact]
    public void Img001_MalformedPositiveRowsWithEmptyInputUsesBufferValidation()
    {
        var decoder = new ScanImageDecoder();
        var destination = new byte[decoder.GetDecodedPixelsPerLine() * 4];

        var exception = Assert.Throws<IOException>(() =>
            decoder.DecodeWaterfallStripToBgra(Array.Empty<byte>(), 1, destination, false, 1.0, false, 0));

        Assert.Contains("Scan buffer size mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Img001_InvalidRowsLeaveDestinationUntouched()
    {
        var decoder = new ScanImageDecoder();
        var destination = Enumerable.Repeat((byte)0x5A, decoder.GetDecodedPixelsPerLine() * 4).ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            decoder.DecodeWaterfallStripToBgra(Array.Empty<byte>(), 0, destination, false, 1.0, false, 0));

        Assert.All(destination, value => Assert.Equal(0x5A, value));
    }

    [Fact]
    public void ScanPreviewPresenter_CompressedWaterfallRejectsNonPositiveRowsAtBoundary()
    {
        var sourcePath = Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "Services", "ScanPreviewPresenter.cs");
        var source = File.ReadAllText(sourcePath);
        const string validationCall = "TryValidateWaterfallRows(options, rows, out error)";
        var firstValidation = source.IndexOf(validationCall, StringComparison.Ordinal);
        var secondValidation = source.IndexOf(validationCall, firstValidation + validationCall.Length, StringComparison.Ordinal);
        var compressedRender = source.IndexOf("RenderWaterfallPreviewFrame(lineBuffer, rows, options, gamma)", StringComparison.Ordinal);
        var bitmapRender = source.IndexOf("RenderWaterfallPreview(lineBuffer, rows, options, bitmap, gamma)", StringComparison.Ordinal);

        Assert.True(firstValidation >= 0, "The presenter must validate waterfall rows at its public boundary.");
        Assert.True(secondValidation >= 0, "Both presenter overloads must validate waterfall rows at their public boundary.");
        Assert.True(compressedRender >= 0, "The compressed waterfall render path must remain present.");
        Assert.True(bitmapRender >= 0, "The bitmap waterfall render path must remain present.");
        Assert.True(firstValidation < compressedRender, "Frame waterfall row validation must happen before compressed rendering allocates buffers.");
        Assert.True(secondValidation < bitmapRender, "Bitmap waterfall row validation must happen before compressed rendering allocates buffers.");
    }

    private static byte[] BuildOneRowInput()
    {
        var input = new byte[ScanDebugConstants.BytesPerLine];
        var groupStart = ScanDebugConstants.LineBufferMarginLeft;
        const ushort firstSample = 0x1234;
        const ushort secondSample = 0xABCD;

        input[groupStart] = (byte)(secondSample >> 8);
        input[groupStart + 1] = (byte)(firstSample >> 8);
        input[groupStart + 2] = (byte)(secondSample & 0xFF);
        input[groupStart + 3] = (byte)(firstSample & 0xFF);
        return input;
    }

    private static byte[] BuildExpectedOneRowOutput(int length)
    {
        var expected = new byte[length];
        for (var index = 3; index < expected.Length; index += 4)
            expected[index] = byte.MaxValue;

        expected[0] = 0x12;
        expected[1] = 0x12;
        expected[2] = 0x12;
        expected[4] = 0xAB;
        expected[5] = 0xAB;
        expected[6] = 0xAB;
        return expected;
    }

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility.Core"))
                && Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software root.");
    }
}
