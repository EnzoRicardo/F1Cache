using F1Cache.Core.Interfaces;
using F1Cache.Core.Models;

namespace F1Cache.Core.Services.Enrichment;

/// <summary>
/// Template Method: fixes the order of the driver resolution pipeline
/// (FetchBase → Validate → EnrichMedia → Normalize) and lets subclasses fill in the steps.
/// Implements <see cref="IDriverProvider"/> so it can sit inside <see cref="CachedDriverProvider"/>,
/// which then caches the fully enriched driver.
/// </summary>
public abstract class DriverDataEnricher(IDriverProvider innerProvider) : IDriverProvider
{
    private readonly IDriverProvider _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

    /// <summary>
    /// The template method. Not virtual, so subclasses can change the steps but never their order.
    /// </summary>
    public async Task<Driver?> GetDriverByIdAsync(string driverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);

        var driver = await FetchBaseAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        if (Validate(driver))
        {
            driver = await EnrichMediaAsync(driver, cancellationToken);
        }

        return Normalize(driver);
    }

    /// <summary>
    /// Pages are passed through unenriched.
    /// </summary>
    public Task<IReadOnlyList<Driver>> GetDriversPageAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default) =>
        _innerProvider.GetDriversPageAsync(limit, offset, cancellationToken);

    /// <summary>
    /// Load the base driver data. Defaults to the wrapped provider.
    /// </summary>
    protected virtual Task<Driver?> FetchBaseAsync(string driverId, CancellationToken cancellationToken) =>
        _innerProvider.GetDriverByIdAsync(driverId, cancellationToken);

    /// <summary>
    /// Decide whether the driver has what enrichment needs
    /// </summary>
    protected abstract bool Validate(Driver driver);

    /// <summary>
    /// Add media data (description, images) from an external source.
    /// </summary>
    protected abstract Task<Driver> EnrichMediaAsync(Driver driver, CancellationToken cancellationToken);

    /// <summary>
    /// Final clean-up, always runs. Defaults to returning the driver unchanged.
    /// </summary>
    protected virtual Driver Normalize(Driver driver) => driver;
}
