using PowerPositionService.Models;

namespace PowerPositionService.Services;

/// <summary>
/// Interface for accessing the PowerDay API.
/// </summary>
public interface IPowerService
{
    /// <summary>
    /// Gets all trades for a specific power day.
    /// </summary>
    /// <param name="powerDay">The power day to retrieve trades for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of trades for the power day.</returns>
    Task<IEnumerable<Trade>> GetTradesAsync(DateTime powerDay, CancellationToken cancellationToken = default);
}
