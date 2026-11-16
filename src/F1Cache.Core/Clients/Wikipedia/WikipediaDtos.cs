using System.Text.Json.Serialization;

namespace F1Cache.Core.Clients.Wikipedia;

internal record WikipediaSummaryDto
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("extract")]
    public string? Extract { get; init; }

    [JsonPropertyName("thumbnail")]
    public WikipediaImageDto? Thumbnail { get; init; }

    [JsonPropertyName("originalimage")]
    public WikipediaImageDto? OriginalImage { get; init; }
}

internal record WikipediaImageDto
{
    [JsonPropertyName("source")]
    public string? Source { get; init; }
}
