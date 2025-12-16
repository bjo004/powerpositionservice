using PowerPositionService.Services;

namespace PowerPositionService;

/// <summary>
/// Background worker service that executes the power position extraction daily.
/// Designed to run as a Windows Service.
/// </summary>
public class Worker : BackgroundService
{
    private const string MutexName = "Global\\PowerPositionService_SingleInstance";
    
    private readonly Scheduler _scheduler;
    private readonly JobProcessor _jobProcessor;
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private Mutex? _mutex;

    public Worker(
        Scheduler scheduler,
        JobProcessor jobProcessor,
        ILogger<Worker> logger,
        IHostApplicationLifetime appLifetime)
    {
        _scheduler = scheduler;
        _jobProcessor = jobProcessor;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Power Position Service starting");
        _logger.LogInformation("Configured run time: {RunTime} {TimeZone}",
            _scheduler.GetRunTime(), _scheduler.GetTimeZone().Id);

        // Acquire single-instance mutex
        if (!TryAcquireMutex())
        {
            _logger.LogError("Another instance is already running. Exiting.");
            _appLifetime.StopApplication();
            return;
        }

        try
        {
            // Run on startup to catch up any missed days
            _logger.LogInformation("Running startup extraction to process any pending jobs");
            await RunExtractionAsync(stoppingToken);

            // Then wait for scheduled runs
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = _scheduler.GetDelayUntilNextRun();
                
                _logger.LogDebug("Waiting {Delay} until next run", delay);
                
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await RunExtractionAsync(stoppingToken);
                }
            }
        }
        finally
        {
            ReleaseMutex();
        }

        _logger.LogInformation("Power Position Service stopping");
    }

    private async Task RunExtractionAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _jobProcessor.ExecuteAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction run failed. Will retry at next scheduled time.");
        }
    }

    private bool TryAcquireMutex()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            
            if (!createdNew)
            {
                // Mutex already exists, try to acquire it with timeout
                if (!_mutex.WaitOne(TimeSpan.Zero))
                {
                    _mutex.Dispose();
                    _mutex = null;
                    return false;
                }
            }
            
            _logger.LogDebug("Acquired single-instance mutex");
            return true;
        }
        catch (AbandonedMutexException)
        {
            // Previous instance crashed, we now own the mutex
            _logger.LogWarning("Acquired abandoned mutex from crashed instance");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire mutex");
            return false;
        }
    }

    private void ReleaseMutex()
    {
        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _logger.LogDebug("Released single-instance mutex");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error releasing mutex");
            }
            _mutex = null;
        }
    }

    public override void Dispose()
    {
        ReleaseMutex();
        base.Dispose();
    }
}
