using System.Reflection;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;
using Xunit.Sdk;

namespace PrismUtility.Core.Tests;

public sealed class Scan001CancellationTests
{
    private const int Width = 64;
    private const int Rows = 32;

    [Fact]
    public void TryBuildRgbComposite_UncanceledToken_ProducesExactBaselinePixels()
    {
        var processor = new ScanCompositeImageProcessor(new TestScanImageDecoder(Width));
        var args = new object?[]
        {
            BuildResult(ushort.MaxValue, 0, 0),
            BuildRgbAssignment(),
            new ScanColorManagementOptions(false, 680.0, 525.0, 450.0, 1.0),
            CancellationToken.None,
            null,
            null
        };

        var success = (bool)GetCancellationMethod(typeof(ScanCompositeImageProcessor), nameof(ScanCompositeImageProcessor.TryBuildRgbComposite))
            .Invoke(processor, args)!;

        Assert.True(success, Assert.IsType<string>(args[5]));
        var frame = Assert.IsType<ScanCompositePixelBuffer>(args[4]);
        Assert.Equal(Width, frame.Width);
        Assert.Equal(Rows, frame.Height);
        Assert.Equal(Enumerable.Repeat(new byte[] { 0, 0, 255, 255 }, Width * Rows).SelectMany(static pixel => pixel), frame.Pixels);
    }

    [Fact]
    public void TryBuildRgbComposite_PreCanceledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        AssertCompositeCanceled(new TestScanImageDecoder(Width), cancellation.Token);
    }

    [Fact]
    public void TryBuildRgbComposite_MidOperationCancellation_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        const int cancelAfterReads = Width;
        var decoder = new TestScanImageDecoder(Width, cancellation, cancelAfterReads);

        AssertCompositeCanceled(decoder, cancellation.Token);

        Assert.True(decoder.ReadCount >= cancelAfterReads);
    }

    [Fact]
    public async Task TryBuildRgbComposite_ColorScaleCancellation_ExitsBeforeDeadline()
    {
        using var cancellation = new CancellationTokenSource();
        var decoder = new TestScanImageDecoder(Width, cancellation, Width);
        var processor = new ScanCompositeImageProcessor(decoder);
        var args = new object?[] { BuildResult(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue), BuildRgbAssignment(), ScanColorManagementOptions.CreateDefault(), cancellation.Token, null, null };

        await Task.Run(() => AssertCanceled(() => GetCancellationMethod(typeof(ScanCompositeImageProcessor), nameof(ScanCompositeImageProcessor.TryBuildRgbComposite)).Invoke(processor, args)))
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(decoder.ReadCount >= Width);
    }

    [Fact]
    public void TryBuildAlignedNormalizedPassBuffers_PreCanceledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        AssertAlignmentCanceled(new TestScanImageDecoder(Width), cancellation.Token);
    }

    [Fact]
    public void TryBuildAlignedNormalizedPassBuffers_MidOperationCancellation_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        const int cancelAfterReads = Width;
        var decoder = new TestScanImageDecoder(Width, cancellation, cancelAfterReads);

        AssertAlignmentCanceled(decoder, cancellation.Token);

        Assert.True(decoder.ReadCount >= cancelAfterReads);
    }

    private static void AssertCompositeCanceled(TestScanImageDecoder decoder, CancellationToken cancellationToken)
    {
        var processor = new ScanCompositeImageProcessor(decoder);
        var args = new object?[]
        {
            BuildResult(ushort.MaxValue, 0, 0),
            BuildRgbAssignment(),
            new ScanColorManagementOptions(false, 680.0, 525.0, 450.0, 1.0),
            cancellationToken,
            null,
            null
        };

        AssertCanceled(() => GetCancellationMethod(typeof(ScanCompositeImageProcessor), nameof(ScanCompositeImageProcessor.TryBuildRgbComposite)).Invoke(processor, args));
    }

    private static void AssertAlignmentCanceled(TestScanImageDecoder decoder, CancellationToken cancellationToken)
    {
        var processor = new ScanCompositeImageProcessor(decoder);
        var alignment = new ScanChannelAlignmentService(processor, decoder);
        var args = new object?[]
        {
            BuildResult(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
            BuildRgbAssignment(),
            ScanChannelAlignmentMode.MutualInformation,
            cancellationToken
        };

        AssertCanceled(() => GetCancellationMethod(typeof(ScanChannelAlignmentService), nameof(ScanChannelAlignmentService.BuildAlignedNormalizedPassBuffers)).Invoke(alignment, args));
    }

    private static void AssertCanceled(Action action)
    {
        var exception = Assert.Throws<TargetInvocationException>(action);
        Assert.IsType<OperationCanceledException>(exception.InnerException);
    }

    private static MethodInfo GetCancellationMethod(Type type, string methodName)
        => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == methodName
                && method.GetParameters().Length >= 4
                && method.GetParameters()[3].ParameterType == typeof(CancellationToken))
            ?? throw new XunitException($"{type.Name}.{methodName} must expose a CancellationToken as its fourth parameter.");

    private static ScanWorkflowResult BuildResult(ushort red, ushort green, ushort blue)
        => new(
            Rows,
            new[]
            {
                BuildPass(0, red),
                BuildPass(1, green),
                BuildPass(2, blue)
            },
            0,
            0,
            0,
            0);

    private static ScanChannelAssignment BuildRgbAssignment()
        => new("Red", "Green", "Blue", "Unused", false, false, false, false);

    private static ScanPassCapture BuildPass(int passIndex, ushort sample)
    {
        var buffer = new byte[Rows * ScanDebugConstants.BytesPerLine];
        for (var y = 0; y < Rows; y++)
        {
            var rowStart = (y * ScanDebugConstants.BytesPerLine) + ScanDebugConstants.LineBufferMarginLeft;
            for (var x = 0; x < Width; x += ScanDebugConstants.PackedGroupPixels)
            {
                var offset = rowStart + ((x / ScanDebugConstants.PackedGroupPixels) * ScanDebugConstants.PackedGroupBytes);
                buffer[offset] = (byte)(sample >> 8);
                buffer[offset + 1] = (byte)(sample >> 8);
                buffer[offset + 2] = (byte)sample;
                buffer[offset + 3] = (byte)sample;
            }
        }

        return new ScanPassCapture(passIndex, (byte)passIndex, true, Rows, 0, buffer);
    }

    private sealed class TestScanImageDecoder(int width, CancellationTokenSource? cancellation = null, int cancelAfterReads = int.MaxValue) : IScanImageDecoder
    {
        private int _readCount;

        public int ReadCount => Volatile.Read(ref _readCount);

        public int GetDecodedPixelsPerLine()
            => width;

        public (int Start, int EndInclusive) GetEffectivePixelRange()
            => (0, width - 1);

        public void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeToBgra(byte[] lineBuffer, int rows, Span<byte> destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample)
        {
            sample = 0;
            if (x < 0 || x >= width || y < 0 || y >= rows)
                return false;

            var offset = (y * ScanDebugConstants.BytesPerLine)
                + ScanDebugConstants.LineBufferMarginLeft
                + ((x / ScanDebugConstants.PackedGroupPixels) * ScanDebugConstants.PackedGroupBytes);
            sample = (ushort)((lineBuffer[offset + 1] << 8) | lineBuffer[offset + 3]);

            if (Interlocked.Increment(ref _readCount) == cancelAfterReads)
                cancellation?.Cancel();

            return true;
        }
    }
}
