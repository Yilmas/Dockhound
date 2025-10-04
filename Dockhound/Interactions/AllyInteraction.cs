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
    public class AllyInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DockhoundContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IGuildSettingsService _guildSettingsService;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public AllyInteraction(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IGuildSettingsService guildSettingsService)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _guildSettingsService = guildSettingsService;
        }

        // REQUEST ALLY
        [UserCommand("Request Ally")]
        public async Task RequestAlly(IUser targetUser)
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

            var allowedRoleIds = cfg.Verify.AllyAssignerRoles?.ToHashSet() ?? new HashSet<ulong>();

            bool isSelf = acting.Id == target.Id;
            bool hasPrivilege = acting.Roles.Any(r => allowedRoleIds.Contains(r.Id));

            if (!isSelf && !hasPrivilege)
            {
                await FollowupAsync("❌ You don't have permission to \"Request Ally\" for this user.", ephemeral: true);
                return;
            }

            var reviewChannel = cfg.Verify.ReviewChannelId is ulong id
                        ? guild.GetTextChannel(id)
                        : null;

            if (reviewChannel is null)
                return;

            // Determine faction (Colonial/Warden/Unknown)
            var factionRole = cfg.Roles.First(p => p.Name == "Faction");
            string faction = target.Roles.Any(r => r.Id == factionRole.Colonial) ? "Colonial"
                          : target.Roles.Any(r => r.Id == factionRole.Warden) ? "Warden"
                          : "Unknown";

            // Planned roles to assign (Ally + faction Ally if available)
            var ally = cfg.Roles.FirstOrDefault(r => string.Equals(r.Name, "Ally", StringComparison.OrdinalIgnoreCase));
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

            // Build a display string for planned roles
            string plannedRoles =
                rolesPlanned.Count switch
                {
                    0 => "-",
                    1 => $"<@&{rolesPlanned[0]}>",
                    _ => string.Join(", ", rolesPlanned.Select(id => $"<@&{id}>"))
                };

            // Faction text (enum or string)
            string factionText = faction is Enum ? faction.ToString()! : faction?.ToString() ?? "-";

            // Build embed
            var eb = new EmbedBuilder()
                .WithTitle("Ally Request")
                .WithColor(Color.Teal)
                .AddField("User", target.Mention, inline: true)
                .AddField("User ID", target.Id.ToString(), inline: true)
                .AddField("Faction", factionText, inline: true)
                .AddField("Planned Roles", plannedRoles, inline: false)
                .WithFooter($"Requested by {acting.Username}");


            var components = new ComponentBuilder()
                .WithButton("Approve", "ally-approve", ButtonStyle.Success)
                .WithButton("Deny", "ally-deny", ButtonStyle.Danger)
                .Build();

            var msg = await reviewChannel.SendMessageAsync(embed: eb.Build(), components: components);

            // Log request
            try
            {
                var log = new LogEvent(
                    eventName: "Ally Request",
                    messageId: msg.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"Requested ally for {target.Username} ({target.Id}), faction {faction}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log ally request: {e.Message}\n{e.StackTrace}");
            }

            await FollowupAsync("Ally request created.", ephemeral: true);
        }

        // APPROVE
        [ComponentInteraction("ally-approve")]
        public async Task ApproveAlly()
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
            var ally = cfg.Roles.FirstOrDefault(r => string.Equals(r.Name, "Ally", StringComparison.OrdinalIgnoreCase));
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


            if (rolesPlanned.Count > 0)
                await member.AddRolesAsync(rolesPlanned);

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
                await member.SendMessageAsync($"✅ You’ve been assigned the **Ally** role{(rolesPlanned.Count > 1 ? "s" : "")} in **{Context.Guild.Name}**. Welcome!");
            }
            catch
            {
                Console.WriteLine($"[WARN] Could not DM {member.Username} about ally approval.");
            }

            // Log
            try
            {
                var log = new LogEvent(
                    eventName: "Ally Approved",
                    messageId: comp.Message.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} approved ally for {member.Username}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log ally approval: {e.Message}\n{e.StackTrace}");
            }

            await FollowupAsync("Approved.", ephemeral: true);
        }

        // DENY
        [ComponentInteraction("ally-deny")]
        public async Task DenyAlly()
        {
            var comp = (SocketMessageComponent)Context.Interaction;
            var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();

            if (embed == null)
                return;

            var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
            if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
                return;

            var modal = new ModalBuilder("Denial Reason", $"ally-deny-reason:{userId}:{comp.Message.Id}")
                .AddTextInput("Why are you denying this?", "deny-reason-text", TextInputStyle.Paragraph, maxLength: 500,
                              placeholder: "Enter the reason for denial...");

            await RespondWithModalAsync(modal.Build());
        }

        // DENY MODAL SUBMIT
        [ModalInteraction("ally-deny-reason:*:*")]
        public async Task SubmitAllyDenyReason(ulong userId, ulong messageId, DenyReasonModal modal)
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

            var embed = reviewMessage.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder().WithTitle("Ally Request");
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
                    await member.SendMessageAsync($"❌ Your ally request has been denied in **{Context.Guild.Name}**.\n**Reason:** {modal.Reason}");
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
                    eventName: "Ally Denied",
                    messageId: reviewMessage.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"{Context.User.Username} denied ally for {member?.Username ?? userId.ToString()} with reason: {modal.Reason}"
                );
                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log ally denial: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
