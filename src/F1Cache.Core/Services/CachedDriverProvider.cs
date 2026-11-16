using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Services;

/// <summary>
/// Decorator that adds in-memory caching to an IDriverProvider.
/// Intercepts requests to check the cache first before delegating to the underlying provider.
/// </summary>
public class CachedDriverProvider(IDriverProvider innerProvider, ICache<string, Driver?> cache) : IDriverProvider
{
    internal static readonly TimeSpan NegativeCacheTimeToLive = TimeSpan.FromSeconds(10);

    private readonly IDriverProvider _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
    private readonly ICache<string, Driver?> _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public async Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);

        if (_cache.TryGet(driverId, out var cachedDriver))
        {
            return cachedDriver;
        }

        var driver = await _innerProvider.GetDriverByIdAsync(driverId, cancellationToken);

        // Always store the result to prevent cache penetration.
        // Misses get a short TTL so a driver added upstream shows up quickly.
        _cache.Set(driverId, driver, driver is null ? NegativeCacheTimeToLive : null);

        return driver;
    }

    public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        return _innerProvider.GetDriversPageAsync(limit, offset, cancellationToken);
    }
}
