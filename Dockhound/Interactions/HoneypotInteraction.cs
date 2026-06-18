using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Services;

namespace Dockhound.Interactions
{
    public class HoneypotInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IHoneypotService _honeypotService;

        public HoneypotInteraction(IHoneypotService honeypotService)
        {
            _honeypotService = honeypotService;
        }

        [ComponentInteraction("honeypot:unban:*")]
        public async Task UnbanHoneypotUserAsync(ulong userId)
        {
            await DeferAsync(ephemeral: true);

            if (Context.Guild is null)
            {
                await FollowupAsync("This action can only be used inside a server.", ephemeral: true);
                return;
            }

            if (Context.User is not SocketGuildUser moderator ||
                !moderator.GuildPermissions.BanMembers)
            {
                await FollowupAsync("You need the Ban Members permission to unban honeypot bans.", ephemeral: true);
                return;
            }

            try
            {
                await Context.Guild.RemoveBanAsync(userId);

                if (Context.Interaction is SocketMessageComponent comp)
                {
                    var embed = comp.Message.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder();
                    embed.WithTitle("Honeypot Ban Reversed");
                    embed.WithDescription($"<@{userId}> was unbanned by {Context.User.Mention} after moderator review.");
                    embed.WithColor(Color.Green);
                    embed.WithFooter($"Saving grace performed by {Context.User.Username}");

                    await comp.Message.ModifyAsync(m =>
                    {
                        m.Embeds = new[] { embed.Build() };
                        m.Components = new ComponentBuilder().Build();
                    });
                }

                await _honeypotService.RecordSavingGraceAsync(Context.Guild);

                await FollowupAsync($"User `{userId}` has been unbanned.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Failed to unban `{userId}`: {ex.Message}", ephemeral: true);
            }
        }
    }
}
