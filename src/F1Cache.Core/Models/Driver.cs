namespace F1Cache.Core.Models;

/// <summary>
/// Domain model representing a Formula 1 driver using positional record syntax.
/// </summary>
public record Driver(
    string Id,
    string GivenName,
    string FamilyName,
    string? PermanentNumber = null,
    string? Code = null,
    DateOnly? DateOfBirth = null,
    string? Nationality = null,
    string? WikipediaUrl = null,
    string? ThumbnailUrl = null,
    string? Description = null)
{
    /// <summary>
    /// Convenient helper returning the full name (e.g., "Fernando Alonso").
    /// </summary>
    public string FullName => $"{GivenName} {FamilyName}".Trim();
}
