using F1Cache.Core.Managers;

namespace F1Cache.Tests;

public class CacheManagerTests
{
    [Fact]
    public void TryGet_BeforeDefaultTtlElapses_ReturnsItem()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, string>(timeProvider: clock);

        cache.Set("key", "value");
        clock.Advance(CacheManager<string, string>.DefaultTimeToLive - TimeSpan.FromSeconds(1));

        Assert.True(cache.TryGet("key", out var result));
        Assert.Equal("value", result);
    }

    [Fact]
    public void TryGet_AfterDefaultTtlElapses_ExpiresItem()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, string>(timeProvider: clock);

        cache.Set("key", "value");
        clock.Advance(CacheManager<string, string>.DefaultTimeToLive);

        Assert.False(cache.TryGet("key", out _));
    }

    [Fact]
    public void DefaultTimeToLive_IsOneMinute()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), CacheManager<string, string>.DefaultTimeToLive);
    }

    [Fact]
    public void Set_WithExplicitTtl_OverridesDefault()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, string>(timeProvider: clock);

        cache.Set("short", "value", TimeSpan.FromSeconds(5));
        cache.Set("long", "value", TimeSpan.FromMinutes(10));
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(cache.TryGet("short", out _));
        Assert.True(cache.TryGet("long", out _));
    }

    [Fact]
    public void Constructor_WithCustomDefaultTtl_UsesIt()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, string>(TimeSpan.FromSeconds(30), timeProvider: clock);

        cache.Set("key", "value");
        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.False(cache.TryGet("key", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveDefaultTtl_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CacheManager<string, string>(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Set_OverwritingKey_ResetsTtl()
    {
        var clock = new ManualTimeProvider();
        var cache = new CacheManager<string, string>(timeProvider: clock);

        cache.Set("key", "old");
        clock.Advance(TimeSpan.FromSeconds(50));
        cache.Set("key", "new");
        clock.Advance(TimeSpan.FromSeconds(50));

        Assert.True(cache.TryGet("key", out var result));
        Assert.Equal("new", result);
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrueAndRemoves()
    {
        var cache = new CacheManager<string, string>();
        cache.Set("key", "value");

        Assert.True(cache.Remove("key"));
        Assert.False(cache.Remove("key"));
        Assert.False(cache.TryGet("key", out _));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var cache = new CacheManager<string, string>();
        cache.Set("a", "1");
        cache.Set("b", "2");

        cache.Clear();

        Assert.False(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
    }

    [Fact]
    public async Task ConcurrentSetAndGet_DoesNotThrowOrLoseItems()
    {
        const int count = 10_000;
        var cache = new CacheManager<int, int>(capacity: count);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var i = worker; i < count; i += 8)
            {
                cache.Set(i, i * 2);
                cache.TryGet(i, out _);
            }
        })));

        for (var i = 0; i < count; i++)
        {
            Assert.True(cache.TryGet(i, out var value));
            Assert.Equal(i * 2, value);
        }
    }
}
