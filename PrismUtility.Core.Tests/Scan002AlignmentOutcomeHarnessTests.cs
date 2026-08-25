using OpenCvSharp;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class Scan002AlignmentOutcomeHarnessTests
{
    private const int Width = 64;
    private const int Rows = 32;

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_ValidInput_ReturnsAlignedOutcome()
    {
        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1), new TestAlignmentBackend()).BuildAlignedNormalizedPassBuffers(
            BuildResult(),
            BuildAssignment(),
            ScanChannelAlignmentMode.Ecc,
            CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Aligned, result.Status);
        Assert.Equal(3, result.AlignedPassBuffers.Length);
        Assert.All(result.ChannelOutcomes, outcome => Assert.Equal(ScanChannelAlignmentStatus.Aligned, outcome.Status));
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_MissingUniqueGreen_ReturnsDisabledOutcomeWithChannelDiagnostics()
    {
        var assignment = new ScanChannelAssignment("Red", "Blue", "Blue", "Unused", false, false, false, false);

        var result = CreateService().BuildAlignedNormalizedPassBuffers(
            BuildResult(),
            assignment,
            ScanChannelAlignmentMode.Ecc,
            CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Disabled, result.Status);
        Assert.All(result.ChannelOutcomes, outcome =>
        {
            Assert.Equal(ScanChannelAlignmentStatus.Disabled, outcome.Status);
            Assert.Equal(ScanChannelAlignmentDiagnosticKind.MissingUniqueGreen, outcome.Diagnostic?.Kind);
        });
        Assert.Equal(3, result.AlignedPassBuffers.Length);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(-1, 63)]
    [InlineData(0, 64)]
    public void BuildAlignedNormalizedPassBuffers_ZeroOrOutOfBoundsRoi_ReturnsFallbackWithoutNativeCall(int effectiveStart, int effectiveEndInclusive)
    {
        var backend = new TestAlignmentBackend();
        var result = CreateService(new TestScanImageDecoder(Width, effectiveStart, effectiveEndInclusive), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.Status);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(ScanChannelAlignmentDiagnosticKind.InvalidRoi, diagnostic.Kind));
        Assert.Equal(0, backend.CallCount);
        Assert.Equal(3, result.AlignedPassBuffers.Length);
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_EmptyPassInput_ReturnsFailureWithoutBuffers()
    {
        var result = CreateService().BuildAlignedNormalizedPassBuffers(
            BuildResult(Array.Empty<byte>(), BuildBuffer(), BuildBuffer()),
            BuildAssignment(),
            ScanChannelAlignmentMode.Ecc,
            CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Failed, result.Status);
        Assert.Empty(result.AlignedPassBuffers);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.EmptyInput, Assert.Single(result.Diagnostics).Kind);
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_EccNonConvergence_ReturnsFallbackDiagnostic()
    {
        var backend = new TestAlignmentBackend(_ => throw new OpenCvSharpException("Iterations do not converge"));

        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.Status);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(ScanChannelAlignmentDiagnosticKind.NonConverged, diagnostic.Kind));
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_InvalidWarpOpenCvException_ReturnsFallbackDiagnostic()
    {
        var backend = new TestAlignmentBackend(_ => throw new OpenCvSharpException("Invalid warp matrix"));

        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.Status);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal(ScanChannelAlignmentDiagnosticKind.OpenCvFailure, diagnostic.Kind);
            Assert.Equal("Invalid warp matrix", diagnostic.Message);
        });
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_OneChannelNativeFailure_FallsBackOnlyThatChannel()
    {
        var backend = new TestAlignmentBackend(callNumber =>
        {
            if (callNumber == 1)
                throw new OpenCvSharpException("Invalid warp matrix");
        });

        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.Status);
        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.ChannelOutcomes[0].Status);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.OpenCvFailure, result.ChannelOutcomes[0].Diagnostic?.Kind);
        Assert.Equal(ScanChannelAlignmentStatus.Aligned, result.ChannelOutcomes[1].Status);
        Assert.Equal(ScanChannelAlignmentStatus.Aligned, result.ChannelOutcomes[2].Status);
        Assert.Equal(BuildBuffer(), result.AlignedPassBuffers[0]);
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_EccFailureThenMutualInformationSuccess_ReturnsFallbackWithUsableBuffers()
    {
        var backend = new TestAlignmentBackend(_ => throw new OpenCvSharpException("Iterations do not converge"));

        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.EccThenMutualInformation, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.Status);
        Assert.Equal(BuildBuffer(), result.AlignedPassBuffers[0]);
        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.ChannelOutcomes[0].Status);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.NonConverged, result.ChannelOutcomes[0].Diagnostic?.Kind);
        Assert.Equal(ScanChannelAlignmentStatus.Aligned, result.ChannelOutcomes[1].Status);
        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.ChannelOutcomes[2].Status);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(ScanChannelAlignmentDiagnosticKind.NonConverged, diagnostic.Kind));
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_EccAndMutualInformationFailure_PreservesBothDiagnostics()
    {
        var backend = new TestAlignmentBackend(_ => throw new OpenCvSharpException("Iterations do not converge"));
        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResultForRows(1), BuildAssignment(), ScanChannelAlignmentMode.EccThenMutualInformation, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.Status);
        Assert.Equal(4, result.Diagnostics.Count);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.NonConverged, result.Diagnostics[0].Kind);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.AlignmentUnavailable, result.Diagnostics[1].Kind);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.NonConverged, result.Diagnostics[2].Kind);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.AlignmentUnavailable, result.Diagnostics[3].Kind);
        Assert.Equal(BuildBuffer(1), result.AlignedPassBuffers[0]);
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_ConcreteOpenCvException_MapsNativeMetadata()
    {
        var backend = new TestAlignmentBackend(_ => throw new OpenCVException(ErrorCode.StsBadArg, "test-func", "native-error", "test-file", 123));
        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, CancellationToken.None);

        var diagnostic = result.Diagnostics[0];
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.OpenCvFailure, diagnostic.Kind);
        Assert.Equal((int)ErrorCode.StsBadArg, diagnostic.NativeCode);
        Assert.Equal("test-func", diagnostic.NativeFunction);
        Assert.Equal("test-file", diagnostic.NativeFile);
        Assert.Equal(123, diagnostic.NativeLine);
        Assert.Equal("native-error", diagnostic.Message);
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_EmptyNormalizedInput_ReturnsFailureWithoutBuffers()
    {
        var result = CreateService(processor: new EmptyNormalizingCompositeImageProcessor())
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Failed, result.Status);
        Assert.Empty(result.AlignedPassBuffers);
        Assert.Equal(ScanChannelAlignmentDiagnosticKind.EmptyInput, Assert.Single(result.Diagnostics).Kind);
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_EmptyReferenceMat_ReturnsFallbackWithNormalizedBuffers()
    {
        var result = CreateService(new TestScanImageDecoder(Width, 0, Width - 1, returnsSamples: false))
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, CancellationToken.None);

        Assert.Equal(ScanChannelAlignmentStatus.Fallback, result.Status);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(ScanChannelAlignmentDiagnosticKind.EmptyInput, diagnostic.Kind));
        Assert.Equal(BuildBuffer(), result.AlignedPassBuffers[0]);
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_PreCanceledToken_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => CreateService().BuildAlignedNormalizedPassBuffers(
            BuildResult(),
            BuildAssignment(),
            ScanChannelAlignmentMode.Ecc,
            cancellation.Token));
    }

    [Fact]
    public void BuildAlignedNormalizedPassBuffers_CanceledAfterNativeCall_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var backend = new TestAlignmentBackend(callNumber =>
        {
            if (callNumber == 1)
                cancellation.Cancel();
        });

        Assert.Throws<OperationCanceledException>(() => CreateService(new TestScanImageDecoder(Width, 0, Width - 1), backend)
            .BuildAlignedNormalizedPassBuffers(BuildResult(), BuildAssignment(), ScanChannelAlignmentMode.Ecc, cancellation.Token));
    }

    private static ScanChannelAlignmentService CreateService(TestScanImageDecoder? decoder = null, IScanChannelAlignmentBackend? backend = null, IScanCompositeImageProcessor? processor = null)
        => new(processor ?? new PassthroughCompositeImageProcessor(), decoder ?? new TestScanImageDecoder(Width, 0, Width - 1), backend);

    private static ScanWorkflowResult BuildResult(params byte[][] buffers)
    {
        var passBuffers = buffers.Length == 0 ? new[] { BuildBuffer(), BuildBuffer(), BuildBuffer() } : buffers;
        return BuildResultForRows(Rows, passBuffers);
    }

    private static ScanWorkflowResult BuildResultForRows(int rows, params byte[][] buffers)
    {
        var passBuffers = buffers.Length == 0 ? new[] { BuildBuffer(rows), BuildBuffer(rows), BuildBuffer(rows) } : buffers;
        return new ScanWorkflowResult(
            rows,
            passBuffers.Select((buffer, index) => new ScanPassCapture(index, (byte)index, true, rows, 0, buffer)).ToArray(),
            0,
            0,
            0,
            0);
    }

    private static ScanChannelAssignment BuildAssignment()
        => new("Red", "Green", "Blue", "Unused", false, false, false, false);

    private static byte[] BuildBuffer(int rows = Rows)
        => new byte[rows * ScanDebugConstants.BytesPerLine];

    private sealed class TestAlignmentBackend(Action<int>? behavior = null) : IScanChannelAlignmentBackend
    {
        private int _callCount;

        public int CallCount => _callCount;

        public void FindTransformEcc(Mat reference, Mat moving, Mat warpMatrix, TermCriteria criteria, int gaussianFilterSize)
        {
            _callCount++;
            behavior?.Invoke(_callCount);
        }
    }

    private sealed class PassthroughCompositeImageProcessor : IScanCompositeImageProcessor
    {
        public byte[] NormalizePassBuffer(ScanPassCapture capture, bool manuallyReverse, CancellationToken cancellationToken)
            => (byte[])capture.ImageBytes.Clone();

        public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, CancellationToken cancellationToken, out ScanCompositePixelBuffer? frame, out string error)
            => throw new NotSupportedException();

        public bool TryBuildPartialRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, IReadOnlyDictionary<string, ScanRowAvailability> availableRowsByRole, CancellationToken cancellationToken, out ScanCompositePixelBuffer? frame, out string error)
            => throw new NotSupportedException();
    }

    private sealed class EmptyNormalizingCompositeImageProcessor : IScanCompositeImageProcessor
    {
        public byte[] NormalizePassBuffer(ScanPassCapture capture, bool manuallyReverse, CancellationToken cancellationToken)
            => Array.Empty<byte>();

        public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, CancellationToken cancellationToken, out ScanCompositePixelBuffer? frame, out string error)
            => throw new NotSupportedException();

        public bool TryBuildPartialRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, IReadOnlyDictionary<string, ScanRowAvailability> availableRowsByRole, CancellationToken cancellationToken, out ScanCompositePixelBuffer? frame, out string error)
            => throw new NotSupportedException();
    }

    private sealed class TestScanImageDecoder(int width, int effectiveStart, int effectiveEndInclusive, bool returnsSamples = true) : IScanImageDecoder
    {
        public int GetDecodedPixelsPerLine()
            => width;

        public (int Start, int EndInclusive) GetEffectivePixelRange()
            => (effectiveStart, effectiveEndInclusive);

        public void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeToBgra(byte[] lineBuffer, int rows, Span<byte> destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample)
        {
            sample = 0;
            return returnsSamples && x >= 0 && x < width && y >= 0 && y < rows;
        }
    }
}
