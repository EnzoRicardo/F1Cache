using F1Cache.Core.Eviction;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;
using F1Cache.Core.Observers;

namespace F1Cache.Core.Managers;

public class CacheManager<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(1);
    public const int DefaultCapacity = 50;

    private readonly Dictionary<TKey, CacheEntry<TValue>> _cache = [];
    private readonly Lock _sync = new();
    private readonly CacheSubject<TKey> _subject = new();
    private readonly IEvictionStrategy<TKey> _evictionStrategy;
    private readonly TimeSpan _defaultTimeToLive;
    private readonly int _capacity;
    private readonly TimeProvider _timeProvider;

    public CacheManager(
        TimeSpan? defaultTimeToLive = null,
        int capacity = DefaultCapacity,
        IEvictionStrategy<TKey>? evictionStrategy = null,
        TimeProvider? timeProvider = null)
    {
        _defaultTimeToLive = defaultTimeToLive ?? DefaultTimeToLive;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_defaultTimeToLive, TimeSpan.Zero, nameof(defaultTimeToLive));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);

        _capacity = capacity;
        _evictionStrategy = evictionStrategy ?? new LruStrategy<TKey>();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Attach(ICacheObserver<TKey> observer) => _subject.Attach(observer);

    public bool Detach(ICacheObserver<TKey> observer) => _subject.Detach(observer);

    public void Set(TKey key, TValue item, TimeSpan? timeToLive = null)
    {
        var entry = new CacheEntry<TValue>(item, timeToLive ?? _defaultTimeToLive, _timeProvider);
        var evicted = false;
        TKey evictedKey = default!;

        lock (_sync)
        {
            if (_cache.ContainsKey(key))
            {
                _cache[key] = entry;
                _evictionStrategy.OnAccessed(key);
            }
            else
            {
                if (_cache.Count >= _capacity)
                {
                    evictedKey = _evictionStrategy.Evict();
                    _cache.Remove(evictedKey);
                    evicted = true;
                }

                _cache.Add(key, entry);
                _evictionStrategy.OnAdded(key);
            }
        }

        if (evicted)
        {
            _subject.NotifyEvict(evictedKey);
        }
    }

    public bool TryGet(TKey key, out TValue result)
    {
        CacheEntry<TValue>? hit = null;
        var expired = false;

        lock (_sync)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired())
                {
                    hit = entry;
                    _evictionStrategy.OnAccessed(key);
                }
                else
                {
                    _cache.Remove(key);
                    _evictionStrategy.OnRemoved(key);
                    expired = true;
                }
            }
        }

        if (hit is not null)
        {
            _subject.NotifyHit(key);
            result = hit.Value;
            return true;
        }

        if (expired)
        {
            _subject.NotifyExpire(key);
        }

        _subject.NotifyMiss(key);
        result = default!;
        return false;
    }

    public bool Remove(TKey key)
    {
        lock (_sync)
        {
            if (!_cache.Remove(key))
            {
                return false;
            }

            _evictionStrategy.OnRemoved(key);
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _cache.Clear();
            _evictionStrategy.Clear();
        }
    }
}
