using F1Cache.Core.Caching;
using F1Cache.Core.Models;
using F1Cache.Core.Providers;

namespace F1Cache.Tests;

public class CachedDriverProviderTests
{
    [Fact]
    public async Task GetDriverByIdAsync_IdIsCaseAndWhitespaceInsensitive()
    {
        var fakeInner = new FakeDriverProvider(new Driver(Id: "alonso", GivenName: "Fernando", FamilyName: "Alonso"));
        var cachedProvider = new CachedDriverProvider(fakeInner, new CacheManager<string, Driver?>());

        await cachedProvider.GetDriverByIdAsync("Alonso");
        await cachedProvider.GetDriverByIdAsync(" ALONSO ");
        await cachedProvider.GetDriverByIdAsync("alonso");

        Assert.Equal(["alonso"], fakeInner.RequestedIds);
    }

    [Fact]
    public async Task GetDriverByIdAsync_WhenCacheMiss_CallsInnerProviderAndCachesResult()
    {
        // Arrange
        var fakeInner = new FakeDriverProvider
        {
            Result = new Driver(
                Id: "alonso",
                GivenName: "Fernando",
                FamilyName: "Alonso",
                Code: "ALO",
                Nationality: "Spanish"
            )
        };

        var cache = new CacheManager<string, Driver?>();
        var cachedProvider = new CachedDriverProvider(fakeInner, cache);

        // Act - First call (Cache MISS)
        var result1 = await cachedProvider.GetDriverByIdAsync("alonso");

        // Assert
        Assert.NotNull(result1);
        Assert.Equal("alonso", result1.Id);
        Assert.Equal(1, fakeInner.Calls);

        // Verify cache now has it
        Assert.True(cache.TryGet("alonso", out var inCache));
        Assert.NotNull(inCache);
        Assert.Equal("Fernando Alonso", inCache.FullName);

        // Act - Second call (Cache HIT)
        var result2 = await cachedProvider.GetDriverByIdAsync("alonso");

        // Assert - Calls should NOT have increased!
        Assert.NotNull(result2);
        Assert.Equal("alonso", result2.Id);
        Assert.Equal(1, fakeInner.Calls);
    }

    [Fact]
    public async Task GetDriverByIdAsync_WhenDriverNotFound_ImplementsNegativeCaching()
    {
        // Arrange
        var fakeInner = new FakeDriverProvider { Result = null };
        var cache = new CacheManager<string, Driver?>();
        var cachedProvider = new CachedDriverProvider(fakeInner, cache);

        // Act 1 - First call (Cache MISS)
        var result1 = await cachedProvider.GetDriverByIdAsync("nonexistent");

        // Assert 1
        Assert.Null(result1);
        Assert.Equal(1, fakeInner.Calls);
        Assert.True(cache.TryGet("nonexistent", out var inCache));
        Assert.Null(inCache);

        // Act 2 - Second call (Negative Cache HIT: should NOT query inner provider again)
        var result2 = await cachedProvider.GetDriverByIdAsync("nonexistent");

        // Assert 2
        Assert.Null(result2);
        Assert.Equal(1, fakeInner.Calls); // Still 1, didn't hit provider!
    }

    [Fact]
    public async Task GetDriverByIdAsync_NegativeEntry_ExpiresAfterTenSeconds()
    {
        var clock = new ManualTimeProvider();
        var fakeInner = new FakeDriverProvider { Result = null };
        var cache = new CacheManager<string, Driver?>(timeProvider: clock);
        var cachedProvider = new CachedDriverProvider(fakeInner, cache);

        await cachedProvider.GetDriverByIdAsync("nonexistent");
        clock.Advance(TimeSpan.FromSeconds(9));
        await cachedProvider.GetDriverByIdAsync("nonexistent");
        Assert.Equal(1, fakeInner.Calls);

        clock.Advance(TimeSpan.FromSeconds(1));
        await cachedProvider.GetDriverByIdAsync("nonexistent");
        Assert.Equal(2, fakeInner.Calls);
    }

    [Fact]
    public async Task GetDriverByIdAsync_FoundDriver_UsesDefaultTtlNotNegativeTtl()
    {
        var clock = new ManualTimeProvider();
        var fakeInner = new FakeDriverProvider
        {
            Result = new Driver(Id: "alonso", GivenName: "Fernando", FamilyName: "Alonso")
        };
        var cache = new CacheManager<string, Driver?>(timeProvider: clock);
        var cachedProvider = new CachedDriverProvider(fakeInner, cache);

        await cachedProvider.GetDriverByIdAsync("alonso");
        clock.Advance(TimeSpan.FromSeconds(30));
        await cachedProvider.GetDriverByIdAsync("alonso");
        Assert.Equal(1, fakeInner.Calls);

        clock.Advance(TimeSpan.FromSeconds(30));
        await cachedProvider.GetDriverByIdAsync("alonso");
        Assert.Equal(2, fakeInner.Calls);
    }
}
