using System.Net;
using System.Net.Http.Headers;
using System.Text;
using F1Cache.Core;
using F1Cache.Core.Http;
using Microsoft.Extensions.Time.Testing;

namespace F1Cache.Tests;

public class HttpHandlerTests
{
    private sealed class TagHandler(string tag, List<string> trail) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            trail.Add(tag);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private static HttpMessageInvoker Chain(DelegatingHandler handler, HttpMessageHandler terminal)
    {
        handler.InnerHandler = terminal;
        return new HttpMessageInvoker(handler);
    }

    private static Task<HttpResponseMessage> Get(HttpMessageInvoker invoker, string url = "https://api.jolpi.ca/x") =>
        invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), CancellationToken.None);

    private static RetryHandler FastRetry(int maxRetries = 2) =>
        new(maxRetries, baseDelay: TimeSpan.FromMilliseconds(1), maxDelay: TimeSpan.FromSeconds(1));

    // ---- Retry ----

    [Fact]
    public async Task Retry_TransientErrorsThenSuccess_ReturnsSuccess()
    {
        var terminal = new ScriptedHttpHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        using var invoker = Chain(FastRetry(), terminal);

        using var response = await Get(invoker);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, terminal.Requests.Count);
    }

    [Fact]
    public async Task Retry_ExhaustsRetries_ReturnsLastResponse()
    {
        var terminal = new ScriptedHttpHandler(HttpStatusCode.InternalServerError);
        using var invoker = Chain(FastRetry(maxRetries: 2), terminal);

        using var response = await Get(invoker);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(3, terminal.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Retry_NonTransientStatus_IsNotRetried(HttpStatusCode status)
    {
        var terminal = new ScriptedHttpHandler(status);
        using var invoker = Chain(FastRetry(), terminal);

        using var response = await Get(invoker);

        Assert.Equal(status, response.StatusCode);
        Assert.Single(terminal.Requests);
    }

    [Fact]
    public async Task Retry_NetworkErrorThenSuccess_ReturnsSuccess()
    {
        var terminal = new ScriptedHttpHandler(new HttpRequestException("reset"), HttpStatusCode.OK);
        using var invoker = Chain(FastRetry(), terminal);

        using var response = await Get(invoker);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, terminal.Requests.Count);
    }

    [Fact]
    public async Task Retry_NetworkErrorEveryTime_RethrowsAfterRetries()
    {
        var terminal = new ScriptedHttpHandler(new HttpRequestException("down"));
        using var invoker = Chain(FastRetry(maxRetries: 1), terminal);

        await Assert.ThrowsAsync<HttpRequestException>(() => Get(invoker));
        Assert.Equal(2, terminal.Requests.Count);
    }

    [Fact]
    public async Task Retry_RetryAfterLongerThanMaxDelay_GivesUpImmediately()
    {
        var tooManyRequests = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooManyRequests.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));
        var terminal = new ScriptedHttpHandler(tooManyRequests, HttpStatusCode.OK);
        using var invoker = Chain(FastRetry(), terminal);

        using var response = await Get(invoker);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Single(terminal.Requests);
    }

    // ---- Rate limit ----

    [Fact]
    public async Task RateLimit_SameHost_SpacesRequestsOneIntervalApart()
    {
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMilliseconds(250);
        using var invoker = Chain(new RateLimitHandler(interval, clock), new ScriptedHttpHandler(HttpStatusCode.OK));

        var first = Get(invoker);
        var second = Get(invoker);
        var third = Get(invoker);

        (await first).Dispose();                 // goes straight through
        Assert.False(second.IsCompleted);        // waiting for its slot

        clock.Advance(interval - TimeSpan.FromMilliseconds(1));
        Assert.False(second.IsCompleted);        // one tick early: still waiting

        clock.Advance(TimeSpan.FromMilliseconds(1));
        (await second).Dispose();
        Assert.False(third.IsCompleted);

        clock.Advance(interval);
        (await third).Dispose();
    }

    [Fact]
    public async Task RateLimit_AfterQuietPeriod_DoesNotWait()
    {
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMilliseconds(250);
        using var invoker = Chain(new RateLimitHandler(interval, clock), new ScriptedHttpHandler(HttpStatusCode.OK));

        (await Get(invoker)).Dispose();
        clock.Advance(TimeSpan.FromSeconds(10));

        var later = Get(invoker);

        Assert.True(later.IsCompleted);
        (await later).Dispose();
    }

    [Fact]
    public async Task RateLimit_DifferentHosts_DoNotWaitOnEachOther()
    {
        var clock = new FakeTimeProvider();
        using var invoker = Chain(new RateLimitHandler(TimeSpan.FromSeconds(1), clock), new ScriptedHttpHandler(HttpStatusCode.OK));

        var jolpica = Get(invoker, "https://api.jolpi.ca/x");
        var wikipedia = Get(invoker, "https://en.wikipedia.org/x");

        Assert.True(jolpica.IsCompleted);
        Assert.True(wikipedia.IsCompleted);
        (await jolpica).Dispose();
        (await wikipedia).Dispose();
    }

    // ---- Logging ----

    [Fact]
    public async Task Logging_WritesMethodUrlAndStatus()
    {
        var lines = new List<string>();
        using var invoker = Chain(new LoggingHandler(lines.Add), new ScriptedHttpHandler(HttpStatusCode.NotFound));

        using var _ = await Get(invoker, "https://api.jolpi.ca/drivers/nobody.json");

        var line = Assert.Single(lines);
        Assert.Contains("GET https://api.jolpi.ca/drivers/nobody.json -> 404", line);
    }

    // ---- Builder wiring ----

    [Fact]
    public async Task Builder_RunsCustomHandlersInOrder_AndBuiltInRetryStillApplies()
    {
        var trail = new List<string>();
        var driverJson = """{ "MRData": { "DriverTable": { "Drivers": [ { "driverId": "alonso", "givenName": "Fernando", "familyName": "Alonso" } ] } } }""";
        var terminal = new ScriptedHttpHandler(
            HttpStatusCode.ServiceUnavailable,
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(driverJson, Encoding.UTF8, "application/json") });

        using var client = F1Client.CreateBuilder()
            .WithRetryPolicy(maxRetries: 2, baseDelay: TimeSpan.Zero)
            .WithRateLimit(TimeSpan.Zero)
            .WithHttpHandler(() => new TagHandler("first", trail))
            .WithHttpHandler(() => new TagHandler("second", trail))
            .WithPrimaryHttpHandler(terminal)
            .Build();

        var driver = await client.GetDriverAsync("alonso");

        Assert.Equal("Fernando Alonso", driver!.FullName);
        Assert.Equal(["first", "second", "first", "second"], trail); // one pass per attempt: 503, then 200
    }

    [Fact]
    public async Task Builder_WithZeroRetries_DoesNotRetry()
    {
        var terminal = new ScriptedHttpHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        using var client = F1Client.CreateBuilder()
            .WithRetryPolicy(maxRetries: 0)
            .WithRateLimit(TimeSpan.Zero)
            .WithPrimaryHttpHandler(terminal)
            .Build();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetDriverAsync("alonso"));
        Assert.Single(terminal.Requests);
    }

    [Fact]
    public void Builder_RetryAndRateLimit_RejectInvalidValues()
    {
        var builder = F1Client.CreateBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithRetryPolicy(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithRetryPolicy(1, baseDelay: TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithRetryPolicy(1, maxDelay: TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithRateLimit(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Builder_RetryPolicyWithCustomHttpClient_Throws()
    {
        var builder = F1Client.CreateBuilder()
            .WithHttpClient(new HttpClient())
            .WithRetryPolicy(3);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Builder_CanBuildTwiceWithHandlerFactories()
    {
        var builder = F1Client.CreateBuilder().WithHttpHandler(() => new LoggingHandler(_ => { }));

        using var first = builder.Build();
        using var second = builder.Build();
    }

    [Fact]
    public void Builder_HttpHandlerWithCustomHttpClient_Throws()
    {
        var builder = F1Client.CreateBuilder()
            .WithHttpClient(new HttpClient())
            .WithHttpHandler(() => new LoggingHandler(_ => { }));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}
