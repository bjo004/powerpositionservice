using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPositionService.Configuration;

namespace PowerPositionService.Services;

/// <summary>
/// Orchestrates the power position extraction process.
/// </summary>
public class JobProcessor
{
    private readonly IPowerService _powerService;
    private readonly JobQueueService _jobQueue;
    private readonly PositionAggregator _aggregator;
    private readonly CsvWriter _csvWriter;
    private readonly PowerPositionOptions _options;
    private readonly ILogger<JobProcessor> _logger;

    public JobProcessor(
        IPowerService powerService,
        JobQueueService jobQueue,
        PositionAggregator aggregator,
        CsvWriter csvWriter,
        IOptions<PowerPositionOptions> options,
        ILogger<JobProcessor> logger)
    {
        _powerService = powerService;
        _jobQueue = jobQueue;
        _aggregator = aggregator;
        _csvWriter = csvWriter;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executes the daily extraction process.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting daily extraction run");

        try
        {
            // Step 1: Calculate next power day based on London time
            var powerDay = CalculateNextPowerDay();
            _logger.LogInformation("Target power day: {PowerDay:yyyy-MM-dd}", powerDay);

            // Step 2: Ensure pending job exists for next power day (if not already done)
            if (!_jobQueue.IsDone(powerDay))
            {
                _jobQueue.EnsurePending(powerDay);
            }

            // Step 3: Process all pending jobs (oldest first)
            var pendingDays = _jobQueue.GetPendingPowerDays();
            _logger.LogInformation("Found {Count} pending power days to process", pendingDays.Count);

            foreach (var pendingDay in pendingDays)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var success = await ProcessPowerDayAsync(pendingDay, cancellationToken);
                if (!success)
                {
                    _logger.LogWarning("Stopping processing due to failure on power day {PowerDay:yyyy-MM-dd}", pendingDay);
                    break;
                }
            }

            _logger.LogInformation("Daily extraction run complete");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Extraction run was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction run failed with unexpected error");
            throw;
        }
    }

    /// <summary>
    /// Calculates the next power day based on London local time.
    /// Power day = today (London) + 1
    /// </summary>
    private DateTime CalculateNextPowerDay()
    {
        var tz = _options.GetTimeZone();
        var londonNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var today = londonNow.Date;
        var powerDay = today.AddDays(1);
        
        _logger.LogDebug("London time: {LondonTime}, Today: {Today}, Power day: {PowerDay}",
            londonNow, today, powerDay);
        
        return powerDay;
    }

    /// <summary>
    /// Processes a single power day.
    /// </summary>
    /// <returns>True if successful, false if processing should stop.</returns>
    private async Task<bool> ProcessPowerDayAsync(DateTime powerDay, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing power day {PowerDay:yyyy-MM-dd}", powerDay);

        var outputPath = _jobQueue.GetOutputFilePath(powerDay);

        // Check if output already exists (idempotency)
        if (_jobQueue.OutputExists(powerDay))
        {
            _logger.LogInformation("Output CSV already exists for {PowerDay:yyyy-MM-dd}, marking as done", powerDay);
            _jobQueue.MarkDone(powerDay);
            return true;
        }

        try
        {
            // Fetch trades from API
            var trades = await _powerService.GetTradesAsync(powerDay, cancellationToken);
            var tradeList = trades.ToList();
            
            if (tradeList.Count == 0)
            {
                _logger.LogWarning("No trades returned for power day {PowerDay:yyyy-MM-dd}", powerDay);
            }

            // Aggregate positions
            var positions = _aggregator.Aggregate(tradeList);

            // Write CSV atomically
            _csvWriter.WriteAtomic(positions, outputPath);

            // Mark as done
            _jobQueue.MarkDone(powerDay);

            _logger.LogInformation("Successfully processed power day {PowerDay:yyyy-MM-dd}", powerDay);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "API call failed for power day {PowerDay:yyyy-MM-dd}, will retry later", powerDay);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process power day {PowerDay:yyyy-MM-dd}", powerDay);
            return false;
        }
    }
}
