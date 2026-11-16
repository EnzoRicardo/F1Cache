namespace F1Cache.Core.Clients.Wikipedia;

/// <summary>
/// Helpers for turning Wikipedia article URLs (as returned by Jolpica) into page slugs.
/// </summary>
public static class WikipediaSlug
{
    private const string ArticlePathPrefix = "/wiki/";

    /// <summary>
    /// Extracts the decoded slug from an English Wikipedia article URL,
    /// e.g. "http://en.wikipedia.org/wiki/Kimi_R%C3%A4ikk%C3%B6nen" → "Kimi_Räikkönen".
    /// Returns null for anything else, including other languages, whose slugs would be mistaken for English ones.
    /// </summary>
    public static string? FromUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("en.wikipedia.org", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(ArticlePathPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var slug = Uri.UnescapeDataString(uri.AbsolutePath[ArticlePathPrefix.Length..]);
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }
}
