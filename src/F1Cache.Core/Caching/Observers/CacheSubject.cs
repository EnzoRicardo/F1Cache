using F1Cache.Core.Interfaces;

namespace F1Cache.Core.Caching.Observers;

/// <summary>
/// Reusable observer registry that caches compose to broadcast lifecycle events.
/// The list is copy-on-write, so notifications iterate a snapshot without locking.
/// </summary>
internal sealed class CacheSubject<TKey> : ICacheSubject<TKey>
    where TKey : notnull
{
    private readonly Lock _lock = new();
    private ICacheObserver<TKey>[] _observers = [];

    public void Attach(ICacheObserver<TKey> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_lock)
        {
            if (!_observers.Contains(observer))
            {
                _observers = [.. _observers, observer];
            }
        }
    }

    public bool Detach(ICacheObserver<TKey> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_lock)
        {
            if (!_observers.Contains(observer))
            {
                return false;
            }

            _observers = [.. _observers.Where(o => o != observer)];
            return true;
        }
    }

    public void NotifyHit(TKey key)
    {
        foreach (var observer in _observers) observer.OnHit(key);
    }

    public void NotifyMiss(TKey key)
    {
        foreach (var observer in _observers) observer.OnMiss(key);
    }

    public void NotifyExpire(TKey key)
    {
        foreach (var observer in _observers) observer.OnExpire(key);
    }

    public void NotifyEvict(TKey key)
    {
        foreach (var observer in _observers) observer.OnEvict(key);
    }
}
