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
    [Group("tracker", "desc")]
    public class GroupSetup : InteractionModuleBase<SocketInteractionContext>
    {
        [DefaultMemberPermissions(GuildPermission.ManageMessages)]
        [SlashCommand("setup", "Initial setup of a tracker.")]
        public async Task SetupTracker(TrackerType type, string location = "RR")
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
                    .WithFields(
                        new EmbedFieldBuilder()
                            .WithName("Job Board")
                            .WithValue("Waiting for Jobs ...")
                        )
                    .WithFooter("Brought to you by WLL Cannonsmoke");

                var builder = new ComponentBuilder()
                    .WithButton(label: "Edit Container Count", "btn-container-count", style: ButtonStyle.Secondary)
                    .WithButton(label: "Edit Job Board", "btn-board-edit", style: ButtonStyle.Secondary);

                await RespondAsync(embed: embed.Build(), components: builder.Build());

                await GetOriginalResponseAsync().ContinueWith(async (msg) => 
                {
                    // Log event with updated fields, author, timestamp UTC
                    
                    var log = new LogEvent(
                        id: msg.Result.Id + "|" + $"{location} Container Yard",
                        author: Context.User.Username + "|" + Context.User.Mention,
                        updated: DateTime.UtcNow,
                        changes: new List<string>{"Created Yard Tracker"}
                    );

                    _ = log.SaveLog();
                });

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

            await GetOriginalResponseAsync().ContinueWith(async (msg) =>
            {
                // Log event with updated fields, author, timestamp UTC

                var log = new LogEvent(
                    id: msg.Result.Id + "|" + $"{title}",
                    author: Context.User.Username + "|" + Context.User.Mention,
                    updated: DateTime.UtcNow,
                    changes: new List<string> { "Created Whiteboard" }
                );

                _ = log.SaveLog();
            });
        }

        [DefaultMemberPermissions(GuildPermission.ViewAuditLog | GuildPermission.ManageMessages)]
        [SlashCommand("log", "[EXPERIMENTAL] Display recent log of activity.")]
        public async Task TrackerLog(string query = "all", DateTime? startDate = null, DateTime? endDate = null)
        {
            string inputFilePath = "./log.txt";

            var filteredEvents = await LogFilter.LoadLogEventsAsync(inputFilePath, query);

            if (filteredEvents.Count == 0)
            {
                await RespondAsync("No log entries found matching the query.", ephemeral: true);
                return;
            }

            if (startDate.HasValue || endDate.HasValue)
            {
                filteredEvents = LogFilter.ApplyDateRangeFilter(filteredEvents, startDate, endDate);
                if (filteredEvents.Count == 0)
                {
                    await RespondAsync("No log entries found within the specified date range.", ephemeral: true);
                    return;
                }
            }

            var memoryStream = LogFilter.ConvertToMemoryStream(filteredEvents);
            await RespondWithFileAsync(memoryStream, "filtered_logs.txt", "Here are the filtered log entries.", ephemeral: true);

            await GetOriginalResponseAsync().ContinueWith(async (msg) =>
            {
                // Log event with updated fields, author, timestamp UTC

                var log = new LogEvent(
                    id: msg.Result.Id + "|" + "Log Retrieval",
                    author: Context.User.Username + "|" + Context.User.Mention,
                    updated: DateTime.UtcNow,
                    changes: new List<string> { "Retrieve Log" }
                );

                _ = log.SaveLog();
            });
        }
    }
}

