using F1Cache.Core.Models;

namespace F1Cache.Core.Clients.Wikipedia;

/// <summary>
/// Extension methods for mapping raw Wikipedia DTOs into domain models.
/// </summary>
internal static class WikipediaSummaryMapper
{
    public static WikipediaSummary ToDomain(this WikipediaSummaryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new WikipediaSummary(
            Title: dto.Title,
            Description: dto.Description,
            Extract: dto.Extract,
            ThumbnailUrl: dto.Thumbnail?.Source,
            ImageUrl: dto.OriginalImage?.Source
        );
    }
}
