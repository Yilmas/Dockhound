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
using Discord.WebSocket;
using Microsoft.VisualBasic;
using WLL_Tracker.Models;
using Microsoft.Extensions.Configuration;
using WLL_Tracker.Modals;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

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
            await DeferAsync(ephemeral: true);

            long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

            ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out ulong reviewChannelId);

            var reviewChannel = Context.Guild.GetTextChannel(reviewChannelId);

            if (reviewChannel == null)
            {
                await FollowupAsync("Verification channel not found. Please contact an admin.", ephemeral: true);
                return;
            }

            using var response = await _httpClient.GetAsync(file.Url);
            response.EnsureSuccessStatusCode();
            await using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());

            var embed = new EmbedBuilder()
                .WithTitle("New Verification Submission")
                .WithDescription($"A verification has been submitted by {Context.User.Username} - ({Context.User.Mention})")
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
            await FollowupAsync("Verification submitted. Please wait for approval.", ephemeral: true);

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
                .AddField("Steps to Verify", "1. Enter `/verify me`\n2. Upload your `MAP SCREEN Screenshot`\n3. Select `Colonial` or `Warden`", false)
                .AddField("**Required Screenshot**", "Map Screenshot **ONLY**\nScreenshots from **Home Region** OR **Secure Map** will be **rejected**.", false)
                .AddField("\u200B​", "\u200B", false)
                .AddField("**How long will it take?**", "If you have given us the correct information, one of the officers will handle your request asap.", false)
                .WithImageUrl(imageUrl)
                .WithColor(Color.Gold)
                .WithFooter("Brought to you by WLL Cannonsmoke")
                .Build();

            await RespondAsync(embed: embed);
        }

        [UserCommand("Assign Applicant")]
        public async Task AssignApplicantAsync(IUser targetUser)
        {
            if (Context.Guild == null)
            {
                await RespondAsync("This command must be used in a server.", ephemeral: true);
                return;
            }

            var guildUser = Context.Guild.GetUser(targetUser.Id);
            if (guildUser == null)
            {
                await RespondAsync("User not found in the guild.", ephemeral: true);
                return;
            }

            string faction = string.Empty;
            var factionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Faction");
            foreach (var r in guildUser.Roles)
            {
                if (r.Id == factionRole.Colonial)
                {
                    faction = "Colonial";
                }

                if (r.Id == factionRole.Warden)
                {
                    faction = "Warden";
                }
            }

            var rolesToAssign = new List<ulong>();
            var applicantFactionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Applicant");
            rolesToAssign.Add(applicantFactionRole.Generic);

            if (faction == "Colonial")
            {
                rolesToAssign.Add(applicantFactionRole.Colonial);
            }

            if (faction == "Warden")
            {
                rolesToAssign.Add(applicantFactionRole.Warden);
            }

            if (rolesToAssign.Count > 0)
            {
                await guildUser.AddRolesAsync(rolesToAssign);
            }

            // Create a thread in the forum
            if (!ulong.TryParse(_configuration["CHANNEL_APPLICANT_FORUM"], out ulong forumChannelId))
            {
                await RespondAsync("Configuration error: Forum channel ID is invalid or missing.", ephemeral: true);
                return;
            }

            var forumChannel = Context.Guild.GetChannel(forumChannelId) as SocketForumChannel;
            if (forumChannel == null)
            {
                await RespondAsync("Forum channel not found or incorrect channel type.", ephemeral: true);
                return;
            }

            var embedBuilder = new EmbedBuilder()
                .WithTitle("Applicant Promotion")
                .WithDescription($"{targetUser.Mention} has been assigned the **Applicant** role. Use this thread to discuss their applicant promotion.")
                .WithThumbnailUrl(targetUser.GetAvatarUrl())
                .WithColor(Color.Green);

            // Add "Applicant By" field only if Context.User is different from targetUser
            if (Context.User.Id != targetUser.Id)
            {
                embedBuilder.AddField("Applicant By", Context.User.Mention, false);
            }

            var embed = embedBuilder.Build();

            ulong.TryParse(_configuration["CHANNEL_APPLICANT_FORUM_PENDINGTAG"], out ulong tagId);

            var tag = forumChannel.Tags.FirstOrDefault(p => p.Id == tagId);

            var thread = await forumChannel.CreatePostAsync(
                title: $"{targetUser.GlobalName}",
                tags: [tag],
                embed: embed
            );

            await RespondAsync($"✅ Assigned **Applicant** to {targetUser.Mention}. Created applicant thread: {thread.Mention}.", ephemeral: true);
        }
    }
}

