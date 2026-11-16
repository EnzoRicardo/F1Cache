namespace F1Cache.Core.Models;

/// <summary>
/// Internal container that holds a cached item along with its expiration metadata.
/// </summary>
internal class CacheEntry<TValue>
{
    private readonly TimeProvider _timeProvider;

    public TValue Value { get; }
    public DateTimeOffset? ExpiresAt { get; }

    public CacheEntry(TValue data, TimeSpan? timeToLive = null, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (timeToLive is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero, nameof(timeToLive));
            ExpiresAt = _timeProvider.GetUtcNow().Add(duration);
        }

        Value = data;
    }

    public bool IsExpired() =>
        ExpiresAt.HasValue && _timeProvider.GetUtcNow() >= ExpiresAt.Value;
}
