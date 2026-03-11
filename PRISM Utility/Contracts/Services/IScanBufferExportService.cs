using Windows.Storage;

namespace PRISM_Utility.Contracts.Services;

public interface IScanBufferExportService
{
    string BuildExportBufferFileName(string selectedRows, int bufferLength, DateTimeOffset timestamp);

    Task<StorageFile?> PickExportFileAsync(string suggestedFileName);

    Task WriteBufferAsync(StorageFile file, byte[] buffer);
}
