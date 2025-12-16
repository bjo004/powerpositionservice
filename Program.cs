using PowerPositionService;
using PowerPositionService.Configuration;
using PowerPositionService.Services;
using Serilog;
using Serilog.Events;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Load configuration
    var config = builder.Configuration
        .GetSection(PowerPositionOptions.SectionName)
        .Get<PowerPositionOptions>() ?? new PowerPositionOptions();

    // Configure Serilog (single configuration, no bootstrap logger)
    var loggerConfig = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

    if (config.EnableFileLog)
    {
        var logDir = Path.GetFullPath(config.LogDirectory);
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "PowerPositionService-.log");

        loggerConfig.WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    }

    Log.Logger = loggerConfig.CreateLogger();
    builder.Services.AddSerilog();

    Log.Information("Starting Power Position Service");

    // Bind configuration
    builder.Services.Configure<PowerPositionOptions>(
        builder.Configuration.GetSection(PowerPositionOptions.SectionName));

    // Register services
    builder.Services.AddSingleton<Scheduler>();
    builder.Services.AddSingleton<JobQueueService>();
    builder.Services.AddSingleton<PositionAggregator>();
    builder.Services.AddSingleton<CsvWriter>();
    builder.Services.AddSingleton<JobProcessor>();

    // Register power service
    // Use MockPowerService for testing, or PowerService for production
    if (config.PowerDayApiUrl == "http://example" || config.PowerDayApiUrl.Contains("mock"))
    {
        Log.Warning("Using MockPowerService - configure PowerDayApiUrl for production");
        builder.Services.AddSingleton<IPowerService, MockPowerService>();
    }
    else
    {
        builder.Services.AddHttpClient<IPowerService, PowerService>();
    }

    // Register the worker
    builder.Services.AddHostedService<Worker>();

    // Configure as Windows Service
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Power Position Service";
    });

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;