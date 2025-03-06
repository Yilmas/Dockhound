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
using Microsoft.Extensions.Configuration;

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
        private readonly IConfiguration _configuration;

        public VerifySetup(WllTrackerContext dbContext, HttpClient httpClient, IConfiguration config)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
        }

        [SlashCommand("me", "Basic Verification")]
        public async Task VerifyMe(IAttachment file, Faction faction)
        {
            long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

            ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out ulong reviewChannelId);

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
                .AddField("Roles to be granted", DiscordRolesList.GetDeltaRoleMentions(Context.Guild.GetUser(Context.User.Id), faction.ToString()), false)
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

        [RequireUserPermission(GuildPermission.ManageMessages)]
        [SlashCommand("info", "Provides information on the verification process.")]
        public async Task VerifyInfo()
        {
            string imageUrl = _configuration["VERIFY_IMAGEURL"];

            var embed = new EmbedBuilder()
                .WithTitle("Looking to Verify?")
                .WithDescription("Follow the steps below to get yourself verified.")
                .AddField("Steps to Verify", "1. Enter `/verify me`\n2. Upload your `F1 Screenshot`\n3. Select `Colonial` or `Warden`", false)
                .AddField("**Required Screenshot**", "F1 Screenshot **ONLY**\nScreenshots from **Home Region** will be **rejected**.", false)
                .AddField("\u200B​", "\u200B", false)
                .AddField("**How long will it take?**", "If you have given us the correct information, one of the officers will handle your request asap.", false)
                .WithImageUrl(imageUrl)
                .WithColor(Color.Gold)
                .WithFooter("Brought to you by WLL Cannonsmoke")
                .Build();

            await RespondAsync(embed: embed);
        }
    }
}

