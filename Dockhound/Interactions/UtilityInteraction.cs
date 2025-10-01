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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dockhound.Interactions
{
    public class UtilityInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DockhoundContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppSettings _settings;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public UtilityInteraction(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _settings = appSettings.Value;
        }

        [ComponentInteraction("btn-remove-bookmark:*:*:*:*")]
        public async Task RemoveBookmark(ulong guildId, ulong channelId, ulong messageId, ulong userId)
        {
            var comp = (SocketMessageComponent)Context.Interaction;

            if (Context.User.Id != userId)
                return;

            // Try to remove the original reaction (guild messages only)
            try
            {
                var chan = Context.Client.GetChannel(channelId) as ITextChannel
                           ?? (Context.Client.GetGuild(guildId)?.GetTextChannel(channelId));

                if (chan != null)
                {
                    if (await chan.GetMessageAsync(messageId) is IUserMessage src)
                    {
                        await src.RemoveReactionAsync(new Emoji("🔖"), userId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to remove bookmark reaction: {ex.Message}");
            }

            // Delete the DM message that contained the button
            try
            {
                await comp.Message.DeleteAsync();
            }
            catch { /* ignore */ }
        }
    }
}
