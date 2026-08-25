namespace PRISM_Utility.Core.Contracts.Services;

public interface ILocalSettingsLegacyCleanupService
{
    Task RemoveSettingsAsync(IReadOnlyCollection<string> keys);
}
