using System.Net;
using System.Text;
using F1Cache.Core;
using F1Cache.Core.Eviction;
using F1Cache.Core.Interfaces;

namespace F1Cache.Tests;

public class F1ClientTests
{
    /// <summary>Answers Jolpica and Wikipedia requests with canned JSON and counts calls per host.</summary>
    private sealed class FakeApiHandler : HttpMessageHandler
    {
        public int JolpicaCalls { get; private set; }
        public int WikipediaCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            string json;

            if (uri.Host == "api.jolpi.ca")
            {
                JolpicaCalls++;
                var id = uri.Segments[^1].Replace(".json", "");
                json = $$"""
                {
                  "MRData": { "DriverTable": { "Drivers": [
                    { "driverId": "{{id}}", "givenName": "Given", "familyName": "{{id}}",
                      "url": "https://en.wikipedia.org/wiki/{{id}}" }
                  ] } }
                }
                """;
            }
            else
            {
                WikipediaCalls++;
                json = """
                {
                  "title": "Some Driver",
                  "extract": "A racing driver.",
                  "thumbnail": { "source": "https://upload.wikimedia.org/thumb.jpg" }
                }
                """;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CountingObserver : ICacheObserver<string>
    {
        public int Hits, Misses, Evictions;

        public void OnHit(string key) => Hits++;
        public void OnMiss(string key) => Misses++;
        public void OnExpire(string key) { }
        public void OnEvict(string key) => Evictions++;
    }

    private static (F1ClientBuilder Builder, FakeApiHandler Handler) NewBuilder()
    {
        var handler = new FakeApiHandler();
        var builder = F1Client.CreateBuilder().WithHttpClient(new HttpClient(handler));
        return (builder, handler);
    }

    [Fact]
    public async Task GetDriverAsync_ReturnsEnrichedDriver()
    {
        var (builder, _) = NewBuilder();
        using var client = builder.Build();

        var driver = await client.GetDriverAsync("alonso");

        Assert.NotNull(driver);
        Assert.Equal("alonso", driver.Id);
        Assert.Equal("A racing driver.", driver.Description);
        Assert.Equal("https://upload.wikimedia.org/thumb.jpg", driver.ThumbnailUrl);
    }

    [Fact]
    public async Task GetDriverAsync_SecondCall_IsServedFromCache()
    {
        var (builder, handler) = NewBuilder();
        using var client = builder.Build();

        await client.GetDriverAsync("alonso");
        await client.GetDriverAsync("alonso");

        Assert.Equal(1, handler.JolpicaCalls);
        Assert.Equal(1, handler.WikipediaCalls);
        Assert.Equal(1, client.Statistics.Misses);
        Assert.Equal(1, client.Statistics.Hits);
    }

    [Fact]
    public async Task ClearCache_ForcesRefetch()
    {
        var (builder, handler) = NewBuilder();
        using var client = builder.Build();

        await client.GetDriverAsync("alonso");
        client.ClearCache();
        await client.GetDriverAsync("alonso");

        Assert.Equal(2, handler.JolpicaCalls);
    }

    [Fact]
    public async Task GetDriversPageAsync_IsNotEnrichedOrCached()
    {
        var (builder, handler) = NewBuilder();
        using var client = builder.Build();

        await client.GetDriversPageAsync(limit: 10);
        await client.GetDriversPageAsync(limit: 10);

        Assert.Equal(2, handler.JolpicaCalls);
        Assert.Equal(0, handler.WikipediaCalls);
    }

    [Fact]
    public async Task WithObserver_ReceivesCacheEvents()
    {
        var observer = new CountingObserver();
        var (builder, _) = NewBuilder();
        using var client = builder.WithObserver(observer).Build();

        await client.GetDriverAsync("alonso");
        await client.GetDriverAsync("alonso");

        Assert.Equal(1, observer.Misses);
        Assert.Equal(1, observer.Hits);
    }

    [Fact]
    public async Task WithCacheCapacityAndFifo_EvictsOldestDriver()
    {
        var (builder, handler) = NewBuilder();
        using var client = builder
            .WithCacheCapacity(2)
            .WithEvictionPolicy(EvictionPolicy.Fifo)
            .Build();

        await client.GetDriverAsync("alonso");
        await client.GetDriverAsync("hamilton");
        await client.GetDriverAsync("alonso");      // hit; FIFO ignores it
        await client.GetDriverAsync("verstappen");  // evicts alonso
        await client.GetDriverAsync("alonso");      // miss again

        Assert.Equal(2, client.Statistics.Evictions);
        Assert.Equal(4, handler.JolpicaCalls);
    }

    [Fact]
    public async Task WithEvictionStrategy_CreatesNewStrategyPerBuild()
    {
        var created = 0;
        var (builder, _) = NewBuilder();
        builder.WithCacheCapacity(1).WithEvictionStrategy(() =>
        {
            created++;
            return new LruStrategy<string>();
        });

        using var first = builder.Build();
        using var second = builder.Build();
        await first.GetDriverAsync("alonso");
        await second.GetDriverAsync("hamilton");

        Assert.Equal(2, created);
        Assert.Equal(0, first.Statistics.Evictions);
        Assert.Equal(0, second.Statistics.Evictions);
    }

    [Fact]
    public void Build_WithDefaults_Succeeds()
    {
        using var client = F1Client.CreateBuilder().Build();

        Assert.NotNull(client.Statistics);
    }

    [Fact]
    public void Build_WithHttpClientAndTimeout_Throws()
    {
        var builder = F1Client.CreateBuilder()
            .WithHttpClient(new HttpClient())
            .WithHttpTimeout(TimeSpan.FromSeconds(5));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void BuilderOptions_RejectInvalidValues()
    {
        var builder = F1Client.CreateBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithCacheTimeToLive(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithCacheCapacity(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithHttpTimeout(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithEvictionPolicy((EvictionPolicy)99));
        Assert.Throws<ArgumentNullException>(() => builder.WithObserver(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithHttpClient(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithEvictionStrategy(null!));
    }

    [Fact]
    public async Task Dispose_DoesNotDisposeCallerOwnedHttpClient()
    {
        var handler = new FakeApiHandler();
        var httpClient = new HttpClient(handler);

        F1Client.CreateBuilder().WithHttpClient(httpClient).Build().Dispose();

        using var response = await httpClient.GetAsync("https://api.jolpi.ca/ergast/f1/drivers/alonso.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
