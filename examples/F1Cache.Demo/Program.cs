using F1Cache.Core.Managers;

var cache = new CacheManager<string, string>();

cache.Set("driver-1", "Max Verstappen");
cache.Set("driver-2", "Sergio Perez");
cache.Set("driver-3", "Charles Leclerc");

if (cache.TryGet("driver-1", out var driver))
{
    Console.WriteLine($"Cache HIT: {driver}");
}
else
{
    Console.WriteLine("Cache MISS");
}

cache.Remove("driver-2");

if (!cache.TryGet("driver-2", out _))
{
    Console.WriteLine("Cache MISS: piloto removido");
}