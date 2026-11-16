using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Providers;

/// <summary>
/// Decorator that adds in-memory caching to an IDriverProvider.
/// Intercepts requests to check the cache first before delegating to the underlying provider.
/// </summary>
public sealed class CachedDriverProvider(IDriverProvider innerProvider, ICache<string, Driver?> cache) : IDriverProvider
{
    internal static readonly TimeSpan NegativeCacheTimeToLive = TimeSpan.FromSeconds(10);

    private readonly IDriverProvider _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
    private readonly ICache<string, Driver?> _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public async Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);

        // Jolpica ids are lowercase, so "Alonso" and " alonso " should share one cache entry.
        var key = driverId.Trim().ToLowerInvariant();

        if (_cache.TryGet(key, out var cachedDriver))
        {
            return cachedDriver;
        }

        var driver = await _innerProvider.GetDriverByIdAsync(key, cancellationToken);

        // Always store the result to prevent cache penetration.
        // Misses get a short TTL so a driver added upstream shows up quickly.
        _cache.Set(key, driver, driver is null ? NegativeCacheTimeToLive : null);

        return driver;
    }

    public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = IDriverProvider.MaxPageSize, int offset = 0, CancellationToken cancellationToken = default)
    {
        return _innerProvider.GetDriversPageAsync(limit, offset, cancellationToken);
    }
}
