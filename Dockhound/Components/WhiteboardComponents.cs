using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Components
{
    public static class WhiteboardComponents
    {
        /// <summary>
        /// Builds the standard component row for a whiteboard message.
        /// </summary>
        public static MessageComponent BuildComponents(long whiteboardId, bool historyEnabled = true)
        {
            return new ComponentBuilder()
                .WithButton(
                    label: "Edit",
                    customId: $"wb:edit:{whiteboardId}",
                    style: ButtonStyle.Primary)
                .WithButton(
                    label: "History",
                    customId: $"wb:history:{whiteboardId}",
                    style: ButtonStyle.Secondary,
                    disabled: !historyEnabled)
                .Build();
        }

        /// <summary>
        /// Builds the display text for a whiteboard message.
        /// </summary>
        public static string BuildMessage(string title, string content, long wbId, int versionIndex, ulong editorId, DateTime editedUtc, (long srcWbId, int srcVerIdx)? clonedFrom = null)
        {
            var header = $"**{title}**";
            var body = string.IsNullOrWhiteSpace(content) ? "\u200b" : content;

            var unix = new DateTimeOffset(editedUtc).ToUnixTimeSeconds();

            var footer = clonedFrom is { } src
                ? $"_v{versionIndex} • Cloned from WB-{src.srcWbId} v{src.srcVerIdx}_"
                : $"_v{versionIndex} • Created by <@{editorId}> • <t:{unix}:R>_";

            return $"{header}\n{body}\n\n{footer}";
        }

        /// <summary>
        /// Builds the display text for a whiteboard embed.
        /// </summary>
        public static Embed BuildEmbed(string title, string content, long wbId, int versionIndex, ulong editorId, DateTime editedUtc, (long srcWbId, int srcVerIdx)? clonedFrom = null)
        {
            var eb = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(Color.DarkTeal);

            var body = string.IsNullOrWhiteSpace(content) ? "_(empty)_" : content;

            string meta;

            if (clonedFrom is { } src)
            {
                meta = $"\n\n_v{versionIndex} • Cloned from WB-{src.srcWbId} v{src.srcVerIdx}_";
            }
            else
            {
                var unix = new DateTimeOffset(editedUtc).ToUnixTimeSeconds();
                meta = $"\n\n_v{versionIndex} • Edited by <@{editorId}> • <t:{unix}:R>_";
            }

            eb.Description = $"{body}{meta}";

            eb.WithFooter("Brought to you by Dockhound");

            return eb.Build();
        }
    }
}
