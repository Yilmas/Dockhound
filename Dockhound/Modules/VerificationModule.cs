using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Dockhound.Enums;
using Dockhound.Extensions;
using Dockhound.Logs;
using Dockhound.Modals;
using Dockhound.Models;
using Dockhound.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Dockhound.Modules;

[CommandContextType(InteractionContextType.Guild)]
[Group("verify", "Root command of the Verification Program")]
public class VerificationModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DockhoundContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly IGuildSettingsService _guildSettingsService;
    private readonly IVerificationHistoryService _verificationHistory;

    public VerificationModule(DockhoundContext dbContext, HttpClient httpClient, IGuildSettingsService guildSettingsService, IVerificationHistoryService verificationHistoryService)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _guildSettingsService = guildSettingsService;
        _verificationHistory = verificationHistoryService;
    }

    [SlashCommand("me", "Basic Verification")]
    public async Task VerifyMe(IAttachment file, Faction faction)
    {
        await DeferAsync(ephemeral: true);

        var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

        long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        var reviewChannel = cfg.Verify.ReviewChannelId is ulong id
                    ? Context.Guild.GetTextChannel(id)
                    : null;

        if (reviewChannel == null)
        {
            await FollowupAsync("Verification channel not found. Please contact an admin.", ephemeral: true);
            return;
        }

        using var response = await _httpClient.GetAsync(file.Url);
        response.EnsureSuccessStatusCode();
        await using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());

        var history = await _verificationHistory.GetTrackRecordAsync(Context.User.Id);

        var track = "_No previous approvals._";

        if (history.Count > 0)
        {
            var lines = new List<string>();

            foreach (var h in history)
            {
                var guildName = await _guildSettingsService.GetGuildDisplayNameAsync(h.GuildId)
                                ?? $"Guild {h.GuildId}";

                lines.Add(
                    $"• {h.Faction} — <t:{new DateTimeOffset(h.ApprovedAtUtc).ToUnixTimeSeconds()}:R> — {guildName}"
                );
            }

            track = string.Join("\n", lines);
        }

        IGuildUser? member = Context.Guild.GetUser(Context.User.Id);

        if (member is null)
        {
            // fallback to REST if not in cache
            member = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, Context.User.Id);
        }

        // At this point `member` is either SocketGuildUser or RestGuildUser, but both are IGuildUser
        var displayName = (member as SocketGuildUser)?.DisplayName
                          ?? member?.Username
                          ?? Context.User.Username;

        var roleMentions = "-";
        if (member is not null)
        {
            roleMentions = await DiscordRolesList.GetDeltaRoleMentionsAsync(
                _guildSettingsService,
                member,
                faction   // Faction enum
            );
            if (string.IsNullOrWhiteSpace(roleMentions))
                roleMentions = "-";
        }


        var embed = new EmbedBuilder()
            .WithTitle("New Verification Submission")
            .WithDescription($"A verification has been submitted by {displayName} — ({Context.User.Mention})")
            .AddField("Faction", faction.ToString(), inline: true)
            .AddField("User ID", Context.User.Id.ToString(), inline: true)
            .AddField("Roles to be granted", roleMentions, inline: false)
            .AddField("Faction history (last 5)", string.IsNullOrWhiteSpace(track) ? "-" : track, inline: false)
            .WithColor(faction == Faction.Colonial ? Color.DarkGreen : Color.DarkBlue)
            .WithCurrentTimestamp()
            .WithFooter("Awaiting Approval")
            .Build();

        var component = new ComponentBuilder()
            .WithButton("Approve", "approve-verification", ButtonStyle.Success)
            .WithButton("Deny", "deny-verification", ButtonStyle.Danger)
            .Build();

        await reviewChannel.SendFileAsync(stream, file.Filename, embed: embed, components: component);
        await FollowupAsync("Verification submitted. Please wait for approval.", ephemeral: true);

        try
        {
            var msg = await GetOriginalResponseAsync();

            var log = new LogEvent(
                eventName: "Verification Module",
                messageId: msg.Id,
                username: Context.User.Username,
                userId: Context.User.Id,
                changes: $"{Context.User.Username} has submitted a verification submission."
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

