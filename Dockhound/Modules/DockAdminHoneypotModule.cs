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

                [RequireUserPermission(GuildPermission.ManageGuild)]
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
                        .AddField("Messages", hp.MessagesEnabled ? "Enabled" : "Disabled", inline: true)
                        .AddField("Reactions", hp.ReactionsEnabled ? "Enabled" : "Disabled", inline: true)
                        .AddField("Trap Channel", hp.ChannelId.HasValue ? $"<#{hp.ChannelId.Value}>" : "-", inline: true)
                        .AddField("Reaction Trap", hp.ReactionChannelId.HasValue && hp.ReactionMessageId.HasValue
                            ? $"<#{hp.ReactionChannelId.Value}> / `{hp.ReactionMessageId.Value}`"
                            : "-", inline: false)
                        .AddField("Message Prune Days", hp.MessagePruneDays.ToString(), inline: true)
                        .AddField("Report Channel", hp.ReportChannelId.HasValue ? $"<#{hp.ReportChannelId.Value}>" : "Server system channel fallback", inline: false)
                        .WithCurrentTimestamp()
                        .Build();

                    await FollowupAsync(embed: embed, ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.ManageGuild)]
                [SlashCommand("enable", "Enable or disable honeypot enforcement.")]
                public async Task EnableAsync(
                    bool enabled,
                    [Summary("messages_enabled", "Enable or disable the message watcher. Omit to keep the current setting.")] bool? messagesEnabled = null,
                    [Summary("reactions_enabled", "Enable or disable the reaction watcher. Omit to keep the current setting.")] bool? reactionsEnabled = null)
                {
                    await DeferAsync(ephemeral: true);

                    var current = await _guildSettingsService.GetAsync(Context.Guild.Id);
                    var nextMessagesEnabled = messagesEnabled ?? current.Honeypot.MessagesEnabled;
                    var nextReactionsEnabled = reactionsEnabled ?? current.Honeypot.ReactionsEnabled;

                    if (enabled && !nextMessagesEnabled && !nextReactionsEnabled)
                    {
                        await FollowupAsync("Honeypot enforcement cannot be enabled while both message and reaction watchers are disabled.", ephemeral: true);
                        return;
                    }

                    await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg =>
                        {
                            cfg.Honeypot.Enabled = enabled;
                            if (messagesEnabled.HasValue)
                                cfg.Honeypot.MessagesEnabled = messagesEnabled.Value;
                            if (reactionsEnabled.HasValue)
                                cfg.Honeypot.ReactionsEnabled = reactionsEnabled.Value;
                        },
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync(
                        $"Honeypot enforcement is now {(enabled ? "enabled" : "disabled")}. Messages: {EnabledText(nextMessagesEnabled)}. Reactions: {EnabledText(nextReactionsEnabled)}.",
                        ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.ManageGuild)]
                [SlashCommand("set-channel", "Ban anyone who sends a message in this channel.")]
                public async Task SetChannelAsync(ITextChannel channel)
                {
                    await DeferAsync(ephemeral: true);

                    var cfg = await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg =>
                        {
                            cfg.Honeypot.Enabled = true;
                            cfg.Honeypot.ChannelId = channel.Id;
                        },
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync(
                        $"Honeypot trap channel set to {channel.Mention}.{WatcherDisabledWarning(cfg.Honeypot.MessagesEnabled, "message")}",
                        ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.ManageGuild)]
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

                [RequireUserPermission(GuildPermission.ManageGuild)]
                [SlashCommand("set-prune-days", "Set how many days of messages to prune when the honeypot bans a user.")]
                public async Task SetPruneDaysAsync(
                    [Summary("days", "Number of days to prune, from 0 to 7.")] int days)
                {
                    await DeferAsync(ephemeral: true);

                    if (days < 0 || days > 7)
                    {
                        await FollowupAsync("Prune days must be between `0` and `7`.", ephemeral: true);
                        return;
                    }

                    await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg => cfg.Honeypot.MessagePruneDays = days,
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync($"Honeypot bans will prune `{days}` day(s) of messages.", ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.ManageGuild)]
                [SlashCommand("set-reaction-message", "Ban anyone who reacts to a specific message.")]
                public async Task SetReactionMessageAsync(ITextChannel channel, string messageId)
                {
                    await DeferAsync(ephemeral: true);

                    if (!ulong.TryParse(messageId, out var parsedMessageId))
                    {
                        await FollowupAsync("Invalid message ID.", ephemeral: true);
                        return;
                    }

                    var cfg = await _guildSettingsService.PatchAsync(
                        Context.Guild.Id,
                        cfg =>
                        {
                            cfg.Honeypot.Enabled = true;
                            cfg.Honeypot.ReactionChannelId = channel.Id;
                            cfg.Honeypot.ReactionMessageId = parsedMessageId;
                        },
                        $"{Context.User.Username}#{Context.User.Id}");

                    await FollowupAsync(
                        $"Reaction honeypot set to message `{parsedMessageId}` in {channel.Mention}.{WatcherDisabledWarning(cfg.Honeypot.ReactionsEnabled, "reaction")}",
                        ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.ManageGuild)]
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
                    var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

                    await FollowupAsync(
                        $"Honeypot message created and registered: `{message.Id}`.{WatcherDisabledWarning(cfg.Honeypot.ReactionsEnabled, "reaction")}",
                        ephemeral: true);
                }

                private static string EnabledText(bool enabled)
                    => enabled ? "enabled" : "disabled";

                private static string WatcherDisabledWarning(bool watcherEnabled, string watcherName)
                    => watcherEnabled
                        ? string.Empty
                        : $" The {watcherName} watcher is currently disabled, so this trap will not ban users until it is enabled.";
        }
    }
}
