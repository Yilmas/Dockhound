using Discord;
using Discord.WebSocket;
using Dockhound.Components;
using Dockhound.Config;
using Dockhound.Logs;
using Dockhound.Models;

namespace Dockhound.Services
{
    public sealed class HoneypotService : IHoneypotService
    {
        private readonly DiscordSocketClient _client;
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly DockhoundContext _dbContext;

        public HoneypotService(DiscordSocketClient client, IGuildSettingsService guildSettingsService, DockhoundContext dbContext)
        {
            _client = client;
            _guildSettingsService = guildSettingsService;
            _dbContext = dbContext;
        }

        public async Task HandleMessageAsync(SocketMessage message)
        {
            if (message.Author.IsBot || message.Author.Id == _client.CurrentUser.Id)
                return;

            if (message.Channel is not SocketTextChannel channel)
                return;

            var cfg = await _guildSettingsService.GetAsync(channel.Guild.Id);
            if (!cfg.Honeypot.Enabled || cfg.Honeypot.ChannelId != channel.Id)
                return;

            await BanAndReportAsync(
                channel.Guild,
                message.Author.Id,
                "Honeypot channel message",
                cfg.Honeypot,
                proof: message.Content,
                sourceChannelId: channel.Id,
                sourceMessageId: message.Id,
                extraFields: BuildMessageFields(message));
        }

        public async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.UserId == _client.CurrentUser.Id)
                return;

            var messageChannel = await channel.GetOrDownloadAsync();
            if (messageChannel is not SocketTextChannel textChannel)
                return;

            var cfg = await _guildSettingsService.GetAsync(textChannel.Guild.Id);
            if (!cfg.Honeypot.Enabled ||
                cfg.Honeypot.ReactionChannelId != textChannel.Id ||
                cfg.Honeypot.ReactionMessageId != message.Id)
                return;

            var reactedMessage = await message.GetOrDownloadAsync();
            var user = reaction.User.IsSpecified
                ? reaction.User.Value
                : await reaction.Channel.GetUserAsync(reaction.UserId);

            if (user?.IsBot == true)
                return;

