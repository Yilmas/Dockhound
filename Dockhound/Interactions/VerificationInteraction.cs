using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Logs;
using Dockhound.Modals;
using Dockhound.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Interactions
{
    public class VerificationInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WllTrackerContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppSettings _settings;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public VerificationInteraction(WllTrackerContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _settings = appSettings.Value;
        }

        // APPROVE
        [ComponentInteraction("approve-verification")]
        public async Task ApproveVerification()
        {
            var comp = (SocketMessageComponent)Context.Interaction;
            var guild = Context.Guild;

            var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder();
            if (embed == null) 
                return;

            var userIdField = embed.Fields.FirstOrDefault(f => f.Name == "User ID");
            if (userIdField == null || !ulong.TryParse(userIdField.Value?.ToString(), out var userId))
                return;

            var factionField = embed.Fields.FirstOrDefault(f => f.Name == "Faction");
            if (factionField == null)
                return;

            var user = guild?.GetUser(userId);
            if (user == null)
                return;

            //_ = ulong.TryParse(_configuration["CHANNEL_VERIFY_NOTIFICATION"], out var notificationChannelId);
            var notificationChannel = guild?.GetTextChannel(_settings.Verify.NotificationChannelId);

            await DeferAsync(ephemeral: true);

            // Assign roles
            var rolesToAssign = DiscordRolesList.GetDeltaRoleIdList(user, factionField.Value?.ToString() ?? string.Empty);
            if (rolesToAssign.Count > 0)
                await user.AddRolesAsync(rolesToAssign);

            // Update the review message: footer + remove buttons
            embed.WithFooter($"Approved ✅ by {Context.User.Username}");
            await comp.Message.ModifyAsync(m =>
            {
                m.Embeds = new[] { embed.Build() };
                m.Components = new ComponentBuilder().Build();
            });

            // Optional DM + faction-secure channel mention
            try
            {
                ulong factionSecureComms = 0;
                var faction = factionField.Value?.ToString();

                if (string.Equals(faction, "Colonial", StringComparison.OrdinalIgnoreCase))
                    factionSecureComms = _settings.Verify.ColonialSecureChannelId;
                //_ = ulong.TryParse(_configuration["CHANNEL_FACTION_COLONIAL_SECURE"], out factionSecureComms);
                else if (string.Equals(faction, "Warden", StringComparison.OrdinalIgnoreCase))
                    factionSecureComms = _settings.Verify.WardenSecureChannelId;
                //_ = ulong.TryParse(_configuration["CHANNEL_FACTION_WARDEN_SECURE"], out factionSecureComms);

                var factionSecureChannel = guild?.GetTextChannel(factionSecureComms);
                await user.SendMessageAsync(
                    $"✅ Your HvL verification has been approved! 🎉 " +
                    (factionSecureChannel != null
                        ? $"You now have access to faction-specific channels such as {factionSecureChannel.Mention}."
                        : "Faction-specific channels are now available to you.")
                );
            }
            catch
            {
                Console.WriteLine($"[ERROR] Failed to send a DM to {user.Username}. They may have DMs disabled.");
            }

            // Optional: notify channel
            // if (notificationChannel != null)
            //     await notificationChannel.SendMessageAsync($"{user.Mention}, your verification has been approved!");

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

            // SocketGuild uses cached sync getters
            var user = guild.GetUser(userId);
            if (user is null)
                return;

            //if (!ulong.TryParse(_configuration["CHANNEL_VERIFY_REVIEW"], out var reviewChannelId))
            //    return;

            var verificationChannel = guild.GetTextChannel(_settings.Verify.ReviewChannelId);
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
                await user.SendMessageAsync($"❌ Your verification for HvL has been denied.\n**Reason:** {modal.Reason}");
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

    }
}
