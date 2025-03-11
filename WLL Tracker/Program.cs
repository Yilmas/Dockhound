using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.EntityFrameworkCore;
using WLL_Tracker.Logs;
using WLL_Tracker.Models;
using WLL_Tracker.Modules;
using Microsoft.ApplicationInsights.Extensibility;

namespace WLL_Tracker;

public class Program
{
    private static IConfiguration _configuration;
    private static IServiceProvider _services;

    private static readonly DiscordSocketConfig _socketConfig = new()
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
    };

    private static readonly InteractionServiceConfig _interactionServiceConfig = new()
    {
        //
    };
    
    public static async Task Main()
    {
        Console.WriteLine("[LOG] Starting WLL Tracker");

        _configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables(prefix: "WLL_")
            .Build();

        _services = new ServiceCollection()
            .AddSingleton(_configuration)
            .AddSingleton(_socketConfig)
            .AddSingleton<HttpClient>()
            .AddDbContext<WllTrackerContext>(options => options.UseSqlServer(_configuration["DBCONN"])) 
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(), _interactionServiceConfig))
            .AddSingleton<InteractionHandler>()
            .AddSingleton<TrackerModule>()
            .AddSingleton<TrackerModule.TrackerSetup>()
            .AddSingleton<VerificationModule>()
            .AddSingleton<VerificationModule.VerifySetup>()
            .AddSingleton<TelemetryConfiguration>(provider =>
            {
                var config = TelemetryConfiguration.CreateDefault();
                config.ConnectionString = _configuration["APPINSIGHTS_CONN"];
                return config;
            })
            .AddSingleton<TelemetryClient>(options =>
            {
                var telemetryConfig = options.GetRequiredService<TelemetryConfiguration>();
                return new TelemetryClient(telemetryConfig);
            })
            .BuildServiceProvider();

        var client = _services.GetRequiredService<DiscordSocketClient>();

        client.Log += LogAsync;

        AppDomain.CurrentDomain.UnhandledException += async (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                await using var scope = _services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<WllTrackerContext>();

                await ExceptionHandler.HandleExceptionAsync(ex, dbContext, "Unhandled global exception");
            }
        };

        await _services.GetRequiredService<InteractionHandler>().InitializeAsync();
        
        if (_configuration["TOKEN"] != null)
            Console.WriteLine("[LOG] Token Acquired!");
        else
        {
            Console.WriteLine("[ERROR] Token Missing!");
            Environment.Exit(1);
        }

        await client.LoginAsync(TokenType.Bot, _configuration["TOKEN"]);
        await client.StartAsync();

        _ = TrackPerformanceMetrics();

        await Task.Delay(Timeout.Infinite);

    }

    private static async Task TrackPerformanceMetrics()
    {
        while (true)
        {
            var telemetryClient = _services.GetRequiredService<TelemetryClient>();

            var process = Process.GetCurrentProcess();

            var memoryUsage = process.WorkingSet64 / (1024 * 1024);
            var cpuUsage = process.TotalProcessorTime.TotalMilliseconds;

            telemetryClient.Track;.GetMetric("MemoryUsageMB").TrackValue(memoryUsage);
            telemetryClient.GetMetric("CPUUsageMilliseconds").TrackValue(cpuUsage);

            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    private static Task LogAsync(LogMessage log)
    {
        var telemetryClient = _services.GetRequiredService<TelemetryClient>();

        if (log.Exception is not null)
        {
            telemetryClient.TrackException(log.Exception);
        }

        telemetryClient.TrackTrace(log.Message, log.Severity switch
        {
            LogSeverity.Critical => SeverityLevel.Critical,
            LogSeverity.Error => SeverityLevel.Error,
            LogSeverity.Warning => SeverityLevel.Warning,
            LogSeverity.Info => SeverityLevel.Information,
            LogSeverity.Verbose => SeverityLevel.Verbose,
            LogSeverity.Debug => SeverityLevel.Verbose,
            _ => SeverityLevel.Information
        });

        telemetryClient.TrackTrace($"[{log.Severity}] {log.Source}: {log.Message}");

        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}