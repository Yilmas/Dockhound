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

            var auditReason = $"{reason}: {user.Username} ({user.Id})";

            try
            {
                await guild.AddBanAsync(userId, 0, auditReason);
            }
            catch (Exception ex)
            {
                await ReportFailureAsync(guild, settings, user, reason, sourceChannelId, sourceMessageId, ex);
                return;
            }

            await ReportBanAsync(guild, settings, user, reason, proof, sourceChannelId, sourceMessageId, extraFields);

            try
            {
                _dbContext.LogEvents.Add(new LogEvent(
                    eventName: "Honeypot Ban",
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
            yield return new EmbedFieldBuilder()
                .WithName("Reaction")
                .WithValue(reaction.Emote.Name)
                .WithIsInline(true);

            var baitText = !string.IsNullOrWhiteSpace(message.Content)
                ? Trunc(message.Content.Trim(), 350)
                : message.Embeds.FirstOrDefault()?.Description;

            if (!string.IsNullOrWhiteSpace(baitText))
            {
                yield return new EmbedFieldBuilder()
                    .WithName("Bait Message")
                    .WithValue(Trunc(baitText, 350))
                    .WithIsInline(false);
            }
        }

        private static IMessageChannel? ResolveReportChannel(SocketGuild guild, GuildConfig.HoneypotSettings settings)
        {
            if (settings.ReportChannelId is ulong configuredId && guild.GetTextChannel(configuredId) is { } configured)
                return configured;

            return guild.SystemChannel ?? guild.SafetyAlertsChannel as IMessageChannel;
        }

        private static string Trunc(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Length <= max ? value : value[..(max - 1)] + "...";
        }
    }
}
