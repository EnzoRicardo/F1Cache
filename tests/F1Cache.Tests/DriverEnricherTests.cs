using F1Cache.Core.Interfaces;
using F1Cache.Core.Caching;
using F1Cache.Core.Models;
using F1Cache.Core.Providers;
using F1Cache.Core.Providers.Enrichment;

namespace F1Cache.Tests;

public class DriverEnricherTests
{
    private static readonly Driver Alonso = new(
        Id: "alonso",
        GivenName: "Fernando",
        FamilyName: "Alonso",
        WikipediaUrl: "http://en.wikipedia.org/wiki/Fernando_Alonso");

    private sealed class FakeWikipediaProvider : IWikipediaProvider
    {
        public WikipediaSummary? Summary { get; init; }
        public Exception? Throw { get; init; }
        public List<string> RequestedSlugs { get; } = [];

        public Task<WikipediaSummary?> GetSummaryAsync(string slug, CancellationToken cancellationToken = default)
        {
            RequestedSlugs.Add(slug);
            cancellationToken.ThrowIfCancellationRequested();
            if (Throw is not null) throw Throw;
            return Task.FromResult(Summary);
        }
    }

    private static readonly WikipediaSummary AlonsoSummary = new(
        Title: "Fernando Alonso",
        Description: "Spanish racing driver (born 1981)",
        Extract: "Fernando Alonso Díaz is a Spanish racing driver.",
        ThumbnailUrl: "https://upload.wikimedia.org/alonso-330px.jpg",
        ImageUrl: "https://upload.wikimedia.org/alonso.jpg");

    // ---- Template Method skeleton ----

    private sealed class RecordingEnricher(IDriverProvider inner, bool valid) : DriverDataEnricher(inner)
    {
        public List<string> Steps { get; } = [];

        protected override Task<Driver?> FetchBaseAsync(string driverId, CancellationToken cancellationToken)
        {
            Steps.Add("fetch");
            return base.FetchBaseAsync(driverId, cancellationToken);
        }

        protected override bool Validate(Driver driver)
        {
            Steps.Add("validate");
            return valid;
        }

        protected override Task<Driver> EnrichMediaAsync(Driver driver, CancellationToken cancellationToken)
        {
            Steps.Add("enrich");
            return Task.FromResult(driver);
        }

        protected override Driver Normalize(Driver driver)
        {
            Steps.Add("normalize");
            return driver;
        }
    }

    [Fact]
    public async Task Template_RunsStepsInFixedOrder()
    {
        var enricher = new RecordingEnricher(new FakeDriverProvider(Alonso), valid: true);

        await enricher.GetDriverByIdAsync("alonso");

        Assert.Equal(["fetch", "validate", "enrich", "normalize"], enricher.Steps);
    }

    [Fact]
    public async Task Template_WhenValidateFails_SkipsEnrichButStillNormalizes()
    {
        var enricher = new RecordingEnricher(new FakeDriverProvider(Alonso), valid: false);

        await enricher.GetDriverByIdAsync("alonso");

        Assert.Equal(["fetch", "validate", "normalize"], enricher.Steps);
    }

    [Fact]
    public async Task Template_WhenDriverNotFound_StopsAfterFetch()
    {
        var enricher = new RecordingEnricher(new FakeDriverProvider(null), valid: true);

        Assert.Null(await enricher.GetDriverByIdAsync("nobody"));
        Assert.Equal(["fetch"], enricher.Steps);
    }

    // ---- Wikipedia enricher ----

