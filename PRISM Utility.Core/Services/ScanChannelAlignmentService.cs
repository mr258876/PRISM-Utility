using OpenCvSharp;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanChannelAlignmentService : IScanChannelAlignmentService
{
    private const int CoarseEccMaxIterations = 50;
    private const int FineEccMaxIterations = 30;
    private const double EccMinIncrement = 1e-3;
    private const int EccGaussianFilterSize = 5;
    private const int CoarseMaxDimension = 2048;
    private const int FineMaxWidth = 2048;
    private const int FineMaxHeight = 2048;
    private const double MinimumMeaningfulShift = 1e-3;
    private const int MutualInformationHistogramBins = 64;
    private const double MutualInformationMinStep = 0.25;
    private const double CoarseMutualInformationSearchRadius = 48.0;
    private const double FineMutualInformationSearchRadius = 12.0;
    private const double MutualInformationCoarseGridStep = 6.0;
    private const double MutualInformationFineGridStep = 2.0;
    private const double MinimumMutualInformationCoverage = 0.35;
    private const int MaximumMutualInformationSamples = 262144;

    private readonly IScanCompositeImageProcessor _processor;
    private readonly IScanImageDecoder _decoder;
    private readonly IScanChannelAlignmentBackend _backend;

    public ScanChannelAlignmentService(IScanCompositeImageProcessor processor, IScanImageDecoder decoder, IScanChannelAlignmentBackend? backend = null)
    {
        _processor = processor;
        _decoder = decoder;
        _backend = backend ?? new OpenCvScanChannelAlignmentBackend();
    }

    public ScanChannelAlignmentResult BuildAlignedNormalizedPassBuffers(
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        ScanChannelAlignmentMode alignmentMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (result.Passes.Count == 0)
            return CreateFailedResult("No scan passes are available for channel alignment.");

        var width = _decoder.GetDecodedPixelsPerLine();
        if (width <= 0 || result.Rows <= 0)
            return CreateFailedResult("Scan dimensions are invalid for channel alignment.");

        var emptyInputIndex = FindEmptyPassIndex(result.Passes);
        if (emptyInputIndex >= 0)
            return CreateFailedResult("A scan pass has no image data.", emptyInputIndex, GetChannelRole(assignment, emptyInputIndex));

        var normalizedPasses = BuildNormalizedPassBuffers(result, assignment, cancellationToken);
        var emptyNormalizedIndex = Array.FindIndex(normalizedPasses, static buffer => buffer.Length == 0);
        if (emptyNormalizedIndex >= 0)
            return CreateFailedResult("A normalized scan pass has no image data.", emptyNormalizedIndex, GetChannelRole(assignment, emptyNormalizedIndex));

        var greenIndex = FindSingleRoleIndex(assignment, "Green");
        if (greenIndex < 0 || greenIndex >= normalizedPasses.Length)
            return CreateUniformOutcome(
                ScanChannelAlignmentStatus.Disabled,
                normalizedPasses,
                assignment,
                ScanChannelAlignmentDiagnosticKind.MissingUniqueGreen,
                "Alignment is disabled because the channel assignment does not contain exactly one Green channel.");

        var effectiveRange = _decoder.GetEffectivePixelRange();
        if (effectiveRange.Start < 0 || effectiveRange.EndInclusive < effectiveRange.Start || effectiveRange.EndInclusive >= width)
            return CreateUniformOutcome(
                ScanChannelAlignmentStatus.Fallback,
                normalizedPasses,
                assignment,
                ScanChannelAlignmentDiagnosticKind.InvalidRoi,
                "The effective alignment ROI is empty or outside decoded scan bounds.");

        var effectiveWidth = effectiveRange.EndInclusive - effectiveRange.Start + 1;
        if (!TryCreateRoi(width, result.Rows, effectiveRange.Start, 0, effectiveWidth, result.Rows, out var coarseRoi))
            return CreateUniformOutcome(
                ScanChannelAlignmentStatus.Fallback,
                normalizedPasses,
                assignment,
                ScanChannelAlignmentDiagnosticKind.InvalidRoi,
                "The effective alignment ROI is empty or outside decoded scan bounds.");

        var fineWindow = BuildFineWindow(effectiveRange.Start, effectiveRange.EndInclusive, result.Rows);
        if (!TryCreateRoi(width, result.Rows, fineWindow.StartX, fineWindow.StartY, fineWindow.Width, fineWindow.Height, out var fineRoi))
            return CreateUniformOutcome(
                ScanChannelAlignmentStatus.Fallback,
                normalizedPasses,
                assignment,
                ScanChannelAlignmentDiagnosticKind.InvalidRoi,
                "The fine alignment ROI is empty or outside decoded scan bounds.");

        try
        {
            using var referenceMat = BuildSampledRoiMat(normalizedPasses[greenIndex], result.Rows, coarseRoi, CoarseMaxDimension, cancellationToken, out var scale);
            if (referenceMat.Empty())
                return CreateUniformOutcome(
                    ScanChannelAlignmentStatus.Fallback,
                    normalizedPasses,
                    assignment,
                    ScanChannelAlignmentDiagnosticKind.EmptyInput,
                    "The Green alignment reference is empty.");

            using var fineReferenceMat = BuildSampledRoiMat(normalizedPasses[greenIndex], result.Rows, fineRoi, 0, cancellationToken, out _);
            var alignedPassBuffers = (byte[][])normalizedPasses.Clone();
            var outcomes = CreateAlignedOutcomes(assignment, normalizedPasses.Length);
            var diagnostics = new List<ScanChannelAlignmentDiagnostic>();

            for (var channelIndex = 0; channelIndex < normalizedPasses.Length; channelIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (channelIndex == greenIndex)
                    continue;

                try
                {
                    using var movingMat = BuildSampledRoiMat(normalizedPasses[channelIndex], result.Rows, coarseRoi, CoarseMaxDimension, cancellationToken, out _);
                    if (movingMat.Empty())
                    {
                        AddFallbackOutcome(outcomes, diagnostics, channelIndex, assignment, ScanChannelAlignmentDiagnosticKind.EmptyInput, "The channel alignment input is empty.");
                        continue;
                    }

                    using var fineMovingMat = fineReferenceMat.Empty()
                        ? new Mat()
                        : BuildSampledRoiMat(normalizedPasses[channelIndex], result.Rows, fineRoi, 0, cancellationToken, out _);

                    var translation = EstimateTranslation(referenceMat, movingMat, fineReferenceMat, fineMovingMat, scale, alignmentMode, cancellationToken);
                    if (translation.EccFallback is { } eccFallback)
                        AddFallbackOutcome(outcomes, diagnostics, channelIndex, assignment, eccFallback);

                    var shiftX = translation.ShiftX;
                    var shiftY = translation.ShiftY;
                    if (Math.Abs(shiftX) < MinimumMeaningfulShift && Math.Abs(shiftY) < MinimumMeaningfulShift)
                        continue;

                    var sourceSamples = DecodeToSampleGrid(normalizedPasses[channelIndex], result.Rows, width, cancellationToken);
                    var alignedSamples = ApplyTranslation(sourceSamples, width, result.Rows, shiftX, shiftY, cancellationToken);
                    alignedPassBuffers[channelIndex] = EncodeSampleGrid(normalizedPasses[channelIndex], alignedSamples, width, result.Rows, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (EccThenMutualInformationFailureException exception)
                {
                    AddFallbackOutcome(outcomes, diagnostics, channelIndex, assignment, exception.EccFailure);
                    AddFallbackDiagnostic(diagnostics, channelIndex, assignment, exception.MutualInformationFailure);
                }
                catch (OpenCVException exception)
                {
                    AddFallbackOutcome(outcomes, diagnostics, channelIndex, assignment, ClassifyOpenCvFailure(exception), exception.ErrMsg, exception);
                }
                catch (OpenCvSharpException exception)
                {
                    AddFallbackOutcome(outcomes, diagnostics, channelIndex, assignment, ClassifyOpenCvFailure(exception), exception.Message, exception);
                }
                catch (InvalidOperationException exception)
                {
                    var kind = Enum.IsDefined(alignmentMode)
                        ? ScanChannelAlignmentDiagnosticKind.AlignmentUnavailable
                        : ScanChannelAlignmentDiagnosticKind.UnsupportedMode;
                    AddFallbackOutcome(outcomes, diagnostics, channelIndex, assignment, kind, exception.Message);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var status = diagnostics.Count == 0 ? ScanChannelAlignmentStatus.Aligned : ScanChannelAlignmentStatus.Fallback;
            return new ScanChannelAlignmentResult(status, alignedPassBuffers, outcomes, diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OpenCVException exception)
        {
            return CreateUniformOutcome(
                ScanChannelAlignmentStatus.Fallback,
                normalizedPasses,
                assignment,
                ClassifyOpenCvFailure(exception),
                exception.ErrMsg,
                exception);
        }
        catch (OpenCvSharpException exception)
        {
            return CreateUniformOutcome(
                ScanChannelAlignmentStatus.Fallback,
                normalizedPasses,
                assignment,
                ClassifyOpenCvFailure(exception),
                exception.Message,
                exception);
        }
    }

    private byte[][] BuildNormalizedPassBuffers(ScanWorkflowResult result, ScanChannelAssignment assignment, CancellationToken cancellationToken)
    {
        var normalizedPasses = new byte[result.Passes.Count][];
        for (var index = 0; index < result.Passes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var shouldReverse = index < assignment.ReversedFlags.Count && assignment.ReversedFlags[index];
            normalizedPasses[index] = _processor.NormalizePassBuffer(result.Passes[index], shouldReverse, cancellationToken);
        }

        return normalizedPasses;
    }

    private static int FindEmptyPassIndex(IReadOnlyList<ScanPassCapture> passes)
    {
        for (var index = 0; index < passes.Count; index++)
        {
            if (passes[index].ImageBytes is null || passes[index].ImageBytes.Length == 0)
                return index;
        }

        return -1;
    }

    private static string GetChannelRole(ScanChannelAssignment assignment, int channelIndex)
        => channelIndex >= 0 && channelIndex < assignment.Roles.Count && !string.IsNullOrWhiteSpace(assignment.Roles[channelIndex])
            ? assignment.Roles[channelIndex]
            : $"Pass {channelIndex}";

    private static ScanChannelAlignmentResult CreateFailedResult(string message, int? channelIndex = null, string? channelRole = null)
    {
        var diagnostic = new ScanChannelAlignmentDiagnostic(
            ScanChannelAlignmentDiagnosticKind.EmptyInput,
            channelIndex,
            channelRole,
            message);
        var outcomes = channelIndex is int index
            ? new[] { new ScanChannelAlignmentChannelOutcome(index, channelRole ?? $"Pass {index}", ScanChannelAlignmentStatus.Failed, diagnostic) }
            : Array.Empty<ScanChannelAlignmentChannelOutcome>();
        return new ScanChannelAlignmentResult(ScanChannelAlignmentStatus.Failed, Array.Empty<byte[]>(), outcomes, new[] { diagnostic });
    }

    private static ScanChannelAlignmentResult CreateUniformOutcome(
        ScanChannelAlignmentStatus status,
        byte[][] normalizedPasses,
        ScanChannelAssignment assignment,
        ScanChannelAlignmentDiagnosticKind kind,
        string message,
        Exception? exception = null)
    {
        var outcomes = new List<ScanChannelAlignmentChannelOutcome>(normalizedPasses.Length);
        var diagnostics = new List<ScanChannelAlignmentDiagnostic>(normalizedPasses.Length);
        for (var channelIndex = 0; channelIndex < normalizedPasses.Length; channelIndex++)
        {
            var diagnostic = CreateDiagnostic(kind, channelIndex, GetChannelRole(assignment, channelIndex), message, exception);
            diagnostics.Add(diagnostic);
            outcomes.Add(new ScanChannelAlignmentChannelOutcome(channelIndex, diagnostic.ChannelRole ?? $"Pass {channelIndex}", status, diagnostic));
        }

        return new ScanChannelAlignmentResult(status, normalizedPasses, outcomes, diagnostics);
    }

    private static List<ScanChannelAlignmentChannelOutcome> CreateAlignedOutcomes(ScanChannelAssignment assignment, int channelCount)
    {
        var outcomes = new List<ScanChannelAlignmentChannelOutcome>(channelCount);
        for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
            outcomes.Add(new ScanChannelAlignmentChannelOutcome(channelIndex, GetChannelRole(assignment, channelIndex), ScanChannelAlignmentStatus.Aligned));

        return outcomes;
    }

    private static void AddFallbackOutcome(
        List<ScanChannelAlignmentChannelOutcome> outcomes,
        List<ScanChannelAlignmentDiagnostic> diagnostics,
        int channelIndex,
        ScanChannelAssignment assignment,
        ScanChannelAlignmentDiagnosticKind kind,
        string message,
        Exception? exception = null)
    {
        var diagnostic = CreateDiagnostic(kind, channelIndex, GetChannelRole(assignment, channelIndex), message, exception);
        outcomes[channelIndex] = new ScanChannelAlignmentChannelOutcome(channelIndex, diagnostic.ChannelRole ?? $"Pass {channelIndex}", ScanChannelAlignmentStatus.Fallback, diagnostic);
        diagnostics.Add(diagnostic);
    }

    private static void AddFallbackOutcome(
        List<ScanChannelAlignmentChannelOutcome> outcomes,
        List<ScanChannelAlignmentDiagnostic> diagnostics,
        int channelIndex,
        ScanChannelAssignment assignment,
        AlignmentFailure failure)
    {
        var diagnostic = CreateDiagnostic(failure.Kind, channelIndex, GetChannelRole(assignment, channelIndex), failure.Message, failure.Exception);
        outcomes[channelIndex] = new ScanChannelAlignmentChannelOutcome(channelIndex, diagnostic.ChannelRole ?? $"Pass {channelIndex}", ScanChannelAlignmentStatus.Fallback, diagnostic);
        diagnostics.Add(diagnostic);
    }

    private static void AddFallbackDiagnostic(
        List<ScanChannelAlignmentDiagnostic> diagnostics,
        int channelIndex,
        ScanChannelAssignment assignment,
        AlignmentFailure failure)
        => diagnostics.Add(CreateDiagnostic(failure.Kind, channelIndex, GetChannelRole(assignment, channelIndex), failure.Message, failure.Exception));

    private static ScanChannelAlignmentDiagnostic CreateDiagnostic(
        ScanChannelAlignmentDiagnosticKind kind,
        int? channelIndex,
        string? channelRole,
        string message,
        Exception? exception = null)
    {
        if (exception is not OpenCVException nativeException)
            return new ScanChannelAlignmentDiagnostic(kind, channelIndex, channelRole, message);

        return new ScanChannelAlignmentDiagnostic(
            kind,
            channelIndex,
            channelRole,
            string.IsNullOrWhiteSpace(nativeException.ErrMsg) ? message : nativeException.ErrMsg,
            (int)nativeException.Status,
            nativeException.FuncName,
            nativeException.FileName,
            nativeException.Line);
    }

    private static ScanChannelAlignmentDiagnosticKind ClassifyOpenCvFailure(Exception exception)
        => exception.Message.Contains("converg", StringComparison.OrdinalIgnoreCase)
            ? ScanChannelAlignmentDiagnosticKind.NonConverged
            : ScanChannelAlignmentDiagnosticKind.OpenCvFailure;

    private static bool TryCreateRoi(int sourceWidth, int sourceRows, int x, int y, int width, int height, out Rect roi)
    {
        roi = default;
        if (sourceWidth <= 0 || sourceRows <= 0 || x < 0 || y < 0 || width <= 0 || height <= 0)
            return false;

        if ((long)x + width > sourceWidth || (long)y + height > sourceRows)
            return false;

        roi = new Rect(x, y, width, height);
        return true;
    }

    private static int FindSingleRoleIndex(ScanChannelAssignment assignment, string role)
    {
        var matchedIndex = -1;
        for (var index = 0; index < assignment.Roles.Count; index++)
        {
            if (!string.Equals(assignment.Roles[index], role, StringComparison.OrdinalIgnoreCase))
                continue;

            if (matchedIndex >= 0)
                return -1;

            matchedIndex = index;
        }

        return matchedIndex;
    }

    private Mat BuildSampledRoiMat(byte[] buffer, int rows, Rect roi, int maxDimension, CancellationToken cancellationToken, out double scale)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.Length == 0 || roi.Width <= 0 || roi.Height <= 0 || rows <= 0)
        {
            scale = 1.0;
            return new Mat();
        }

        scale = maxDimension > 0
            ? Math.Min(1.0, maxDimension / (double)Math.Max(roi.Width, roi.Height))
            : 1.0;
        var sampledWidth = Math.Max(1, (int)Math.Round(roi.Width * scale));
        var sampledHeight = Math.Max(1, (int)Math.Round(roi.Height * scale));

        var mat = new Mat(sampledHeight, sampledWidth, MatType.CV_32FC1);
        var hasSample = false;
        for (var y = 0; y < sampledHeight; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceYOffset = sampledHeight == 1
                ? 0
                : (int)Math.Round(y * (roi.Height - 1d) / Math.Max(sampledHeight - 1d, 1d));
            var sourceY = roi.Y + sourceYOffset;

            for (var x = 0; x < sampledWidth; x++)
            {
                var sourceXOffset = sampledWidth == 1
                    ? 0
                    : (int)Math.Round(x * (roi.Width - 1d) / Math.Max(sampledWidth - 1d, 1d));
                var sourceX = roi.X + sourceXOffset;
                if (!_decoder.TryGetSample16(buffer, rows, sourceX, sourceY, out var sample))
                    sample = 0;
                else
                    hasSample = true;

                mat.Set(y, x, sample / (float)ushort.MaxValue);
            }
        }

        if (!hasSample)
        {
            mat.Dispose();
            return new Mat();
        }

        return mat;
    }

    private TranslationEstimate EstimateTranslation(
        Mat coarseReference,
        Mat coarseMoving,
        Mat fineReference,
        Mat fineMoving,
        double coarseScale,
        ScanChannelAlignmentMode alignmentMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (alignmentMode == ScanChannelAlignmentMode.EccThenMutualInformation)
        {
            AlignmentFailure? eccFailure = null;
            double eccShiftX;
            double eccShiftY;
            try
            {
                (eccShiftX, eccShiftY) = EstimateEccTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale, cancellationToken);
            }
            catch (OpenCVException exception)
            {
                eccFailure = CreateOpenCvFailure(exception);
                eccShiftX = 0.0;
                eccShiftY = 0.0;
            }
            catch (OpenCvSharpException exception)
            {
                eccFailure = CreateOpenCvFailure(exception);
                eccShiftX = 0.0;
                eccShiftY = 0.0;
            }

            try
            {
                var (shiftX, shiftY) = EstimateMutualInformationTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale, eccShiftX, eccShiftY, cancellationToken);
                return new TranslationEstimate(shiftX, shiftY, eccFailure);
            }
            catch (OpenCVException exception) when (eccFailure is { } capturedEccFailure)
            {
                throw new EccThenMutualInformationFailureException(capturedEccFailure, CreateOpenCvFailure(exception));
            }
            catch (OpenCvSharpException exception) when (eccFailure is { } capturedEccFailure)
            {
                throw new EccThenMutualInformationFailureException(capturedEccFailure, CreateOpenCvFailure(exception));
            }
            catch (InvalidOperationException exception) when (eccFailure is { } capturedEccFailure)
            {
                throw new EccThenMutualInformationFailureException(
                    capturedEccFailure,
                    new AlignmentFailure(ScanChannelAlignmentDiagnosticKind.AlignmentUnavailable, exception.Message, exception));
            }
        }

        var translation = alignmentMode switch
        {
            ScanChannelAlignmentMode.Ecc => EstimateEccTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale, cancellationToken),
            ScanChannelAlignmentMode.MutualInformation => EstimateMutualInformationTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale, 0.0, 0.0, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported scan channel alignment mode '{alignmentMode}'.")
        };
        return new TranslationEstimate(translation.ShiftX, translation.ShiftY, null);
    }

    private static AlignmentFailure CreateOpenCvFailure(Exception exception)
        => new(ClassifyOpenCvFailure(exception), exception.Message, exception);

    private (double ShiftX, double ShiftY) EstimateEccTranslation(Mat coarseReference, Mat coarseMoving, Mat fineReference, Mat fineMoving, double coarseScale, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (shiftX, shiftY) = EstimateEccTranslationCore(coarseReference, coarseMoving, coarseScale, 0.0, 0.0, CoarseEccMaxIterations, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!fineReference.Empty() && !fineMoving.Empty())
        {
            (shiftX, shiftY) = EstimateEccTranslationCore(fineReference, fineMoving, 1.0, shiftX, shiftY, FineEccMaxIterations, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return (shiftX, shiftY);
    }

    private static (double ShiftX, double ShiftY) EstimateMutualInformationTranslation(Mat coarseReference, Mat coarseMoving, Mat fineReference, Mat fineMoving, double coarseScale, double initialShiftX, double initialShiftY, CancellationToken cancellationToken)
    {
        var (shiftX, shiftY) = EstimateMutualInformationTranslationCore(
            coarseReference,
            coarseMoving,
            coarseScale,
            initialShiftX,
            initialShiftY,
            CoarseMutualInformationSearchRadius,
            MutualInformationCoarseGridStep,
            cancellationToken);

        if (!fineReference.Empty() && !fineMoving.Empty())
        {
            (shiftX, shiftY) = EstimateMutualInformationTranslationCore(
                fineReference,
                fineMoving,
                1.0,
                shiftX,
                shiftY,
                FineMutualInformationSearchRadius,
                MutualInformationFineGridStep,
                cancellationToken);
        }

        return (shiftX, shiftY);
    }

    private (double ShiftX, double ShiftY) EstimateEccTranslationCore(Mat reference, Mat moving, double scale, double initialShiftX, double initialShiftY, int maxIterations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var warpMatrix = new Mat(2, 3, MatType.CV_32FC1, Scalar.All(0));
        warpMatrix.Set(0, 0, 1f);
        warpMatrix.Set(1, 1, 1f);
        warpMatrix.Set(0, 2, (float)(scale * initialShiftX));
        warpMatrix.Set(1, 2, (float)(scale * initialShiftY));
        var criteria = new TermCriteria(CriteriaTypes.Count | CriteriaTypes.Eps, maxIterations, EccMinIncrement);
        _backend.FindTransformEcc(reference, moving, warpMatrix, criteria, EccGaussianFilterSize);
        cancellationToken.ThrowIfCancellationRequested();

        var shiftX = warpMatrix.At<float>(0, 2);
        var shiftY = warpMatrix.At<float>(1, 2);
        if (scale <= 0 || Math.Abs(scale - 1.0) < double.Epsilon)
            return (shiftX, shiftY);
        
        return (shiftX / scale, shiftY / scale);
    }

    private static (double ShiftX, double ShiftY) EstimateMutualInformationTranslationCore(
        Mat reference,
        Mat moving,
        double scale,
        double initialShiftX,
        double initialShiftY,
        double searchRadius,
        double gridStep,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference.Empty() || moving.Empty())
            throw new InvalidOperationException("Mutual-information alignment requires non-empty images.");

        var bestShiftX = initialShiftX;
        var bestShiftY = initialShiftY;
        var bestScore = ComputeMutualInformation(reference, moving, scale, bestShiftX, bestShiftY, cancellationToken);
        var radius = Math.Max(searchRadius, gridStep);
        var step = Math.Max(gridStep, MutualInformationMinStep);

        for (var offsetY = -radius; offsetY <= radius; offsetY += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var offsetX = -radius; offsetX <= radius; offsetX += step)
            {
                var candidateShiftX = initialShiftX + offsetX;
                var candidateShiftY = initialShiftY + offsetY;
                var candidateScore = ComputeMutualInformation(reference, moving, scale, candidateShiftX, candidateShiftY, cancellationToken);
                if (candidateScore > bestScore)
                {
                    bestScore = candidateScore;
                    bestShiftX = candidateShiftX;
                    bestShiftY = candidateShiftY;
                }
            }
        }

        var refinementStep = Math.Max(step / 2.0, MutualInformationMinStep);
        while (refinementStep >= MutualInformationMinStep)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var improved = false;
            for (var deltaY = -refinementStep; deltaY <= refinementStep; deltaY += refinementStep)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var deltaX = -refinementStep; deltaX <= refinementStep; deltaX += refinementStep)
                {
                    if (Math.Abs(deltaX) < double.Epsilon && Math.Abs(deltaY) < double.Epsilon)
                        continue;

                    var candidateShiftX = bestShiftX + deltaX;
                    var candidateShiftY = bestShiftY + deltaY;
                    var candidateScore = ComputeMutualInformation(reference, moving, scale, candidateShiftX, candidateShiftY, cancellationToken);
                    if (candidateScore > bestScore)
                    {
                        bestScore = candidateScore;
                        bestShiftX = candidateShiftX;
                        bestShiftY = candidateShiftY;
                        improved = true;
                    }
                }
            }

            if (!improved)
                refinementStep /= 2.0;
        }

        if (double.IsNegativeInfinity(bestScore))
            throw new InvalidOperationException("Mutual-information alignment could not find a usable overlap.");

        return (bestShiftX, bestShiftY);
    }

    private static double ComputeMutualInformation(Mat reference, Mat moving, double scale, double shiftX, double shiftY, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scaledShiftX = scale * shiftX;
        var scaledShiftY = scale * shiftY;
        var rows = reference.Rows;
        var cols = reference.Cols;
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((rows * (double)cols) / MaximumMutualInformationSamples)));
        var sampledRows = Math.Max(1, ((rows - 1) / stride) + 1);
        var sampledCols = Math.Max(1, ((cols - 1) / stride) + 1);
        var sampledPixelCount = sampledRows * sampledCols;
        var requiredSamples = Math.Max(256, (int)Math.Round(sampledPixelCount * MinimumMutualInformationCoverage));
        var jointHistogram = new double[MutualInformationHistogramBins * MutualInformationHistogramBins];
        var referenceHistogram = new double[MutualInformationHistogramBins];
        var movingHistogram = new double[MutualInformationHistogramBins];
        var sampleCount = 0;

        for (var y = 0; y < rows; y += stride)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < cols; x += stride)
            {
                if (!TrySampleNormalized(moving, x + scaledShiftX, y + scaledShiftY, out var movingValue))
                    continue;

                var referenceValue = Math.Clamp(reference.At<float>(y, x), 0f, 1f);
                var referenceBin = Math.Min(MutualInformationHistogramBins - 1, (int)(referenceValue * MutualInformationHistogramBins));
                var movingBin = Math.Min(MutualInformationHistogramBins - 1, (int)(movingValue * MutualInformationHistogramBins));
                jointHistogram[(referenceBin * MutualInformationHistogramBins) + movingBin]++;
                referenceHistogram[referenceBin]++;
                movingHistogram[movingBin]++;
                sampleCount++;
            }
        }

        if (sampleCount < requiredSamples)
            return double.NegativeInfinity;

        var inverseSampleCount = 1.0 / sampleCount;
        var score = 0.0;
        for (var referenceBin = 0; referenceBin < MutualInformationHistogramBins; referenceBin++)
        {
            var px = referenceHistogram[referenceBin] * inverseSampleCount;
            if (px <= 0.0)
                continue;

            for (var movingBin = 0; movingBin < MutualInformationHistogramBins; movingBin++)
            {
                var jointCount = jointHistogram[(referenceBin * MutualInformationHistogramBins) + movingBin];
                if (jointCount <= 0.0)
                    continue;

                var py = movingHistogram[movingBin] * inverseSampleCount;
                var pxy = jointCount * inverseSampleCount;
                score += pxy * Math.Log(pxy / (px * py));
            }
        }

        return score;
    }

    private static bool TrySampleNormalized(Mat mat, double x, double y, out float value)
    {
        value = 0f;
        if (mat.Empty() || x < 0 || y < 0 || x > mat.Cols - 1 || y > mat.Rows - 1)
            return false;

        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = Math.Min(x0 + 1, mat.Cols - 1);
        var y1 = Math.Min(y0 + 1, mat.Rows - 1);
        var wx = x - x0;
        var wy = y - y0;

        var topLeft = mat.At<float>(y0, x0);
        var topRight = mat.At<float>(y0, x1);
        var bottomLeft = mat.At<float>(y1, x0);
        var bottomRight = mat.At<float>(y1, x1);
        var top = Lerp(topLeft, topRight, wx);
        var bottom = Lerp(bottomLeft, bottomRight, wx);
        value = (float)Math.Clamp(Lerp(top, bottom, wy), 0.0, 1.0);
        return true;
    }

    private ushort[] DecodeToSampleGrid(byte[] buffer, int rows, int width, CancellationToken cancellationToken)
    {
        var samples = new ushort[checked(width * rows)];
        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                if (_decoder.TryGetSample16(buffer, rows, x, y, out var sample))
                    samples[rowOffset + x] = sample;
            }
        }

        return samples;
    }

    private static ushort[] ApplyTranslation(ushort[] sourceSamples, int width, int rows, double shiftX, double shiftY, CancellationToken cancellationToken)
    {
        var destinationSamples = new ushort[sourceSamples.Length];
        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceY = y + shiftY;
            var y0 = (int)Math.Floor(sourceY);
            var y1 = y0 + 1;
            var wy = sourceY - y0;

            for (var x = 0; x < width; x++)
            {
                var sourceX = x + shiftX;
                var x0 = (int)Math.Floor(sourceX);
                var x1 = x0 + 1;
                var wx = sourceX - x0;

                var topLeft = GetSampleOrZero(sourceSamples, width, rows, x0, y0);
                var topRight = GetSampleOrZero(sourceSamples, width, rows, x1, y0);
                var bottomLeft = GetSampleOrZero(sourceSamples, width, rows, x0, y1);
                var bottomRight = GetSampleOrZero(sourceSamples, width, rows, x1, y1);

                var top = Lerp(topLeft, topRight, wx);
                var bottom = Lerp(bottomLeft, bottomRight, wx);
                var value = Lerp(top, bottom, wy);
                destinationSamples[(y * width) + x] = (ushort)Math.Clamp((int)Math.Round(value), 0, ushort.MaxValue);
            }
        }

        return destinationSamples;
    }

    private static double GetSampleOrZero(ushort[] samples, int width, int rows, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= rows)
            return 0.0;

        return samples[(y * width) + x];
    }

    private static double Lerp(double start, double end, double t)
        => start + ((end - start) * t);

    private static byte[] EncodeSampleGrid(byte[] sourceBuffer, ushort[] samples, int width, int rows, CancellationToken cancellationToken)
    {
        var buffer = (byte[])sourceBuffer.Clone();
        for (var y = 0; y < rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowStart = y * ScanDebugConstants.BytesPerLine;
            var groupStart = rowStart + ScanDebugConstants.LineBufferMarginLeft;
            for (var x = 0; x < width; x += ScanDebugConstants.PackedGroupPixels)
            {
                var evenSample = samples[(y * width) + x];
                var oddSample = x + 1 < width ? samples[(y * width) + x + 1] : evenSample;
                var offset = groupStart + ((x / ScanDebugConstants.PackedGroupPixels) * ScanDebugConstants.PackedGroupBytes);

                buffer[offset] = (byte)(oddSample >> 8);
                buffer[offset + 1] = (byte)(evenSample >> 8);
                buffer[offset + 2] = (byte)(oddSample & 0xFF);
                buffer[offset + 3] = (byte)(evenSample & 0xFF);
            }
        }

        return buffer;
    }

    private static FineWindow BuildFineWindow(int effectiveStartX, int effectiveEndXInclusive, int rows)
    {
        var roiWidth = Math.Max(1, effectiveEndXInclusive - effectiveStartX + 1);
        var width = Math.Min(roiWidth, FineMaxWidth);
        var height = Math.Min(rows, FineMaxHeight);
        var startX = effectiveStartX + Math.Max(0, (roiWidth - width) / 2);
        var startY = Math.Max(0, (rows - height) / 2);
        return new FineWindow(startX, startY, width, height);
    }

    private readonly record struct TranslationEstimate(double ShiftX, double ShiftY, AlignmentFailure? EccFallback);

    private readonly record struct AlignmentFailure(
        ScanChannelAlignmentDiagnosticKind Kind,
        string Message,
        Exception Exception);

    private sealed class EccThenMutualInformationFailureException(
        AlignmentFailure eccFailure,
        AlignmentFailure mutualInformationFailure) : Exception
    {
        public AlignmentFailure EccFailure { get; } = eccFailure;

        public AlignmentFailure MutualInformationFailure { get; } = mutualInformationFailure;
    }

    private readonly record struct FineWindow(int StartX, int StartY, int Width, int Height);
}
