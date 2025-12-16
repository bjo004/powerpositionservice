namespace PowerPositionService.Models;

/// <summary>
/// Represents a single trade with 24 period volumes for a power day.
/// </summary>
public class Trade
{
    /// <summary>
    /// Unique identifier for the trade.
    /// </summary>
    public string TradeId { get; set; } = string.Empty;

    /// <summary>
    /// The power day this trade applies to.
    /// </summary>
    public DateTime PowerDay { get; set; }

    /// <summary>
    /// Volume values for periods 1-24.
    /// Period 1 = 23:00, Period 2 = 00:00, ..., Period 24 = 22:00
    /// </summary>
    public double[] Periods { get; set; } = new double[24];

    /// <summary>
    /// Gets the volume for a specific period (1-24).
    /// </summary>
    public double GetVolume(int period)
    {
        if (period < 1 || period > 24)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be between 1 and 24");
        return Periods[period - 1];
    }
}

/// <summary>
/// Represents an aggregated power position for a specific local time.
/// </summary>
public class PowerPosition
{
    /// <summary>
    /// The local time (London) for this position.
    /// </summary>
    public TimeOnly LocalTime { get; set; }

    /// <summary>
    /// The period number (1-24).
    /// </summary>
    public int Period { get; set; }

    /// <summary>
    /// The aggregated volume across all trades.
    /// </summary>
    public double Volume { get; set; }
}
