using F1Cache.Core.Clients.Jolpica;
using F1Cache.Core.Managers;
using F1Cache.Core.Models;
using F1Cache.Core.Services;

// Setup in-memory cache and Jolpica API provider
var cache = new CacheManager<string, Driver?>();
using var httpClient = new HttpClient();
var apiProvider = new JolpicaDriverProvider(httpClient);
var driverProvider = new CachedDriverProvider(apiProvider, cache);

// First fetch: retrieves from Jolpica API and stores in cache (Cache MISS)
var driver = await driverProvider.GetDriverByIdAsync("alonso");
Console.WriteLine($"Driver: {driver?.FullName} ({driver?.Nationality}) - #{driver?.PermanentNumber}");

// Second fetch: retrieved directly from memory cache (Cache HIT)
var cachedDriver = await driverProvider.GetDriverByIdAsync("alonso");
Console.WriteLine($"Cached: {cachedDriver?.FullName} - #{cachedDriver?.PermanentNumber}");
