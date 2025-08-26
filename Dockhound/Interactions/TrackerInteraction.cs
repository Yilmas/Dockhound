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
    public class TrackerInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WllTrackerContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppSettings _settings;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public TrackerInteraction(WllTrackerContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
            _settings = appSettings.Value;
        }

        //////////////////////////////
        //                          //
        // CONTAINER INTERACTIONS   //
        //                          //
        //////////////////////////////

        [ComponentInteraction("btn-container-count")]
        public async Task OpenCountModal()
        {
            var msgEmbed = Context.Interaction is SocketMessageComponent comp
            ? comp.Message.Embeds.First().ToEmbedBuilder()
            : null;

            // read current values from embed
            var countRed = msgEmbed.Fields.Single(x => x.Name.ToLower() == "red").Value;
            var countGreen = msgEmbed.Fields.Single(x => x.Name.ToLower() == "green").Value;
            var countBlue = msgEmbed.Fields.Single(x => x.Name.ToLower() == "blue").Value;
            var countDarkBlue = msgEmbed.Fields.Single(x => x.Name.ToLower() == "darkblue").Value;
            var countWhite = msgEmbed.Fields.Single(x => x.Name.ToLower() == "white").Value;

            var modal = new ModalBuilder()
                .WithTitle($"Update Container Count")
                .WithCustomId("update-count-modal")
                .AddTextInput("Red", $"update-count-red", value: countRed.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("Green", $"update-count-green", value: countGreen.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("Blue", $"update-count-blue", value: countBlue.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("Dark Blue", $"update-count-darkblue", value: countDarkBlue.ToString(), minLength: 1, maxLength: 3)
                .AddTextInput("White", $"update-count-white", value: countWhite.ToString(), minLength: 1, maxLength: 3);

            await Context.Interaction.RespondWithModalAsync(modal.Build());
        }

        [ModalInteraction("update-count-modal")]
        public async Task UpdateCountModal(UpdateContainerModal modal)
        {
            var socketModal = (SocketModal)Context.Interaction;

            bool valid = Regex.IsMatch(modal.Red, @"^\d+$")
              && Regex.IsMatch(modal.Green, @"^\d+$")
              && Regex.IsMatch(modal.Blue, @"^\d+$")
              && Regex.IsMatch(modal.DarkBlue, @"^\d+$")
              && Regex.IsMatch(modal.White, @"^\d+$");
            if (!valid)
            {
                await RespondAsync("You must input a number!", ephemeral: true);
                return;
            }

            // Trim leading zeros
            string T(string s) => s == "0" ? "0" : s.TrimStart('0');

            // New values
            string newRed = T(modal.Red);
            string newGreen = T(modal.Green);
            string newBlue = T(modal.Blue);
            string newDarkBlue = T(modal.DarkBlue);
            string newWhite = T(modal.White);

            // Update the embed with new values
            var eb = socketModal.Message.Embeds.First().ToEmbedBuilder();
            eb.WithDescription("Last Updated by " + Context.User.Mention + " <t:" + seconds + ":R>");
            eb.Fields.Single(x => x.Name.Equals("red", StringComparison.OrdinalIgnoreCase)).Value = newRed;
            eb.Fields.Single(x => x.Name.Equals("green", StringComparison.OrdinalIgnoreCase)).Value = newGreen;
            eb.Fields.Single(x => x.Name.Equals("blue", StringComparison.OrdinalIgnoreCase)).Value = newBlue;
            eb.Fields.Single(x => x.Name.Equals("darkblue", StringComparison.OrdinalIgnoreCase)).Value = newDarkBlue;
            eb.Fields.Single(x => x.Name.Equals("white", StringComparison.OrdinalIgnoreCase)).Value = newWhite;

            await socketModal.UpdateAsync(m => m.Embed = eb.Build());

            try
            {
                var location = eb.Title;

                var log = new LogEvent(
                    eventName: "Updated Yard Tracker",
                    messageId: socketModal.Message.Id,
                    username: socketModal.User.Username,
                    userId: socketModal.User.Id,
                    changes: $"Updated the count for {location}: Red {newRed}, Green {newGreen}, Blue {newBlue}, Dark Blue {newDarkBlue}, White {newWhite}"
                );

                _dbContext.LogEvents.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to log event: {e.Message}\n{e.StackTrace}");
            }
        }

        //////////////////////////////
        //                          //
        // WHITEBOARD INTERACTIONS  //
        //                          //
        //////////////////////////////

        [ComponentInteraction("btn-whiteboard-update")]
        public async Task OpenWhiteboardModal()
        {
            if (Context.Guild == null)
            {
                await RespondAsync("Must be used in a guild.", ephemeral: true);
                return;
            }

            var comp = (SocketMessageComponent)Context.Interaction;

            var eb = comp.Message.Embeds.First().ToEmbedBuilder();
            var desc = eb.Description ?? string.Empty;

            // Remove the trailing 2 lines that contain the "_Last Updated by ..._" footer
            var lines = desc.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var trimmed = lines.Length >= 2 ? string.Join("\n", lines.Take(lines.Length - 2)) : desc;
            var boardValue = trimmed;

            var modal = new ModalBuilder()
                .WithTitle("Add Message")
                .WithCustomId("update-whiteboard-modal")
                .AddTextInput(
                    label: "Message",
                    customId: "update-whiteboard-edit",
                    style: TextInputStyle.Paragraph,
                    value: boardValue,
                    maxLength: 2048,
                    required: true
                );

            await RespondWithModalAsync(modal.Build());
        }

        [ModalInteraction("update-whiteboard-modal")]
        public async Task UpdateWhiteboard(UpdateWhiteboardModal modal)
        {
            var socketModal = (Discord.WebSocket.SocketModal)Context.Interaction;
            var msg = socketModal.Message as IUserMessage;
            if (msg is null)
                return;

            var eb = msg.Embeds.First().ToEmbedBuilder();
            var existing = eb.Description ?? string.Empty;

            static string StripFooter(string s)
            {
                var re = new System.Text.RegularExpressions.Regex(@"\n\s*\n_Last Updated by .+?_", System.Text.RegularExpressions.RegexOptions.Singleline);
                return re.Replace(s, string.Empty);
            }

            static string Normalize(string s) => (s ?? string.Empty).Replace("\r\n", "\n").Trim();

            var currentCore = Normalize(StripFooter(existing));
            var newCore = Normalize(modal.Message);

            // If nothing actually changed, bail early
            if (string.Equals(currentCore, newCore, StringComparison.Ordinal))
                return;

            long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

            var newDescription = string.IsNullOrWhiteSpace(modal.Message) ? "Waiting for squibbles ..." : $"{modal.Message}\n\n_Last Updated by {Context.User.Mention} <t:{seconds}:R>_";

            eb.Description = newDescription;

            await socketModal.UpdateAsync(m => m.Embed = eb.Build());

            try
            {
                var location = eb.Title;
                var log = new LogEvent(
                    eventName: "Updated Whiteboard",
                    messageId: msg.Id,
                    username: Context.User.Username,
                    userId: Context.User.Id,
                    changes: $"Updated the whiteboard for {location}"
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
