using Discord;
using Discord.Interactions;
using Dockhound.Services;

namespace Dockhound.Modules
{
    public partial class DockAdminModule
    {
        [Group("honeypot", "Configure spam-bot honeypots")]
        public class HoneypotAdminSetup : InteractionModuleBase<SocketInteractionContext>
        {
            private readonly IGuildSettingsService _guildSettingsService;
            private readonly IHoneypotService _honeypotService;

            public HoneypotAdminSetup(IGuildSettingsService guildSettingsService, IHoneypotService honeypotService)
            {
                _guildSettingsService = guildSettingsService;
                _honeypotService = honeypotService;
            }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("status", "Show current honeypot settings.")]
                public async Task StatusAsync()
                {
                    await DeferAsync(ephemeral: true);

                    var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);
                    var hp = cfg.Honeypot;

                    var embed = new EmbedBuilder()
                        .WithTitle("Honeypot Settings")
                        .WithColor(hp.Enabled ? Color.Green : Color.DarkGrey)
                        .AddField("Enabled", hp.Enabled ? "Yes" : "No", inline: true)
                        .AddField("Trap Channel", hp.ChannelId.HasValue ? $"<#{hp.ChannelId.Value}>" : "-", inline: true)
                        .AddField("Reaction Trap", hp.ReactionChannelId.HasValue && hp.ReactionMessageId.HasValue
                            ? $"<#{hp.ReactionChannelId.Value}> / `{hp.ReactionMessageId.Value}`"
                            : "-", inline: false)
                        .AddField("Report Channel", hp.ReportChannelId.HasValue ? $"<#{hp.ReportChannelId.Value}>" : "Server system channel fallback", inline: false)
                        .WithCurrentTimestamp()
                        .Build();

                    await FollowupAsync(embed: embed, ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("enable", "Enable or disable honeypot enforcement.")]
                public async Task EnableAsync(bool enabled)
                {
                    await DeferAsync(ephemeral: true);

                    await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg => cfg.Honeypot.Enabled = enabled,
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync($"Honeypot enforcement is now {(enabled ? "enabled" : "disabled")}.", ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("set-channel", "Ban anyone who sends a message in this channel.")]
                public async Task SetChannelAsync(ITextChannel channel)
                {
                    await DeferAsync(ephemeral: true);

                    await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg =>
                        {
                            cfg.Honeypot.Enabled = true;
                            cfg.Honeypot.ChannelId = channel.Id;
                        },
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync($"Honeypot trap channel set to {channel.Mention}.", ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("set-report-channel", "Set the channel where honeypot bans are reported.")]
                public async Task SetReportChannelAsync(ITextChannel channel)
                {
                    await DeferAsync(ephemeral: true);

                    await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg => cfg.Honeypot.ReportChannelId = channel.Id,
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync($"Honeypot reports will be sent to {channel.Mention}.", ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("set-reaction-message", "Ban anyone who reacts to a specific message.")]
                public async Task SetReactionMessageAsync(ITextChannel channel, string messageId)
                {
                    await DeferAsync(ephemeral: true);

                    if (!ulong.TryParse(messageId, out var parsedMessageId))
                    {
                        await FollowupAsync("Invalid message ID.", ephemeral: true);
                        return;
                    }

                    await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg =>
                        {
                            cfg.Honeypot.Enabled = true;
                            cfg.Honeypot.ReactionChannelId = channel.Id;
                            cfg.Honeypot.ReactionMessageId = parsedMessageId;
                        },
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync($"Reaction honeypot set to message `{parsedMessageId}` in {channel.Mention}.", ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("create-honeypot", "Post and register a honeypot reaction message in this channel.")]
                public async Task CreateHoneypotAsync(string? content = null)
                {
                    await DeferAsync(ephemeral: true);

                    if (Context.Channel is not IMessageChannel channel)
                    {
                        await FollowupAsync("This command must be used in a message channel.", ephemeral: true);
                        return;
                    }

                    var message = await _honeypotService.CreateHoneypotMessageAsync(Context.Guild, channel, Context.User, content);

                    await FollowupAsync($"Honeypot message created and registered: `{message.Id}`.", ephemeral: true);
                }
        }
    }
}
