using Microsoft.Extensions.Logging;
using PowerPositionService.Models;

namespace PowerPositionService.Services;

/// <summary>
/// Aggregates trade volumes into power positions by period.
/// </summary>
public class PositionAggregator
{
    private readonly ILogger<PositionAggregator> _logger;

    public PositionAggregator(ILogger<PositionAggregator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Aggregates volumes from multiple trades into 24 power positions.
    /// </summary>
    /// <param name="trades">The trades to aggregate.</param>
    /// <returns>24 power positions with local times and aggregated volumes.</returns>
    public IReadOnlyList<PowerPosition> Aggregate(IEnumerable<Trade> trades)
    {
        var tradeList = trades.ToList();
        _logger.LogDebug("Aggregating {TradeCount} trades", tradeList.Count);

        // Initialize aggregated volumes for 24 periods
        var aggregatedVolumes = new double[24];

        foreach (var trade in tradeList)
        {
            for (int period = 1; period <= 24; period++)
            {
                aggregatedVolumes[period - 1] += trade.GetVolume(period);
            }
        }

        // Build power positions with correct local times
        // Period 1 = 23:00, Period 2 = 00:00, ..., Period 24 = 22:00
        var positions = new List<PowerPosition>(24);

        for (int period = 1; period <= 24; period++)
        {
            var localTime = PeriodToLocalTime(period);
            positions.Add(new PowerPosition
            {
                Period = period,
                LocalTime = localTime,
                Volume = Math.Round(aggregatedVolumes[period - 1], 2)
            });
        }

        // Sort by local time (23:00, 00:00, 01:00, ..., 22:00)
        // This is already in period order which matches the required output
        _logger.LogDebug("Aggregation complete, {PositionCount} positions generated", positions.Count);
        
        return positions;
    }

    /// <summary>
    /// Converts a period number (1-24) to local time.
    /// Period 1 = 23:00 (previous calendar day)
    /// Period 2 = 00:00
    /// ...
    /// Period 24 = 22:00
    /// </summary>
    public static TimeOnly PeriodToLocalTime(int period)
    {
        if (period < 1 || period > 24)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be between 1 and 24");

        // Period 1 = 23:00
        // Period 2 = 00:00
        // Period N = (22 + N) % 24 = hour
        int hour = (22 + period) % 24;
        return new TimeOnly(hour, 0);
    }
}
