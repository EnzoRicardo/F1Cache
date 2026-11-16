using F1Cache.Core.Eviction;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Managers;

namespace F1Cache.Tests;

public class EvictionStrategyTests
{
    private sealed class EvictionRecorder : ICacheObserver<string>
    {
        public List<string> Evicted { get; } = [];

        public void OnHit(string key) { }
        public void OnMiss(string key) { }
        public void OnExpire(string key) { }
        public void OnEvict(string key) => Evicted.Add(key);
    }

    // ---- Strategies in isolation ----

    [Fact]
    public void Fifo_EvictsOldestInserted_IgnoringAccess()
    {
        var fifo = new FifoStrategy<string>();
        fifo.OnAdded("a");
        fifo.OnAdded("b");
        fifo.OnAdded("c");
        fifo.OnAccessed("a");

        Assert.Equal("a", fifo.Evict());
        Assert.Equal("b", fifo.Evict());
    }

    [Fact]
    public void Lru_EvictsLeastRecentlyAccessed()
    {
        var lru = new LruStrategy<string>();
        lru.OnAdded("a");
        lru.OnAdded("b");
        lru.OnAdded("c");
        lru.OnAccessed("a");

        Assert.Equal("b", lru.Evict());
        Assert.Equal("c", lru.Evict());
        Assert.Equal("a", lru.Evict());
    }

    [Fact]
    public void Lfu_EvictsLeastFrequentlyAccessed()
    {
        var lfu = new LfuStrategy<string>();
        lfu.OnAdded("a");
        lfu.OnAdded("b");
        lfu.OnAdded("c");
        lfu.OnAccessed("a");
        lfu.OnAccessed("a");
        lfu.OnAccessed("c");

        Assert.Equal("b", lfu.Evict());
        Assert.Equal("c", lfu.Evict());
        Assert.Equal("a", lfu.Evict());
    }

    [Fact]
    public void Lfu_OnTie_EvictsLeastRecentlyUsed()
    {
        var lfu = new LfuStrategy<string>();
        lfu.OnAdded("a");
        lfu.OnAdded("b");
        lfu.OnAccessed("b");
        lfu.OnAccessed("a");

        Assert.Equal("b", lfu.Evict());
    }

    [Fact]
    public void Lfu_AfterRemovingMinimumBucket_FindsNextMinimum()
    {
        var lfu = new LfuStrategy<string>();
        lfu.OnAdded("a");
        lfu.OnAdded("b");
        lfu.OnAccessed("b");
        lfu.OnAccessed("b");
        lfu.OnRemoved("a");

        Assert.Equal("b", lfu.Evict());
    }

    public static TheoryData<IEvictionStrategy<string>> AllStrategies() => new()
    {
        new FifoStrategy<string>(),
        new LruStrategy<string>(),
        new LfuStrategy<string>(),
    };

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void RemovedKey_IsNeverEvicted(IEvictionStrategy<string> strategy)
    {
        strategy.OnAdded("a");
        strategy.OnAdded("b");
        strategy.OnRemoved("a");

        Assert.Equal("b", strategy.Evict());
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Evict_WhenEmpty_Throws(IEvictionStrategy<string> strategy)
    {
        strategy.OnAdded("a");
        strategy.Clear();

        Assert.Throws<InvalidOperationException>(() => strategy.Evict());
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void UnknownKey_AccessAndRemove_AreIgnored(IEvictionStrategy<string> strategy)
    {
        strategy.OnAccessed("ghost");
        strategy.OnRemoved("ghost");
        strategy.OnAdded("a");

        Assert.Equal("a", strategy.Evict());
    }

    // ---- Strategies plugged into the cache ----

    [Fact]
    public void Cache_DefaultsToLru()
    {
        var cache = new CacheManager<string, int>(capacity: 2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.TryGet("a", out _);

        cache.Set("c", 3);

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void Cache_WithFifo_EvictsOldestEvenIfRead()
    {
        var cache = new CacheManager<string, int>(capacity: 2, evictionStrategy: new FifoStrategy<string>());
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.TryGet("a", out _);

        cache.Set("c", 3);

        Assert.False(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("b", out _));
    }

    [Fact]
    public void Cache_WithLfu_EvictsLeastRead()
    {
        var cache = new CacheManager<string, int>(capacity: 2, evictionStrategy: new LfuStrategy<string>());
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.TryGet("a", out _);
        cache.TryGet("a", out _);
        cache.TryGet("b", out _);

        cache.Set("c", 3);

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
    }

    [Fact]
    public void Cache_Eviction_NotifiesObservers()
    {
        var cache = new CacheManager<string, int>(capacity: 1);
        var recorder = new EvictionRecorder();
        cache.Attach(recorder);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);

        Assert.Equal(["a", "b"], recorder.Evicted);
    }

    [Fact]
    public void Cache_OverwritingExistingKey_DoesNotEvict()
    {
        var cache = new CacheManager<string, int>(capacity: 2);
        var recorder = new EvictionRecorder();
        cache.Attach(recorder);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("a", 10);

        Assert.Empty(recorder.Evicted);
        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(10, value);
    }

    [Fact]
    public void Cache_RemoveAndExpire_FreeCapacityWithoutEviction()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, int>(capacity: 2, timeProvider: clock);
        var recorder = new EvictionRecorder();
        cache.Attach(recorder);

        cache.Set("a", 1);
        cache.Set("b", 2, TimeSpan.FromSeconds(1));
        cache.Remove("a");
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.TryGet("b", out _);

        cache.Set("c", 3);
        cache.Set("d", 4);

        Assert.Empty(recorder.Evicted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cache_NonPositiveCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheManager<string, int>(capacity: capacity));
    }
}
