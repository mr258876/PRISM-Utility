using Microsoft.UI.Xaml;

using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;

namespace PRISM_Utility.Activation;

public class DefaultActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly INavigationService _navigationService;

    public DefaultActivationHandler(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        // None of the ActivationHandlers has handled the activation.
        return _navigationService.Frame?.Content == null;
    }

    protected async override Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        _navigationService.NavigateTo(AppRoute.Main, args.Arguments);

#if PRISM_VISUAL_QA
        PrismVisualQaPendingCalibrationHook.Activate(_navigationService);
#endif

        await Task.CompletedTask;
    }
}
