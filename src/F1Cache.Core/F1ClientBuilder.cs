using F1Cache.Core.Clients.Jolpica;
using F1Cache.Core.Clients.Wikipedia;
using F1Cache.Core.Eviction;
using F1Cache.Core.Http;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Managers;
using F1Cache.Core.Models;
using F1Cache.Core.Observers;
using F1Cache.Core.Services;
using F1Cache.Core.Services.Enrichment;

namespace F1Cache.Core;

/// <summary>
/// Builder: fluent configuration of an <see cref="F1Client"/>, replacing long constructor parameter lists.
/// Every option has a default, so <c>F1Client.CreateBuilder().Build()</c> works on its own.
/// </summary>
public sealed class F1ClientBuilder
{
    public static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(10);

    private TimeSpan _timeToLive = CacheManager<string, Driver?>.DefaultTimeToLive;
    private int _capacity = CacheManager<string, Driver?>.DefaultCapacity;
    private Func<IEvictionStrategy<string>> _evictionStrategyFactory = () => new LruStrategy<string>();
    private readonly List<ICacheObserver<string>> _observers = [];
    private readonly List<Func<DelegatingHandler>> _httpHandlerFactories = [];
    private HttpClient? _httpClient;
    private TimeSpan? _httpTimeout;
    private HttpMessageHandler? _primaryHttpHandler;

    internal F1ClientBuilder()
    {
    }

    /// <summary>How long a found driver stays cached. Default: 1 minute.</summary>
    public F1ClientBuilder WithCacheTimeToLive(TimeSpan timeToLive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);
        _timeToLive = timeToLive;
        return this;
    }

    /// <summary>Maximum number of cached drivers before eviction starts. Default: 50.</summary>
    public F1ClientBuilder WithCacheCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        _capacity = capacity;
        return this;
    }

    /// <summary>Picks a built-in eviction strategy. Default: LRU.</summary>
    public F1ClientBuilder WithEvictionPolicy(EvictionPolicy policy)
    {
        _evictionStrategyFactory = policy switch
        {
            EvictionPolicy.Lru => () => new LruStrategy<string>(),
            EvictionPolicy.Lfu => () => new LfuStrategy<string>(),
            EvictionPolicy.Fifo => () => new FifoStrategy<string>(),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown eviction policy."),
        };
        return this;
    }

    /// <summary>
    /// Plugs in a custom eviction strategy. Takes a factory because a strategy tracks one cache's keys,
    /// so every built client needs its own instance.
    /// </summary>
    public F1ClientBuilder WithEvictionStrategy(Func<IEvictionStrategy<string>> strategyFactory)
    {
        ArgumentNullException.ThrowIfNull(strategyFactory);
        _evictionStrategyFactory = strategyFactory;
        return this;
    }

    /// <summary>Attaches an observer to the cache's hit, miss, expire and evict events. Can be called several times.</summary>
    public F1ClientBuilder WithObserver(ICacheObserver<string> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        _observers.Add(observer);
        return this;
    }

    /// <summary>
    /// Adds a link to the HTTP Chain of Responsibility. The built-in chain is
    /// Retry → RateLimit → (your handlers, in the order added) → network.
    /// Takes a factory because a handler can only belong to one chain.
    /// </summary>
    public F1ClientBuilder WithHttpHandler(Func<DelegatingHandler> handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(handlerFactory);
        _httpHandlerFactories.Add(handlerFactory);
        return this;
    }

    /// <summary>
    /// Uses your own HttpClient (e.g., from IHttpClientFactory). You keep ownership: the client will not dispose it.
    /// Your client is used as-is, so this cannot be combined with <see cref="WithHttpTimeout"/> or <see cref="WithHttpHandler"/>.
    /// </summary>
    public F1ClientBuilder WithHttpClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        return this;
    }

    /// <summary>Timeout for each Jolpica and Wikipedia request. Default: 10 seconds.</summary>
    public F1ClientBuilder WithHttpTimeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _httpTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Assembles the object graph: Cache(Enricher(Jolpica, Wikipedia)). Each call returns an independent client.
    /// </summary>
    public F1Client Build()
    {
        if (_httpClient is not null && (_httpTimeout is not null || _httpHandlerFactories.Count > 0))
        {
            throw new InvalidOperationException(
                $"{nameof(WithHttpTimeout)} and {nameof(WithHttpHandler)} only apply to the built-in HttpClient. " +
                $"Configure the HttpClient passed to {nameof(WithHttpClient)} directly instead.");
        }

        var ownsHttpClient = _httpClient is null;
        var httpClient = _httpClient ?? new HttpClient(BuildHttpHandlerChain()) { Timeout = _httpTimeout ?? DefaultHttpTimeout };

        var cache = new CacheManager<string, Driver?>(_timeToLive, _capacity, _evictionStrategyFactory());
        var statistics = new CacheStatisticsObserver<string>();
        cache.Attach(statistics);
        foreach (var observer in _observers)
        {
            cache.Attach(observer);
        }

        var drivers = new CachedDriverProvider(
            new WikipediaDriverEnricher(
                new JolpicaDriverProvider(httpClient),
                new WikipediaSummaryProvider(httpClient)),
            cache);

        return new F1Client(drivers, cache, statistics, httpClient, ownsHttpClient);
    }

    /// <summary>Replaces the network layer at the end of the chain. Tests use it to fake API responses.</summary>
    internal F1ClientBuilder WithPrimaryHttpHandler(HttpMessageHandler handler)
    {
        _primaryHttpHandler = handler;
        return this;
    }

    /// <summary>
    /// Links the handlers so each one's InnerHandler is the next: Retry → RateLimit → custom... → network.
    /// </summary>
    private HttpMessageHandler BuildHttpHandlerChain()
    {
        List<DelegatingHandler> links = [new RetryHandler(), new RateLimitHandler()];
        links.AddRange(_httpHandlerFactories.Select(factory =>
            factory() ?? throw new InvalidOperationException("An HTTP handler factory returned null.")));

        HttpMessageHandler next = _primaryHttpHandler ?? new SocketsHttpHandler();
        for (var i = links.Count - 1; i >= 0; i--)
        {
            links[i].InnerHandler = next;
            next = links[i];
        }

        return next;
    }
}
