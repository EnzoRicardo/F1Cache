using F1Cache.Core.Interfaces;

namespace F1Cache.Core.Caching.Eviction;

/// <summary>
/// First In, First Out: evicts the oldest inserted key, ignoring how it is used.
/// </summary>
public sealed class FifoStrategy<TKey> : IEvictionStrategy<TKey>
    where TKey : notnull
{
    private readonly LinkedList<TKey> _order = new();
    private readonly Dictionary<TKey, LinkedListNode<TKey>> _nodes = [];

    public void OnAdded(TKey key) => _nodes[key] = _order.AddLast(key);

    public void OnAccessed(TKey key)
    {
        // Usage does not affect insertion order.
    }

    public void OnRemoved(TKey key)
    {
        if (_nodes.Remove(key, out var node))
        {
            _order.Remove(node);
        }
    }

    public TKey Evict()
    {
        var oldest = _order.First ?? throw new InvalidOperationException("No keys to evict.");
        OnRemoved(oldest.Value);
        return oldest.Value;
    }

    public void Clear()
    {
        _order.Clear();
        _nodes.Clear();
    }
}
