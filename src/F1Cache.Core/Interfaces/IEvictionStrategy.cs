namespace F1Cache.Core.Interfaces;

/// <summary>
/// Decides which key to remove when the cache is full (GoF Strategy).
/// The cache tells the strategy about every key it adds, reads or removes, and asks it for a victim when it needs space.
/// Implementations are not thread-safe: the cache calls them under its own lock. Use one instance per cache.
/// </summary>
public interface IEvictionStrategy<TKey>
    where TKey : notnull
{
    /// <summary>A new key was stored.</summary>
    void OnAdded(TKey key);

    /// <summary>An existing key was read (hit) or overwritten.</summary>
    void OnAccessed(TKey key);

    /// <summary>A key left the cache for any reason other than eviction (removed, expired).</summary>
    void OnRemoved(TKey key);

    /// <summary>Picks the key to evict and stops tracking it.</summary>
    TKey Evict();

    void Clear();
}
