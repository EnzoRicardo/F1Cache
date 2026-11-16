namespace F1Cache.Core.Interfaces;

/// <summary>
/// Read-only view of cache counters.
/// </summary>
public interface ICacheStatistics
{
    long Hits { get; }
    long Misses { get; }
    long Expirations { get; }
    long Evictions { get; }

    /// <summary>Hits divided by lookups (hits + misses); 0 when nothing has been looked up yet.</summary>
    double HitRatio { get; }
}
