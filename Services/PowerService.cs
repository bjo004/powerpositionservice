using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPositionService.Configuration;
using PowerPositionService.Models;

namespace PowerPositionService.Services;

/// <summary>
/// Implementation of IPowerService that calls the PowerDay API.
/// </summary>
public class PowerService : IPowerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PowerService> _logger;
    private readonly PowerPositionOptions _options;

    public PowerService(
        HttpClient httpClient,
        IOptions<PowerPositionOptions> options,
        ILogger<PowerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.PowerDayApiUrl);
    }

    public async Task<IEnumerable<Trade>> GetTradesAsync(DateTime powerDay, CancellationToken cancellationToken = default)
    {
        var dateStr = powerDay.ToString("yyyy-MM-dd");
        _logger.LogInformation("Fetching trades for power day {PowerDay} from API", dateStr);

        try
        {
            // Expected API endpoint: GET /trades?powerDay=2024-01-15
            var response = await _httpClient.GetAsync(
                $"/trades?powerDay={dateStr}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var trades = await response.Content.ReadFromJsonAsync<List<Trade>>(cancellationToken: cancellationToken);
            
            _logger.LogInformation("Retrieved {TradeCount} trades for power day {PowerDay}", 
                trades?.Count ?? 0, dateStr);

            return trades ?? Enumerable.Empty<Trade>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "API request failed for power day {PowerDay}", dateStr);
            throw;
        }
    }
}

/// <summary>
/// Mock implementation of IPowerService for testing without a real API.
/// Generates sample trade data.
/// </summary>
public class MockPowerService : IPowerService
{
    private readonly ILogger<MockPowerService> _logger;
    private readonly Random _random = new(42);

    public MockPowerService(ILogger<MockPowerService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<Trade>> GetTradesAsync(DateTime powerDay, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockPowerService: Generating sample trades for power day {PowerDay:yyyy-MM-dd}", powerDay);

        // Generate 3-5 sample trades
        var tradeCount = _random.Next(3, 6);
        var trades = new List<Trade>();

        for (int i = 0; i < tradeCount; i++)
        {
            var periods = new double[24];
            for (int p = 0; p < 24; p++)
            {
                // Generate realistic-looking power volumes (50-500 MW)
                periods[p] = Math.Round(_random.NextDouble() * 450 + 50, 2);
            }

            trades.Add(new Trade
            {
                TradeId = $"TRADE-{powerDay:yyyyMMdd}-{i + 1:D3}",
                PowerDay = powerDay,
                Periods = periods
            });
        }

        _logger.LogInformation("MockPowerService: Generated {TradeCount} trades", tradeCount);
        return Task.FromResult<IEnumerable<Trade>>(trades);
    }
}
