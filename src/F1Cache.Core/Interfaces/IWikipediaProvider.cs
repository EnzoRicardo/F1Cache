using F1Cache.Core.Models;

namespace F1Cache.Core.Interfaces;

/// <summary>
/// Abstraction for retrieving Wikipedia page summaries (description and images).
/// </summary>
public interface IWikipediaProvider
{
    /// <summary>
    /// Retrieves the summary of an English Wikipedia page by its slug, as Jolpica links to it
    /// (e.g., "Fernando_Alonso" or "Kimi_Räikkönen", not URL-encoded). Returns null when the page does not exist.
    /// </summary>
    Task<WikipediaSummary?> GetSummaryAsync(string slug, CancellationToken cancellationToken = default);
}
