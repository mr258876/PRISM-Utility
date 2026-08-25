namespace PRISM_Utility.Core.Services;

public static class ScanFilmProfileFileName
{
    public static string CreateSuggestedName(string? profileName)
    {
        var name = string.IsNullOrWhiteSpace(profileName)
            ? "film_profile"
            : string.Concat(profileName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
        return string.IsNullOrWhiteSpace(name) ? "film_profile" : name;
    }
}
