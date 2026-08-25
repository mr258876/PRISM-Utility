namespace PRISM_Utility.Core.Models;

public static class ScanFilmProfileDirtyState
{
    public static bool IsDirty(bool workspaceIsDirty, bool hasInvalidInput)
        => workspaceIsDirty || hasInvalidInput;
}
