using OpenCvSharp;
using PRISM_Utility.Core.Contracts.Services;

namespace PRISM_Utility.Core.Services;

public sealed class OpenCvScanChannelAlignmentBackend : IScanChannelAlignmentBackend
{
    public void FindTransformEcc(Mat reference, Mat moving, Mat warpMatrix, TermCriteria criteria, int gaussianFilterSize)
        => Cv2.FindTransformECC(reference, moving, warpMatrix, MotionTypes.Translation, criteria, null, gaussianFilterSize);
}
