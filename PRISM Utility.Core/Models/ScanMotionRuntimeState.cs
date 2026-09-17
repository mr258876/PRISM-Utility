namespace PRISM_Utility.Core.Models;

public readonly record struct ScanMotionProducerToken(
    long SessionGeneration,
    long StopGeneration,
    CancellationToken CancellationToken);

public readonly record struct ScanMotionReadToken(long SessionGeneration, long StopGeneration, long FaultGeneration);

public sealed class ScanMotionRuntimeState : IDisposable
{
    private readonly object _gate = new();
    private readonly List<CancellationTokenSource> _retiredStopSources = [];
    private CancellationTokenSource _stopSource = new();
    private long _sessionGeneration;
    private long _stopGeneration;
    private long _faultGeneration;
    private bool _requiresRead;
    private bool _disposed;

    public bool RequiresRead
    {
        get
        {
            lock (_gate)
                return _requiresRead;
        }
    }

    public ScanMotionProducerToken CaptureProducer()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return new(_sessionGeneration, _stopGeneration, _stopSource.Token);
        }
    }

    public bool IsCurrent(ScanMotionProducerToken token)
    {
        lock (_gate)
            return !_disposed
                && !token.CancellationToken.IsCancellationRequested
                && token.SessionGeneration == _sessionGeneration
                && token.StopGeneration == _stopGeneration;
    }

    public void BeginGlobalStop()
    {
        CancellationTokenSource prior;
        lock (_gate)
        {
            ThrowIfDisposed();
            prior = _stopSource;
            _retiredStopSources.Add(prior);
            _stopSource = new CancellationTokenSource();
            _stopGeneration++;
        }

        prior.Cancel();
    }

    public void AdvanceSession()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _sessionGeneration++;
        }
    }

    public void MarkReadRequired()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _faultGeneration++;
            _requiresRead = true;
        }
    }

    public ScanMotionReadToken CaptureRead()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return new(_sessionGeneration, _stopGeneration, _faultGeneration);
        }
    }

    public bool IsCurrent(ScanMotionReadToken token)
    {
        lock (_gate)
            return !_disposed
                && token.SessionGeneration == _sessionGeneration
                && token.StopGeneration == _stopGeneration
                && token.FaultGeneration == _faultGeneration;
    }

    public bool TryAcceptCompleteRead(ScanMotionReadToken token, IReadOnlyList<ScanMotorState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (!HasExactlyOneStateForEachMotor(states))
            return false;

        lock (_gate)
        {
            if (_disposed
                || token.SessionGeneration != _sessionGeneration
                || token.StopGeneration != _stopGeneration
                || token.FaultGeneration != _faultGeneration)
            {
                return false;
            }

            _requiresRead = false;
            return true;
        }
    }

    private static bool HasExactlyOneStateForEachMotor(IReadOnlyList<ScanMotorState> states)
        => states.Count == ScanDebugConstants.MotionMotorCount
            && states.Select(state => state.MotorId).OrderBy(id => id).SequenceEqual([(byte)0, (byte)1, (byte)2]);

    public void Dispose()
    {
        List<CancellationTokenSource> sources;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            sources = [.. _retiredStopSources, _stopSource];
            _retiredStopSources.Clear();
        }

        foreach (var source in sources)
        {
            source.Cancel();
            source.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ScanMotionRuntimeState));
    }
}
