namespace PRISM_Utility.Contracts.Services;

public interface IScanOutputLocationService
{
    Task<bool> OpenAsync(string path, bool isFile);
}
