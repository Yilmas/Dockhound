using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Logs;
using Dockhound.Modals;
using Dockhound.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Dockhound.Interactions
{
    public class RecruitmentInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WllTrackerContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppSettings _settings;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public RecruitmentInteraction(WllTrackerContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _settings = appSettings.Value;
        }

        [UserCommand("Request Recruit")]
        public async Task RequestRecruit(IUser targetUser)
        {
            if (Context.Guild is null)
                return;

            var guild = Context.Guild;
            var acting = guild.GetUser(Context.User.Id);
            var target = guild.GetUser(targetUser.Id);

            if (acting is null || target is null)
                return;

            // Permission gate: self OR has any allowed role
            var allowedRoleIds = (_configuration["ALLOWED_RECRUIT_ASSIGNER_ROLES"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => ulong.TryParse(s, out var id) ? id : (ulong?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            bool isSelf = acting.Id == target.Id;
            bool hasPrivilege = acting.Roles.Any(r => allowedRoleIds.Contains(r.Id));

            if (!isSelf && !hasPrivilege)
            {
                await RespondAsync("❌ You don't have permission to \"Request Recruit\" for this user.", ephemeral: true);
                return;
            }

            if (!ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out var reviewChannelId))
                return;

            var reviewChannel = guild.GetTextChannel(reviewChannelId);
            if (reviewChannel is null)
                return;

            // Determine faction
            var factionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Faction");
            string faction = target.Roles.Any(r => r.Id == factionRole.Colonial) ? "Colonial"
                          : target.Roles.Any(r => r.Id == factionRole.Warden) ? "Warden"
                          : "Unknown";

            // Desired roles to assign
            var recruitRole = DiscordRolesList.GetRoles().First(p => p.Name == "Recruit");
            var desiredRoleIds = new List<ulong> { recruitRole.Generic };
            if (string.Equals(faction, "Colonial", StringComparison.OrdinalIgnoreCase)) desiredRoleIds.Add(recruitRole.Colonial);
            if (string.Equals(faction, "Warden", StringComparison.OrdinalIgnoreCase)) desiredRoleIds.Add(recruitRole.Warden);

            // Filter out roles the user already has (idempotent)
            var existing = target.Roles.Select(r => r.Id).ToHashSet();
            var toAssign = desiredRoleIds.Where(id => !existing.Contains(id)).ToList();

            if (toAssign.Any())
            {
                try { await target.AddRolesAsync(toAssign); }
                catch (Exception e)
                {
                    Console.WriteLine($"[ERROR] AddRoles failed: {e.Message}");
                    await RespondAsync("Could not assign one or more roles, contact an administrator.", ephemeral: true);
                    return;
                }
            }

            // Build announcement
            string grantedText = toAssign.Any()
                ? string.Join(", ", toAssign.Select(id => $"<@&{id}>"))
                : "None (already had required roles)";

            var eb = new EmbedBuilder()
                .WithTitle("Recruitment Assigned")
                .WithColor(Color.Green)
                .AddField("User", target.Mention, true)
                .AddField("User ID", target.Id, true)
                .AddField("Faction", faction, true)
                .AddField("Granted Roles", grantedText, false)
                .WithFooter($"Assigned by {acting.Username}")
                .WithCurrentTimestamp();

            var msg = await reviewChannel.SendMessageAsync(embed: eb.Build());

            // Best-effort DM
            try
            {
                if (toAssign.Any())
                    await target.SendMessageAsync($"✅ You’ve been assigned the **Recruit** role{(toAssign.Count > 1 ? "s" : "")} in **{guild.Name}**. Welcome!");
            }
            catch { /* ignore if DMs are closed */ }

            // Log
            try
            {
                var log = new LogEvent(
                    eventName: "Recruit Role Auto-Assigned",
                    messageId: msg.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{acting.Username} assigned recruit for {target.Username}; granted: {grantedText}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log recruit assignment: {e.Message}\n{e.StackTrace}");
            }

            await RespondAsync("Recruit assigned.", ephemeral: true);
        }


        // REQUEST RECRUIT
        //[UserCommand("Request Recruit")]
        //public async Task RequestRecruit(IUser targetUser)
        //{
        //    if (Context.Guild is null)
        //        return;

        //    var guild = Context.Guild;
        //    var acting = guild.GetUser(Context.User.Id);
        //    var target = guild.GetUser(targetUser.Id);

        //    if (acting is null || target is null)
        //        return;

        //    // --- Permission gate: self OR has any allowed role ---
        //    var allowedRoleIds = (_configuration["ALLOWED_RECRUIT_ASSIGNER_ROLES"] ?? "")
        //        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        //        .Select(s => ulong.TryParse(s, out var id) ? id : (ulong?)null)
        //        .Where(id => id.HasValue)
        //        .Select(id => id!.Value)
        //        .ToHashSet();

        //    bool isSelf = acting.Id == target.Id;
        //    bool hasPrivilege = acting.Roles.Any(r => allowedRoleIds.Contains(r.Id));

        //    if (!isSelf && !hasPrivilege)
        //    {
        //        await RespondAsync("❌ You don't have permission to \"Request Recruit\" for this user.", ephemeral: true);
        //        return;
        //    }
        //    // --- End permission gate ---

        //    if (!ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out var reviewChannelId))
        //        return;

        //    var reviewChannel = guild.GetTextChannel(reviewChannelId);
        //    if (reviewChannel is null)
        //        return;

        //    // Determine faction
        //    var factionRole = DiscordRolesList.GetRoles().First(p => p.Name == "Faction");
        //    string faction = target.Roles.Any(r => r.Id == factionRole.Colonial) ? "Colonial"
        //                  : target.Roles.Any(r => r.Id == factionRole.Warden) ? "Warden"
        //                  : "Unknown";

        //    // Planned roles to assign
        //    var recruitRole = DiscordRolesList.GetRoles().First(p => p.Name == "Recruit");
        //    var rolesPlanned = new List<ulong> { recruitRole.Generic };
        //    if (string.Equals(faction, "Colonial", StringComparison.OrdinalIgnoreCase)) rolesPlanned.Add(recruitRole.Colonial);
        //    if (string.Equals(faction, "Warden", StringComparison.OrdinalIgnoreCase)) rolesPlanned.Add(recruitRole.Warden);

        //    // Build embed
        //    var eb = new EmbedBuilder()
        //        .WithTitle("Recruitment Request")
        //        .WithColor(Color.DarkBlue)
        //        .AddField("User", target.Mention, true)
        //        .AddField("User ID", target.Id, true)
        //        .AddField("Faction", faction, true)
        //        .AddField("Planned Roles",
        //            rolesPlanned.Count == 1
        //                ? $"<@&{recruitRole.Generic}>"
        //                : string.Join(", ", rolesPlanned.Select(id => $"<@&{id}>")),
        //            false)
        //        .WithFooter($"Requested by {acting.Username}");

        //    var components = new ComponentBuilder()
        //        .WithButton("Approve", "recruit_approve", ButtonStyle.Success)
        //        .WithButton("Deny", "recruit_deny", ButtonStyle.Danger)
        //        .Build();

        //    var msg = await reviewChannel.SendMessageAsync(embed: eb.Build(), components: components);

        //    // Log request
        //    try
        //    {
        //        var log = new LogEvent(
        //            eventName: "Recruitment Request",
        //            messageId: msg.Id,
        //            username: Context.User.Username,
        //            userId: Context.User.Id,
        //            changes: $"Requested recruit for {target.Username} ({target.Id}), faction {faction}"
        //        );
        //        _dbContext.LogEvents.Add(log);
        //        await _dbContext.SaveChangesAsync();
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine($"[ERROR] Failed to log recruit request: {e.Message}\n{e.StackTrace}");
        //    }

        //    await RespondAsync("Recruitment request created.", ephemeral: true);
        //}

        //// APPROVE
        //[ComponentInteraction("recruit_approve")]
        //public async Task ApproveRecruit()
        //{
        //    var comp = (SocketMessageComponent)Context.Interaction;
        //    var guild = Context.Guild;

        //    var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();
        //    if (embed == null)
        //        return;

        //    var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
        //    var factionField = embed.Fields.FirstOrDefault(f => f.Name == "Faction");

        //    if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
        //        return;
        //    if (factionField == null)
        //        return;

        //    var member = guild?.GetUser(userId);
        //    if (member == null)
        //        return;

        //    await DeferAsync(ephemeral: true);

        //    // Planned roles (assign now)
        //    var faction = factionField.Value?.ToString() ?? "Unknown";
        //    var recruitRole = DiscordRolesList.GetRoles().First(p => p.Name == "Recruit");
        //    var rolesToAssign = new List<ulong> { recruitRole.Generic };
        //    if (string.Equals(faction, "Colonial", StringComparison.OrdinalIgnoreCase)) rolesToAssign.Add(recruitRole.Colonial);
        //    if (string.Equals(faction, "Warden", StringComparison.OrdinalIgnoreCase)) rolesToAssign.Add(recruitRole.Warden);

        //    if (rolesToAssign.Count > 0)
        //        await member.AddRolesAsync(rolesToAssign);

        //    // Update review message
        //    embed.WithFooter($"Approved ✅ by {Context.User.Username}");
        //    embed.Color = Color.Green;

        //    await comp.Message.ModifyAsync(m =>
        //    {
        //        m.Embeds = new[] { embed.Build() };
        //        m.Components = new ComponentBuilder().Build();
        //    });

        //    // DM the user (best-effort)
        //    try
        //    {
        //        await member.SendMessageAsync($"✅ You’ve been assigned the **Recruit** role{(rolesToAssign.Count > 1 ? "s" : "")} for HvL. Welcome!");
        //    }
        //    catch
        //    {
        //        Console.WriteLine($"[WARN] Could not DM {member.Username} about recruitment approval.");
        //    }

        //    // Log
        //    try
        //    {
        //        var log = new LogEvent(
        //            eventName: "Recruitment Approved",
        //            messageId: comp.Message.Id,
        //            username: Context.User.Username,
        //            userId: Context.User.Id,
        //            changes: $"{Context.User.Username} approved recruit for {member.Username}"
        //        );
        //        _dbContext.LogEvents.Add(log);
        //        await _dbContext.SaveChangesAsync();
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine($"[ERROR] Failed to log recruit approval: {e.Message}\n{e.StackTrace}");
        //    }

        //    await FollowupAsync("Approved.", ephemeral: true);
        //}

        //// DENY
        //[ComponentInteraction("recruit_deny")]
        //public async Task DenyRecruit()
        //{
        //    var comp = (SocketMessageComponent)Context.Interaction;
        //    var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();

        //    if (embed == null)
        //        return;

        //    var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
        //    if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
        //        return;

        //    var modal = new ModalBuilder("Denial Reason", $"recruit_deny_reason:{userId}:{comp.Message.Id}")
        //        .AddTextInput("Why are you denying this?", "deny_reason_text", TextInputStyle.Paragraph, maxLength: 500,
        //                      placeholder: "Enter the reason for denial...");

        //    await RespondWithModalAsync(modal.Build());
        //}

        //// DENY MODAL SUBMIT
        //[ModalInteraction("recruit_deny_reason:*:*")]
        //public async Task SubmitRecruitDenyReason(ulong userId, ulong messageId, DenyReasonModal modal)
        //{
        //    var guild = Context.Guild;
        //    if (guild is null)
        //        return;

        //    IUserMessage? reviewMessage = null;

        //    if (Context.Interaction is SocketModal modalInteraction && modalInteraction.Message != null)
        //    {
        //        reviewMessage = modalInteraction.Message as IUserMessage;
        //    }

        //    if (reviewMessage == null)
        //    {
        //        if (!ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out var reviewChannelId))
        //            return;

        //        var reviewChannel = guild.GetTextChannel(reviewChannelId);
        //        reviewMessage = await reviewChannel?.GetMessageAsync(messageId) as IUserMessage;
        //    }

        //    if (reviewMessage is null)
        //        return;

        //    var embed = reviewMessage.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder().WithTitle("Recruitment Request");
        //    embed.AddField("Denial Reason", modal.Reason, inline: true);
        //    embed.WithFooter($"Denied ❌ by {Context.User.Username}");
        //    embed.WithColor(Color.Red);

        //    await reviewMessage.ModifyAsync(m =>
        //    {
        //        m.Embeds = new[] { embed.Build() };
        //        m.Components = new ComponentBuilder().Build();
        //    });

        //    // Notify user via DM
        //    var member = guild.GetUser(userId);
        //    if (member != null)
        //    {
        //        try
        //        {
        //            await member.SendMessageAsync($"❌ Your recruitment request has been denied.\n**Reason:** {modal.Reason}");
        //        }
        //        catch
        //        {
        //            await RespondAsync($"Could not DM {member.Username}. They may have DMs disabled.", ephemeral: true);
        //        }
        //    }

        //    await RespondAsync("Deny reason submitted and user has been notified.", ephemeral: true);

        //    // Log
        //    try
        //    {
        //        var log = new LogEvent(
        //            eventName: "Recruitment Denied",
        //            messageId: reviewMessage.Id,
        //            username: Context.User.Username,
        //            userId: Context.User.Id,
        //            changes: $"{Context.User.Username} denied recruit for {member?.Username ?? userId.ToString()} with reason: {modal.Reason}"
        //        );
        //        _dbContext.LogEvents.Add(log);
        //        await _dbContext.SaveChangesAsync();
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine($"[ERROR] Failed to log recruit denial: {e.Message}\n{e.StackTrace}");
        //    }
        //}
    }
}
