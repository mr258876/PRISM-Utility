using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanChannelAlignmentService
{
    bool TryBuildAlignedNormalizedPassBuffers(
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        ScanChannelAlignmentMode alignmentMode,
        out byte[][] alignedPassBuffers,
        out string error);
}
