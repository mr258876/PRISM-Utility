namespace PRISM_Utility.Core.Models;

public sealed record ScanFilmProfileDocumentParseResult(
    ScanFilmParameterProfileSet? Document,
    ScanFilmProfileValidationResult Validation)
{
    public bool CanApply => Document is not null && Validation.IsValid;
}

public sealed record ScanFilmProfileDocumentBuildResult(
    ScanFilmParameterProfileSet? Document,
    ScanFilmProfileValidationResult Validation)
{
    public bool CanApply => Document is not null && Validation.IsValid;
}
