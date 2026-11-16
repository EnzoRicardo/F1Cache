using System.Net;
using System.Text;
using F1Cache.Core.Clients.Wikipedia;

namespace F1Cache.Tests;

public class WikipediaSummaryProviderTests
{
    [Fact]
    public async Task GetSummaryAsync_ParsesDescriptionExtractAndImages()
    {
        var json = """
        {
          "type": "standard",
          "title": "Fernando Alonso",
          "description": "Spanish racing driver (born 1981)",
          "extract": "Fernando Alonso Díaz is a Spanish racing driver who competes in Formula One.",
          "thumbnail": { "source": "https://upload.wikimedia.org/thumb/alonso-320px.jpg", "width": 320, "height": 400 },
          "originalimage": { "source": "https://upload.wikimedia.org/alonso.jpg", "width": 1200, "height": 1500 }
        }
        """;
        var provider = new WikipediaSummaryProvider(new HttpClient(new ScriptedHttpHandler(ScriptedHttpHandler.Json(json))));

        var summary = await provider.GetSummaryAsync("Fernando_Alonso");

        Assert.NotNull(summary);
        Assert.Equal("Fernando Alonso", summary.Title);
        Assert.Equal("Spanish racing driver (born 1981)", summary.Description);
        Assert.StartsWith("Fernando Alonso Díaz", summary.Extract);
        Assert.Equal("https://upload.wikimedia.org/thumb/alonso-320px.jpg", summary.ThumbnailUrl);
        Assert.Equal("https://upload.wikimedia.org/alonso.jpg", summary.ImageUrl);
    }

    [Fact]
    public async Task GetSummaryAsync_PageWithoutImage_ReturnsNullImageUrls()
    {
        var json = """{ "title": "Some Driver", "description": "Racing driver" }""";
        var provider = new WikipediaSummaryProvider(new HttpClient(new ScriptedHttpHandler(ScriptedHttpHandler.Json(json))));

        var summary = await provider.GetSummaryAsync("Some_Driver");

        Assert.NotNull(summary);
        Assert.Null(summary.ThumbnailUrl);
        Assert.Null(summary.ImageUrl);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenNotFound_ReturnsNull()
    {
        var provider = new WikipediaSummaryProvider(new HttpClient(new ScriptedHttpHandler(new HttpResponseMessage(HttpStatusCode.NotFound))));

        Assert.Null(await provider.GetSummaryAsync("Does_Not_Exist"));
    }

    [Fact]
    public async Task GetSummaryAsync_WhenServerError_Throws()
    {
        var provider = new WikipediaSummaryProvider(new HttpClient(new ScriptedHttpHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError))));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetSummaryAsync("Fernando_Alonso"));
    }

    [Fact]
    public async Task GetSummaryAsync_EncodesSlugAndSendsUserAgent()
    {
        var handler = new ScriptedHttpHandler(ScriptedHttpHandler.Json("""{ "title": "Kimi Räikkönen" }"""));
        var provider = new WikipediaSummaryProvider(new HttpClient(handler));

        await provider.GetSummaryAsync("Kimi Räikkönen");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            "https://en.wikipedia.org/api/rest_v1/page/summary/Kimi_R%C3%A4ikk%C3%B6nen",
            handler.LastRequest.RequestUri!.AbsoluteUri);
        Assert.NotEmpty(handler.LastRequest.Headers.UserAgent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSummaryAsync_BlankSlug_Throws(string slug)
    {
        var provider = new WikipediaSummaryProvider(new HttpClient(new ScriptedHttpHandler(ScriptedHttpHandler.Json("{}"))));

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetSummaryAsync(slug));
    }

    [Theory]
    [InlineData("https://en.wikipedia.org/wiki/Fernando_Alonso", "Fernando_Alonso")]
    [InlineData("http://en.wikipedia.org/wiki/Kimi_R%C3%A4ikk%C3%B6nen", "Kimi_Räikkönen")]
    [InlineData("http://en.wikipedia.org/wiki/George_Russell_(racing_driver)", "George_Russell_(racing_driver)")]
    public void WikipediaSlug_FromUrl_ExtractsDecodedSlug(string url, string expected)
    {
        Assert.Equal(expected, WikipediaSlug.FromUrl(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://example.com/wiki/Fernando_Alonso")]
    [InlineData("https://en.wikipedia.org/w/index.php?title=Fernando_Alonso")]
    [InlineData("https://en.wikipedia.org/wiki/")]
    [InlineData("https://pt.wikipedia.org/wiki/Fernando_Alonso")]
    public void WikipediaSlug_FromUrl_InvalidUrl_ReturnsNull(string? url)
    {
        Assert.Null(WikipediaSlug.FromUrl(url));
    }
}
