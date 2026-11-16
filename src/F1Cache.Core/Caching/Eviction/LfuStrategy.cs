using F1Cache.Core.Interfaces;

namespace F1Cache.Core.Caching.Eviction;

/// <summary>
/// Least Frequently Used: evicts the key with the fewest accesses; ties go to the least recently used of them.
/// Keys are grouped into buckets by access count, so all operations are O(1).
/// </summary>
public sealed class LfuStrategy<TKey> : IEvictionStrategy<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, int> _frequencies = [];
    private readonly Dictionary<TKey, LinkedListNode<TKey>> _nodes = [];
    private readonly Dictionary<int, LinkedList<TKey>> _buckets = [];
    private int _minFrequency;

    public void OnAdded(TKey key)
    {
        _frequencies[key] = 1;
        _nodes[key] = GetBucket(1).AddLast(key);
        _minFrequency = 1;
    }

    public void OnAccessed(TKey key)
    {
        if (!_frequencies.TryGetValue(key, out var frequency))
        {
            return;
        }

        DetachFromBucket(key, frequency);
        if (_minFrequency == frequency && !_buckets.ContainsKey(frequency))
        {
            _minFrequency = frequency + 1;
        }

        _frequencies[key] = frequency + 1;
        _nodes[key] = GetBucket(frequency + 1).AddLast(key);
    }

    public void OnRemoved(TKey key)
    {
        if (_frequencies.Remove(key, out var frequency))
        {
            DetachFromBucket(key, frequency);
            _nodes.Remove(key);
        }
    }

    public TKey Evict()
    {
        if (_frequencies.Count == 0)
        {
            throw new InvalidOperationException("No keys to evict.");
        }

        // An explicit removal may have emptied the minimum bucket; fall back to a scan of bucket keys.
        if (!_buckets.ContainsKey(_minFrequency))
        {
            _minFrequency = _buckets.Keys.Min();
        }

        var victim = _buckets[_minFrequency].First!.Value;
        OnRemoved(victim);
        return victim;
    }

    public void Clear()
    {
        _frequencies.Clear();
        _nodes.Clear();
        _buckets.Clear();
        _minFrequency = 0;
    }

    private LinkedList<TKey> GetBucket(int frequency)
    {
        if (!_buckets.TryGetValue(frequency, out var bucket))
        {
            bucket = new LinkedList<TKey>();
            _buckets[frequency] = bucket;
        }

        return bucket;
    }

    private void DetachFromBucket(TKey key, int frequency)
    {
        var bucket = _buckets[frequency];
        bucket.Remove(_nodes[key]);
        if (bucket.Count == 0)
        {
            _buckets.Remove(frequency);
        }
    }
}
