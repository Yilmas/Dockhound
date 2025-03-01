using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WLL_Tracker.Enums;
using System.Reflection.Emit;
using Discord.Rest;
using WLL_Tracker.Extensions;
using WLL_Tracker.Logs;
using System.IO;
using System.Collections;
using System.Threading.Channels;
using WLL_Tracker.Models;

namespace WLL_Tracker.Modules;

public class CommandModule : InteractionModuleBase<SocketInteractionContext>
{
    public InteractionService Commands { get; set; }

    private InteractionHandler _handler;

    public CommandModule(InteractionHandler handler)
    {
        _handler = handler;
    }

    [CommandContextType(InteractionContextType.Guild)]
    [Group("tracker", "Root command of Dockhound")]
    public class GroupSetup : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WllTrackerContext _dbContext;

        public GroupSetup(WllTrackerContext dbContext)
        {
            _dbContext = dbContext;
        }

        [DefaultMemberPermissions(GuildPermission.ManageMessages)]
        [SlashCommand("setup", "Initial setup of a tracker.")]
        public async Task SetupTracker(TrackerType type, string location)
        {
            long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

            if (type == TrackerType.Container)
            {
                var embed = new EmbedBuilder()
                    .WithTitle($"{location} Container Yard")
                    .WithDescription("Last Updated by " + Context.User.Mention + " <t:" + seconds + ":R>")
                    .AddField("Red", 0, true)
                    .AddField("Green", 0, true)
                    .AddField("Blue", 0, true)
                    .AddField("DarkBlue", 0, true)
                    .AddField("White", 0, true)
                    .WithFooter("Brought to you by WLL Cannonsmoke");

                var builder = new ComponentBuilder()
                    .WithButton(label: "Edit Container Count", "btn-container-count", style: ButtonStyle.Secondary);

                await RespondAsync(embed: embed.Build(), components: builder.Build());
                
                try
                {
                    var msg = await GetOriginalResponseAsync();

                    var log = new LogEvent(
                        eventName: "Yard Tracker Setup",
                        messageId: msg.Id,
                        username: Context.User.Username,
                        userId: Context.User.Id,
                        changes: $"Created Yard Tracker: {location}"
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

        [SlashCommand("whiteboard", "Setup Whiteboard")]
        public async Task SetupWhiteboard(string title = "Whiteboard", bool pin = false)
        {
            long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

            var embed = new EmbedBuilder()
                .WithTitle($"{title}")
                .WithDescription("**Messages**\nWaiting for Squibbles\n\n_Last Updated by " + Context.User.Mention + " <t:" + seconds + ":R>_");

            var builder = new ComponentBuilder()
                .WithButton(label: "Update Board", "btn-whiteboard-update", style: ButtonStyle.Secondary);

            await RespondAsync(embed: embed.Build(), components: builder.Build());

            if (Context.Interaction.Permissions.ManageMessages && pin)
            {
                await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.PinAsync());
            }

            try
            {
                var msg = await GetOriginalResponseAsync();

                var log = new LogEvent(
                    eventName: "Whiteboard Setup",
                    messageId: msg.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"Created Whiteboard: {title}"
                );

                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log event: {e.Message}\n{e.StackTrace}");
            }
        }

        [RequireUserPermission(GuildPermission.ViewAuditLog | GuildPermission.ManageMessages, Group = "a")]
        [RequireUserPermission(ChannelPermission.ManageMessages, Group = "a")]
        [SlashCommand("log", "Display audit log for the bot.")]
        public async Task TrackerLog(string query = "all", DateTime? startDate = null, DateTime? endDate = null)
        {

            var lookup = await LogFilter.LookupLogEventsAsync(_dbContext, query, startDate, endDate);

            var memoryStream = LogFilter.ConvertToMemoryStream(lookup);

            string start = startDate?.ToString("yyyyMMdd_HHmmss") ?? "start";
            string end = endDate?.ToString("yyyyMMdd_HHmmss") ?? "now";

            await RespondWithFileAsync(memoryStream, $"logs_{start}_to_{end}.json", "Here are the filtered log entries.", ephemeral: true);

            try
            {
                var msg = await GetOriginalResponseAsync();

                var log = new LogEvent(
                    eventName: "Log Retrieval",
                    messageId: msg.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id
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

