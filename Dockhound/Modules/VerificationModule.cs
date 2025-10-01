using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dockhound.Enums;
using System.Reflection.Emit;
using Discord.Rest;
using Dockhound.Extensions;
using Dockhound.Logs;
using System.IO;
using System.Collections;
using System.Threading.Channels;
using Discord.WebSocket;
using Microsoft.VisualBasic;
using Dockhound.Models;
using Microsoft.Extensions.Configuration;
using Dockhound.Modals;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using Microsoft.Extensions.Options;
using System.Runtime;
using Microsoft.IdentityModel.Tokens;

namespace Dockhound.Modules;

public class VerificationModule : InteractionModuleBase<SocketInteractionContext>
{
    public InteractionService Commands { get; set; }

    private InteractionHandler _handler;

    public VerificationModule(InteractionHandler handler)
    {
        _handler = handler;
    }

    [CommandContextType(InteractionContextType.Guild)]
    [Group("verify", "Root command of the HvL Verification Program")]
    public class VerifySetup : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DockhoundContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppSettings _settings;

        public VerifySetup(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _settings = appSettings.Value;
        }

        [SlashCommand("me", "Basic Verification")]
        public async Task VerifyMe(IAttachment file, Faction faction)
        {
            await DeferAsync(ephemeral: true);

            long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
            
            var reviewChannelId = _settings.Verify.ReviewChannelId; //ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out ulong reviewChannelId);

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
                .WithDescription($"A verification has been submitted by {Context.Guild.GetUser(Context.User.Id).DisplayName} - ({Context.User.Mention})")
                .AddField("Faction", faction, true)
                .AddField("User ID", Context.User.Id.ToString(), true)
                .AddField("Roles to be granted", DiscordRolesList.GetDeltaRoleMentions(Context.Guild.GetUser(Context.User.Id), faction.ToString()), false)
                .WithColor(faction == Faction.Colonial ? Color.DarkGreen : Color.DarkBlue)
                .WithCurrentTimestamp()
                .WithFooter("Awaiting Approval")
                .Build();

            var component = new ComponentBuilder()
                .WithButton("Approve", "approve-verification", ButtonStyle.Success)
                .WithButton("Deny", "deny-verification", ButtonStyle.Danger)
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

        // TODO: Removed applicant, but keeping to maintain code reference.

        //[UserCommand("Assign Applicant")]
        //public async Task AssignApplicantAsync(IUser targetUser)
        //{
        //    await DeferAsync(ephemeral: true);

        //    if (Context.Guild == null)
        //    {
        //        await RespondAsync("This command must be used in a server.", ephemeral: true);
        //        return;
        //    }

        //    var guildUser = Context.Guild.GetUser(targetUser.Id);
        //    var actingUser = Context.Guild.GetUser(Context.User.Id);

        //    if (guildUser == null || actingUser == null)
        //    {
        //        await FollowupAsync("User not found.", ephemeral: true);
        //        return;
        //    }

        //    // Retrieve allowed roles from environment variable
        //    var allowedRoleIds = _configuration["ALLOWED_APPLICANT_ASSIGNER_ROLES"]
        //        ?.Split(',')
        //        .Select(id => ulong.TryParse(id, out var roleId) ? roleId : (ulong?)null)
        //        .Where(id => id.HasValue)
        //        .Select(id => id.Value)
        //        .ToList() ?? new List<ulong>();

        //    // Check if the acting user is assigning to themselves OR has a required role
        //    bool canAssign = Context.User.Id == targetUser.Id || actingUser.Roles.Any(r => allowedRoleIds.Contains(r.Id));

        //    if (!canAssign)
        //    {
        //        await FollowupAsync("❌ You do not have permission to assign the **Applicant** role.", ephemeral: true);
        //        return;
        //    }

        //    // Determine faction based on roles
        //    var factionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Faction");
        //    string faction = guildUser.Roles.Any(r => r.Id == factionRole.Colonial) ? "Colonial"
        //                  : guildUser.Roles.Any(r => r.Id == factionRole.Warden) ? "Warden"
        //                  : string.Empty;

        //    // Assign applicant roles
        //    var applicantFactionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Applicant");
        //    var rolesToAssign = new List<ulong> { applicantFactionRole.Generic };

        //    if (faction == "Colonial") rolesToAssign.Add(applicantFactionRole.Colonial);
        //    if (faction == "Warden") rolesToAssign.Add(applicantFactionRole.Warden);

        //    if (rolesToAssign.Any())
        //        await guildUser.AddRolesAsync(rolesToAssign);

        //    // Retrieve and validate forum channel
        //    if (!ulong.TryParse(_configuration["CHANNEL_APPLICANT_FORUM"], out ulong forumChannelId) ||
        //        Context.Guild.GetChannel(forumChannelId) is not SocketForumChannel forumChannel)
        //    {
        //        await FollowupAsync("Forum channel not found or configuration error.", ephemeral: true);
        //        return;
        //    }

        //    // Build the embed
        //    var embedBuilder = new EmbedBuilder()
        //        .WithTitle("Applicant Promotion")
        //        .WithDescription($"{targetUser.Mention} has been assigned the **Applicant** role. Use this thread to discuss their applicant promotion.")
        //        .WithThumbnailUrl(targetUser.GetAvatarUrl())
        //        .WithColor(Color.Green);

        //    if (Context.User.Id != targetUser.Id)
        //        embedBuilder.AddField("Applicant By", Context.User.Mention, false);

        //    // Retrieve and validate forum tag
        //    ulong.TryParse(_configuration["CHANNEL_APPLICANT_FORUM_PENDINGTAG"], out ulong tagId);
        //    var tag = forumChannel.Tags.FirstOrDefault(p => p.Id == tagId);

        //    // Create the thread
        //    var thread = await forumChannel.CreatePostAsync(
        //        title: guildUser.DisplayName,
        //        tags: tag != null ? [tag] : null,
        //        embed: embedBuilder.Build()
        //    );

        //    await FollowupAsync($"✅ Assigned **Applicant** to {targetUser.Mention}. Created applicant thread: {thread.Mention}.", ephemeral: true);
        //}
    }
}

