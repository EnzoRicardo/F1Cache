using F1Cache.Core.Interfaces;
using F1Cache.Core.Managers;
using F1Cache.Core.Models;
using F1Cache.Core.Observers;
using F1Cache.Core.Services;

namespace F1Cache.Tests;

public class CacheObserverTests
{
    private sealed class RecordingObserver : ICacheObserver<string>
    {
        public List<string> Events { get; } = [];

        public void OnHit(string key) => Events.Add($"hit:{key}");
        public void OnMiss(string key) => Events.Add($"miss:{key}");
        public void OnExpire(string key) => Events.Add($"expire:{key}");
        public void OnEvict(string key) => Events.Add($"evict:{key}");
    }

    [Fact]
    public void TryGet_LiveEntry_NotifiesHit()
    {
        var cache = new CacheManager<string, string>();
        var observer = new RecordingObserver();
        cache.Attach(observer);
        cache.Set("key", "value");

        cache.TryGet("key", out _);

        Assert.Equal(["hit:key"], observer.Events);
    }

    [Fact]
    public void TryGet_AbsentKey_NotifiesMiss()
    {
        var cache = new CacheManager<string, string>();
        var observer = new RecordingObserver();
        cache.Attach(observer);

        cache.TryGet("missing", out _);

        Assert.Equal(["miss:missing"], observer.Events);
    }

    [Fact]
    public void TryGet_ExpiredEntry_NotifiesExpireThenMiss()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, string>(timeProvider: clock);
        var observer = new RecordingObserver();
        cache.Attach(observer);
        cache.Set("key", "value");

        clock.Advance(CacheManager<string, string>.DefaultTimeToLive);
        cache.TryGet("key", out _);
        cache.TryGet("key", out _);

        Assert.Equal(["expire:key", "miss:key", "miss:key"], observer.Events);
    }

    [Fact]
    public void SetRemoveAndClear_DoNotNotify()
    {
        var cache = new CacheManager<string, string>();
        var observer = new RecordingObserver();
        cache.Attach(observer);

        cache.Set("key", "value");
        cache.Remove("key");
        cache.Clear();

        Assert.Empty(observer.Events);
    }

    [Fact]
    public void Attach_MultipleObservers_AllNotified()
    {
        var cache = new CacheManager<string, string>();
        var first = new RecordingObserver();
        var second = new RecordingObserver();
        cache.Attach(first);
        cache.Attach(second);

        cache.TryGet("key", out _);

        Assert.Equal(["miss:key"], first.Events);
        Assert.Equal(["miss:key"], second.Events);
    }

    [Fact]
    public void Attach_SameObserverTwice_NotifiedOnce()
    {
        var cache = new CacheManager<string, string>();
        var observer = new RecordingObserver();
        cache.Attach(observer);
        cache.Attach(observer);

        cache.TryGet("key", out _);

        Assert.Single(observer.Events);
    }

    [Fact]
    public void Detach_StopsNotifications()
    {
        var cache = new CacheManager<string, string>();
        var observer = new RecordingObserver();
        cache.Attach(observer);

        Assert.True(cache.Detach(observer));
        Assert.False(cache.Detach(observer));
        cache.TryGet("key", out _);

        Assert.Empty(observer.Events);
    }

    [Fact]
    public void Attach_ThroughICacheAbstraction_ReceivesEvents()
    {
        ICache<string, string> cache = new CacheManager<string, string>();
        var observer = new RecordingObserver();

        cache.Attach(observer);
        cache.TryGet("key", out _);

        Assert.Equal(["miss:key"], observer.Events);
    }

    [Fact]
    public void Attach_Null_Throws()
    {
        var cache = new CacheManager<string, string>();

        Assert.Throws<ArgumentNullException>(() => cache.Attach(null!));
    }

    [Fact]
    public void StatisticsObserver_CountsEventsAndComputesHitRatio()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, string>(timeProvider: clock);
        var stats = new CacheStatisticsObserver<string>();
        cache.Attach(stats);

        Assert.Equal(0, stats.HitRatio);

        cache.Set("key", "value");
        cache.TryGet("key", out _);
        cache.TryGet("key", out _);
        cache.TryGet("key", out _);
        cache.TryGet("missing", out _);

        Assert.Equal(3, stats.Hits);
        Assert.Equal(1, stats.Misses);
        Assert.Equal(0.75, stats.HitRatio);

        clock.Advance(CacheManager<string, string>.DefaultTimeToLive);
        cache.TryGet("key", out _);

        Assert.Equal(1, stats.Expirations);
        Assert.Equal(2, stats.Misses);
    }

    [Fact]
    public async Task StatisticsObserver_ThroughCachedDriverProvider_TracksMissThenHit()
    {
        var cache = new CacheManager<string, Driver?>();
        var stats = new CacheStatisticsObserver<string>();
        cache.Attach(stats);
        var provider = new CachedDriverProvider(new StubDriverProvider(), cache);

        await provider.GetDriverByIdAsync("alonso");
        await provider.GetDriverByIdAsync("alonso");

        Assert.Equal(1, stats.Misses);
        Assert.Equal(1, stats.Hits);
    }

    private sealed class StubDriverProvider : IDriverProvider
    {
        public Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Driver?>(new Driver(driverId, "Fernando", "Alonso"));

        public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Driver>>([]);
    }
}
