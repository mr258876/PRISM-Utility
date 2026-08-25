using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileFileNameTests
{
    [Theory]
    [InlineData("", "film_profile")]
    [InlineData("   ", "film_profile")]
    [InlineData("Untitled", "Untitled")]
    [InlineData("bad<>:\\name", "bad____name")]
    public void SuggestedName_PreservesExistingSafeFilenameContract(string input, string expected)
        => Assert.Equal(expected, ScanFilmProfileFileName.CreateSuggestedName(input));
}
