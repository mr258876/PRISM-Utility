namespace PRISM_Utility.Contracts.Services;

public interface ILanguageSelectorService
{
    string CurrentLanguage { get; }

    Task InitializeAsync();

    Task ApplyLanguageAsync();

    Task SetLanguageAsync(string languageTag);
}
