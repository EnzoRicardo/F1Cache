using F1Cache.Core.Models;

namespace F1Cache.Core.Clients.Jolpica;

/// <summary>
/// Extension methods for mapping raw Jolpica DTOs into domain models.
/// </summary>
internal static class JolpicaDriverMapper
{
    public static Driver ToDomain(this JolpicaDriverDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        DateOnly? dateOfBirth = null;
        if (DateOnly.TryParse(dto.DateOfBirth, out var parsed))
        {
            dateOfBirth = parsed;
        }

        return new Driver(
            Id: dto.DriverId,
            GivenName: dto.GivenName,
            FamilyName: dto.FamilyName,
            PermanentNumber: dto.PermanentNumber,
            Code: dto.Code,
            DateOfBirth: dateOfBirth,
            Nationality: dto.Nationality,
            WikipediaUrl: dto.Url,
            ThumbnailUrl: null
        );
    }
}
