namespace F1Cache.Core.Models;

/// <summary>
/// Internal container that holds a cached item along with its expiration metadata.
/// </summary>
internal class CacheEntry<TValue>
{
    public TValue Value { get; }
    public DateTimeOffset? ExpiresAt { get; }

    public CacheEntry(TValue data, TimeSpan? timeToLive = null)
    {
        if (timeToLive is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero, nameof(timeToLive));
            ExpiresAt = DateTimeOffset.UtcNow.Add(duration);
        }

        Value = data;
    }

    public bool IsExpired() =>
        ExpiresAt.HasValue && DateTimeOffset.UtcNow >= ExpiresAt.Value;
}
