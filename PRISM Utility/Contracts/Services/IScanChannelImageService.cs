using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;
using Windows.Storage;

namespace PRISM_Utility.Contracts.Services;

public interface IScanChannelImageService
{
    bool TryBuildRawPreview(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, int channelIndex, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error);
    bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false);
    Task<ScanCompositePixelBuffer> BuildRgbCompositeBufferAsync(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false);
    Task<StorageFile?> PickRgbImageFileAsync(string suggestedFileName);
    Task SaveRgbImageAsync(StorageFile file, ScanCompositeFrame frame);
    Task SaveRgbImageAsync(StorageFile file, ScanCompositePixelBuffer buffer);
    Task<StorageFolder?> PickDngExportFolderAsync();
    Task ExportDngChannelsAsync(StorageFolder folder, ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, ScanDngExportMode exportMode = ScanDngExportMode.LinearRaw4, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null);
    Task ExportMonochromeDngAsync(StorageFolder folder, byte[] pixelData, int rows, ushort exposureTicks, uint sysClockKhz, string channelLabel, ScanChannelCalibrationProfile? profile = null);
    bool TryComputeAlignedChannelColumnAverage(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, string channelRole, ScanColumnRange range, out ushort average, out string error);
}
