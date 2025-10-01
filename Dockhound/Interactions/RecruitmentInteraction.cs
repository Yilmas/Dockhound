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
        private readonly DockhoundContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppSettings _settings;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public RecruitmentInteraction(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
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
            var allowedRoleIds = _settings.Verify.RecruitAssignerRoles?.ToHashSet() ?? new HashSet<ulong>();


            bool isSelf = acting.Id == target.Id;
            bool hasPrivilege = acting.Roles.Any(r => allowedRoleIds.Contains(r.Id));

            if (!isSelf && !hasPrivilege)
            {
                await RespondAsync("❌ You don't have permission to \"Request Recruit\" for this user.", ephemeral: true);
                return;
            }

            //if (!ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out var reviewChannelId))
            //    return;

            var reviewChannel = guild.GetTextChannel(_settings.Verify.ReviewChannelId);
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
    }
}
