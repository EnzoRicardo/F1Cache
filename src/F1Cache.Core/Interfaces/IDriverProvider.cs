using F1Cache.Core.Models;

namespace F1Cache.Core.Interfaces;

/// <summary>
/// Abstraction for retrieving Formula 1 driver data.
/// </summary>
public interface IDriverProvider
{
    /// <summary>
    /// Retrieves a driver by their unique identifier.
    /// </summary>
    Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of drivers (max limit per page in Jolpica is 100).
    /// </summary>
    Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
}
