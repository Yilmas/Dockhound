using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using System;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.XPath;
using WLL_Tracker.Logs;
using WLL_Tracker.Models;

namespace WLL_Tracker;


public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _handler;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly WllTrackerContext _dbContext;

    public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services, IConfiguration config, WllTrackerContext dbContext)
    {
        _client = client;
        _handler = handler;
        _services = services;
        _configuration = config;
        _dbContext = dbContext;
    }

    public async Task InitializeAsync()
    {
        _client.Ready += ReadyAsync;
        _handler.Log += LogAsync;

        await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        _client.InteractionCreated += HandleInteraction;
        _handler.InteractionExecuted += HandleInteractionExecute;
        _client.SelectMenuExecuted += SelectMenuExecuted;
        _client.ModalSubmitted += ModalSubmitted;
        _client.ButtonExecuted += ButtonExecuted;
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        await DeleteAllCommandsAsync();

        foreach (var item in _client.Guilds)
        {
            await _handler.RegisterCommandsToGuildAsync(guildId: item.Id, deleteMissing: true);
        }
    }

    public async Task DeleteAllCommandsAsync()
    {
        var globalCommands = await _client.Rest.GetGlobalApplicationCommands();
        foreach (var command in globalCommands)
        {
            await command.DeleteAsync();
        }

        foreach (var guild in _client.Guilds)
        {
            var guildCommands = await _client.Rest.GetGuildApplicationCommands(guild.Id);
            foreach (var command in guildCommands)
            {
                await command.DeleteAsync();
            }
        }
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
            var context = new SocketInteractionContext(_client, interaction);

            // Execute the incoming command.
            var result = await _handler.ExecuteCommandAsync(context, _services);

            // Due to async nature of InteractionFramework, the result here may always be success.
            // That's why we also need to handle the InteractionExecuted event.
            if (!result.IsSuccess)
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        await context.Interaction.RespondAsync($"Unmet Precondition: {result.ErrorReason}", ephemeral: true);
                        break;
                    case InteractionCommandError.UnknownCommand:
                        //await context.Interaction.RespondAsync("Unknown command", ephemeral: true);
                        break;
                    case InteractionCommandError.BadArgs:
                        await context.Interaction.RespondAsync("Invalid number or arguments", ephemeral: true);
                        break;
                    case InteractionCommandError.Exception:
                        await context.Interaction.RespondAsync($"Command exception: {result.ErrorReason}", ephemeral: true);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        await context.Interaction.RespondAsync("Command could not be executed", ephemeral: true);
                        break;
                    default:
                        break;
                }
        }
        catch
        {
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }

    private Task HandleInteractionExecute(ICommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
            switch (result.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    context.Interaction.RespondAsync($"Unmet Precondition: {result.ErrorReason}", ephemeral: true);
                    break;
                case InteractionCommandError.UnknownCommand:
                    //context.Interaction.RespondAsync("Unknown command", ephemeral: true);
                    break;
                case InteractionCommandError.BadArgs:
                    context.Interaction.RespondAsync("Invalid number or arguments", ephemeral: true);
                    break;
                case InteractionCommandError.Exception:
                    if (result.ErrorReason.Contains("valid DateTime"))
                    {
                        // Tell the user a valid format for dates
                        context.Interaction.RespondAsync($"**Date format error:** Please use **`2024-02-22` → `yyyy-MM-dd`** instead. Optional: With time via **`2024-02-22 15:30` → `yyyy-MM-dd HH:mm`**", ephemeral: true);
                    }
                    else
                    {
                        context.Interaction.RespondAsync($"Command exception: {result.ErrorReason}", ephemeral: true);
                    }
                    break;
                case InteractionCommandError.Unsuccessful:
                    context.Interaction.RespondAsync("Command could not be executed", ephemeral: true);
                    break;
                default:
                    break;
            }

        return Task.CompletedTask;
    }

    public async Task SelectMenuExecuted(SocketMessageComponent arg)
    {
        throw new NotImplementedException();
    }

    public async Task ButtonExecuted(SocketMessageComponent arg)
    {
        if (arg.Data.CustomId == "btn-container-count")
        {
            var msgEmbed = arg.Message.Embeds.First().ToEmbedBuilder();
            var countRed = msgEmbed.Fields.Single(x => x.Name.ToLower() == "red").Value;
            var countGreen = msgEmbed.Fields.Single(x => x.Name.ToLower() == "green").Value;
            var countBlue = msgEmbed.Fields.Single(x => x.Name.ToLower() == "blue").Value;
            var countDarkBlue = msgEmbed.Fields.Single(x => x.Name.ToLower() == "darkblue").Value;
            var countWhite = msgEmbed.Fields.Single(x => x.Name.ToLower() == "white").Value;

            var modal = new ModalBuilder()
                .WithTitle($"Update Container Count")
                .WithCustomId("update-count-modal")
                .AddTextInput("Red", $"update-count-red", value: countRed.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("Green", $"update-count-green", value: countGreen.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("Blue", $"update-count-blue", value: countBlue.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("Dark Blue", $"update-count-darkblue", value: countDarkBlue.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("White", $"update-count-white", value: countWhite.ToString(), minLength: 1, maxLength: 3);

            await arg.RespondWithModalAsync(modal.Build());
        }

        if (arg.Data.CustomId == "btn-whiteboard-update")
        {
            if (arg.GuildId == null)
            {
                await arg.RespondAsync("Must be used in a guild.", ephemeral: true);
                return;
            }

            var msgBoardEmbed = arg.Message.Embeds.First().ToEmbedBuilder();

            var lines = msgBoardEmbed.Description.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var modLines = lines.Take((lines.Length - 2));
            string boardValue = string.Join("\n", modLines);

            var modalBoard = new ModalBuilder()
                .WithTitle($"Add Message")
                .WithCustomId("update-whiteboard-modal")
                .AddTextInput("Message", "update-whiteboard-edit", TextInputStyle.Paragraph, value: boardValue, maxLength: 2048, required: true);

            await arg.RespondWithModalAsync(modalBoard.Build());
        }
    }

    public async Task ModalSubmitted(SocketModal arg)
    {
        long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        var comp = arg.Data.Components.First();

        // Count
        if (arg.Data.CustomId == "update-count-modal")
        {
            var red = arg.Data.Components.Single(x => x.CustomId == "update-count-red").Value;
            var green = arg.Data.Components.Single(x => x.CustomId == "update-count-green").Value;
            var blue = arg.Data.Components.Single(x => x.CustomId == "update-count-blue").Value;
            var darkblue = arg.Data.Components.Single(x => x.CustomId == "update-count-darkblue").Value;
            var white = arg.Data.Components.Single(x => x.CustomId == "update-count-white").Value;

            if (!Regex.IsMatch(red, @"^\d+$") ||
                !Regex.IsMatch(green, @"^\d+$") ||
                !Regex.IsMatch(blue, @"^\d+$") ||
                !Regex.IsMatch(darkblue, @"^\d+$") ||
                !Regex.IsMatch(white, @"^\d+$"))
            {
                await arg.RespondAsync("You must input a number!", ephemeral: true);
                return;
            }

            red = red == "0" ? "0" : red.TrimStart('0');
            green = green == "0" ? "0" : green.TrimStart('0');
            blue = blue == "0" ? "0" : blue.TrimStart('0');
            darkblue = darkblue == "0" ? "0" : darkblue.TrimStart('0');
            white = white == "0" ? "0" : white.TrimStart('0');


            var msgEmbed = arg.Message.Embeds.First().ToEmbedBuilder();
            msgEmbed.WithDescription("Last Updated by " + arg.User.Mention + " <t:" + seconds + ":R>");
            msgEmbed.Fields.Single(x => x.Name.ToLower() == "red").Value = red;
            msgEmbed.Fields.Single(x => x.Name.ToLower() == "green").Value = green;
            msgEmbed.Fields.Single(x => x.Name.ToLower() == "blue").Value = blue;
            msgEmbed.Fields.Single(x => x.Name.ToLower() == "darkblue").Value = darkblue;
            msgEmbed.Fields.Single(x => x.Name.ToLower() == "white").Value = white;

            try
            {
                await arg.UpdateAsync(x =>
                {
                    x.Embed = msgEmbed.Build();
                });
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                // Log event with updated fields, author, timestamp UTC

                var location = arg.Message.Embeds.First().Title;

                try
                {
                    var log = new LogEvent(
                        eventName: "Updated Yard Tracker",
                        messageId: arg.Message.Id,
                        username: arg.User.Username,
                        userId: arg.User.Id,
                        changes: $"Updated the count for {location}: Red: {red}, Green: {green}, Blue: {blue}, Dark Blue: {darkblue}, White: {white}"
                    );

                    _dbContext.LogEvents.Add(log);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[ERROR] Failed to log event: {e.Message}\n{e.StackTrace}");
                }
            }
            
        }

        // Whiteboard
        if (arg.Data.CustomId == "update-whiteboard-modal")
        {
            var msg = await arg.Channel.GetMessageAsync(arg.Message.Id);

            var msgEmbed = msg.Embeds.First().ToEmbedBuilder();
            msgEmbed.Description = (comp.Value == string.Empty ? "Waiting for squibbles ..." : comp.Value + "\n\n_Last Updated by " + arg.User.Mention + " <t:" + seconds + ":R>_");

            await arg.UpdateAsync(x =>
            {
                x.Embed = msgEmbed.Build();
            });

            var location = arg.Message.Embeds.First().Title;

            try
            {
                var log = new LogEvent(
                    eventName: "Updated Whiteboard",
                    messageId: arg.Message.Id,
                    username: arg.User.Username,
                    userId: arg.User.Id,
                    changes: $"Updated the whiteboard for {location}"
                );

                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log event: {e.Message}\n{e.StackTrace}");
            }
        }

    }
}
