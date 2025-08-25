using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Enums;
using Dockhound.Logs;
using Dockhound.Modals;
using Dockhound.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using System;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.XPath;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Dockhound;


public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _handler;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly WllTrackerContext _dbContext;
    private readonly AppSettings _settings;

    public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services, IConfiguration config, WllTrackerContext dbContext, IOptions<AppSettings> appSettings)
    {
        _client = client;
        _handler = handler;
        _services = services;
        _configuration = config;
        _dbContext = dbContext;
        _settings = appSettings.Value;
    }

    public async Task InitializeAsync()
    {
        _client.Ready += ReadyAsync;
        _handler.Log += LogAsync;

        await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        _client.InteractionCreated += HandleInteraction;
        _handler.InteractionExecuted += HandleInteractionExecute;
        _client.ReactionAdded += OnReactionAddedAsync;
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        await _client.SetActivityAsync(new Game("user requests", ActivityType.Listening));

        if(AppSettingsService.GetCurrentEnvironment() == EnvironmentState.Development)
        {
            await DeleteAllCommandsAsync();
        }

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

    // TODO: Applicant role assignment button (disabled for now)
    //public async Task ButtonExecuted(SocketMessageComponent arg)
    //{
    //    if (arg.Data.CustomId is "assign_applicant")
    //    {
    //        await arg.DeferAsync(ephemeral: true);

    //        ulong targetUserId = arg.User.Id;

    //        if (arg.GuildId == null)
    //        {
    //            await arg.FollowupAsync("This command must be used in a server.", ephemeral: true);
    //            return;
    //        }

    //        var guild = (arg.Channel as SocketGuildChannel)?.Guild;
    //        var guildUser = guild?.GetUser(targetUserId);
    //        var actingUser = guild?.GetUser(arg.User.Id);

    //        if (guildUser == null || actingUser == null)
    //        {
    //            await arg.FollowupAsync("User not found.", ephemeral: true);
    //            return;
    //        }

    //        // Retrieve allowed roles from configuration
    //        var allowedRoleIds = _configuration["ALLOWED_APPLICANT_ASSIGNER_ROLES"]
    //            ?.Split(',')
    //            .Select(id => ulong.TryParse(id, out var roleId) ? roleId : (ulong?)null)
    //            .Where(id => id.HasValue)
    //            .Select(id => id.Value)
    //            .ToList() ?? new List<ulong>();

    //        bool canAssign = arg.User.Id == targetUserId || actingUser.Roles.Any(r => allowedRoleIds.Contains(r.Id));

    //        if (!canAssign)
    //        {
    //            await arg.FollowupAsync("❌ You do not have permission to assign the **Applicant** role.", ephemeral: true);
    //            return;
    //        }

    //        // Determine faction
    //        var factionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Faction");
    //        string faction = guildUser.Roles.Any(r => r.Id == factionRole.Colonial) ? "Colonial"
    //                      : guildUser.Roles.Any(r => r.Id == factionRole.Warden) ? "Warden"
    //                      : string.Empty;

    //        var applicantFactionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Applicant");
    //        var rolesToAssign = new List<ulong> { applicantFactionRole.Generic };

    //        if (faction == "Colonial") rolesToAssign.Add(applicantFactionRole.Colonial);
    //        if (faction == "Warden") rolesToAssign.Add(applicantFactionRole.Warden);

    //        if (rolesToAssign.Any())
    //            await guildUser.AddRolesAsync(rolesToAssign);

    //        // Forum channel
    //        if (!ulong.TryParse(_configuration["CHANNEL_APPLICANT_FORUM"], out ulong forumChannelId) ||
    //            guild.GetChannel(forumChannelId) is not SocketForumChannel forumChannel)
    //        {
    //            await arg.FollowupAsync("Forum channel not found or configuration error.", ephemeral: true);
    //            return;
    //        }

    //        // Embed
    //        var embedBuilder = new EmbedBuilder()
    //            .WithTitle("Applicant Promotion")
    //            .WithDescription($"{guildUser.Mention} has been assigned the **Applicant** role. Use this thread to discuss their applicant promotion.")
    //            .WithThumbnailUrl(guildUser.GetAvatarUrl())
    //            .WithColor(Color.Green);

    //        if (arg.User.Id != guildUser.Id)
    //            embedBuilder.AddField("Applicant By", arg.User.Mention, false);

    //        // Forum tag
    //        ulong.TryParse(_configuration["CHANNEL_APPLICANT_FORUM_PENDINGTAG"], out ulong tagId);
    //        var tag = forumChannel.Tags.FirstOrDefault(p => p.Id == tagId);

    //        var thread = await forumChannel.CreatePostAsync(
    //            title: guildUser.DisplayName,
    //            tags: tag != null ? [tag] : null,
    //            embed: embedBuilder.Build()
    //        );

    //        await arg.FollowupAsync($"✅ Assigned **Applicant** to {guildUser.Mention}. Created applicant thread: {thread.Mention}.", ephemeral: true);
    //    }
    //}

    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> cachechannel, SocketReaction reaction)
    {
        if(reaction.UserId == _client.CurrentUser.Id)
            return;


        if (reaction.Emote.Name == "🔖" || (reaction.Emote is Emote emote && emote.Name == "🔖"))
        {
            string link = string.Empty;
            var message = await cacheable.GetOrDownloadAsync();
            var channel = await cachechannel.GetOrDownloadAsync();

            var user = reaction.User.IsSpecified ? reaction.User.Value : await reaction.Channel.GetUserAsync(reaction.UserId);

            if (channel is IGuildChannel guildChannel)
            {
                link = $"https://discord.com/channels/{guildChannel.GuildId}/{channel.Id}/{message.Id}";
            }

            string content = string.IsNullOrWhiteSpace(message.Content)
                ? "\u200B"
                : (message.Content.Length > 500
                    ? message.Content.Substring(0, 500) + "…"
                    : message.Content);

            var embed = new EmbedBuilder()
                .WithAuthor(message.Author)
                .WithDescription($"{content}\n\n[Jump to message]({link})")
                .WithTimestamp(message.Timestamp)
                .Build();


            var button = new ComponentBuilder()
                .WithButton("❌", $"btn-remove-bookmark", ButtonStyle.Secondary);

            await user.SendMessageAsync(embed: embed, components: button.Build());
        }
    }
}
