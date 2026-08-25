namespace PRISM_Utility.Contracts.ViewModels;

public interface IPageViewModelHost
{
    object ViewModel { get; }
}

public interface IPageViewModelHost<TViewModel> : IPageViewModelHost
    where TViewModel : notnull
{
    new TViewModel ViewModel { get; }

    object IPageViewModelHost.ViewModel => ViewModel;
}
