namespace F1Cache.Core.Http;

/// <summary>
/// Chain of Responsibility link: spaces out requests to the same host so we stay under API rate limits
/// (Jolpica allows a few requests per second). Requests to different hosts don't wait on each other.
/// </summary>
public sealed class RateLimitHandler : DelegatingHandler
{
    public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan _minInterval;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTimeOffset> _nextSlotByHost = [];

    public RateLimitHandler(TimeSpan? minInterval = null, TimeProvider? timeProvider = null)
    {
        _minInterval = minInterval ?? DefaultMinInterval;
        ArgumentOutOfRangeException.ThrowIfLessThan(_minInterval, TimeSpan.Zero, nameof(minInterval));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri?.Host ?? string.Empty;
        TimeSpan wait;

        // Reserve the next free slot for this host; concurrent callers line up one interval apart.
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            var slot = _nextSlotByHost.TryGetValue(host, out var next) && next > now ? next : now;
            _nextSlotByHost[host] = slot + _minInterval;
            wait = slot - now;
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, _timeProvider, cancellationToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
