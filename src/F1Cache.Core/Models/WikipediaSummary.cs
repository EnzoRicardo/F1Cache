namespace F1Cache.Core.Models;

/// <summary>
/// Summary of a Wikipedia page: short description, intro paragraph and lead image.
/// </summary>
/// <param name="Title">Display title of the page (e.g., "Fernando Alonso").</param>
/// <param name="Description">One-line description (e.g., "Spanish racing driver (born 1981)").</param>
/// <param name="Extract">Plain-text first paragraph of the article.</param>
/// <param name="ThumbnailUrl">Small version of the lead image (~320px).</param>
/// <param name="ImageUrl">Full-resolution lead image.</param>
public record WikipediaSummary(
    string Title,
    string? Description = null,
    string? Extract = null,
    string? ThumbnailUrl = null,
    string? ImageUrl = null);
