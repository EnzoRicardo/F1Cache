namespace F1Cache.Core.Caching.Eviction;

/// <summary>
/// Built-in eviction strategies selectable from <see cref="F1ClientBuilder"/>.
/// </summary>
public enum EvictionPolicy
{
    /// <summary>Evicts the least recently used driver.</summary>
    Lru,

    /// <summary>Evicts the least frequently used driver.</summary>
    Lfu,

    /// <summary>Evicts the oldest inserted driver.</summary>
    Fifo,
}
