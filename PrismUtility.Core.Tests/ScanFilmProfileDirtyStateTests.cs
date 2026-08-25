using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileDirtyStateTests
{
    public static TheoryData<string, bool, bool, bool> SemanticTransitions => new()
    {
        { "Initial", false, false, false },
        { "Edit", true, false, true },
        { "EquivalentEdit", false, false, false },
        { "Invalid", false, true, true },
        { "Recovered", false, false, false },
        { "StageUnchanged", true, false, true },
        { "DiscardUnchanged", true, false, true },
        { "Apply", false, false, false },
        { "ExportSuccess", false, false, false },
        { "ExportCancelPreserved", true, false, true },
        { "ExportFailurePreserved", true, false, true },
        { "SelectionChange", true, false, true },
        { "SameSelection", false, false, false }
    };

    [Theory]
    [MemberData(nameof(SemanticTransitions))]
    public void IsDirty_ProjectsWorkspaceAndInvalidOverlay(string _, bool workspaceIsDirty, bool hasInvalidInput, bool expected)
        => Assert.Equal(expected, ScanFilmProfileDirtyState.IsDirty(workspaceIsDirty, hasInvalidInput));
}
