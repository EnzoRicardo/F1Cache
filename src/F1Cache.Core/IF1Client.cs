using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core;

/// <summary>
/// Public contract of the SDK. Depend on this instead of <see cref="F1Client"/> so your own code can swap in a fake in tests.
/// </summary>
public interface IF1Client : IDisposable
{
    /// <summary>Cache hit, miss, expiration and eviction counters for this client.</summary>
    ICacheStatistics Statistics { get; }

    /// <summary>
    /// Gets a driver by Jolpica id (e.g., "alonso"; case-insensitive), enriched with Wikipedia description and thumbnail.
    /// Cached: repeated calls within the TTL make no network requests. Returns null if the driver does not exist.
    /// </summary>
    Task<Driver?> GetDriverAsync(string driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of drivers (at most <see cref="IDriverProvider.MaxPageSize"/>). Not cached and not enriched with Wikipedia data.
    /// </summary>
    Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = IDriverProvider.MaxPageSize, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>Removes every cached driver.</summary>
    void ClearCache();
}
