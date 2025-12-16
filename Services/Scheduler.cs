using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPositionService.Configuration;

namespace PowerPositionService.Services;

/// <summary>
/// Calculates scheduling delays based on configured run time and time zone.
/// </summary>
public class Scheduler
{
    private readonly PowerPositionOptions _options;
    private readonly ILogger<Scheduler> _logger;

    public Scheduler(
        IOptions<PowerPositionOptions> options,
        ILogger<Scheduler> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Calculates the delay until the next scheduled run time.
    /// </summary>
    /// <returns>TimeSpan until next run, or TimeSpan.Zero if should run immediately.</returns>
    public TimeSpan GetDelayUntilNextRun()
    {
        var tz = _options.GetTimeZone();
        var runTime = _options.GetRunTime();
        
        var utcNow = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        var localToday = localNow.Date;

        // Target run time today
        var targetToday = localToday.Add(runTime.ToTimeSpan());
        
        DateTime nextRunLocal;
        if (localNow < targetToday)
        {
            // Run later today
            nextRunLocal = targetToday;
        }
        else
        {
            // Run tomorrow
            nextRunLocal = targetToday.AddDays(1);
        }

        // Convert back to UTC and calculate delay
        var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, tz);
        var delay = nextRunUtc - utcNow;

        // Ensure non-negative delay
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _logger.LogInformation(
            "Next run scheduled for {NextRun:yyyy-MM-dd HH:mm:ss} {TimeZone} (in {Delay})",
            nextRunLocal, _options.TimeZone, delay);

        return delay;
    }

    /// <summary>
    /// Gets the configured time zone.
    /// </summary>
    public TimeZoneInfo GetTimeZone() => _options.GetTimeZone();

    /// <summary>
    /// Gets the configured run time.
    /// </summary>
    public TimeOnly GetRunTime() => _options.GetRunTime();
}
