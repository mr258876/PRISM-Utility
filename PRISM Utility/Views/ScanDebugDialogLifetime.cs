namespace PRISM_Utility.Views;

internal sealed class ScanDebugDialogLifetime
{
    [ThreadStatic]
    private static ScanDebugDialogLifetime? _currentThread;

    private readonly object _gate = new();
    private Lease? _active;

    internal static ScanDebugDialogLifetime ForCurrentThread => _currentThread ??= new ScanDebugDialogLifetime();

    internal Lease? TryAcquire(object page, int activationEpoch, Action<bool> retire)
    {
        lock (_gate)
        {
            if (_active is not null)
                return null;

            return _active = new Lease(this, page, activationEpoch, retire);
        }
    }

    internal void Retire(object page, int activationEpoch)
    {
        Lease? lease;
        lock (_gate)
        {
            lease = _active is { } current && ReferenceEquals(current.Page, page) && current.ActivationEpoch == activationEpoch
                ? current : null;
        }

        lease?.Retire();
    }

    private void Release(Lease lease)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_active, lease))
                _active = null;
        }
    }

    internal sealed class Lease
    {
        private readonly ScanDebugDialogLifetime _lifetime;
        private readonly Action<bool> _retire;
        private readonly object _gate = new();
        private bool _settled;
        private bool _showStarted;

        internal Lease(ScanDebugDialogLifetime lifetime, object page, int activationEpoch, Action<bool> retire)
        {
            _lifetime = lifetime;
            Page = page;
            ActivationEpoch = activationEpoch;
            _retire = retire;
        }

        internal object Page { get; }

        internal int ActivationEpoch { get; }

        internal void Retire()
        {
            bool showStarted;
            lock (_gate)
            {
                if (_settled)
                    return;

                _settled = true;
                showStarted = _showStarted;
            }

            _retire(showStarted);
        }

        internal async Task RunAsync<TResult>(
            CancellationToken hostCancellationToken,
            Func<bool> isCurrentActivation,
            Func<Task<TResult>> showAsync,
            Action<TResult> complete,
            Action<Exception> fault)
        {
            using var registration = hostCancellationToken.Register(Retire);
            try
            {
                if (!isCurrentActivation())
                {
                    Retire();
                    return;
                }

                lock (_gate)
                {
                    if (_settled)
                        return;

                    _showStarted = true;
                }

                var result = await showAsync();
                if (!isCurrentActivation())
                {
                    Retire();
                    return;
                }

                TryComplete(() => complete(result));
            }
            catch (Exception ex)
            {
                TryComplete(() => fault(ex));
            }
            finally
            {
                _lifetime.Release(this);
            }
        }

        private void TryComplete(Action complete)
        {
            lock (_gate)
            {
                if (_settled)
                    return;

                _settled = true;
            }

            complete();
        }
    }
}
