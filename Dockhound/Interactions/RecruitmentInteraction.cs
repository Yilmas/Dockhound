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
            var ally = cfg.Roles.FirstOrDefault(r => string.Equals(r.Name, "Recruit", StringComparison.OrdinalIgnoreCase));
            var rolesPlanned = new List<ulong>();

            void Add(ulong? id) { if (id.HasValue && id.Value != 0) rolesPlanned.Add(id.Value); }

            if (ally is not null)
            {
                Add(ally.Generic);

                if (string.Equals(faction, "Colonial", StringComparison.OrdinalIgnoreCase))
                    Add(ally.Colonial);
                else if (string.Equals(faction, "Warden", StringComparison.OrdinalIgnoreCase))
                    Add(ally.Warden);
            }

            rolesPlanned = rolesPlanned.Distinct().ToList();


            // Filter out roles the user already has (idempotent)
            var existing = target.Roles.Select(r => r.Id).ToHashSet();
            var toAssign = rolesPlanned.Where(id => !existing.Contains(id)).ToList();

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

            await FollowupAsync("Recruit requested.", ephemeral: true);
        }
    }
}
