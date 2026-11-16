using System.Diagnostics;
using F1Cache.Core;
using F1Cache.Core.Caching.Eviction;
using F1Cache.Core.Http;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

// F1Cache demo: look up Formula 1 drivers from the terminal.
//
//   dotnet run --project examples/F1Cache.Demo                         interactive mode
//   dotnet run --project examples/F1Cache.Demo -- alonso hamilton      look up drivers and exit
//   dotnet run --project examples/F1Cache.Demo -- --policy lfu --capacity 3 --verbose

if (DemoOptions.TryParse(args, out var options, out var error) is false)
{
    Console.Error.WriteLine(error);
    Console.WriteLine(DemoOptions.Usage);
    return 1;
}

if (options.ShowHelp)
{
    Console.WriteLine(DemoOptions.Usage);
    return 0;
}

var builder = F1Client.CreateBuilder()
    .WithCacheCapacity(options.Capacity)
    .WithEvictionPolicy(options.Policy)
    .WithObserver(new ConsoleCacheObserver());

if (options.Verbose)
{
    builder.WithHttpHandler(() => new LoggingHandler(line => Output.Write(line, ConsoleColor.DarkGray)));
}

using IF1Client f1 = builder.Build();

if (options.DriverIds.Count > 0)
{
    foreach (var id in options.DriverIds)
    {
        await ShowDriverAsync(id);
    }

    ShowStatistics();
    return 0;
}

const string Commands =
    "Type a driver id (alonso, hamilton, max_verstappen, ...) or: list [page], stats, clear, help, quit";

Console.WriteLine($"F1Cache demo (capacity {options.Capacity}, {options.Policy})");
Console.WriteLine(Commands);

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (line is null) break;

    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) continue;

    switch (parts[0].ToLowerInvariant())
    {
        case "quit" or "exit":
            return 0;
        case "help":
            Console.WriteLine(Commands);
            break;
        case "stats":
            ShowStatistics();
            break;
        case "clear":
            f1.ClearCache();
            Console.WriteLine("Cache cleared.");
            break;
        case "list":
            await ListDriversAsync(parts.Length > 1 && int.TryParse(parts[1], out var page) ? page : 1);
            break;
        default:
            await ShowDriverAsync(parts[0]);
            break;
    }
}

return 0;

async Task ShowDriverAsync(string driverId)
{
    var stopwatch = Stopwatch.StartNew();
    Driver? driver;
    try
    {
        driver = await f1.GetDriverAsync(driverId);
    }
    catch (HttpRequestException ex)
    {
        Output.Write($"Could not reach Jolpica: {ex.Message}", ConsoleColor.Red);
        return;
    }

    if (driver is null)
    {
        Output.Write($"No driver with id '{driverId}'. Try 'list' to see valid ids.", ConsoleColor.Yellow);
        return;
    }

    var number = driver.PermanentNumber is null ? "" : $" #{driver.PermanentNumber}";
    var code = driver.Code is null ? "" : $" ({driver.Code})";
    Output.Write($"{driver.FullName}{number}{code}", ConsoleColor.Cyan);
    Console.WriteLine($"  Nationality: {driver.Nationality ?? "?"}    Born: {driver.DateOfBirth?.ToString("yyyy-MM-dd") ?? "?"}");
    foreach (var descriptionLine in Output.Wrap(driver.Description ?? "", width: 90))
    {
        Console.WriteLine($"  {descriptionLine}");
    }

    Console.WriteLine($"  Image: {driver.ThumbnailUrl}");
    Output.Write($"  ({stopwatch.ElapsedMilliseconds} ms)", ConsoleColor.DarkGray);
}

async Task ListDriversAsync(int page)
{
    const int pageSize = 20;
    page = Math.Max(page, 1);

    IReadOnlyList<Driver> drivers;
    try
    {
        drivers = await f1.GetDriversPageAsync(pageSize, (page - 1) * pageSize);
    }
    catch (HttpRequestException ex)
    {
        Output.Write($"Could not reach Jolpica: {ex.Message}", ConsoleColor.Red);
        return;
    }

    if (drivers.Count == 0)
    {
        Console.WriteLine("No more drivers.");
        return;
    }

    foreach (var driver in drivers)
    {
        Console.WriteLine($"  {driver.Id,-22} {driver.FullName,-30} {driver.Nationality}");
    }

    Output.Write($"  Page {page}. Type 'list {page + 1}' for more.", ConsoleColor.DarkGray);
}

void ShowStatistics()
{
    var stats = f1.Statistics;
    Console.WriteLine(
        $"Cache: {stats.Hits} hits, {stats.Misses} misses ({stats.HitRatio:P0} hit ratio), " +
        $"{stats.Evictions} evictions, {stats.Expirations} expirations");
}

/// <summary>Prints every cache event, so you can watch hits, misses and evictions happen.</summary>
sealed class ConsoleCacheObserver : ICacheObserver<string>
{
    public void OnHit(string key) => Output.Write($"[cache] HIT {key}", ConsoleColor.Green);
    public void OnMiss(string key) => Output.Write($"[cache] MISS {key}", ConsoleColor.Yellow);
    public void OnExpire(string key) => Output.Write($"[cache] EXPIRED {key}", ConsoleColor.DarkYellow);
    public void OnEvict(string key) => Output.Write($"[cache] EVICTED {key}", ConsoleColor.Magenta);
}

static class Output
{
    private static readonly Lock ConsoleLock = new();

    public static void Write(string text, ConsoleColor color)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }

    public static IEnumerable<string> Wrap(string text, int width)
    {
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > width)
            {
                yield return line;
                line = "";
            }

            line = line.Length == 0 ? word : $"{line} {word}";
        }

        if (line.Length > 0) yield return line;
    }
}

sealed class DemoOptions
{
    public const string Usage = """
        Usage: F1Cache.Demo [options] [driver ids...]

          --policy <name>     Eviction policy: lru, lfu or fifo (default: lru)
          --capacity <n>      Max cached drivers (default: 50); try 3 to watch evictions
          --verbose, -v       Log every HTTP request
          --help, -h          Show this help

        With driver ids, looks them up and exits; otherwise starts interactive mode.
        """;

    public EvictionPolicy Policy { get; private set; } = EvictionPolicy.Lru;
    public int Capacity { get; private set; } = 50;
    public bool Verbose { get; private set; }
    public bool ShowHelp { get; private set; }
    public List<string> DriverIds { get; } = [];

    public static bool TryParse(string[] args, out DemoOptions options, out string error)
    {
        options = new DemoOptions();
        error = "";

        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;

            switch (args[i])
            {
                case "--policy":
                    if (!Enum.TryParse<EvictionPolicy>(value, ignoreCase: true, out var policy) || !Enum.IsDefined(policy))
                        return Fail("--policy must be lru, lfu or fifo.", out error);
                    options.Policy = policy;
                    i++;
                    break;
                case "--capacity":
                    if (!int.TryParse(value, out var capacity) || capacity <= 0)
                        return Fail("--capacity must be a positive number.", out error);
                    options.Capacity = capacity;
                    i++;
                    break;
                case "--verbose" or "-v":
                    options.Verbose = true;
                    break;
                case "--help" or "-h":
                    options.ShowHelp = true;
                    break;
                default:
                    if (args[i].StartsWith('-')) return Fail($"Unknown option '{args[i]}'.", out error);
                    options.DriverIds.Add(args[i]);
                    break;
            }
        }

        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
