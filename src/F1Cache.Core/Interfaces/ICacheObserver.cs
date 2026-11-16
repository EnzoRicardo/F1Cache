namespace F1Cache.Core.Interfaces;

/// <summary>
/// Observer notified about cache lifecycle events (GoF Observer).
/// Callbacks run synchronously on the caller's thread, so implementations should be fast and must not throw.
/// </summary>
public interface ICacheObserver<in TKey>
    where TKey : notnull
{
    /// <summary>A lookup found a live entry.</summary>
    void OnHit(TKey key);

    /// <summary>A lookup found no live entry (absent or just expired).</summary>
    void OnMiss(TKey key);

    /// <summary>An entry was removed because its TTL elapsed.</summary>
    void OnExpire(TKey key);

    /// <summary>An entry was removed by the eviction strategy to free capacity.</summary>
    void OnEvict(TKey key);
}
