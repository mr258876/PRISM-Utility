namespace PRISM_Utility.Core.Models;

/// <summary>Editor text retained separately from the validated, scan-ready profile.</summary>
public sealed record ScanFilmProfileEditorInputs(
    IReadOnlyDictionary<string, string> GlobalInputs,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ChannelInputs,
    string? SelectedCalibrationChannel);
