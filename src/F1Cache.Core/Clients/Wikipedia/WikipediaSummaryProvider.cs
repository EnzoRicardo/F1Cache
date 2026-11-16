using System.Net;
using System.Net.Http.Json;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Clients.Wikipedia;

/// <summary>
/// HTTP provider that fetches page summaries from the English Wikipedia REST API.
/// Acts as an Adapter converting Wikipedia JSON into library domain models.
/// </summary>
public sealed class WikipediaSummaryProvider(HttpClient httpClient) : IWikipediaProvider
{
    private const string BaseUrl = "https://en.wikipedia.org/api/rest_v1/page/summary/";

    // Wikimedia rejects requests without a descriptive User-Agent.
    private const string UserAgent = "F1Cache/1.0 (educational .NET SDK)";

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<WikipediaSummary?> GetSummaryAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedSlug = slug.Trim().Replace(' ', '_');
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{Uri.EscapeDataString(normalizedSlug)}");
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            request.Headers.UserAgent.ParseAdd(UserAgent);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<WikipediaSummaryDto>(cancellationToken: cancellationToken);
        return dto?.ToDomain();
    }
}
