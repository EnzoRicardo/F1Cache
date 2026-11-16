using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core;

/// <summary>
/// Facade: the single public entry point of the SDK. Hides the Jolpica and Wikipedia adapters,
/// the enrichment pipeline and the cache behind a few simple methods.
/// Create one with <see cref="CreateBuilder"/>.
/// </summary>
public sealed class F1Client : IF1Client
{
    private readonly IDriverProvider _drivers;
    private readonly ICache<string, Driver?> _cache;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    internal F1Client(
        IDriverProvider drivers,
        ICache<string, Driver?> cache,
        ICacheStatistics statistics,
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

    /// <inheritdoc />
    public ICacheStatistics Statistics { get; }

    /// <inheritdoc />
    public Task<Driver?> GetDriverAsync(string driverId, CancellationToken cancellationToken = default) =>
        _drivers.GetDriverByIdAsync(driverId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = IDriverProvider.MaxPageSize, int offset = 0, CancellationToken cancellationToken = default) =>
        _drivers.GetDriversPageAsync(limit, offset, cancellationToken);

    /// <inheritdoc />
    public void ClearCache() => _cache.Clear();

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
