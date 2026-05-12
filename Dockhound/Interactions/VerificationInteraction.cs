using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Enums;
using Dockhound.Logs;
using Dockhound.Modals;
using Dockhound.Models;
using Dockhound.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using static Dockhound.Services.VerificationHistoryService;

namespace Dockhound.Interactions
{
    public class VerificationInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DockhoundContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IVerificationHistoryService _verificationHistory;
        private readonly ISteamService _steamService;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public VerificationInteraction(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IGuildSettingsService guildSettingsService, IVerificationHistoryService verificationHistoryService, ISteamService steamService)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _guildSettingsService = guildSettingsService;
            _verificationHistory = verificationHistoryService;
            _steamService = steamService;
        }

        // APPROVE
        [ComponentInteraction("approve-verification")]
        public async Task ApproveVerification()
        {
            var comp = (SocketMessageComponent)Context.Interaction;
            var guild = Context.Guild;

            var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

            var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();
            if (embed == null) 
                return;

            var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
            if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
                return;

            var steamProfile = embed.Fields.FirstOrDefault(f => f.Name == "Steam Profile");
            if (steamProfile == null)
                return;

            var factionField = embed.Fields.FirstOrDefault(f => f.Name == "Faction");
            if (factionField == null)
                return;

            var user = guild?.GetUser(userId);
            if (user == null)
                return;

            var notificationChannel = cfg.Verify.NotificationChannelId is ulong id
                        ? guild.GetTextChannel(id)
                        : null;

            if (notificationChannel is null)
                return;

            await DeferAsync(ephemeral: true);

            // Assign roles
            var factionF = FactionParser.Parse(factionField.Value?.ToString()); // throws if invalid

            var rolesToAssign = await DiscordRolesList.GetDeltaRoleIdListAsync(
                _guildSettingsService, // IGuildSettingsService injected
                user,                  // IGuildUser
                factionF                // Faction enum
            );

            if (rolesToAssign.Count > 0)
            {
                await user.AddRolesAsync(rolesToAssign);
            }

            // Update the review message: footer + remove buttons
            embed.WithFooter($"Approved ✅ by {Context.User.Username}");
            await comp.Message.ModifyAsync(m =>
            {
                m.Embeds = new[] { embed.Build() };
                m.Components = new ComponentBuilder().Build();
            });

            try
            {
                var faction = factionField.Value?.ToString();

                var factionSecureChannel = faction?.Trim().ToLowerInvariant() switch
                {
                    "colonial" => cfg.Verify.ColonialSecureChannelId is ulong colonialId ? guild.GetTextChannel(colonialId) : null,
                    "warden" => cfg.Verify.WardenSecureChannelId is ulong wardenId ? guild.GetTextChannel(wardenId) : null,
                    _ => null
                };

                var factionEnum = FactionParser.Parse(faction);

                var attachment = comp.Message.Attachments.FirstOrDefault();

                ulong? steam64Id = null;
                if (!string.IsNullOrWhiteSpace(factionField.Value?.ToString()))
                {
                    var steamResult = await _steamService.ResolveSteam64IdAsync(factionField.Value?.ToString());

                    if (steamResult.Status == SteamResolveStatus.Resolved)
                        steam64Id = steamResult.Steam64Id;
                }

                await _verificationHistory.LogApprovalAsync(
                    guildId: Context.Guild.Id,
                    userId: user.Id,
                    faction: factionEnum,
                    imageUrl: attachment?.Url,
                    approvedByUserId: Context.User.Id,
                    steam64Id: steam64Id
                );

                if (factionSecureChannel is null)
                    return;

                var displayName = await _guildSettingsService.GetGuildDisplayNameAsync(Context.Guild.Id) ?? "";

                await user.SendMessageAsync(
                    $"✅ Your {displayName} verification has been approved! 🎉 " +
                    (factionSecureChannel != null
                        ? $"You now have access to faction-specific channels such as {factionSecureChannel.Mention}."
                        : "Faction-specific channels are now available to you.")
                );
            }
            catch
            {
                Console.WriteLine($"[ERROR] Failed to send a DM to {user.Username}. They may have DMs disabled.");
            }

            // Log
            try
            {
                var log = new LogEvent(
                    eventName: "Verification Handler",
                    messageId: comp.Message.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} approved access for {user.Username}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log event: {e.Message}\n{e.StackTrace}");
            }

            await FollowupAsync("Approved.", ephemeral: true);
        }

        // DENY
        [ComponentInteraction("deny-verification")]
        public async Task DenyVerification()
        {
            var comp = (SocketMessageComponent)Context.Interaction;
            var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();

            if (embed == null)
                return;

            var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
            if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
                return;

            // Pass both the target user and the review-message ID into the modal CustomId
            var modal = new ModalBuilder("Denial Reason", $"verify-deny-reason:{userId}:{comp.Message.Id}")
                .AddTextInput("Why are you denying this?", "deny-reason-text", TextInputStyle.Paragraph, maxLength: 500,
                              placeholder: "Enter the reason for denial...");

            await RespondWithModalAsync(modal.Build());
        }

        // DENY REASON
        [ModalInteraction("verify-deny-reason:*:*")]
        public async Task SubmitDenyReason(ulong userId, ulong messageId, DenyReasonModal modal)
        {
            var guild = Context.Guild;
            if (guild is null)
                return;

            var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

            // SocketGuild uses cached sync getters
            var user = guild.GetUser(userId);
            if (user is null)
                return;

            //if (!ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out var reviewChannelId))
            //    return;

            var verificationChannel = cfg.Verify.ReviewChannelId is ulong id
                        ? guild.GetTextChannel(id)
                        : null;

            if (verificationChannel is null)
                return;

            var message = await verificationChannel.GetMessageAsync(messageId) as IUserMessage;
            if (message is null)
                return;

            // Update the embed with the denial reason
            var embed = message.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder();
            embed.AddField("Denial Reason", modal.Reason, inline: true);
            embed.WithFooter($"Denied ❌ by {Context.User.Username}");
            embed.WithColor(Color.Red);

            await message.ModifyAsync(m =>
            {
                m.Embeds = new[] { embed.Build() };
                m.Components = new ComponentBuilder().Build(); // remove approve/deny buttons
            });

            // Notify user via DM (best-effort)
            try
            {

                var displayName = await _guildSettingsService.GetGuildDisplayNameAsync(Context.Guild.Id) ?? "the server";
                await user.SendMessageAsync($"❌ Your verification for {displayName} has been denied.\n**Reason:** {modal.Reason}");
            }
            catch
            {
                await RespondAsync($"Could not DM {user.Username}. They may have DMs disabled.", ephemeral: true);
            }

            await RespondAsync("Denial reason submitted and user has been notified.", ephemeral: true);

            // Log the denial
            try
            {
                var log = new LogEvent(
                    eventName: "Verification Denial",
                    messageId: messageId,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} denied access for {user.Username} with reason: {modal.Reason}"
                );

                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log event: {e.Message}\n{e.StackTrace}");
            }
        }


        [ModalInteraction("verify_me_required")]
        public async Task HandleVerifyRequiredAsync(VerifyMeSteamRequiredModal modal)
        {
            await DeferAsync(ephemeral: true);

            var result = await VerifyAsync(
                modal.File,
                modal.Faction,
                modal.Steam64Id,
                steamRequired: true);

            await FollowupAsync(result, ephemeral: true);
        }

        [ModalInteraction("verify_me_optional")]
        public async Task HandleVerifyOptionalAsync(VerifyMeSteamOptionalModal modal)
        {
            await DeferAsync(ephemeral: true);

            var result = await VerifyAsync(
                modal.File,
                modal.Faction,
                modal.Steam64Id,
                steamRequired: false);

            await FollowupAsync(result, ephemeral: true);
        }

        [ComponentInteraction("verify:metoo")]
        public async Task VerifyMeTooButtonAsync()
        {
            var steamRequired = false;
            var restrictionLevel = AccessRestriction.Open;

            if (_guildSettingsService.TryGetCached(Context.Guild.Id, out var cfg))
            {
                steamRequired = cfg?.Verify?.IsSteamRequired == true;
                restrictionLevel = cfg?.Verify?.RestrictedAccess?.CurrentRestrictionLevel ?? AccessRestriction.Open;
            }

            if (restrictionLevel == AccessRestriction.Restricted)
            {
                await RespondAsync(
                    "Verification is currently restricted. Please try again later.",
                    ephemeral: true);

                return;
            }

            if (restrictionLevel == AccessRestriction.MembersOnly)
            {
                var member = Context.User as SocketGuildUser;

                if (member is null)
                {
                    await RespondAsync(
                        "This can only be used inside a server.",
                        ephemeral: true);

                    return;
                }

                var memberOnlyRoles = cfg?.Verify?.RestrictedAccess?.MemberOnlyRoles?.ToHashSet()
                    ?? new HashSet<ulong>();

                var alwaysRestrictRoles = cfg?.Verify?.RestrictedAccess?.AlwaysRestrictRoles?.ToHashSet()
                    ?? new HashSet<ulong>();

                var allowedRoles = memberOnlyRoles
                    .Except(alwaysRestrictRoles)
                    .ToHashSet();

                var userRoleIds = member.Roles
                    .Select(role => role.Id);

                var hasAllowedRole = allowedRoles.Overlaps(userRoleIds);
                var isAlwaysRestricted = alwaysRestrictRoles.Overlaps(userRoleIds);

                if (isAlwaysRestricted || !hasAllowedRole)
                {
                    await RespondAsync(
                        "Verification is currently limited to configured member roles.",
                        ephemeral: true);

                    return;
                }
            }

            if (steamRequired)
            {
                await RespondWithModalAsync<VerifyMeSteamRequiredModal>("verify_me_required");
            }
            else
            {
                await RespondWithModalAsync<VerifyMeSteamOptionalModal>("verify_me_optional");
            }
        }

        private async Task<string> VerifyAsync(IAttachment file, string faction, string steamInput, bool steamRequired)
        {
            if (steamRequired && string.IsNullOrWhiteSpace(steamInput))
                return "Steam ID is required for this server.";

            ulong? steam64Id = null;
            string? steamProfileUrl = null;

            if (!string.IsNullOrWhiteSpace(steamInput))
            {
                var steamResult = await _steamService.ResolveSteam64IdAsync(steamInput);

                if (steamResult.Status != SteamResolveStatus.Resolved)
                    return GetSteamResolveErrorMessage(steamResult);

                steam64Id = steamResult.Steam64Id;
                steamProfileUrl = steamResult.ProfileUrl;
            }

            var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

            var reviewChannel = cfg.Verify.ReviewChannelId is ulong id
                    ? Context.Guild.GetTextChannel(id)
                    : null;

            if (reviewChannel == null)
            {
                await FollowupAsync("Verification channel not found. Please contact an admin.", ephemeral: true);
            }

            // Download attachment content
            using var response = await _httpClient.GetAsync(file.Url);
            response.EnsureSuccessStatusCode();
            await using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());

            // Build recent faction history (for the requester)
            var history = await _verificationHistory.GetTrackRecordAsync(Context.User.Id);

            var track = "_No previous approvals._";
            if (history.Count > 0)
            {
                var lines = new List<string>();
                foreach (var h in history)
                {
                    var guildName = await _guildSettingsService.GetGuildDisplayNameAsync(h.GuildId) ?? $"Guild {h.GuildId}";
                    lines.Add($"• {h.Faction} — <t:{new DateTimeOffset(h.ApprovedAtUtc).ToUnixTimeSeconds()}:R> — {guildName}");
                }
                track = string.Join("\n", lines);
            }

            // Steam checks: detect if this steam64Id was used before, by this user (same or different), or by other users.
            string steamHistory = "-";
            bool steamUsedByOthers = false;
            bool steamDiffersFromUserLast = false;

            if (steam64Id.HasValue)
            {
                // recent records for this exact Steam64Id (last 5)
                var exactMatches = await _dbContext.VerificationRecords
                    .Where(r => r.Steam64Id.HasValue && r.Steam64Id == steam64Id.Value)
                    .OrderByDescending(r => r.ApprovedAtUtc)
                    .Take(5)
                    .ToListAsync();

                // recent records for this user (last 5)
                var userRecent = await _dbContext.VerificationRecords
                    .Where(r => r.UserId == Context.User.Id && r.Steam64Id.HasValue)
                    .OrderByDescending(r => r.ApprovedAtUtc)
                    .Take(5)
                    .ToListAsync();

                // Determine if user's most-recent steam differs
                var userLast = userRecent.FirstOrDefault();
                if (userLast != null && userLast.Steam64Id.HasValue && userLast.Steam64Id.Value != steam64Id.Value)
                    steamDiffersFromUserLast = true;

                // Determine if this steam64 was used by other users
                var otherMatches = exactMatches.Where(r => r.UserId != Context.User.Id).ToList();
                if (otherMatches.Any())
                    steamUsedByOthers = true;

                // Only provide steam history to reviewers when there is an "offense":
                // - the supplied Steam64 was used by another Discord account
                // - the supplied Steam64 differs from this user's last recorded Steam64.
                if (steamUsedByOthers || steamDiffersFromUserLast)
                {
                    var sb = new List<string>();

                    if (exactMatches.Any())
                    {
                        sb.Add("Recent uses of this Steam64ID:");
                        foreach (var r in exactMatches)
                        {
                            var gName = await _guildSettingsService.GetGuildDisplayNameAsync(r.GuildId) ?? $"Guild {r.GuildId}";
                            sb.Add($"• User {r.UserId} — <t:{new DateTimeOffset(r.ApprovedAtUtc).ToUnixTimeSeconds()}:R> — {gName}");
                        }
                    }

                    if (userRecent.Any())
                    {
                        sb.Add("This user's recent Steam64IDs:");
                        foreach (var r in userRecent)
                        {
                            var gName = await _guildSettingsService.GetGuildDisplayNameAsync(r.GuildId) ?? $"Guild {r.GuildId}";
                            sb.Add($"• {r.Steam64Id} — <t:{new DateTimeOffset(r.ApprovedAtUtc).ToUnixTimeSeconds()}:R> — {gName}");
                        }
                    }

                    steamHistory = string.Join("\n", sb);
                    if (string.IsNullOrWhiteSpace(steamHistory))
                        steamHistory = string.Empty;
                }
                else
                {
                    // No offenses -> leave steamHistory blank
                    steamHistory = string.Empty;
                }
            }
            else
            {
                // No steam provided -> blank
                steamHistory = string.Empty;
            }

            // Resolve member and display name
            IGuildUser? member = Context.Guild.GetUser(Context.User.Id);
            if (member is null)
            {
                member = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, Context.User.Id);
            }

            var displayName = (member as SocketGuildUser)?.DisplayName
                              ?? member?.Username
                              ?? Context.User.Username;

            var roleMentions = "-";
            if (member is not null)
            {
                var factionEnum = FactionParser.Parse(faction);
                roleMentions = await DiscordRolesList.GetDeltaRoleMentionsAsync(
                    _guildSettingsService,
                    member,
                    factionEnum
                );
                if (string.IsNullOrWhiteSpace(roleMentions))
                    roleMentions = "-";
            }

            var desc = $"A verification has been submitted by {displayName} — ({Context.User.Mention})";

            var steamProfileField = !string.IsNullOrWhiteSpace(steamProfileUrl) ? steamProfileUrl : "-";

            if (steamDiffersFromUserLast)
                steamProfileField += "\n⚠️ Provided Steam ID differs from this user's last used Steam64.";

            if (steamUsedByOthers)
                steamProfileField += "\n⚠️ Provided Steam ID was used by another Discord account.";

            var embed = new EmbedBuilder()
                .WithTitle("New Verification Submission")
                .WithDescription(desc)
                .AddField("Faction", faction.ToString(), inline: true)
                .AddField("User ID", Context.User.Id.ToString(), inline: true)
                .AddField("Steam Profile", !string.IsNullOrWhiteSpace(steamProfileField) ? steamProfileField : "-", inline: false)
                .AddField("Roles to be granted", roleMentions, inline: false)
                .AddField("Steam history (recent)", string.IsNullOrWhiteSpace(steamHistory) ? "-" : steamHistory, inline: false)
                .AddField("Faction history (last 5)", string.IsNullOrWhiteSpace(track) ? "-" : track, inline: false)
                .WithColor(faction == "Colonial" ? Color.DarkGreen : Color.DarkBlue)
                .WithCurrentTimestamp()
                .WithFooter("Awaiting Approval")
                .Build();

            var component = new ComponentBuilder()
                .WithButton("Approve", "approve-verification", ButtonStyle.Success)
                .WithButton("Deny", "deny-verification", ButtonStyle.Danger)
                .Build();

            await reviewChannel.SendFileAsync(stream, file.Filename, embed: embed, components: component);
            
            // Log the submission event
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

            return "Verification submitted. Please wait for approval.";
        }

        private static string GetSteamResolveErrorMessage(SteamResolveResult steamResult)
        {
            return steamResult.Status switch
            {
                SteamResolveStatus.EmptyInput =>
                    "Steam ID is required for this server.",

                SteamResolveStatus.MissingApiKey =>
                    "Steam verification is not configured correctly. Please contact an administrator.",

                SteamResolveStatus.InvalidInput =>
                    "Please provide a valid Steam64 ID, Steam profile URL, or Steam vanity name.",

                SteamResolveStatus.NotFound =>
                    "Could not find a Steam profile matching that input.",

                SteamResolveStatus.RateLimited =>
                    "Steam is currently rate-limiting profile lookups. Please try again later.",

                SteamResolveStatus.SteamUnavailable =>
                    "Steam is currently unavailable. Please try again later.",

                SteamResolveStatus.Failed when !string.IsNullOrWhiteSpace(steamResult.Message) =>
                    $"Could not resolve the Steam profile. {steamResult.Message}",

                _ =>
                    "Could not resolve the Steam profile. Please check the input and try again."
            };
        }
    }
}
