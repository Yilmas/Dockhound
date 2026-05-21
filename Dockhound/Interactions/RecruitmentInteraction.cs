using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Logs;
using Dockhound.Modals;
using Dockhound.Models;
using Dockhound.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Dockhound.Interactions
{
    public class RecruitmentInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DockhoundContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IGuildSettingsService _guildSettingsService;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public RecruitmentInteraction(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IGuildSettingsService guildSettingsService)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _guildSettingsService = guildSettingsService;
        }

        [UserCommand("Request Recruit")]
        public async Task RequestRecruit(IUser targetUser)
        {
            await DeferAsync(ephemeral: true);

            if (Context.Guild is null)
                return;

            var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

            var guild = Context.Guild;
            var acting = guild.GetUser(Context.User.Id);
            var target = guild.GetUser(targetUser.Id);

            if (acting is null || target is null)
                return;

            // Permission gate: self OR has any allowed role
            var allowedRoleIds = cfg.Verify.RecruitAssignerRoles?.ToHashSet() ?? new HashSet<ulong>();

            bool isSelf = acting.Id == target.Id;
            bool hasPrivilege = acting.Roles.Any(r => allowedRoleIds.Contains(r.Id));

            if (!isSelf && !hasPrivilege)
            {
                await FollowupAsync("❌ You don't have permission to \"Request Recruit\" for this user.", ephemeral: true);
                return;
            }

            var reviewChannel = cfg.Verify.ReviewChannelId is ulong id
                ? guild.GetTextChannel(id)
                : null;

            if (reviewChannel is null)
                return;

            // Determine faction
            var factionRole = cfg.Roles.First(p => p.Name == "Faction");
            string faction = target.Roles.Any(r => r.Id == factionRole.Colonial) ? "Colonial"
                          : target.Roles.Any(r => r.Id == factionRole.Warden) ? "Warden"
                          : "Unknown";

            // Desired roles to assign
            var recruit = cfg.Roles.FirstOrDefault(r => string.Equals(r.Name, "Recruit", StringComparison.OrdinalIgnoreCase));
            var rolesPlanned = new List<ulong>();

            void Add(ulong? id) { if (id.HasValue && id.Value != 0) rolesPlanned.Add(id.Value); }

            if (recruit is not null)
            {
                Add(recruit.Generic);

                if (string.Equals(faction, "Colonial", StringComparison.OrdinalIgnoreCase))
                    Add(recruit.Colonial);
                else if (string.Equals(faction, "Warden", StringComparison.OrdinalIgnoreCase))
                    Add(recruit.Warden);
            }

            rolesPlanned = rolesPlanned.Distinct().ToList();

            // Filter out roles the user already has (idempotent)
            var existing = target.Roles.Select(r => r.Id).ToHashSet();
            var toAssign = rolesPlanned.Where(id => !existing.Contains(id)).ToList();

            if (hasPrivilege)
            {
                // Auto-approve path for recruiters: assign roles immediately, post an "Assigned" message (no review buttons)
                if (toAssign.Any())
                {
                    try { await target.AddRolesAsync(toAssign); }
                    catch (Exception e)
                    {
                        Console.WriteLine($"[ERROR] AddRoles failed: {e.Message}");
                        await FollowupAsync("Could not assign one or more roles, contact an administrator.", ephemeral: true);
                        return;
                    }
                }

                // Build announcement for auto-assigned
                string grantedText = toAssign.Any()
                    ? string.Join(", ", toAssign.Select(id => $"<@&{id}>"))
                    : "None (already had required roles)";

                var eb = new EmbedBuilder()
                    .WithTitle("Recruitment Assigned (Auto-Approved)")
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
                        changes: $"{acting.Username} auto-assigned recruit for {target.Username}; granted: {grantedText}"
                    );
                    _dbContext.LogEvents.Add(log);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[ERROR] Failed to log recruit assignment: {e.Message}\n{e.StackTrace}");
                }

                await FollowupAsync("Recruit request auto-approved.", ephemeral: true);
                return;
            }

            // Non-privileged: create a review request for manual approval (Approve/Deny buttons)
            string plannedRoles =
                rolesPlanned.Count switch
                {
                    0 => "-",
                    1 => $"<@&{rolesPlanned[0]}>",
                    _ => string.Join(", ", rolesPlanned.Select(id => $"<@&{id}>"))
                };

            var embedRequest = new EmbedBuilder()
                .WithTitle("Recruit Request")
                .WithColor(Color.Teal)
                .AddField("User", target.Mention, inline: true)
                .AddField("User ID", target.Id.ToString(), inline: true)
                .AddField("Faction", faction, inline: true)
                .AddField("Planned Roles", plannedRoles, inline: false)
                .WithFooter($"Requested by {acting.Username}");

            var components = new ComponentBuilder()
                .WithButton("Approve", "recruit-approve", ButtonStyle.Success)
                .WithButton("Deny", "recruit-deny", ButtonStyle.Danger)
                .Build();

            var reviewMsg = await reviewChannel.SendMessageAsync(embed: embedRequest.Build(), components: components);

            // Log request
            try
            {
                var log = new LogEvent(
                    eventName: "Recruit Request",
                    messageId: reviewMsg.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"Requested recruit for {target.Username} ({target.Id}), faction {faction}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log recruit request: {e.Message}\n{e.StackTrace}");
            }

            await FollowupAsync("Recruit request created.", ephemeral: true);
        }

        // APPROVE (manual reviewer)
        [ComponentInteraction("recruit-approve")]
        public async Task ApproveRecruit()
        {
            await DeferAsync(ephemeral: true);

            var comp = (SocketMessageComponent)Context.Interaction;
            var guild = Context.Guild;

            var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

            var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();
            if (embed == null)
                return;

            var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
            var factionField = embed.Fields.FirstOrDefault(f => f.Name == "Faction");

            if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
                return;
            if (factionField == null)
                return;

            var member = guild?.GetUser(userId);
            if (member == null)
                return;

            // Planned roles (assign now)
            var faction = factionField.Value?.ToString() ?? "Unknown";
            var recruit = cfg.Roles.FirstOrDefault(r => string.Equals(r.Name, "Recruit", StringComparison.OrdinalIgnoreCase));
            var rolesPlanned = new List<ulong>();

            void Add(ulong? id) { if (id.HasValue && id.Value != 0) rolesPlanned.Add(id.Value); }

            if (recruit is not null)
            {
                Add(recruit.Generic);

                if (string.Equals(faction, "Colonial", StringComparison.OrdinalIgnoreCase))
                    Add(recruit.Colonial);
                else if (string.Equals(faction, "Warden", StringComparison.OrdinalIgnoreCase))
                    Add(recruit.Warden);
            }

            rolesPlanned = rolesPlanned.Distinct().ToList();

            if (rolesPlanned.Count > 0)
            {
                try { await member.AddRolesAsync(rolesPlanned); }
                catch (Exception e)
                {
                    Console.WriteLine($"[ERROR] AddRoles failed on approve: {e.Message}");
                }
            }

            // Update review message
            embed.WithFooter($"Approved ✅ by {Context.User.Username}");
            embed.Color = Color.Green;

            await comp.Message.ModifyAsync(m =>
            {
                m.Embeds = new[] { embed.Build() };
                m.Components = new ComponentBuilder().Build();
            });

            // DM the user (best-effort)
            try
            {
                await member.SendMessageAsync($"✅ You’ve been assigned the **Recruit** role{(rolesPlanned.Count > 1 ? "s" : "")} in **{Context.Guild.Name}**. Welcome!");
            }
            catch
            {
                Console.WriteLine($"[WARN] Could not DM {member.Username} about recruit approval.");
            }

            // Log
            try
            {
                var log = new LogEvent(
                    eventName: "Recruit Approved",
                    messageId: comp.Message.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} approved recruit for {member.Username}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log recruit approval: {e.Message}\n{e.StackTrace}");
            }

            await FollowupAsync("Approved.", ephemeral: true);
        }

        // DENY (open modal for reason)
        [ComponentInteraction("recruit-deny")]
        public async Task DenyRecruit()
        {
            var comp = (SocketMessageComponent)Context.Interaction;
            var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();

            if (embed == null)
                return;

            var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
            if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
                return;

            var modal = new ModalBuilder("Denial Reason", $"recruit-deny-reason:{userId}:{comp.Message.Id}")
                .AddTextInput("Why are you denying this?", "deny-reason-text", TextInputStyle.Paragraph, maxLength: 500,
                              placeholder: "Enter the reason for denial...");

            await RespondWithModalAsync(modal.Build());
        }

        // DENY MODAL SUBMIT
        [ModalInteraction("recruit-deny-reason:*:*")]
        public async Task SubmitRecruitDenyReason(ulong userId, ulong messageId, DenyReasonModal modal)
        {
            await DeferAsync(ephemeral: true);

            var guild = Context.Guild;
            if (guild is null)
                return;

            IUserMessage? reviewMessage = null;

            if (Context.Interaction is SocketModal modalInteraction && modalInteraction.Message != null)
            {
                reviewMessage = modalInteraction.Message as IUserMessage;
            }

            if (reviewMessage == null)
            {
                var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);
                var reviewChannel = cfg.Verify.ReviewChannelId is ulong id
                        ? guild.GetTextChannel(id)
                        : null;

                reviewMessage = await reviewChannel?.GetMessageAsync(messageId) as IUserMessage;
            }

            if (reviewMessage is null)
                return;

            var embed = reviewMessage.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder().WithTitle("Recruit Request");
            embed.AddField("Denial Reason", modal.Reason, inline: true);
            embed.WithFooter($"Denied ❌ by {Context.User.Username}");
            embed.WithColor(Color.Red);

            await reviewMessage.ModifyAsync(m =>
            {
                m.Embeds = new[] { embed.Build() };
                m.Components = new ComponentBuilder().Build();
            });

            // Notify user via DM
            var member = guild.GetUser(userId);
            if (member != null)
            {
                try
                {
                    await member.SendMessageAsync($"❌ Your recruit request has been denied in **{Context.Guild.Name}**.\n**Reason:** {modal.Reason}");
                }
                catch
                {
                    await FollowupAsync($"Could not DM {member.Username}. They may have DMs disabled.", ephemeral: true);
                }
            }

            await FollowupAsync("Deny reason submitted and user has been notified.", ephemeral: true);

            // Log
            try
            {
                var log = new LogEvent(
                    eventName: "Recruit Denied",
                    messageId: reviewMessage.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} denied recruit for {member?.Username ?? userId.ToString()} with reason: {modal.Reason}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log recruit denial: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
