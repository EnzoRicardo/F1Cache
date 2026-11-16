using F1Cache.Core.Interfaces;

namespace F1Cache.Core.Observers;

/// <summary>
/// Concrete observer that counts cache events and exposes the hit ratio. Thread-safe.
/// </summary>
public sealed class CacheStatisticsObserver<TKey> : ICacheObserver<TKey>
    where TKey : notnull
{
    private long _hits;
    private long _misses;
    private long _expirations;
    private long _evictions;

    public long Hits => Interlocked.Read(ref _hits);
    public long Misses => Interlocked.Read(ref _misses);
    public long Expirations => Interlocked.Read(ref _expirations);
    public long Evictions => Interlocked.Read(ref _evictions);

    public double HitRatio
    {
        get
        {
            var hits = Hits;
            var total = hits + Misses;
            return total == 0 ? 0 : (double)hits / total;
        }
    }

    public void OnHit(TKey key) => Interlocked.Increment(ref _hits);
    public void OnMiss(TKey key) => Interlocked.Increment(ref _misses);
    public void OnExpire(TKey key) => Interlocked.Increment(ref _expirations);
    public void OnEvict(TKey key) => Interlocked.Increment(ref _evictions);
}
