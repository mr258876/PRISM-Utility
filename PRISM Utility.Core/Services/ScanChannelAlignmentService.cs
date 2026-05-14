using OpenCvSharp;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanChannelAlignmentService : IScanChannelAlignmentService
{
    private const MotionTypes MotionModel = MotionTypes.Translation;
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

    public ScanChannelAlignmentService(IScanCompositeImageProcessor processor, IScanImageDecoder decoder)
    {
        _processor = processor;
        _decoder = decoder;
    }

    public bool TryBuildAlignedNormalizedPassBuffers(
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        ScanChannelAlignmentMode alignmentMode,
        out byte[][] alignedPassBuffers,
        out string error)
    {
        alignedPassBuffers = Array.Empty<byte[]>();
        error = string.Empty;

        if (result.Passes.Count == 0)
        {
            error = "No scan passes are available for channel alignment.";
            return false;
        }

        var width = _decoder.GetDecodedPixelsPerLine();
        if (width <= 0 || result.Rows <= 0)
        {
            error = "Scan dimensions are invalid for channel alignment.";
            return false;
        }

        var normalizedPasses = BuildNormalizedPassBuffers(result, assignment);
        var greenIndex = FindSingleRoleIndex(assignment, "Green");
        if (greenIndex < 0 || greenIndex >= normalizedPasses.Length)
        {
            alignedPassBuffers = normalizedPasses;
            return true;
        }

        var effectiveRange = _decoder.GetEffectivePixelRange();
        if (effectiveRange.EndInclusive < effectiveRange.Start)
        {
            alignedPassBuffers = normalizedPasses;
            return true;
        }

        alignedPassBuffers = new byte[normalizedPasses.Length][];
        Array.Copy(normalizedPasses, alignedPassBuffers, normalizedPasses.Length);

        using var referenceMat = BuildSampledRoiMat(
            normalizedPasses[greenIndex],
            result.Rows,
            effectiveRange.Start,
            0,
            effectiveRange.EndInclusive - effectiveRange.Start + 1,
            result.Rows,
            CoarseMaxDimension,
            out var scale);
        if (referenceMat.Empty())
            return true;

        var fineWindow = BuildFineWindow(effectiveRange.Start, effectiveRange.EndInclusive, result.Rows);
        using var fineReferenceMat = BuildSampledRoiMat(
            normalizedPasses[greenIndex],
            result.Rows,
            fineWindow.StartX,
            fineWindow.StartY,
            fineWindow.Width,
            fineWindow.Height,
            0,
            out _);

        for (var channelIndex = 0; channelIndex < normalizedPasses.Length; channelIndex++)
        {
            if (channelIndex == greenIndex)
                continue;

            try
            {
                using var movingMat = BuildSampledRoiMat(
                    normalizedPasses[channelIndex],
                    result.Rows,
                    effectiveRange.Start,
                    0,
                    effectiveRange.EndInclusive - effectiveRange.Start + 1,
                    result.Rows,
                    CoarseMaxDimension,
                    out _);
                if (movingMat.Empty())
                    continue;

                using var fineMovingMat = !fineReferenceMat.Empty()
                    ? BuildSampledRoiMat(
                        normalizedPasses[channelIndex],
                        result.Rows,
                        fineWindow.StartX,
                        fineWindow.StartY,
                        fineWindow.Width,
                        fineWindow.Height,
                        0,
                        out _)
                    : new Mat();

                var (shiftX, shiftY) = EstimateTranslation(
                    referenceMat,
                    movingMat,
                    fineReferenceMat,
                    fineMovingMat,
                    scale,
                    alignmentMode);

                if (Math.Abs(shiftX) < MinimumMeaningfulShift && Math.Abs(shiftY) < MinimumMeaningfulShift)
                    continue;

                var sourceSamples = DecodeToSampleGrid(normalizedPasses[channelIndex], result.Rows, width);
                var alignedSamples = ApplyTranslation(sourceSamples, width, result.Rows, shiftX, shiftY);
                alignedPassBuffers[channelIndex] = EncodeSampleGrid(normalizedPasses[channelIndex], alignedSamples, width, result.Rows);
            }
            catch (Exception ex) when (ex is OpenCVException or InvalidOperationException)
            {
                alignedPassBuffers[channelIndex] = normalizedPasses[channelIndex];
            }
        }

        return true;
    }

    private byte[][] BuildNormalizedPassBuffers(ScanWorkflowResult result, ScanChannelAssignment assignment)
    {
        var normalizedPasses = new byte[result.Passes.Count][];
        for (var index = 0; index < result.Passes.Count; index++)
        {
            var shouldReverse = index < assignment.ReversedFlags.Count && assignment.ReversedFlags[index];
            normalizedPasses[index] = _processor.NormalizePassBuffer(result.Passes[index], shouldReverse);
        }

        return normalizedPasses;
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

    private Mat BuildSampledRoiMat(byte[] buffer, int rows, int startX, int startY, int width, int height, int maxDimension, out double scale)
    {
        var roiWidth = Math.Max(0, width);
        var roiHeight = Math.Max(0, height);
        if (roiWidth == 0 || roiHeight == 0 || rows <= 0)
        {
            scale = 1.0;
            return new Mat();
        }

        scale = maxDimension > 0
            ? Math.Min(1.0, maxDimension / (double)Math.Max(roiWidth, roiHeight))
            : 1.0;
        var sampledWidth = Math.Max(1, (int)Math.Round(roiWidth * scale));
        var sampledHeight = Math.Max(1, (int)Math.Round(roiHeight * scale));

        var mat = new Mat(sampledHeight, sampledWidth, MatType.CV_32FC1);
        for (var y = 0; y < sampledHeight; y++)
        {
            var sourceYOffset = sampledHeight == 1
                ? 0
                : (int)Math.Round(y * (roiHeight - 1d) / Math.Max(sampledHeight - 1d, 1d));
            var sourceY = startY + sourceYOffset;

            for (var x = 0; x < sampledWidth; x++)
            {
                var sourceXOffset = sampledWidth == 1
                    ? 0
                    : (int)Math.Round(x * (roiWidth - 1d) / Math.Max(sampledWidth - 1d, 1d));
                var sourceX = startX + sourceXOffset;
                if (!_decoder.TryGetSample16(buffer, rows, sourceX, sourceY, out var sample))
                    sample = 0;

                mat.Set(y, x, sample / (float)ushort.MaxValue);
            }
        }

        return mat;
    }

    private static (double ShiftX, double ShiftY) EstimateTranslation(
        Mat coarseReference,
        Mat coarseMoving,
        Mat fineReference,
        Mat fineMoving,
        double coarseScale,
        ScanChannelAlignmentMode alignmentMode)
    {
        if (alignmentMode == ScanChannelAlignmentMode.EccThenMutualInformation)
        {
            try
            {
                var (eccShiftX, eccShiftY) = EstimateEccTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale);
                return EstimateMutualInformationTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale, eccShiftX, eccShiftY);
            }
            catch (OpenCVException)
            {
                return EstimateMutualInformationTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale, 0.0, 0.0);
            }
        }

        return alignmentMode switch
        {
            ScanChannelAlignmentMode.Ecc => EstimateEccTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale),
            ScanChannelAlignmentMode.MutualInformation => EstimateMutualInformationTranslation(coarseReference, coarseMoving, fineReference, fineMoving, coarseScale, 0.0, 0.0),
            _ => throw new InvalidOperationException($"Unsupported scan channel alignment mode '{alignmentMode}'.")
        };
    }

    private static (double ShiftX, double ShiftY) EstimateEccTranslation(Mat coarseReference, Mat coarseMoving, Mat fineReference, Mat fineMoving, double coarseScale)
    {
        var (shiftX, shiftY) = EstimateEccTranslationCore(coarseReference, coarseMoving, coarseScale, 0.0, 0.0, CoarseEccMaxIterations);
        if (!fineReference.Empty() && !fineMoving.Empty())
            (shiftX, shiftY) = EstimateEccTranslationCore(fineReference, fineMoving, 1.0, shiftX, shiftY, FineEccMaxIterations);

        return (shiftX, shiftY);
    }

    private static (double ShiftX, double ShiftY) EstimateMutualInformationTranslation(Mat coarseReference, Mat coarseMoving, Mat fineReference, Mat fineMoving, double coarseScale, double initialShiftX, double initialShiftY)
    {
        var (shiftX, shiftY) = EstimateMutualInformationTranslationCore(
            coarseReference,
            coarseMoving,
            coarseScale,
            initialShiftX,
            initialShiftY,
            CoarseMutualInformationSearchRadius,
            MutualInformationCoarseGridStep);

        if (!fineReference.Empty() && !fineMoving.Empty())
        {
            (shiftX, shiftY) = EstimateMutualInformationTranslationCore(
                fineReference,
                fineMoving,
                1.0,
                shiftX,
                shiftY,
                FineMutualInformationSearchRadius,
                MutualInformationFineGridStep);
        }

        return (shiftX, shiftY);
    }

    private static (double ShiftX, double ShiftY) EstimateEccTranslationCore(Mat reference, Mat moving, double scale, double initialShiftX, double initialShiftY, int maxIterations)
    {
        using var warpMatrix = new Mat(2, 3, MatType.CV_32FC1, Scalar.All(0));
        warpMatrix.Set(0, 0, 1f);
        warpMatrix.Set(1, 1, 1f);
        warpMatrix.Set(0, 2, (float)(scale * initialShiftX));
        warpMatrix.Set(1, 2, (float)(scale * initialShiftY));
        var criteria = new TermCriteria(CriteriaTypes.Count | CriteriaTypes.Eps, maxIterations, EccMinIncrement);
        Cv2.FindTransformECC(reference, moving, warpMatrix, MotionModel, criteria, null, EccGaussianFilterSize);

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
        double gridStep)
    {
        if (reference.Empty() || moving.Empty())
            throw new InvalidOperationException("Mutual-information alignment requires non-empty images.");

        var bestShiftX = initialShiftX;
        var bestShiftY = initialShiftY;
        var bestScore = ComputeMutualInformation(reference, moving, scale, bestShiftX, bestShiftY);
        var radius = Math.Max(searchRadius, gridStep);
        var step = Math.Max(gridStep, MutualInformationMinStep);

        for (var offsetY = -radius; offsetY <= radius; offsetY += step)
        {
            for (var offsetX = -radius; offsetX <= radius; offsetX += step)
            {
                var candidateShiftX = initialShiftX + offsetX;
                var candidateShiftY = initialShiftY + offsetY;
                var candidateScore = ComputeMutualInformation(reference, moving, scale, candidateShiftX, candidateShiftY);
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
            var improved = false;
            for (var deltaY = -refinementStep; deltaY <= refinementStep; deltaY += refinementStep)
            {
                for (var deltaX = -refinementStep; deltaX <= refinementStep; deltaX += refinementStep)
                {
                    if (Math.Abs(deltaX) < double.Epsilon && Math.Abs(deltaY) < double.Epsilon)
                        continue;

                    var candidateShiftX = bestShiftX + deltaX;
                    var candidateShiftY = bestShiftY + deltaY;
                    var candidateScore = ComputeMutualInformation(reference, moving, scale, candidateShiftX, candidateShiftY);
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

    private static double ComputeMutualInformation(Mat reference, Mat moving, double scale, double shiftX, double shiftY)
    {
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

    private ushort[] DecodeToSampleGrid(byte[] buffer, int rows, int width)
    {
        var samples = new ushort[checked(width * rows)];
        for (var y = 0; y < rows; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                if (_decoder.TryGetSample16(buffer, rows, x, y, out var sample))
                    samples[rowOffset + x] = sample;
            }
        }

        return samples;
    }

    private static ushort[] ApplyTranslation(ushort[] sourceSamples, int width, int rows, double shiftX, double shiftY)
    {
        var destinationSamples = new ushort[sourceSamples.Length];
        for (var y = 0; y < rows; y++)
        {
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

    private static byte[] EncodeSampleGrid(byte[] sourceBuffer, ushort[] samples, int width, int rows)
    {
        var buffer = (byte[])sourceBuffer.Clone();
        for (var y = 0; y < rows; y++)
        {
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

    private readonly record struct FineWindow(int StartX, int StartY, int Width, int Height);
}
