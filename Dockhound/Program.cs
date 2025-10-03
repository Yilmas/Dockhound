using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Dockhound.Logs;
using Dockhound.Models;
using Dockhound.Modules;
using Dockhound.Interactions;
using Dockhound.Config;
using Dockhound.Services;

namespace Dockhound;

public class Program
{
    private static IConfiguration _configuration;
    private static IServiceProvider _services;

    private static readonly DiscordSocketConfig _socketConfig = new()
    {
        GatewayIntents = GatewayIntents.Guilds
                        | GatewayIntents.GuildMembers 
                        | GatewayIntents.GuildMessages 
                        | GatewayIntents.GuildMessageReactions 
                        | GatewayIntents.DirectMessages 
                        | GatewayIntents.MessageContent,
        MessageCacheSize = 100,
        AlwaysDownloadUsers = false,
        DefaultRetryMode = RetryMode.RetryRatelimit
        //GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
    };

    private static readonly InteractionServiceConfig _interactionServiceConfig = new()
    {
        DefaultRunMode = RunMode.Async,
        UseCompiledLambda = true,
        ThrowOnError = false
    };
    
    public static async Task Main()
    {
        Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [LOG] Starting Dockhound");

        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "DOCK_")
            .Build();

        var services = new ServiceCollection()
            .Configure<AppSettings>(_configuration)
            .AddSingleton(_configuration)
            .AddSingleton(_socketConfig)
            .AddSingleton<HttpClient>()
            .AddDbContextFactory<DockhoundContext>(options => options.UseSqlServer(_configuration["Configuration:DatabaseConnectionString"])) 
            .AddMemoryCache()
            .Configure<GuildDefaults>(p => p.Value = new GuildConfig())
            .AddSingleton<IAppSettingsService, AppSettingsService>()
            .AddSingleton<IGuildSettingsService, GuildSettingsService>()
            .AddSingleton<IVerificationHistoryService, VerificationHistoryService>()
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(), _interactionServiceConfig))
            .AddSingleton<InteractionHandler>();

        bool enableTelemetry = !string.IsNullOrEmpty(_configuration["Configuration:AppInsightsConnectionString"]);

        if (enableTelemetry)
        {
            services.AddSingleton<TelemetryConfiguration>(provider =>
            {
                var config = TelemetryConfiguration.CreateDefault();
                config.ConnectionString = _configuration["Configuration:AppInsightsConnectionString"];
                return config;
            });

            services.AddSingleton<TelemetryClient>(provider =>
            {
                var telemetryConfig = provider.GetRequiredService<TelemetryConfiguration>();
                return new TelemetryClient(telemetryConfig);
            });
        }
        else
        {
            services.AddSingleton<TelemetryClient>(new TelemetryClient());
            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [LOG] Application Insights Telemetry Disabled.");
        }

        _services = services.BuildServiceProvider();

        var client = _services.GetRequiredService<DiscordSocketClient>();

        client.Log += LogAsync;

        AppDomain.CurrentDomain.UnhandledException += async (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                await using var scope = _services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DockhoundContext>();

                await ExceptionHandler.HandleExceptionAsync(ex, dbContext, "Unhandled global exception");
            }
        };

        await _services.GetRequiredService<InteractionHandler>().InitializeAsync();
        
        if (_configuration["Configuration:DiscordToken"] != null)
            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [LOG] Token Acquired!");
        else
        {
            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [ERROR] Token Missing!");
            Environment.Exit(1);
        }

        await client.LoginAsync(TokenType.Bot, _configuration["Configuration:DiscordToken"]);
        await client.StartAsync();

        _ = TrackPerformanceMetrics();

        await Task.Delay(Timeout.Infinite);

    }

    private static async Task TrackPerformanceMetrics()
    {
        var telemetryClient = _services.GetRequiredService<TelemetryClient>();

        if (!telemetryClient.IsEnabled()) return; // Skip telemetry if disabled

        while (true)
        {
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64 / (1024 * 1024);
            var cpuUsage = process.TotalProcessorTime.TotalMilliseconds;

            telemetryClient.GetMetric("MemoryUsageMB").TrackValue(memoryUsage);
            telemetryClient.GetMetric("CPUUsageMilliseconds").TrackValue(cpuUsage);

            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    private static Task LogAsync(LogMessage log)
    {
        var telemetryClient = _services.GetRequiredService<TelemetryClient>();

        if (!telemetryClient.IsEnabled())
        {
            // Skip telemetry if disabled
            Console.WriteLine($"{log.ToString()}");
            return Task.CompletedTask; 
        }

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

        Console.WriteLine($"{log.ToString()}");
        return Task.CompletedTask;
    }
}