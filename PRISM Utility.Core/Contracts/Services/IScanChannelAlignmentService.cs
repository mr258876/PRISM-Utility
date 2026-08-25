using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanChannelAlignmentService
{
    ScanChannelAlignmentResult BuildAlignedNormalizedPassBuffers(
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        ScanChannelAlignmentMode alignmentMode,
        CancellationToken cancellationToken);
}
