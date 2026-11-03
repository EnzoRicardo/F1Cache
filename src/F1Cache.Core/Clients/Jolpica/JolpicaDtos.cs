using System.Text.Json.Serialization;

namespace F1Cache.Core.Clients.Jolpica;

internal record JolpicaRootResponse
{
    [JsonPropertyName("MRData")]
    public JolpicaMrData? MRData { get; init; }
}

internal record JolpicaMrData
{
    [JsonPropertyName("DriverTable")]
    public JolpicaDriverTable? DriverTable { get; init; }
}

internal record JolpicaDriverTable
{
    [JsonPropertyName("driverId")]
    public string? DriverId { get; init; }

    [JsonPropertyName("Drivers")]
    public List<JolpicaDriverDto> Drivers { get; init; } = [];
}

internal record JolpicaDriverDto
{
    [JsonPropertyName("driverId")]
    public string DriverId { get; init; } = string.Empty;

    [JsonPropertyName("permanentNumber")]
    public string? PermanentNumber { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("familyName")]
    public string FamilyName { get; init; } = string.Empty;

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; init; }

    [JsonPropertyName("nationality")]
    public string? Nationality { get; init; }
}
