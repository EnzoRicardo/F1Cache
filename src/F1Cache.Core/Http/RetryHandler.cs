using System.Net;

namespace F1Cache.Core.Http;

/// <summary>
/// Chain of Responsibility link: retries transient failures (HTTP 429, 5xx, network errors)
/// with exponential backoff, honouring the server's Retry-After header.
/// Gives up and passes the last response or exception back up the chain once retries run out.
/// </summary>
public sealed class RetryHandler : DelegatingHandler
{
    public const int DefaultMaxRetries = 2;
    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(5);

    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public RetryHandler(int maxRetries = DefaultMaxRetries, TimeSpan? baseDelay = null, TimeSpan? maxDelay = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? DefaultBaseDelay;
        _maxDelay = maxDelay ?? DefaultMaxDelay;
        ArgumentOutOfRangeException.ThrowIfLessThan(_baseDelay, TimeSpan.Zero, nameof(baseDelay));
        ArgumentOutOfRangeException.ThrowIfLessThan(_maxDelay, TimeSpan.Zero, nameof(maxDelay));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                await Task.Delay(Backoff(attempt), cancellationToken);
                continue;
            }

            if (!IsTransient(response.StatusCode) || attempt >= _maxRetries)
            {
                return response;
            }

            // A Retry-After longer than we are willing to wait (e.g., an hourly quota) is passed straight back.
            var delay = RetryAfter(response) ?? Backoff(attempt);
            if (delay > _maxDelay)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private TimeSpan Backoff(int attempt) => _baseDelay * Math.Pow(2, attempt);

    private static TimeSpan? RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => date - DateTimeOffset.UtcNow,
        _ => null,
    };
}
