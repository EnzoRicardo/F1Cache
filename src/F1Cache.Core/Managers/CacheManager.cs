using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Managers;

public class CacheManager<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, CacheEntry<TValue>> _cache = [];

    public void Set(TKey key, TValue item) =>
        _cache[key] = new CacheEntry<TValue>(item);

    public bool TryGet(TKey key, out TValue result)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired())
            {
                result = entry.Value;
                return true;
            }

            _cache.Remove(key);
        }

        result = default!;
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
