using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanFilmProfileDocumentService
{
    int CurrentSchemaVersion { get; }

    ScanFilmProfileDocumentParseResult Parse(string json);
    ScanFilmProfileDocumentBuildResult Build(ScanFilmProfileDraft? draft);
    ScanFilmProfileValidationResult Validate(ScanFilmProfileDraft? draft);
    string Serialize(ScanFilmParameterProfileSet document);
    IReadOnlyDictionary<string, ScanChannelCalibrationProfile> MigrateLegacyLocalProfiles(IReadOnlyDictionary<string, ScanParameterSnapshot>? legacyProfiles);
}
