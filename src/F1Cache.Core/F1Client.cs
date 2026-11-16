using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;
using F1Cache.Core.Observers;

namespace F1Cache.Core;

/// <summary>
/// Facade: the single public entry point of the SDK. Hides the Jolpica and Wikipedia adapters,
/// the enrichment pipeline and the cache behind a few simple methods.
/// Create one with <see cref="CreateBuilder"/>.
/// </summary>
public sealed class F1Client : IDisposable
{
    private readonly IDriverProvider _drivers;
    private readonly ICache<string, Driver?> _cache;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    internal F1Client(
        IDriverProvider drivers,
        ICache<string, Driver?> cache,
        CacheStatisticsObserver<string> statistics,
        HttpClient httpClient,
        bool ownsHttpClient)
    {
        _drivers = drivers;
        _cache = cache;
        Statistics = statistics;
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    /// <summary>Starts a fluent configuration of a new client.</summary>
    public static F1ClientBuilder CreateBuilder() => new();

    /// <summary>Cache hit, miss, expiration and eviction counters for this client.</summary>
    public CacheStatisticsObserver<string> Statistics { get; }

    /// <summary>
    /// Gets a driver by Jolpica id (e.g., "alonso"), enriched with Wikipedia description and thumbnail.
    /// Cached: repeated calls within the TTL make no network requests. Returns null if the driver does not exist.
    /// </summary>
    public Task<Driver?> GetDriverAsync(string driverId, CancellationToken cancellationToken = default) =>
        _drivers.GetDriverByIdAsync(driverId, cancellationToken);

    /// <summary>
    /// Gets a page of drivers (max 100 per page). Not cached and not enriched with Wikipedia data.
    /// </summary>
    public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default) =>
        _drivers.GetDriversPageAsync(limit, offset, cancellationToken);

    /// <summary>Removes every cached driver.</summary>
    public void ClearCache() => _cache.Clear();

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
