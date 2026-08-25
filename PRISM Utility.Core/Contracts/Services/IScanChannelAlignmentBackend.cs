using OpenCvSharp;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanChannelAlignmentBackend
{
    void FindTransformEcc(Mat reference, Mat moving, Mat warpMatrix, TermCriteria criteria, int gaussianFilterSize);
}
