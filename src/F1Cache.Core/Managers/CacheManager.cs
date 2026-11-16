using System.Collections.Concurrent;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;
using F1Cache.Core.Observers;

namespace F1Cache.Core.Managers;

public class CacheManager<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _cache = new();
    private readonly CacheSubject<TKey> _subject = new();
    private readonly TimeSpan _defaultTimeToLive;
    private readonly TimeProvider _timeProvider;

    public CacheManager(TimeSpan? defaultTimeToLive = null, TimeProvider? timeProvider = null)
    {
        _defaultTimeToLive = defaultTimeToLive ?? DefaultTimeToLive;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_defaultTimeToLive, TimeSpan.Zero, nameof(defaultTimeToLive));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Attach(ICacheObserver<TKey> observer) => _subject.Attach(observer);

    public bool Detach(ICacheObserver<TKey> observer) => _subject.Detach(observer);

    public void Set(TKey key, TValue item, TimeSpan? timeToLive = null) =>
        _cache[key] = new CacheEntry<TValue>(item, timeToLive ?? _defaultTimeToLive, _timeProvider);

    public bool TryGet(TKey key, out TValue result)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired())
            {
                _subject.NotifyHit(key);
                result = entry.Value;
                return true;
            }

            if (_cache.TryRemove(KeyValuePair.Create(key, entry)))
            {
                _subject.NotifyExpire(key);
            }
        }

        _subject.NotifyMiss(key);
        result = default!;
        return false;
    }

    public bool Remove(TKey key) => _cache.TryRemove(key, out _);

    public void Clear() => _cache.Clear();
}
