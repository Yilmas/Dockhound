using Discord;
using Discord.Interactions;
using Dockhound.Config;
using Dockhound.Models;
using Dockhound.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Dockhound.Modules
{
    public partial class DockAdminModule
    {
        [Group("settings", "Settings for Dockhound")]
        public class DockSettings : InteractionModuleBase<SocketInteractionContext>
        {
            private readonly DockhoundContext _dbContext;
            private readonly HttpClient _httpClient;
            private readonly IGuildSettingsService _guildSettingsService;
            private readonly IDbContextFactory<DockhoundContext> _dbFactory;

            public DockSettings(
                DockhoundContext dbContext,
                HttpClient httpClient,
                IConfiguration config,
                IOptions<AppSettings> appSettings,
                IOptionsMonitor<AppSettings> monitorSettings,
                IAppSettingsService appSettingsService,
                IGuildSettingsService guildSettingsService,
                IDbContextFactory<DockhoundContext> dbFactory)
            {
                _dbContext = dbContext;
                _httpClient = httpClient;
                _guildSettingsService = guildSettingsService;
                _dbFactory = dbFactory;
            }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("view", "Displays current guild settings")]
                public async Task GetGuildSettings()
                {
                    await DeferAsync(ephemeral: true);

                    var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

                    var node = JsonSerializer.SerializeToNode(
                        cfg,
                        new JsonSerializerOptions { WriteIndented = true }
                    ) as JsonObject;

                    var pretty = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";
                    var bytes = Encoding.UTF8.GetBytes(pretty);

                    await using var ms = new MemoryStream(bytes);
                    ms.Position = 0;

                    var filename = $"guild-{Context.Guild.Id}-settings.json";
                    await FollowupWithFileAsync(ms, filename,
                        text: $"Guild settings for `{Context.Guild.Id}` (attached).", ephemeral: true);
                }

                [RequireContext(ContextType.Guild)]
                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("config-update", "Upload a GuildConfig JSON file to update this guild's configuration.")]
                public async Task UpdateConfigAsync(
                    [Summary("file", "JSON file containing Dockhound GuildConfig")] IAttachment file)
                {
                    await DeferAsync(ephemeral: true);

                    var targetGuildId = Context.Guild!.Id;

                    if (file.Size <= 0 || file.Size > 2_000_000)
                    {
                        await FollowupAsync("❌ The file is empty or larger than 2 MB. Please upload a valid JSON under 2 MB.", ephemeral: true);
                        return;
                    }

                    string json;
                    try
                    {
                        using var stream = await _httpClient.GetStreamAsync(file.Url);
                        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                        json = await reader.ReadToEndAsync();
                    }
                    catch (Exception ex)
                    {
                        await FollowupAsync($"❌ Failed to download the attachment: `{ex.Message}`", ephemeral: true);
                        return;
                    }

                    var deserializeOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = JsonNumberHandling.AllowReadingFromString,
                    };

                    GuildConfig? cfg;
                    try
                    {
                        cfg = JsonSerializer.Deserialize<GuildConfig>(json, deserializeOptions)
                              ?? throw new JsonException("Deserialized result was null.");
                    }
                    catch (JsonException jx)
                    {
                        await FollowupAsync($"❌ Invalid JSON for `GuildConfig`: `{jx.Message}`", ephemeral: true);
                        return;
                    }

                    cfg.SchemaVersion = cfg.SchemaVersion <= 0 ? _guildSettingsService.CurrentSchemaVersion : cfg.SchemaVersion;
                    cfg.Roles ??= new List<GuildConfig.RoleSet>();
                    cfg.Verify ??= new GuildConfig.VerificationSettings();
                    cfg.Verify.RestrictedAccess ??= new GuildConfig.RestrictedAccessSettings();
                    cfg.Verify.RecruitAssignerRoles ??= new List<ulong>();
                    cfg.Verify.AllyAssignerRoles ??= new List<ulong>();
                    cfg.Verify.TrustedRoles ??= new List<ulong>();
                    cfg.Verify.RestrictedAccess.AlwaysRestrictRoles ??= new List<ulong>();
                    cfg.Verify.RestrictedAccess.MemberOnlyRoles ??= new List<ulong>();
                    cfg.Honeypot ??= new GuildConfig.HoneypotSettings();
                    cfg.Honeypot.MessagePruneDays = Math.Clamp(cfg.Honeypot.MessagePruneDays, 0, 7);

                    try
                    {
                        await _guildSettingsService.UpdateAsync(
                            targetGuildId,
                            cfg,
                            changedBy: $"{Context.User.Username}#{Context.User.Id}"
                        );
                    }
                    catch (InvalidOperationException cex)
                    {
                        await FollowupAsync($"⚠️ Concurrency conflict while saving. Please retry.\n`{cex.InnerException?.Message ?? cex.Message}`", ephemeral: true);
                        return;
                    }
                    catch (Exception ex)
                    {
                        await FollowupAsync($"❌ Failed to save configuration: `{ex.Message}`", ephemeral: true);
                        return;
                    }

                    var saved = await _guildSettingsService.GetAsync(targetGuildId);
                    var pretty = JsonSerializer.Serialize(saved, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (pretty.Length > 1800)
                    {
                        var bytes = Encoding.UTF8.GetBytes(pretty);
                        await using var s = new MemoryStream(bytes);
                        await FollowupWithFileAsync(s, $"guild-{targetGuildId}-config.json",
                            text: $"✅ Updated configuration for guild `{targetGuildId}`. Here is the saved JSON:", ephemeral: true);
                    }
                    else
                    {
                        await FollowupAsync($"✅ Updated configuration for guild `{targetGuildId}`. Saved JSON:\n```json\n{pretty}\n```", ephemeral: true);
                    }

                    if (Context.Guild != null && Context.Guild.SafetyAlertsChannel.Id is ulong modChannelId)
                    {
                        if (Context.Guild.GetChannel(modChannelId) is IMessageChannel modChannel)
                        {
                            var embed = new EmbedBuilder()
                                .WithTitle("Configuration Updated")
                                .WithDescription($"Guild configuration was updated by {Context.User.Mention}")
                                .WithColor(Color.Orange)
                                .WithCurrentTimestamp()
                                .Build();

                            await modChannel.SendMessageAsync(embed: embed);
                        }
                    }
                }

                [RequireContext(ContextType.Guild)]
                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("guild-update", "Update this guild's name and tag.")]
                public async Task UpdateGuildAsync(
                    [Summary("name", "New name of the guild")] string? name = null,
                    [Summary("tag", "Short tag/identifier for the guild")] string? tag = null)
                {
                    await DeferAsync(ephemeral: true);

                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(tag))
                    {
                        await FollowupAsync("❌ You must specify at least one of `name` or `tag`.", ephemeral: true);
                        return;
                    }

                    await using var db = await _dbFactory.CreateDbContextAsync();
                    var guildId = Context.Guild.Id;

                    var guild = await db.Guilds.FirstOrDefaultAsync(g => g.GuildId == guildId);
                    if (guild is null)
                    {
                        await FollowupAsync("❌ You must run the command config-update first!", ephemeral: true);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                        guild.Name = name;

                    if (!string.IsNullOrWhiteSpace(tag))
                        guild.Tag = tag;

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        await FollowupAsync($"❌ Failed to update guild info: {ex.InnerException?.Message ?? ex.Message}", ephemeral: true);
                        return;
                    }

                    var embed = new EmbedBuilder()
                        .WithTitle("✅ Guild Updated")
                        .WithColor(Color.Green)
                        .AddField("Name", string.IsNullOrWhiteSpace(guild.Name) ? "-" : guild.Name, inline: true)
                        .AddField("Tag", string.IsNullOrWhiteSpace(guild.Tag) ? "-" : guild.Tag, inline: true)
                        .WithFooter($"Updated by {Context.User}")
                        .WithCurrentTimestamp()
                        .Build();

                    await FollowupAsync(embed: embed, ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.ViewAuditLog | GuildPermission.ManageMessages)]
                [SlashCommand("logs-per-message", "Show all log events for a given message id over a given timespan in days")]
                public async Task GetLogEvents([Summary("message_id", "The Discord message ID")] string messageId, [Summary("days_span", "Days into the past")] int daysSpan)
                {
                    await DeferAsync(ephemeral: true);

                    daysSpan = -1 * daysSpan;

                    if (!ulong.TryParse(messageId, out var msgId))
                    {
                        await FollowupAsync("Invalid message ID format.", ephemeral: true);
                        return;
                    }

                    var cutoff = DateTime.UtcNow.Date.AddDays(daysSpan);
                    var logs = await _dbContext.LogEvents
                        .Where(l => l.MessageId == msgId && l.Updated >= cutoff)
                        .ToListAsync();

                    var weeklyCounts = logs
                        .GroupBy(l =>
                        {
                            var dt = DateTime.SpecifyKind(l.Updated, DateTimeKind.Utc);
                            return (Year: ISOWeek.GetYear(dt), Week: ISOWeek.GetWeekOfYear(dt));
                        })
                        .ToDictionary(g => g.Key, g => g.Count());

                    static DateTime StartOfIsoWeek(DateTime d)
                    {
                        int dow = (int)d.DayOfWeek;
                        int diff = (dow == 0 ? -6 : 1 - dow);
                        return d.Date.AddDays(diff);
                    }

                    var buckets = new List<(int Year, int Week, DateTime Start)>();
                    var cursor = StartOfIsoWeek(cutoff);
                    var end = DateTime.UtcNow.Date;
                    while (cursor <= end)
                    {
                        buckets.Add((ISOWeek.GetYear(cursor), ISOWeek.GetWeekOfYear(cursor), cursor));
                        cursor = cursor.AddDays(7);
                    }

                    var lines = new List<string>(buckets.Count);
                    int total = 0;
                    foreach (var b in buckets)
                    {
                        var key = (b.Year, b.Week);
                        var count = weeklyCounts.TryGetValue(key, out var c) ? c : 0;
                        total += count;
                        lines.Add($"{b.Year}-W{b.Week:D2}  ({b.Start:dd MMM}) : {count}");
                    }

                    var eb = new EmbedBuilder()
                        .WithTitle($"Log events for message {messageId}")
                        .WithColor(Color.DarkGrey)
                        .AddField($"Total (last {daysSpan} days)", total, inline: true)
                        .AddField("Weeks covered", buckets.Count, inline: true)
                        .AddField("Weekly updates", "```\n" + string.Join("\n", lines) + "\n```", inline: false);

                    await FollowupAsync(embed: eb.Build(), ephemeral: true);
                }
        }
    }
}
