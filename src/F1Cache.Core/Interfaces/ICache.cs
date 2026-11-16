namespace F1Cache.Core.Interfaces;

public interface ICache<TKey, TValue> : ICacheSubject<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Stores an item. When <paramref name="timeToLive"/> is null, the cache's default TTL is used.
    /// </summary>
    void Set(TKey key, TValue item, TimeSpan? timeToLive = null);

    bool TryGet(TKey key, out TValue result);

    bool Remove(TKey key);

    void Clear();
}
