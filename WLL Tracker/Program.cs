using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Entity;
using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WLL_Tracker.Logs;
using WLL_Tracker.Models;
using WLL_Tracker.Modules;

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
            .AddDbContext<WllTrackerContext>(options => options.UseSqlServer(_configuration["dbconn"])) 
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(), _interactionServiceConfig))
            .AddSingleton<InteractionHandler>()
            .AddSingleton<CommandModule>()
            .AddSingleton<CommandModule.GroupSetup>()
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
        
        if (_configuration["token"] != null)
            Console.WriteLine("[LOG] Token Acquired!");
        else
        {
            Console.WriteLine("[ERROR] Token Missing!");
            Environment.Exit(1);
        }

        await client.LoginAsync(TokenType.Bot, _configuration["token"]);
        await client.StartAsync();

        await Task.Delay(Timeout.Infinite);

    }

    private static Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}