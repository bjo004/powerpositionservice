using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using PowerPositionService.Models;

namespace PowerPositionService.Services;

/// <summary>
/// Writes power positions to CSV files with atomic write guarantees.
/// </summary>
public class CsvWriter
{
    private readonly ILogger<CsvWriter> _logger;

    public CsvWriter(ILogger<CsvWriter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Writes power positions to a CSV file atomically.
    /// Uses temp file + rename pattern to ensure consumers never see partial files.
    /// </summary>
    /// <param name="positions">The positions to write.</param>
    /// <param name="outputPath">The final output file path.</param>
    public void WriteAtomic(IReadOnlyList<PowerPosition> positions, string outputPath)
    {
        if (positions.Count != 24)
        {
            throw new ArgumentException($"Expected 24 positions, got {positions.Count}", nameof(positions));
        }

        var outputDir = Path.GetDirectoryName(outputPath) ?? ".";
        var tempPath = Path.Combine(outputDir, $".tmp_{Guid.NewGuid():N}.csv");

        _logger.LogDebug("Writing CSV to temp file: {TempPath}", tempPath);

        try
        {
            // Write to temp file
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
            {
                // Write header
                writer.WriteLine("Local Time,Volume");

                // Write rows in period order (23:00, 00:00, 01:00, ..., 22:00)
                foreach (var position in positions.OrderBy(p => p.Period))
                {
                    var timeStr = position.LocalTime.ToString("HH:mm", CultureInfo.InvariantCulture);
                    var volumeStr = position.Volume.ToString("F2", CultureInfo.InvariantCulture);
                    writer.WriteLine($"{timeStr},{volumeStr}");
                }
            }

            // Atomic move/replace
            if (File.Exists(outputPath))
            {
                _logger.LogDebug("Replacing existing file: {OutputPath}", outputPath);
                File.Replace(tempPath, outputPath, null);
            }
            else
            {
                _logger.LogDebug("Moving temp file to: {OutputPath}", outputPath);
                File.Move(tempPath, outputPath);
            }

            _logger.LogInformation("Successfully wrote CSV: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write CSV: {OutputPath}", outputPath);
            
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }

            throw;
        }
    }
}
