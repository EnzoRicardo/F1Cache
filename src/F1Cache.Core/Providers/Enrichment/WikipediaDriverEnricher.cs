using System.Text.Json;
using F1Cache.Core.Clients.Wikipedia;
using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Providers.Enrichment;

/// <summary>
/// Fills in a driver's description and thumbnail from Wikipedia.
/// Wikipedia problems never fail the lookup: the driver is returned with placeholder values instead.
/// </summary>
public sealed class WikipediaDriverEnricher(IDriverProvider innerProvider, IWikipediaProvider wikipediaProvider)
    : DriverDataEnricher(innerProvider)
{
    public const string MissingDescription = "N/A";
    public const string FailedDescription = "Failed to fetch";
    private const string PlaceholderImageBaseUrl = "https://placehold.co/330x400?text=";

    private readonly IWikipediaProvider _wikipediaProvider = wikipediaProvider ?? throw new ArgumentNullException(nameof(wikipediaProvider));

    protected override bool Validate(Driver driver) => SlugOf(driver) is not null;

    protected override async Task<Driver> EnrichMediaAsync(Driver driver, CancellationToken cancellationToken)
    {
        if (SlugOf(driver) is not { } slug)
        {
            return driver;
        }

        try
        {
            var summary = await _wikipediaProvider.GetSummaryAsync(slug, cancellationToken);
            if (summary is null)
            {
                return driver;
            }

            return driver with
            {
                Description = summary.Extract ?? summary.Description,
                ThumbnailUrl = summary.ThumbnailUrl,
            };
        }
        catch (Exception ex) when (IsWikipediaFailure(ex, cancellationToken))
        {
            return driver with { Description = FailedDescription };
        }
    }

    protected override Driver Normalize(Driver driver) => driver with
    {
        Description = string.IsNullOrWhiteSpace(driver.Description) ? MissingDescription : driver.Description,
        ThumbnailUrl = driver.ThumbnailUrl ?? PlaceholderImageBaseUrl + Uri.EscapeDataString(driver.FullName),
    };

    private static string? SlugOf(Driver driver) => WikipediaSlug.FromUrl(driver.WikipediaUrl);

    private static bool IsWikipediaFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or JsonException ||
        (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
