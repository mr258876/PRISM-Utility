using PRISM_Utility.Contracts.Services;
using Windows.Storage;
using Windows.System;

namespace PRISM_Utility.Services;

public sealed class ScanOutputLocationService : IScanOutputLocationService
{
    public async Task<bool> OpenAsync(string path, bool isFile)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // Resolve the saved item first: a deleted file must not report that
            // its location was opened successfully merely because its parent exists.
            var folder = isFile
                ? await (await StorageFile.GetFileFromPathAsync(path)).GetParentAsync()
                : await StorageFolder.GetFolderFromPathAsync(path);
            return folder is not null && await Launcher.LaunchFolderAsync(folder);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
