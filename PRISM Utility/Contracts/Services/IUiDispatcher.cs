namespace PRISM_Utility.Contracts.Services;

public interface IUiDispatcher
{
    bool TryEnqueue(Action action);
}