            await BanAndReportAsync(
                textChannel.Guild,
                reaction.UserId,
                "Honeypot message reaction",
                cfg.Honeypot,
                proof: $"Reacted with {reaction.Emote.Name}",
                sourceChannelId: textChannel.Id,
                sourceMessageId: reactedMessage.Id,
                extraFields: BuildReactionFields(reaction, reactedMessage));
        }

        public async Task<IUserMessage> CreateHoneypotMessageAsync(SocketGuild guild, IMessageChannel channel, IUser createdBy, string? content = null)
        {
            var message = await channel.SendMessageAsync(embed: HoneypotComponents.BuildTrapEmbed(content));

            await _guildSettingsService.PatchAsync(
                guild.Id,
                cfg =>
                {
                    cfg.Honeypot.Enabled = true;
                    cfg.Honeypot.ReactionChannelId = channel.Id;
                    cfg.Honeypot.ReactionMessageId = message.Id;
                },
                $"{createdBy.Username}#{createdBy.Id}");

            return message;
        }

        public Task RecordSavingGraceAsync(SocketGuild guild)
            => IncrementTrapMessageCounterAsync(guild, "Saving Graces");

        private async Task BanAndReportAsync(
            SocketGuild guild,
            ulong userId,
            string reason,
            GuildConfig.HoneypotSettings settings,
            string proof,
            ulong sourceChannelId,
            ulong sourceMessageId,
            IEnumerable<EmbedFieldBuilder> extraFields)
        {
            IGuildUser? user = guild.GetUser(userId);
            user ??= await _client.Rest.GetGuildUserAsync(guild.Id, userId);
            if (user is null || user.IsBot)
                return;

            if (IsProtectedMember(user))
            {
                await ReportProtectedUserAsync(guild, settings, user, reason, sourceChannelId, sourceMessageId);
                return;
            }

            var auditReason = $"{reason}: {user.Username} ({user.Id})";

            try
            {
                await guild.AddBanAsync(userId, Math.Clamp(settings.MessagePruneDays, 0, 7), auditReason);
            }
            catch (Exception ex)
            {
                await ReportFailureAsync(guild, settings, user, reason, sourceChannelId, sourceMessageId, ex);
                return;
            }

            await ReportBanAsync(guild, settings, user, reason, proof, sourceChannelId, sourceMessageId, extraFields);
            await IncrementTrapMessageCounterAsync(guild, "Bots Squashed");

            try
            {
                _dbContext.LogEvents.Add(new LogEvent(
                    eventType: LogEventType.HoneypotBan,
                    guildId: guild.Id,
                    messageId: sourceMessageId,
                    username: user.Username,
                    userId: user.Id,
                    changes: $"{reason} in channel {sourceChannelId}. Proof: {Trunc(proof, 250)}"));

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to log honeypot ban: {ex.Message}");
            }
        }

        private async Task ReportBanAsync(
            SocketGuild guild,
            GuildConfig.HoneypotSettings settings,
            IGuildUser user,
            string reason,
            string proof,
            ulong sourceChannelId,
            ulong sourceMessageId,
            IEnumerable<EmbedFieldBuilder> extraFields)
        {
            var reportChannel = ResolveReportChannel(guild, settings);
            if (reportChannel is null)
                return;

            var jumpLink = $"https://discord.com/channels/{guild.Id}/{sourceChannelId}/{sourceMessageId}";

            await reportChannel.SendMessageAsync(
                embed: HoneypotComponents.BuildBanReportEmbed(user, reason, proof, jumpLink, extraFields),
                components: HoneypotComponents.BuildReviewComponents(user.Id));
        }

        private async Task ReportFailureAsync(SocketGuild guild, GuildConfig.HoneypotSettings settings, IGuildUser user, string reason, ulong sourceChannelId, ulong sourceMessageId, Exception ex)
        {
            var reportChannel = ResolveReportChannel(guild, settings);
            if (reportChannel is null)
                return;

            var jumpLink = $"https://discord.com/channels/{guild.Id}/{sourceChannelId}/{sourceMessageId}";
            await reportChannel.SendMessageAsync(embed: HoneypotComponents.BuildBanFailureEmbed(user, reason, jumpLink, ex));
        }

        private async Task ReportProtectedUserAsync(SocketGuild guild, GuildConfig.HoneypotSettings settings, IGuildUser user, string reason, ulong sourceChannelId, ulong sourceMessageId)
        {
            var reportChannel = ResolveReportChannel(guild, settings);
            if (reportChannel is null)
                return;

            var jumpLink = $"https://discord.com/channels/{guild.Id}/{sourceChannelId}/{sourceMessageId}";
            var embed = HoneypotComponents.BuildProtectedUserEmbed(
                user,
                reason,
                jumpLink,
                GetProtectedPermissionSummary(user));

            await reportChannel.SendMessageAsync(embed: embed);
        }

        private static IEnumerable<EmbedFieldBuilder> BuildMessageFields(SocketMessage message)
        {
            if (message.Attachments.Count > 0)
            {
                yield return new EmbedFieldBuilder()
                    .WithName("Attachments")
                    .WithValue(string.Join("\n", message.Attachments.Take(3).Select(a => $"{a.Filename}: {a.Url}")))
                    .WithIsInline(false);
            }
        }

        private static IEnumerable<EmbedFieldBuilder> BuildReactionFields(SocketReaction reaction, IUserMessage message)
        {
            return Enumerable.Empty<EmbedFieldBuilder>();
        }

        private static IMessageChannel? ResolveReportChannel(SocketGuild guild, GuildConfig.HoneypotSettings settings)
        {
            if (settings.ReportChannelId is ulong configuredId && guild.GetTextChannel(configuredId) is { } configured)
                return configured;

            return guild.SystemChannel ?? guild.SafetyAlertsChannel as IMessageChannel;
        }

        private static bool IsProtectedMember(IGuildUser user)
        {
            var permissions = user.GuildPermissions;

            return permissions.Administrator
                || permissions.ManageGuild
                || permissions.BanMembers
                || permissions.KickMembers
                || permissions.ManageRoles
                || permissions.ManageChannels
                || permissions.ManageMessages
                || permissions.ModerateMembers
                || permissions.ManageNicknames
                || permissions.ManageWebhooks;
        }

        private static string GetProtectedPermissionSummary(IGuildUser user)
        {
            var permissions = user.GuildPermissions;
            var names = new List<string>();

            if (permissions.Administrator) names.Add("Administrator");
            if (permissions.ManageGuild) names.Add("Manage Server");
            if (permissions.BanMembers) names.Add("Ban Members");
            if (permissions.KickMembers) names.Add("Kick Members");
            if (permissions.ManageRoles) names.Add("Manage Roles");
            if (permissions.ManageChannels) names.Add("Manage Channels");
            if (permissions.ManageMessages) names.Add("Manage Messages");
            if (permissions.ModerateMembers) names.Add("Moderate Members");
            if (permissions.ManageNicknames) names.Add("Manage Nicknames");
            if (permissions.ManageWebhooks) names.Add("Manage Webhooks");

            return names.Count == 0 ? "Unknown privileged permission" : string.Join(", ", names);
        }

        private async Task IncrementTrapMessageCounterAsync(SocketGuild guild, string fieldName)
        {
            var cfg = await _guildSettingsService.GetAsync(guild.Id);
            var hp = cfg.Honeypot;

            if (hp.ReactionChannelId is not ulong channelId || hp.ReactionMessageId is not ulong messageId)
                return;

            var channel = guild.GetTextChannel(channelId);
            if (channel is null)
                return;

            try
            {
                if (await channel.GetMessageAsync(messageId) is not IUserMessage message)
                    return;

                var embed = message.Embeds.FirstOrDefault()?.ToEmbedBuilder()
                    ?? HoneypotComponents.BuildTrapEmbed(null).ToEmbedBuilder();

                IncrementField(embed, fieldName);
                EnsureCounterField(embed, fieldName == "Bots Squashed" ? "Saving Graces" : "Bots Squashed");

                await message.ModifyAsync(m => m.Embeds = new[] { embed.Build() });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to update honeypot counter message: {ex.Message}");
            }
        }

        private static void IncrementField(EmbedBuilder embed, string fieldName)
        {
            var index = FindFieldIndex(embed, fieldName);
            var value = index >= 0 && int.TryParse(embed.Fields[index].Value?.ToString(), out var current)
                ? current + 1
                : 1;

            SetCounterField(embed, fieldName, value, index);
        }

        private static void EnsureCounterField(EmbedBuilder embed, string fieldName)
        {
            if (FindFieldIndex(embed, fieldName) < 0)
                SetCounterField(embed, fieldName, 0, -1);
        }

        private static int FindFieldIndex(EmbedBuilder embed, string fieldName)
        {
            for (var i = 0; i < embed.Fields.Count; i++)
            {
                if (string.Equals(embed.Fields[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void SetCounterField(EmbedBuilder embed, string fieldName, int value, int index)
        {
            var field = new EmbedFieldBuilder()
                .WithName(fieldName)
                .WithValue(value.ToString())
                .WithIsInline(true);

            if (index >= 0)
                embed.Fields[index] = field;
            else
                embed.Fields.Add(field);
        }

        private static string Trunc(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Length <= max ? value : value[..(max - 1)] + "...";
        }
    }
}
