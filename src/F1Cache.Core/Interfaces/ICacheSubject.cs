namespace F1Cache.Core.Interfaces;

/// <summary>
/// Subject side of the Observer pattern: lets observers subscribe to cache events.
/// </summary>
public interface ICacheSubject<TKey>
    where TKey : notnull
{
    void Attach(ICacheObserver<TKey> observer);

    bool Detach(ICacheObserver<TKey> observer);
}