    [Fact]
    public async Task Wikipedia_FillsDescriptionAndThumbnail()
    {
        var wiki = new FakeWikipediaProvider { Summary = AlonsoSummary };
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), wiki);

        var driver = await enricher.GetDriverByIdAsync("alonso");

        Assert.NotNull(driver);
        Assert.Equal(["Fernando_Alonso"], wiki.RequestedSlugs);
        Assert.Equal(AlonsoSummary.Extract, driver.Description);
        Assert.Equal(AlonsoSummary.ThumbnailUrl, driver.ThumbnailUrl);
    }

    [Fact]
    public async Task Wikipedia_PageWithoutExtract_FallsBackToShortDescription()
    {
        var wiki = new FakeWikipediaProvider { Summary = AlonsoSummary with { Extract = null } };
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), wiki);

        var driver = await enricher.GetDriverByIdAsync("alonso");

        Assert.Equal("Spanish racing driver (born 1981)", driver!.Description);
    }

    [Fact]
    public async Task Wikipedia_PageWithoutImage_UsesPlaceholderImage()
    {
        var wiki = new FakeWikipediaProvider { Summary = AlonsoSummary with { ThumbnailUrl = null } };
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), wiki);

        var driver = await enricher.GetDriverByIdAsync("alonso");

        Assert.Equal(AlonsoSummary.Extract, driver!.Description);
        Assert.Equal("https://placehold.co/330x400?text=Fernando%20Alonso", driver.ThumbnailUrl);
    }

    [Fact]
    public async Task Wikipedia_PageNotFound_UsesNaAndPlaceholder()
    {
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), new FakeWikipediaProvider { Summary = null });

        var driver = await enricher.GetDriverByIdAsync("alonso");

        Assert.Equal(WikipediaDriverEnricher.MissingDescription, driver!.Description);
        Assert.StartsWith("https://placehold.co/", driver.ThumbnailUrl);
    }

    [Fact]
    public async Task Wikipedia_DriverWithoutWikipediaUrl_SkipsRequestAndUsesNa()
    {
        var wiki = new FakeWikipediaProvider { Summary = AlonsoSummary };
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso with { WikipediaUrl = null }), wiki);

        var driver = await enricher.GetDriverByIdAsync("alonso");

        Assert.Empty(wiki.RequestedSlugs);
        Assert.Equal(WikipediaDriverEnricher.MissingDescription, driver!.Description);
        Assert.StartsWith("https://placehold.co/", driver.ThumbnailUrl);
    }

    [Fact]
    public async Task Wikipedia_RequestFails_ReturnsDriverWithFailedToFetch()
    {
        var wiki = new FakeWikipediaProvider { Throw = new HttpRequestException("boom") };
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), wiki);

        var driver = await enricher.GetDriverByIdAsync("alonso");

        Assert.NotNull(driver);
        Assert.Equal("Fernando", driver.GivenName);
        Assert.Equal(WikipediaDriverEnricher.FailedDescription, driver.Description);
        Assert.StartsWith("https://placehold.co/", driver.ThumbnailUrl);
    }

    [Fact]
    public async Task Wikipedia_Timeout_ReturnsDriverWithFailedToFetch()
    {
        var wiki = new FakeWikipediaProvider { Throw = new TaskCanceledException("timeout") };
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), wiki);

        var driver = await enricher.GetDriverByIdAsync("alonso");

        Assert.Equal(WikipediaDriverEnricher.FailedDescription, driver!.Description);
    }

    [Fact]
    public async Task Wikipedia_CallerCancellation_IsNotSwallowed()
    {
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), new FakeWikipediaProvider { Summary = AlonsoSummary });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enricher.GetDriverByIdAsync("alonso", cts.Token));
    }

    [Fact]
    public async Task Wikipedia_DriversPage_IsPassedThroughUnenriched()
    {
        var wiki = new FakeWikipediaProvider { Summary = AlonsoSummary };
        var enricher = new WikipediaDriverEnricher(new FakeDriverProvider(Alonso), wiki);

        var page = await enricher.GetDriversPageAsync();

        Assert.Empty(wiki.RequestedSlugs);
        Assert.Null(Assert.Single(page).Description);
    }

    // ---- Full chain: cache outermost ----

    [Fact]
    public async Task CachedEnricher_SecondLookup_HitsNeitherJolpicaNorWikipedia()
    {
        var jolpica = new FakeDriverProvider(Alonso);
        var wiki = new FakeWikipediaProvider { Summary = AlonsoSummary };
        var provider = new CachedDriverProvider(
            new WikipediaDriverEnricher(jolpica, wiki),
            new CacheManager<string, Driver?>());

        var first = await provider.GetDriverByIdAsync("alonso");
        var second = await provider.GetDriverByIdAsync("alonso");

        Assert.Equal(1, jolpica.Calls);
        Assert.Single(wiki.RequestedSlugs);
        Assert.Equal(AlonsoSummary.Extract, second!.Description);
        Assert.Same(first, second);
    }
}
