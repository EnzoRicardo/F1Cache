using System.Diagnostics;

namespace F1Cache.Core.Http;

/// <summary>
/// Chain of Responsibility link: writes one line per outgoing request with its status and duration.
/// Opt-in, e.g. <c>.WithHttpHandler(() => new LoggingHandler(Console.WriteLine))</c>.
/// </summary>
public sealed class LoggingHandler(Action<string> log) : DelegatingHandler
{
    private readonly Action<string> _log = log ?? throw new ArgumentNullException(nameof(log));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            _log($"[HTTP] {request.Method} {request.RequestUri} -> {(int)response.StatusCode} ({stopwatch.ElapsedMilliseconds} ms)");
            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"[HTTP] {request.Method} {request.RequestUri} -> {ex.GetType().Name} ({stopwatch.ElapsedMilliseconds} ms)");
            throw;
        }
    }
}
