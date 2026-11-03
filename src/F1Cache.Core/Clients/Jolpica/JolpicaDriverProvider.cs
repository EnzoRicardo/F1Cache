using System.Net;
using System.Net.Http.Json;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Clients.Jolpica;

/// <summary>
/// HTTP provider that fetches Formula 1 driver details from the Jolpica (Ergast) API.
/// Acts as an Adapter converting external Jolpica JSON data into library domain models.
/// </summary>
public class JolpicaDriverProvider(HttpClient httpClient) : IDriverProvider
{
    private const string BaseUrl = "https://api.jolpi.ca/ergast/f1/";
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);

        var url = $"{BaseUrl}drivers/{Uri.EscapeDataString(driverId)}.json";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var root = await response.Content.ReadFromJsonAsync<JolpicaRootResponse>(cancellationToken: cancellationToken);
        var driverDto = root?.MRData?.DriverTable?.Drivers.FirstOrDefault();

        return driverDto?.ToDomain();
    }

    public async Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);

        var url = $"{BaseUrl}drivers.json?limit={limit}&offset={offset}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var root = await response.Content.ReadFromJsonAsync<JolpicaRootResponse>(cancellationToken: cancellationToken);
        var drivers = root?.MRData?.DriverTable?.Drivers;

        if (drivers == null || drivers.Count == 0)
        {
            return [];
        }

        return [.. drivers.Select(d => d.ToDomain())];
    }
}
