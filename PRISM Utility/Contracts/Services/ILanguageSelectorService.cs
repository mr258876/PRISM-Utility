namespace PRISM_Utility.Contracts.Services;

public interface ILanguageSelectorService
{
    string CurrentLanguage { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task ApplyLanguageAsync(CancellationToken cancellationToken = default);

    Task SetLanguageAsync(string languageTag, CancellationToken cancellationToken = default);
}
