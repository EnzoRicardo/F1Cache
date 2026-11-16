using F1Cache.Core.Caching;

namespace F1Cache.Tests;

public class CacheEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenTimeToLiveIsNull_NeverExpires()
    {
        var entry = new CacheEntry<string>("test-data", Now, timeToLive: null);

        Assert.Equal("test-data", entry.Value);
        Assert.Null(entry.ExpiresAt);
        Assert.False(entry.IsExpired(Now.AddYears(100)));
    }

    [Fact]
    public void Constructor_WhenTimeToLiveIsPositive_ExpiresAtCreationPlusTtl()
    {
        var entry = new CacheEntry<string>("test-data", Now, TimeSpan.FromMinutes(5));

        Assert.Equal(Now.AddMinutes(5), entry.ExpiresAt);
    }

    [Fact]
    public void IsExpired_TrueFromTheExpiryInstantOnwards()
    {
        var entry = new CacheEntry<string>("test-data", Now, TimeSpan.FromMinutes(5));

        Assert.False(entry.IsExpired(Now.AddMinutes(5).AddTicks(-1)));
        Assert.True(entry.IsExpired(Now.AddMinutes(5)));
        Assert.True(entry.IsExpired(Now.AddHours(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WhenTimeToLiveIsZeroOrNegative_ThrowsArgumentOutOfRangeException(int seconds)
    {
        var invalidTtl = TimeSpan.FromSeconds(seconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheEntry<string>("test-data", Now, invalidTtl));
    }
}
