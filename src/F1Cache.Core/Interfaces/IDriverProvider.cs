using F1Cache.Core.Models;

namespace F1Cache.Core.Interfaces;

/// <summary>
/// Abstraction for retrieving Formula 1 driver data.
/// </summary>
public interface IDriverProvider
{
    /// <summary>Largest page Jolpica returns in one request.</summary>
    const int MaxPageSize = 100;

    /// <summary>
    /// Retrieves a driver by their unique identifier.
    /// </summary>
    Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a page of drivers (at most <see cref="MaxPageSize"/> per page).
    /// </summary>
    Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = MaxPageSize, int offset = 0, CancellationToken cancellationToken = default);
}
