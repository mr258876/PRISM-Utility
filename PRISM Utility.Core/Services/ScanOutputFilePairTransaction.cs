namespace PRISM_Utility.Core.Services;

public sealed class ScanOutputFilePairTransaction : IDisposable
{
    private readonly ScanOutputFileTransaction _first;
    private readonly ScanOutputFileTransaction _second;

    public ScanOutputFilePairTransaction(string firstTargetPath, string secondTargetPath, Action? firstPublishFault = null, Action? secondPublishFault = null, Action? firstCleanupFault = null)
    {
        _first = new(firstTargetPath, firstPublishFault, firstCleanupFault);
        _second = new(secondTargetPath, secondPublishFault);
    }

    public string FirstTemporaryPath => _first.TemporaryPath;
    public string SecondTemporaryPath => _second.TemporaryPath;

    public void Publish(CancellationToken cancellationToken, Action? betweenPublishes = null)
    {
        try
        {
            _first.Publish(cancellationToken, retainBackup: true);
            betweenPublishes?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            _second.Publish(cancellationToken, retainBackup: true);
        }
        catch
        {
            _first.Rollback();
            _second.Rollback();
            throw;
        }

        try
        {
            _first.Complete();
            _second.Complete();
        }
        catch (IOException)
        {
            return;
        }
    }

    public void Dispose()
    {
        _first.Dispose();
        _second.Dispose();
    }
}
