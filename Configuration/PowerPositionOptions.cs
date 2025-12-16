namespace PowerPositionService.Configuration;

/// <summary>
/// Configuration options for the Power Position Service.
/// </summary>
public class PowerPositionOptions
{
    public const string SectionName = "PowerPositionService";

    /// <summary>
    /// Daily run time in HH:mm format (24-hour, London local time).
    /// Default: 23:05
    /// </summary>
    public string DailyRunTime { get; set; } = "23:05";

    /// <summary>
    /// IANA time zone identifier for scheduling and power day calculation.
    /// Default: Europe/London
    /// </summary>
    public string TimeZone { get; set; } = "Europe/London";

    /// <summary>
    /// Base URL for the PowerDay API.
    /// </summary>
    public string PowerDayApiUrl { get; set; } = "http://example";

    /// <summary>
    /// Root directory for output files and state directories.
    /// Relative paths are resolved from the application directory.
    /// </summary>
    public string OutputDirectory { get; set; } = ".";

    /// <summary>
    /// Enable file-based logging in addition to console output.
    /// </summary>
    public bool EnableFileLog { get; set; } = true;

    /// <summary>
    /// Directory for log files when EnableFileLog is true.
    /// </summary>
    public string LogDirectory { get; set; } = ".";

    /// <summary>
    /// Gets the parsed daily run time as TimeOnly.
    /// </summary>
    public TimeOnly GetRunTime()
    {
        if (TimeOnly.TryParse(DailyRunTime, out var time))
            return time;
        return new TimeOnly(23, 5);
    }

    /// <summary>
    /// Gets the configured time zone info.
    /// </summary>
    public TimeZoneInfo GetTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch
        {
            // Fallback for Windows time zone IDs
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
    }
}
