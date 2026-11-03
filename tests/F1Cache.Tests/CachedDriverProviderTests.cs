using F1Cache.Core.Interfaces;
using F1Cache.Core.Managers;
using F1Cache.Core.Models;
using F1Cache.Core.Services;

namespace F1Cache.Tests;

public class CachedDriverProviderTests
{
    private class FakeDriverProvider : IDriverProvider
    {
        public int FetchCount { get; private set; }
        public Driver? ResultToReturn { get; set; }

        public Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default)
        {
            FetchCount++;
            return Task.FromResult(ResultToReturn);
        }

        public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
        {
            FetchCount++;
            return Task.FromResult<IReadOnlyList<Driver>>(ResultToReturn != null ? new[] { ResultToReturn } : Array.Empty<Driver>());
        }
    }

    [Fact]
    public async Task GetDriverByIdAsync_WhenCacheMiss_CallsInnerProviderAndCachesResult()
    {
        // Arrange
        var fakeInner = new FakeDriverProvider
        {
            ResultToReturn = new Driver(
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
        Assert.Equal(1, fakeInner.FetchCount);

        // Verify cache now has it
        Assert.True(cache.TryGet("alonso", out var inCache));
        Assert.NotNull(inCache);
        Assert.Equal("Fernando Alonso", inCache.FullName);

        // Act - Second call (Cache HIT)
        var result2 = await cachedProvider.GetDriverByIdAsync("alonso");

        // Assert - FetchCount should NOT have increased!
        Assert.NotNull(result2);
        Assert.Equal("alonso", result2.Id);
        Assert.Equal(1, fakeInner.FetchCount);
    }

    [Fact]
    public async Task GetDriverByIdAsync_WhenDriverNotFound_ImplementsNegativeCaching()
    {
        // Arrange
        var fakeInner = new FakeDriverProvider { ResultToReturn = null };
        var cache = new CacheManager<string, Driver?>();
        var cachedProvider = new CachedDriverProvider(fakeInner, cache);

        // Act 1 - First call (Cache MISS)
        var result1 = await cachedProvider.GetDriverByIdAsync("nonexistent");

        // Assert 1
        Assert.Null(result1);
        Assert.Equal(1, fakeInner.FetchCount);
        Assert.True(cache.TryGet("nonexistent", out var inCache));
        Assert.Null(inCache);

        // Act 2 - Second call (Negative Cache HIT: should NOT query inner provider again)
        var result2 = await cachedProvider.GetDriverByIdAsync("nonexistent");

        // Assert 2
        Assert.Null(result2);
        Assert.Equal(1, fakeInner.FetchCount); // Still 1, didn't hit provider!
    }
}
