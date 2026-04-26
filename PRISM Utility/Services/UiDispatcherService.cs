using Microsoft.UI.Dispatching;
using PRISM_Utility.Contracts.Services;

namespace PRISM_Utility.Services;

public sealed class UiDispatcherService : IUiDispatcher
{
    public bool TryEnqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var queue = App.MainWindow.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        return queue.TryEnqueue(() => action());
    }
}
