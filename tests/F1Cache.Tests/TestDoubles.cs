using System.Net;
using System.Text;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Tests;

/// <summary>Driver provider that returns a fixed result and counts lookups.</summary>
internal sealed class FakeDriverProvider(Driver? result = null) : IDriverProvider
{
    public Driver? Result { get; set; } = result;
    public List<string> RequestedIds { get; } = [];
    public int Calls => RequestedIds.Count;

    public Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default)
    {
        RequestedIds.Add(driverId);
        return Task.FromResult(Result);
    }

    public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Driver>>(Result is null ? [] : [Result]);
}

/// <summary>Cache observer that records every event as "kind:key" (kind = hit, miss, expire, evict).</summary>
internal sealed class RecordingObserver : ICacheObserver<string>
{
    public List<string> Events { get; } = [];

    public void OnHit(string key) => Events.Add($"hit:{key}");
    public void OnMiss(string key) => Events.Add($"miss:{key}");
    public void OnExpire(string key) => Events.Add($"expire:{key}");
    public void OnEvict(string key) => Events.Add($"evict:{key}");

    /// <summary>Keys of the events of one kind, in order. E.g. <c>Keys("evict")</c>.</summary>
    public List<string> Keys(string kind) =>
        [.. Events.Where(e => e.StartsWith(kind + ":")).Select(e => e[(kind.Length + 1)..])];

    public int Count(string kind) => Keys(kind).Count;
}

/// <summary>
/// End of an HTTP chain: replays scripted outcomes in order (status code, response or exception),
/// repeating the last one, and records every request it receives.
/// </summary>
internal sealed class ScriptedHttpHandler(params object[] script) : HttpMessageHandler
{
    private int _index;

    public List<HttpRequestMessage> Requests { get; } = [];
    public HttpRequestMessage? LastRequest => Requests.LastOrDefault();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var step = script[Math.Min(_index++, script.Length - 1)];
        return step switch
        {
            Exception ex => Task.FromException<HttpResponseMessage>(ex),
            HttpResponseMessage message => Task.FromResult(message),
            HttpStatusCode code => Task.FromResult(new HttpResponseMessage(code)),
            _ => throw new InvalidOperationException($"Unsupported script step: {step}"),
        };
    }

    public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
