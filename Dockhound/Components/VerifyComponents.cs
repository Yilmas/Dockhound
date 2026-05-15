using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Components
{
    public static class VerifyComponents
    {
        /// <summary>
        /// Builds the standard component row for a verify review message.
        /// </summary>
        public static MessageComponent BuildReviewComponents()
        {
            return new ComponentBuilder()
                .WithButton(
                    label: "Approve",
                    customId: $"verify:approve",
                    style: ButtonStyle.Success)
                .WithButton(
                    label: "History",
                    customId: $"verify:deny",
                    style: ButtonStyle.Danger)
                .Build();
        }

        /// <summary>
        /// Build a verification review embed used for both manual and auto-approved posts.
        /// </summary>
        public static Embed BuildEmbed(string title, string description, string faction, ulong userId, string? steamProfile, string rolesToBeGranted, string? steamHistory, string factionHistory, Color color, string footer)
        {
            var eb = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(color)
                .WithCurrentTimestamp()
                .WithFooter(footer);

            // Faction + User ID
            eb.AddField("Faction", string.IsNullOrWhiteSpace(faction) ? "-" : faction, inline: true);
            eb.AddField("User ID", userId.ToString(), inline: true);

            // Steam Profile - show explicit "-" if not provided
            eb.AddField("Steam Profile", !string.IsNullOrWhiteSpace(steamProfile) ? steamProfile! : "-", inline: false);

            // Roles
            eb.AddField("Roles to be granted", string.IsNullOrWhiteSpace(rolesToBeGranted) ? "-" : rolesToBeGranted, inline: false);

            // Steam history: render visually blank if empty (no offense), otherwise display provided content
            var steamHistoryField = string.IsNullOrEmpty(steamHistory) ? "\u200b" : steamHistory!;
            eb.AddField("Steam history (recent)", steamHistoryField, inline: false);

            // Faction history (last 5)
            eb.AddField("Faction history (last 5)", string.IsNullOrWhiteSpace(factionHistory) ? "-" : factionHistory, inline: false);

            return eb.Build();
        }
    }
}