using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPositionService.Configuration;

namespace PowerPositionService.Services;

/// <summary>
/// Manages the file-based durable work queue.
/// Uses filesystem directories: pending/, done/, out/
/// </summary>
public class JobQueueService
{
    private readonly ILogger<JobQueueService> _logger;
    private readonly string _pendingDir;
    private readonly string _doneDir;
    private readonly string _outDir;

    public JobQueueService(
        IOptions<PowerPositionOptions> options,
        ILogger<JobQueueService> logger)
    {
        _logger = logger;
        
        var baseDir = Path.GetFullPath(options.Value.OutputDirectory);
        _pendingDir = Path.Combine(baseDir, "pending");
        _doneDir = Path.Combine(baseDir, "done");
        _outDir = Path.Combine(baseDir, "out");

        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Gets the output directory path.
    /// </summary>
    public string OutputDirectory => _outDir;

    /// <summary>
    /// Ensures the required directories exist.
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_pendingDir);
        Directory.CreateDirectory(_doneDir);
        Directory.CreateDirectory(_outDir);
        _logger.LogDebug("Ensured directories exist: pending={Pending}, done={Done}, out={Out}", 
            _pendingDir, _doneDir, _outDir);
    }

    /// <summary>
    /// Checks if a power day has already been completed.
    /// </summary>
    public bool IsDone(DateTime powerDay)
    {
        var doneFile = GetDoneFilePath(powerDay);
        return File.Exists(doneFile);
    }

    /// <summary>
    /// Checks if a power day is already pending.
    /// </summary>
    public bool IsPending(DateTime powerDay)
    {
        var pendingFile = GetPendingFilePath(powerDay);
        return File.Exists(pendingFile);
    }

    /// <summary>
    /// Ensures a job file exists in the pending directory for the given power day.
    /// </summary>
    public void EnsurePending(DateTime powerDay)
    {
        if (IsDone(powerDay))
        {
            _logger.LogDebug("Power day {PowerDay:yyyyMMdd} already done, skipping pending creation", powerDay);
            return;
        }

        var pendingFile = GetPendingFilePath(powerDay);
        if (!File.Exists(pendingFile))
        {
            // Write creation timestamp to job file for audit purposes
            File.WriteAllText(pendingFile, $"Created: {DateTime.UtcNow:O}");
            _logger.LogInformation("Created pending job for power day {PowerDay:yyyyMMdd}", powerDay);
        }
    }

    /// <summary>
    /// Gets all pending power days sorted oldest to newest.
    /// </summary>
    public IReadOnlyList<DateTime> GetPendingPowerDays()
    {
        var pendingFiles = Directory.GetFiles(_pendingDir, "*.job")
            .OrderBy(f => f)
            .ToList();

        var powerDays = new List<DateTime>();
        foreach (var file in pendingFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(fileName, "yyyyMMdd", null, 
                System.Globalization.DateTimeStyles.None, out var powerDay))
            {
                powerDays.Add(powerDay);
            }
            else
            {
                _logger.LogWarning("Invalid job file name: {FileName}", file);
            }
        }

        _logger.LogDebug("Found {Count} pending power days", powerDays.Count);
        return powerDays;
    }

    /// <summary>
    /// Checks if the output CSV already exists for a power day.
    /// </summary>
    public bool OutputExists(DateTime powerDay)
    {
        var outputFile = GetOutputFilePath(powerDay);
        return File.Exists(outputFile);
    }

    /// <summary>
    /// Gets the output file path for a power day.
    /// </summary>
    public string GetOutputFilePath(DateTime powerDay)
    {
        return Path.Combine(_outDir, $"PowerPosition_{powerDay:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Marks a power day as completed by moving its job file to the done directory.
    /// </summary>
    public void MarkDone(DateTime powerDay)
    {
        var pendingFile = GetPendingFilePath(powerDay);
        var doneFile = GetDoneFilePath(powerDay);

        if (File.Exists(pendingFile))
        {
            // Append completion timestamp
            var content = File.ReadAllText(pendingFile);
            content += $"\nCompleted: {DateTime.UtcNow:O}";
            
            // Move to done (write new, delete old for cross-volume support)
            File.WriteAllText(doneFile, content);
            File.Delete(pendingFile);
            
            _logger.LogInformation("Marked power day {PowerDay:yyyyMMdd} as done", powerDay);
        }
        else if (!File.Exists(doneFile))
        {
            // Create done file even if pending didn't exist (recovery scenario)
            File.WriteAllText(doneFile, $"Completed: {DateTime.UtcNow:O}");
            _logger.LogInformation("Created done marker for power day {PowerDay:yyyyMMdd}", powerDay);
        }
    }

    private string GetPendingFilePath(DateTime powerDay)
    {
        return Path.Combine(_pendingDir, $"{powerDay:yyyyMMdd}.job");
    }

    private string GetDoneFilePath(DateTime powerDay)
    {
        return Path.Combine(_doneDir, $"{powerDay:yyyyMMdd}.job");
    }
}
