using Discord;

namespace Dockhound.Components
{
    public static class HoneypotComponents
    {
        public static Embed BuildTrapEmbed(string? content)
        {
            var messageText = string.IsNullOrWhiteSpace(content)
                ? "Verification required. React here to continue."
                : content.Trim();

            return new EmbedBuilder()
                .WithTitle("Verification Required")
                .WithDescription(messageText)
                .WithColor(Color.DarkGrey)
                .WithFooter("Dockhound honeypot")
                .WithCurrentTimestamp()
                .Build();
        }

        public static Embed BuildBanReportEmbed(
            IGuildUser user,
            string reason,
            string proof,
            string jumpLink,
            IEnumerable<EmbedFieldBuilder> extraFields)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Honeypot Ban")
                .WithDescription($"{user.Mention} was automatically banned by the honeypot.")
                .WithColor(Color.Red)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .AddField("User", $"{user.Username}#{user.Discriminator} ({user.Id})", inline: false)
                .AddField("Account Created", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:F> (<t:{user.CreatedAt.ToUnixTimeSeconds()}:R>)", inline: false)
                .AddField("Reason", reason, inline: false)
                .AddField("Proof", string.IsNullOrWhiteSpace(proof) ? "(no text content)" : Trunc(proof.Trim(), 900), inline: false)
                .AddField("Source", $"[Jump to offense]({jumpLink})", inline: false)
                .WithFooter("Review this carefully. Use the button to unban if this was a false positive.")
                .WithCurrentTimestamp();

            foreach (var field in extraFields)
                embed.AddField(field);

            return embed.Build();
        }

        public static Embed BuildBanFailureEmbed(
            IGuildUser user,
            string reason,
            string jumpLink,
            Exception exception)
        {
            return new EmbedBuilder()
                .WithTitle("Honeypot Ban Failed")
                .WithDescription($"Dockhound detected {user.Mention}, but could not ban them.")
                .WithColor(Color.Orange)
                .AddField("User", $"{user.Username}#{user.Discriminator} ({user.Id})", inline: false)
                .AddField("Reason", reason, inline: false)
                .AddField("Source", $"[Jump to offense]({jumpLink})", inline: false)
                .AddField("Error", Trunc(exception.Message, 900), inline: false)
                .WithCurrentTimestamp()
                .Build();
        }

        public static MessageComponent BuildReviewComponents(ulong userId)
        {
            return new ComponentBuilder()
                .WithButton("Unban", $"honeypot:unban:{userId}", ButtonStyle.Danger)
                .Build();
        }

        private static string Trunc(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Length <= max ? value : value[..(max - 1)] + "...";
        }
    }
}
