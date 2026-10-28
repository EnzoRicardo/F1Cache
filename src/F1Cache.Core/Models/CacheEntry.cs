namespace F1Cache.Core.Models;

internal class CacheEntry<TValue>
{
    public TValue Value { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public CacheEntry(TValue value, TimeSpan? ttl = null)
    {
        if (ttl.HasValue && ttl.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ttl),
                "TTL não pode ser negativo."
            );
        }

        Value = value;

        ExpiresAt = ttl.HasValue
            ? DateTimeOffset.UtcNow.Add(ttl.Value)
            : null;
    }

    public bool IsExpired()
    {
        return ExpiresAt.HasValue &&
               DateTimeOffset.UtcNow >= ExpiresAt.Value;
    }
}
