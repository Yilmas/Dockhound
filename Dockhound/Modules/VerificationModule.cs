using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Dockhound.Enums;
using Dockhound.Extensions;
using Dockhound.Logs;
using Dockhound.Modals;
using Dockhound.Models;
using Dockhound.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Dockhound.Modules;

[CommandContextType(InteractionContextType.Guild)]
[Group("verify", "Root command of the Verification Program")]
public class VerificationModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DockhoundContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly IGuildSettingsService _guildSettingsService;
    private readonly IVerificationHistoryService _verificationHistory;

    public VerificationModule(DockhoundContext dbContext, HttpClient httpClient, IGuildSettingsService guildSettingsService, IVerificationHistoryService verificationHistoryService)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _guildSettingsService = guildSettingsService;
        _verificationHistory = verificationHistoryService;
    }

    [SlashCommand("me", "Basic Verification")]
    public async Task VerifyMe(IAttachment file, Faction faction)
    {
        await DeferAsync(ephemeral: true);

        var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

        long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        var reviewChannel = cfg.Verify.ReviewChannelId is ulong id
                    ? Context.Guild.GetTextChannel(id)
                    : null;

        if (reviewChannel == null)
        {
            await FollowupAsync("Verification channel not found. Please contact an admin.", ephemeral: true);
            return;
        }

        using var response = await _httpClient.GetAsync(file.Url);
        response.EnsureSuccessStatusCode();
        await using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());

        var history = await _verificationHistory.GetTrackRecordAsync(Context.User.Id);

        var track = "_No previous approvals._";

        if (history.Count > 0)
        {
            var lines = new List<string>();

            foreach (var h in history)
            {
                var guildName = await _guildSettingsService.GetGuildDisplayNameAsync(h.GuildId)
                                ?? $"Guild {h.GuildId}";

                lines.Add(
                    $"• {h.Faction} — <t:{new DateTimeOffset(h.ApprovedAtUtc).ToUnixTimeSeconds()}:R> — {guildName}"
                );
            }

            track = string.Join("\n", lines);
        }

        IGuildUser? member = Context.Guild.GetUser(Context.User.Id);

        if (member is null)
        {
            // fallback to REST if not in cache
            member = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, Context.User.Id);
        }

        bool isTrusted = cfg.Verify.TrustedRoles?.Any(member.RoleIds.Contains) == true;

        if(isTrusted) {
            // Compute display name and roles-to-be-granted text (same as manual path)
            var displayName = (member as SocketGuildUser)?.DisplayName
                              ?? member?.Username
                              ?? Context.User.Username;

            var roleMentions = "-";
            if (member is not null)
            {
                roleMentions = await DiscordRolesList.GetDeltaRoleMentionsAsync(
                    _guildSettingsService,
                    member,
                    faction
                );
                if (string.IsNullOrWhiteSpace(roleMentions))
                    roleMentions = "-";
            }

            // Build approved embed (no action buttons)
            var embedApproved = new EmbedBuilder()
                .WithTitle("Verification Submission (Auto-Approved)")
                .WithDescription($"A verification has been auto-approved for {displayName} — ({Context.User.Mention})")
                .AddField("Faction", faction.ToString(), inline: true)
                .AddField("User ID", Context.User.Id.ToString(), inline: true)
                .AddField("Roles granted", roleMentions, inline: false)
                .AddField("Faction history (last 5)", string.IsNullOrWhiteSpace(track) ? "-" : track, inline: false)
                .WithColor(faction == Faction.Colonial ? Color.DarkGreen : Color.DarkBlue)
                .WithCurrentTimestamp()
                .WithFooter($"Auto-approved ✅ by ({Context.Client.CurrentUser.Mention})")
                .Build();

            // Determine roles to assign and try to assign
            var rolesToAssign = await DiscordRolesList.GetDeltaRoleIdListAsync(
                _guildSettingsService,
                member,
                faction
            );

            if (rolesToAssign.Count > 0)
            {
                try
                {
                    if (member is SocketGuildUser socketUser)
                    {
                        await socketUser.AddRolesAsync(rolesToAssign);
                    }
                    else if (member is RestGuildUser restUser)
                    {
                        foreach (var rId in rolesToAssign)
                        {
                            try { await restUser.AddRoleAsync(rId); }
                            catch (Exception ex) { Console.WriteLine($"[WARN] AddRoleAsync failed for {restUser.Username}: {ex.Message}"); }
                        }
                    }
                    else
                    {
                        // Fallback attempt for any IGuildUser implementations
                        try { await member.AddRolesAsync(rolesToAssign); } catch { /* ignore */ }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[ERROR] Auto-assign roles failed: {e.Message}");
                }
            }

            var posted = await reviewChannel.SendFileAsync(stream, file.Filename, embed: embedApproved, components: new ComponentBuilder().Build());

            // Log verification history (approvedByUserId = null for auto)
            try
            {
                await _verificationHistory.LogApprovalAsync(
                    guildId: Context.Guild.Id,
                    userId: Context.User.Id,
                    faction: faction,
                    imageUrl: file.Url,
                    approvedByUserId: null,
                    steam64Id: null
                );
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log verification history: {e.Message}");
            }

            // Notify user by DM (best-effort) using configured secure channels
            try
            {
                var factionSecureChannel = faction switch
                {
                    Faction.Colonial => cfg.Verify.ColonialSecureChannelId is ulong colonialId ? Context.Guild.GetTextChannel(colonialId) : null,
                    Faction.Warden => cfg.Verify.WardenSecureChannelId is ulong wardenId ? Context.Guild.GetTextChannel(wardenId) : null,
                    _ => null
                };

                var displayNameGuild = await _guildSettingsService.GetGuildDisplayNameAsync(Context.Guild.Id) ?? "";

                await Context.User.SendMessageAsync(
                    $"✅ Your {displayNameGuild} verification has been auto-approved! 🎉 " +
                    (factionSecureChannel != null
                        ? $"You now have access to faction-specific channels such as {factionSecureChannel.Mention}."
                        : "Faction-specific channels are now available to you.")
                );
            }
            catch
            {
                Console.WriteLine($"[ERROR] Failed to send a DM to {Context.User.Username}. They may have DMs disabled.");
            }

            // Persist a log event for the auto-approval
            try
            {
                var log = new LogEvent(
                    eventName: "Verification Module (Auto)",
                    messageId: posted.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} was auto-approved via trusted role."
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log auto-approval event: {e.Message}\n{e.StackTrace}");
            }

            await FollowupAsync("Verification auto-approved. You should now have the appropriate roles and access.", ephemeral: true);
            return;
        }
        else
        {
            // At this point `member` is either SocketGuildUser or RestGuildUser, but both are IGuildUser
            var displayName = (member as SocketGuildUser)?.DisplayName
                                ?? member?.Username
                                ?? Context.User.Username;

            var roleMentions = "-";
            if (member is not null)
            {
                roleMentions = await DiscordRolesList.GetDeltaRoleMentionsAsync(
                    _guildSettingsService,
                    member,
                    faction   // Faction enum
                );
                if (string.IsNullOrWhiteSpace(roleMentions))
                    roleMentions = "-";
            }


            var embed = new EmbedBuilder()
                .WithTitle("New Verification Submission")
                .WithDescription($"A verification has been submitted by {displayName} — ({Context.User.Mention})")
                .AddField("Faction", faction.ToString(), inline: true)
                .AddField("User ID", Context.User.Id.ToString(), inline: true)
                .AddField("Roles to be granted", roleMentions, inline: false)
                .AddField("Faction history (last 5)", string.IsNullOrWhiteSpace(track) ? "-" : track, inline: false)
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
    }

    [SlashCommand("metoo", "Start the verification process.")]
    public async Task VerifyAsync()
    {
        var steamRequired = false;

        if (_guildSettingsService.TryGetCached(Context.Guild.Id, out var cfg))
            steamRequired = cfg?.Verify?.IsSteamRequired == true;

        if (steamRequired)
        {
            await RespondWithModalAsync<VerifyMeSteamRequiredModal>("verify_me_required");
        }
        else
        {
            await RespondWithModalAsync<VerifyMeSteamOptionalModal>("verify_me_optional");
        }
    }
}

