namespace PRISM_Utility.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
