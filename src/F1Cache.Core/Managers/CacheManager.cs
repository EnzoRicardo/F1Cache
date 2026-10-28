using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Managers;

public class CacheManager<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, CacheEntry<TValue>> _cache = new();

    public void Set(TKey key, TValue value)
    {
        _cache[key] = new CacheEntry<TValue>(value);
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired())
            {
                value = entry.Value;
                return true;
            }

            _cache.Remove(key);
        }

        value = default!;
        return false;
    }

    public bool Remove(TKey key)
    {
        return _cache.Remove(key);
    }

    public void Clear()
    {
        _cache.Clear();
    }
}
