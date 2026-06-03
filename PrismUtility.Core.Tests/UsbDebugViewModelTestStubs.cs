using System.Globalization;

namespace PRISM_Utility.Contracts.Services
{
    public interface IDebugOutputMirrorService
    {
        void Mirror(string source, string message);
    }

    public interface IUiDispatcher
    {
        bool TryEnqueue(Action action);
    }
}
