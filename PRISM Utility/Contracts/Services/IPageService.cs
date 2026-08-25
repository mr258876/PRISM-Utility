using PRISM_Utility.Contracts.Navigation;

namespace PRISM_Utility.Contracts.Services;

public interface IPageService
{
    Type GetPageType(AppRoute route);
}
