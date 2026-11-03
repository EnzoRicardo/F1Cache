using System.Net;
using System.Text;
using F1Cache.Core.Clients.Jolpica;

namespace F1Cache.Tests;

public class JolpicaDriverProviderTests
{
    private class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task GetDriverByIdAsync_ParsesJolpicaResponseCorrectly()
    {
        // Sample Jolpica response JSON
        var json = """
        {
          "MRData": {
            "DriverTable": {
              "driverId": "alonso",
              "Drivers": [
                {
                  "driverId": "alonso",
                  "permanentNumber": "14",
                  "code": "ALO",
                  "url": "https://en.wikipedia.org/wiki/Fernando_Alonso",
                  "givenName": "Fernando",
                  "familyName": "Alonso",
                  "dateOfBirth": "1981-07-29",
                  "nationality": "Spanish"
                }
              ]
            }
          }
        }
        """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new FakeHttpMessageHandler(response));
        var provider = new JolpicaDriverProvider(httpClient);

        // Act
        var driver = await provider.GetDriverByIdAsync("alonso");

        // Assert
        Assert.NotNull(driver);
        Assert.Equal("alonso", driver.Id);
        Assert.Equal("14", driver.PermanentNumber);
        Assert.Equal("ALO", driver.Code);
        Assert.Equal("Fernando", driver.GivenName);
        Assert.Equal("Alonso", driver.FamilyName);
        Assert.Equal("Fernando Alonso", driver.FullName);
        Assert.Equal(new DateOnly(1981, 7, 29), driver.DateOfBirth);
        Assert.Equal("Spanish", driver.Nationality);
        Assert.Equal("https://en.wikipedia.org/wiki/Fernando_Alonso", driver.WikipediaUrl);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(101, 0)]
    [InlineData(50, -1)]
    public async Task GetDriversPageAsync_WhenArgumentsInvalid_ThrowsArgumentOutOfRangeException(int limit, int offset)
    {
        // Arrange
        var httpClient = new HttpClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        var provider = new JolpicaDriverProvider(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.GetDriversPageAsync(limit, offset));
    }

    [Fact]
    public async Task GetDriversPageAsync_WhenLimitIsZero_ReturnsEmptyListWithoutError()
    {
        // Jolpica returns empty list when limit=0
        var json = """
        {
          "MRData": {
            "DriverTable": {
              "Drivers": []
            }
          }
        }
        """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new FakeHttpMessageHandler(response));
        var provider = new JolpicaDriverProvider(httpClient);

        var result = await provider.GetDriversPageAsync(limit: 0, offset: 0);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
