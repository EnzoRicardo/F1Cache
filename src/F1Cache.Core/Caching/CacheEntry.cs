namespace F1Cache.Core.Caching;

/// <summary>
/// Internal container that holds a cached item along with its expiration time.
/// Plain data: the cache owns the clock and passes the current time in.
/// </summary>
internal sealed class CacheEntry<TValue>
{
    public TValue Value { get; }
    public DateTimeOffset? ExpiresAt { get; }

    public CacheEntry(TValue value, DateTimeOffset createdAt, TimeSpan? timeToLive = null)
    {
        if (timeToLive is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero, nameof(timeToLive));
            ExpiresAt = createdAt + duration;
        }

        Value = value;
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiresAt && now >= expiresAt;
}
