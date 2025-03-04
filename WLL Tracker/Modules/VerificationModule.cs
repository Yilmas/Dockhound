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
using Microsoft.VisualBasic;
using WLL_Tracker.Models;

namespace WLL_Tracker.Modules;

public class VerificationModule : InteractionModuleBase<SocketInteractionContext>
{
    public InteractionService Commands { get; set; }

    private InteractionHandler _handler;

    public VerificationModule(InteractionHandler handler)
    {
        _handler = handler;
    }

    [CommandContextType(InteractionContextType.Guild)]
    [Group("verify", "Root command of WLL Verification Program")]
    public class VerifySetup : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WllTrackerContext _dbContext;
        private readonly HttpClient _httpClient;

        public VerifySetup(WllTrackerContext dbContext, HttpClient httpClient)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
        }

        [SlashCommand("me", "Basic Verification")]
        public async Task VerifyMe(IAttachment file, Faction faction)
        {
            long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

            ulong reviewChannelId = 1346102016086245487;
            ulong notificationChannelId = 1346102062890356756;

            var reviewChannel = Context.Guild.GetTextChannel(reviewChannelId);

            if (reviewChannel == null)
            {
                await RespondAsync("Verification channel not found. Please contact an admin.", ephemeral: true);
                return;
            }

            using var response = await _httpClient.GetAsync(file.Url);
            response.EnsureSuccessStatusCode();
            await using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());

            var embed = new EmbedBuilder()
                .WithTitle("New Verification Submission")
                .WithDescription($"A verification has been submitted by {Context.User.Mention}")
                .AddField("Faction", faction, true)
                .AddField("User ID", Context.User.Id.ToString(), true)
                .WithColor(faction == Faction.Colonial ? Color.DarkGreen : Color.DarkBlue)
                .WithCurrentTimestamp()
                .WithFooter("Awaiting Approval")
                .Build();

            var component = new ComponentBuilder()
                .WithButton("Approve", "approve_verification", ButtonStyle.Success)
                .WithButton("Deny", "deny_verification", ButtonStyle.Danger)
                .Build();

            await reviewChannel.SendFileAsync(stream, file.Filename, embed: embed, components: component);
            await RespondAsync("Verification submitted. Please wait for approval.", ephemeral: true);

            try
            {
                var msg = await GetOriginalResponseAsync();

                var log = new LogEvent(
                    eventName: "Verification Module",
                    messageId: msg.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} has submitted a verification submission."
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

