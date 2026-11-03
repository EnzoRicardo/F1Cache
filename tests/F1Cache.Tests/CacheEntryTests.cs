using F1Cache.Core.Models;

namespace F1Cache.Tests;

public class CacheEntryTests
{
    [Fact]
    public void Constructor_WhenTimeToLiveIsNull_ExpiresAtIsNullAndNotExpired()
    {
        var entry = new CacheEntry<string>("test-data", timeToLive: null);

        Assert.Equal("test-data", entry.Value);
        Assert.Null(entry.ExpiresAt);
        Assert.False(entry.IsExpired());
    }

    [Fact]
    public void Constructor_WhenTimeToLiveIsPositive_SetsExpiresAtInFuture()
    {
        var entry = new CacheEntry<string>("test-data", TimeSpan.FromMinutes(5));

        Assert.NotNull(entry.ExpiresAt);
        Assert.True(entry.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.False(entry.IsExpired());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WhenTimeToLiveIsZeroOrNegative_ThrowsArgumentOutOfRangeException(int seconds)
    {
        var invalidTtl = TimeSpan.FromSeconds(seconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheEntry<string>("test-data", invalidTtl));
    }
}
