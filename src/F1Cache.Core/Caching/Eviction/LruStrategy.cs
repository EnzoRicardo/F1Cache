using F1Cache.Core.Interfaces;

namespace F1Cache.Core.Caching.Eviction;

/// <summary>
/// Least Recently Used: evicts the key that has gone longest without being read or written.
/// A linked list keeps keys ordered by recency (most recent at the front); all operations are O(1).
/// </summary>
public sealed class LruStrategy<TKey> : IEvictionStrategy<TKey>
    where TKey : notnull
{
    private readonly LinkedList<TKey> _recency = new();
    private readonly Dictionary<TKey, LinkedListNode<TKey>> _nodes = [];

    public void OnAdded(TKey key) => _nodes[key] = _recency.AddFirst(key);

    public void OnAccessed(TKey key)
    {
        if (_nodes.TryGetValue(key, out var node))
        {
            _recency.Remove(node);
            _recency.AddFirst(node);
        }
    }

    public void OnRemoved(TKey key)
    {
        if (_nodes.Remove(key, out var node))
        {
            _recency.Remove(node);
        }
    }

    public TKey Evict()
    {
        var leastRecent = _recency.Last ?? throw new InvalidOperationException("No keys to evict.");
        OnRemoved(leastRecent.Value);
        return leastRecent.Value;
    }

    public void Clear()
    {
        _recency.Clear();
        _nodes.Clear();
    }
}
