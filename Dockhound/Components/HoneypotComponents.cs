using Discord;

namespace Dockhound.Components
{
    public static class HoneypotComponents
    {
        public static Embed BuildTrapEmbed(string? content, int botsSquashed = 0, int savingGraces = 0)
        {
            var messageText = string.IsNullOrWhiteSpace(content)
                ? "Do not react or send messages in this channel or you will be banned! You have been warned!"
                : content.Trim();

            return new EmbedBuilder()
                .WithTitle("Bringing you the tears of bots 24/7!")
                .WithDescription(messageText)
                .WithColor(Color.DarkGrey)
                .AddField("Bots Squashed", botsSquashed.ToString(), inline: true)
                .AddField("Saving Graces", savingGraces.ToString(), inline: true)
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
                .AddField("User", $"{user.Username} ({user.Id})", inline: false)
                .AddField("Account Created", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:F> (<t:{user.CreatedAt.ToUnixTimeSeconds()}:R>)", inline: false)
                .AddField("Proof", string.IsNullOrWhiteSpace(proof) ? "(no text content)" : Trunc(proof.Trim(), 900), inline: false)
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
                .AddField("User", $"{user.Username} ({user.Id})", inline: false)
                .AddField("Error", Trunc(exception.Message, 900), inline: false)
                .WithCurrentTimestamp()
                .Build();
        }

        public static Embed BuildProtectedUserEmbed(
            IGuildUser user,
            string reason,
            string jumpLink,
            string protectedPermissions)
        {
            return new EmbedBuilder()
                .WithTitle("Honeypot Ban Skipped")
                .WithDescription($"{user.Mention} triggered the honeypot, but was not banned because they have protected permissions.")
                .WithColor(Color.Gold)
                .AddField("User", $"{user.Username} ({user.Id})", inline: false)
                .AddField("Protected Permissions", protectedPermissions, inline: false)
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
