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
    private readonly DockhoundContext _dbContext;
    private readonly AppSettings _settings;

    public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services, IConfiguration config, DockhoundContext dbContext, IOptions<AppSettings> appSettings)
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

        if (_settings.Configuration.Environment == EnvironmentState.Development)
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

    private static string Trunc(string s, int max)
    => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> cachechannel, SocketReaction reaction)
    {
        if (reaction.UserId == _client.CurrentUser.Id) return;

        if (reaction.Emote.Name == "🔖" || (reaction.Emote is Emote e && e.Name == "🔖"))
        {
            var message = await cacheable.GetOrDownloadAsync();
            var channel = await cachechannel.GetOrDownloadAsync();
            var user = reaction.User.IsSpecified ? reaction.User.Value : await reaction.Channel.GetUserAsync(reaction.UserId);

            // Jump link (guild only)
            string link = "";
            if (channel is IGuildChannel gch)
                link = $"https://discord.com/channels/{gch.GuildId}/{channel.Id}/{message.Id}";

            // Source embed (if any)
            var srcEmbed = message.Embeds?.FirstOrDefault();
            var srcEb = srcEmbed?.ToEmbedBuilder();

            // Description prefers message content; else embed description; else ZWSP
            string baseText = !string.IsNullOrWhiteSpace(message.Content)
                ? Trunc(message.Content.Trim(), 1000)
                : (!string.IsNullOrWhiteSpace(srcEb?.Description) ? Trunc(srcEb.Description, 1000) : "\u200B");

            var eb = new EmbedBuilder()
                .WithUrl(string.IsNullOrEmpty(link) ? null : link)
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName(message.Author.Username)
                    .WithIconUrl((message.Author as IUser)?.GetAvatarUrl() ?? (message.Author as IUser)?.GetDefaultAvatarUrl()))
                .WithDescription($"{baseText}\n\n[Jump to message]({(string.IsNullOrEmpty(link) ? "#" : link)})")
                .WithTimestamp(message.Timestamp);

            // Title ONLY when the original message had an embed
            if (srcEmbed != null)
            {
                string title =
                    !string.IsNullOrWhiteSpace(srcEb?.Title) ? Trunc(srcEb.Title, 256) :
                    !string.IsNullOrWhiteSpace(message.Content) ? $"Bookmark: {Trunc(message.Content.Trim(), 80)}" :
                    $"Bookmark from {message.Author.Username}";
                eb.WithTitle(title);
            }

            if (srcEb != null)
            {
                // Thumbnail (prefer source thumbnail, else source author icon)
                if (!string.IsNullOrWhiteSpace(srcEb.ThumbnailUrl))
                    eb.WithThumbnailUrl(srcEb.ThumbnailUrl);
                else if (srcEb.Author != null && !string.IsNullOrWhiteSpace(srcEb.Author.IconUrl))
                    eb.WithThumbnailUrl(srcEb.Author.IconUrl);

                // Image
                if (!string.IsNullOrWhiteSpace(srcEb.ImageUrl))
                    eb.WithImageUrl(srcEb.ImageUrl);

                // Optional: keep color for a visual hint
                if (srcEb.Color.HasValue)
                    eb.WithColor(srcEb.Color.Value);

                // Copy up to 2 fields from the ORIGINAL embed
                if (srcEmbed.Fields != null)
                {
                    foreach (var f in srcEmbed.Fields.Take(2))
                    {
                        var name = Trunc(f.Name ?? "Field", 256);
                        var val = Trunc(f.Value ?? string.Empty, 1024);
                        if (!string.IsNullOrWhiteSpace(val))
                            eb.AddField(name, val, f.Inline);
                    }
                }
            }

            // If no image yet, attach first image attachment
            if (eb.ImageUrl == null && message.Attachments?.Count > 0)
            {
                var img = message.Attachments.FirstOrDefault(a =>
                    (a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    a.Filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    a.Filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    a.Filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    a.Filename.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));
                if (img != null) eb.WithImageUrl(img.Url);
            }

            var guildId = (channel as IGuildChannel)?.GuildId ?? 0;
            var channelId = channel.Id;
            var messageId = message.Id;
            var userId = reaction.UserId;

            var components = new ComponentBuilder()
                .WithButton("❌", $"btn-remove-bookmark:{guildId}:{channelId}:{messageId}:{userId}", ButtonStyle.Secondary)
                .Build();

            try
            {
                await user.SendMessageAsync(embed: eb.Build(), components: components);
            }
            catch
            {
                // DMs might be closed
            }

            // Log request
            try
            {
                var log = new LogEvent(
                    eventName: "User Bookmarked a Message",
                    messageId: messageId,
                    username: user.Username,
                    userId: user.Id,
                    changes: $"User {user.Username} bookmarked {messageId}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to log bookmark request: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }



}
