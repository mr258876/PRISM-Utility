namespace PRISM_Utility.Core.Services;

/// <summary>Publishes an output set together, restoring every earlier file if publication fails.</summary>
public sealed class ScanOutputFileSetTransaction : IDisposable
{
    private readonly List<ScanOutputFileTransaction> _files = [];
    private bool _published;

    public ScanOutputFileSetTransaction(IEnumerable<string> targetPaths)
    {
        ArgumentNullException.ThrowIfNull(targetPaths);
        var paths = targetPaths.Select(Path.GetFullPath).ToArray();
        if (paths.Length == 0)
            throw new ArgumentException("At least one output target is required.", nameof(targetPaths));
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
            throw new ArgumentException("Output targets must be distinct.", nameof(targetPaths));

        try
        {
            foreach (var path in paths)
                _files.Add(new ScanOutputFileTransaction(path));
        }
        catch
        {
            Dispose();
            throw;
        }

        TemporaryPaths = Array.AsReadOnly(_files.Select(file => file.TemporaryPath).ToArray());
    }

    public IReadOnlyList<string> TemporaryPaths { get; }

    public void Publish(CancellationToken cancellationToken, Action<int>? afterPublish = null)
    {
        if (_published)
            throw new InvalidOperationException("This output set has already been published.");

        try
        {
            for (var index = 0; index < _files.Count; index++)
            {
                _files[index].Publish(cancellationToken, retainBackup: true);
                afterPublish?.Invoke(index);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception publicationFailure)
        {
            var failures = new List<Exception> { publicationFailure };
            for (var index = _files.Count - 1; index >= 0; index--)
            {
                try { _files[index].Rollback(); }
                catch (Exception rollbackFailure) { failures.Add(rollbackFailure); }
            }
            if (failures.Count > 1)
                throw new AggregateException("Output publication and restoration failed.", failures);
            throw;
        }

        _published = true;
        foreach (var file in _files)
        {
            try { file.Complete(); }
            catch (IOException) { /* The output is committed; disposal retries backup cleanup. */ }
        }
    }

    public void Dispose()
    {
        List<Exception>? failures = null;
        foreach (var file in _files)
        {
            try { file.Dispose(); }
            catch (Exception failure) { (failures ??= []).Add(failure); }
        }
        if (failures is not null)
            throw new AggregateException("Output temporary-file cleanup failed.", failures);
    }
}
