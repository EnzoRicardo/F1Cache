using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Services;

/// <summary>
/// Decorator that adds in-memory caching to an IDriverProvider.
/// Intercepts requests to check the cache first before delegating to the underlying provider.
/// </summary>
public class CachedDriverProvider(IDriverProvider innerProvider, ICache<string, Driver?> cache) : IDriverProvider
{
    private readonly IDriverProvider _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
    private readonly ICache<string, Driver?> _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public async Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);

        // 1. Cache HIT check
        if (_cache.TryGet(driverId, out var cachedDriver))
        {
            return cachedDriver;
        }

        // 2. Cache MISS: delegate to underlying provider
        var driver = await _innerProvider.GetDriverByIdAsync(driverId, cancellationToken);

        // 3. Always store the result to prevent cache penetration
        _cache.Set(driverId, driver);

        return driver;
    }

    public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        return _innerProvider.GetDriversPageAsync(limit, offset, cancellationToken);
    }
}
