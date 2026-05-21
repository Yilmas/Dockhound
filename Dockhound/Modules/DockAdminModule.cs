using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Components;
using Dockhound.Config;
using Dockhound.Enums;
using Dockhound.Extensions;
using Dockhound.Logs;
using Dockhound.Models;
using Dockhound.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Dockhound.Modules
{
    public class DockAdminModule : InteractionModuleBase<SocketInteractionContext>
    {
        [CommandContextType(InteractionContextType.Guild)]
        [Group("dockadmin", "Root command of Dockhound Admin")]
        public class DockAdminSetup : InteractionModuleBase<SocketInteractionContext>
        {
            [Group("settings", "Settings for Dockhound")]
            public class DockSettings : InteractionModuleBase<SocketInteractionContext>
            {
                private readonly DockhoundContext _dbContext;
                private readonly HttpClient _httpClient;
                private readonly IConfiguration _configuration;
                private readonly AppSettings _settings;
                private readonly IOptionsMonitor<AppSettings> _monitorSettings;
                private readonly IAppSettingsService _appSettingsService;
                private readonly IGuildSettingsService _guildSettingsService;
                private readonly IDbContextFactory<DockhoundContext> _dbFactory;

                public DockSettings(DockhoundContext dbContext, HttpClient httpClient, 
                                    IConfiguration config, IOptions<AppSettings> appSettings, 
                                    IOptionsMonitor<AppSettings> monitorSettings, IAppSettingsService appSettingsService, 
                                    IGuildSettingsService guildSettingsService,
                                    IDbContextFactory<DockhoundContext> dbFactory)
                {
                    _dbContext = dbContext;
                    _httpClient = httpClient;
                    _configuration = config;
                    _settings = appSettings.Value;
                    _appSettingsService = appSettingsService;
                    _monitorSettings = monitorSettings;
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

                    // Send as downloadable JSON file (viewable in Discord)
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
                [Summary("file", "JSON file containing Dockhound GuildConfig")] IAttachment file
                )
                {
                    await DeferAsync(ephemeral: true);

                    var targetGuildId = Context.Guild!.Id;

                    // Basic checks
                    if (file.Size <= 0 || file.Size > 2_000_000)
                    {
                        await FollowupAsync("❌ The file is empty or larger than 2 MB. Please upload a valid JSON under 2 MB.", ephemeral: true);
                        return;
                    }

                    // Download
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

                    // Deserialize (case-insensitive, allow numbers-as-strings for ulongs)
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

                    // Defensive defaults to avoid null refs
                    cfg.SchemaVersion = cfg.SchemaVersion <= 0 ? _guildSettingsService.CurrentSchemaVersion : cfg.SchemaVersion;
                    cfg.Roles ??= new List<GuildConfig.RoleSet>();
                    cfg.Verify ??= new GuildConfig.VerificationSettings();
                    cfg.Verify.RestrictedAccess ??= new GuildConfig.RestrictedAccessSettings();
                    cfg.Verify.RecruitAssignerRoles ??= new List<ulong>();
                    cfg.Verify.AllyAssignerRoles ??= new List<ulong>();
                    cfg.Verify.TrustedRoles ??= new List<ulong>();
                    cfg.Verify.RestrictedAccess.AlwaysRestrictRoles ??= new List<ulong>();
                    cfg.Verify.RestrictedAccess.MemberOnlyRoles ??= new List<ulong>();

                    // Persist
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

                    // Read-back & pretty print
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

                    // --- Audit to moderation/notification channel ---
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

                    // Group by ISO year+week
                    var weeklyCounts = logs
                        .GroupBy(l =>
                        {
                            var dt = DateTime.SpecifyKind(l.Updated, DateTimeKind.Utc);
                            return (Year: ISOWeek.GetYear(dt), Week: ISOWeek.GetWeekOfYear(dt));
                        })
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Build week buckets from the Monday of the cutoff to today, step 7 days
                    static DateTime StartOfIsoWeek(DateTime d)
                    {
                        int dow = (int)d.DayOfWeek;
                        int diff = (dow == 0 ? -6 : 1 - dow); // move back to Monday
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

                    // Render lines, include zero weeks
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

            [Group("verify", "Admin root for Verify Module")]
            public class VerifyAdminSetup : InteractionModuleBase<SocketInteractionContext>
            {
                private readonly DockhoundContext _dbContext;
                private readonly HttpClient _httpClient;
                private readonly IConfiguration _configuration;
                private readonly AppSettings _settings;
                private readonly IAppSettingsService _appSettingsService; 
                private readonly IGuildSettingsService _guildSettingsService;

                public VerifyAdminSetup(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings, IAppSettingsService appSettingsService, IGuildSettingsService guildSettingsService)
                {
                    _dbContext = dbContext;
                    _httpClient = httpClient;
                    _configuration = config;
                    _settings = appSettings.Value;
                    _appSettingsService = appSettingsService;
                    _guildSettingsService = guildSettingsService;
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("info", "Provides information on the verification process.")]
                public async Task VerifyInfo()
                {
                    await DeferAsync(ephemeral: true);

                    var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);
                    string imageUrl = cfg.Verify.ImageUrl; //_configuration["VERIFY_IMAGEURL"];
                    var displayName = await _guildSettingsService.GetGuildDisplayNameAsync(Context.Guild.Id) ?? "the server";
                    var restriction = cfg.Verify?.RestrictedAccess?.CurrentRestrictionLevel ?? AccessRestriction.Open;
                    var steamRequired = cfg.Verify?.IsSteamRequired ?? false;

                    var embedInfo = VerifyComponents.BuildInfoEmbed(imageUrl, restriction, displayName, steamRequired);

                    // Post the info message in the current channel and save its channel/message id to settings
                    if (Context.Channel is IMessageChannel postChannel)
                    {
                        var msg = await postChannel.SendMessageAsync(embed: embedInfo, components: VerifyComponents.BuildInfoComponents());

                        // persist reference and current restriction level
                        await _guildSettingsService.UpdateRestrictedAccessAsync(
                            Context.Guild.Id,
                            postChannel.Id,
                            msg.Id,
                            restriction,
                            Context.User.Username + "#" + Context.User.Id
                        );

                        await FollowupAsync($"Info message posted and saved (Channel: {postChannel.Id}, Message: {msg.Id}).", ephemeral: true);
                        return;
                    }

                    // fallback: respond with the embed to the admin if channel is not an IMessageChannel
                    await FollowupAsync(embed: embedInfo, components: VerifyComponents.BuildInfoComponents(), ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("restrict", "Set verification access mode")]
                public async Task VerifyRestrict([Summary("setting", "Restricted / MembersOnly / Open")] AccessRestriction accessLevel)
                {
                    await DeferAsync(ephemeral: true);

                    var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

                    // Persist new restriction level while preserving stored info message/channel ids
                    var ra = await _guildSettingsService.GetRestrictedAccessAsync(Context.Guild.Id);
                    await _guildSettingsService.UpdateRestrictedAccessAsync(
                        Context.Guild.Id,
                        ra.ChannelId,
                        ra.MessageId,
                        accessLevel,
                        Context.User.Username + "#" + Context.User.Id
                    );

                    // Attempt to update the persisted Info message if available
                    if (ra.ChannelId.HasValue && ra.MessageId.HasValue)
                    {
                        var infoChannel = Context.Guild.GetTextChannel(ra.ChannelId.Value);
                        if (infoChannel is not null)
                        {
                            try
                            {
                                var msg = await infoChannel.GetMessageAsync(ra.MessageId.Value) as IUserMessage;
                                if (msg is not null)
                                {
                                    var displayName = await _guildSettingsService.GetGuildDisplayNameAsync(Context.Guild.Id) ?? "the server";
                                    var imageUrl = cfg.Verify.ImageUrl;
                                    var steamRequired = cfg.Verify.IsSteamRequired;

                                    var infoEmbed = VerifyComponents.BuildInfoEmbed(imageUrl, accessLevel, displayName, steamRequired);

                                    await msg.ModifyAsync(m => m.Embeds = new[] { infoEmbed });
                                }
                            }
                            catch
                            {
                                // ignore failures updating the message (permissions / deleted / etc.)
                            }
                        }
                    }
                    else
                    {
                        // No stored info message; create one in the channel the command was executed in
                        if (Context.Channel is IMessageChannel createChannel)
                        {
                            try
                            {
                                var displayName = await _guildSettingsService.GetGuildDisplayNameAsync(Context.Guild.Id) ?? "the server";
                                var imageUrl = cfg.Verify.ImageUrl;
                                var steamRequired = cfg.Verify.IsSteamRequired;

                                var infoEmbed = VerifyComponents.BuildInfoEmbed(imageUrl, accessLevel, displayName, steamRequired);
                                var posted = await createChannel.SendMessageAsync(embed: infoEmbed, components: VerifyComponents.BuildInfoComponents());

                                await _guildSettingsService.UpdateRestrictedAccessAsync(
                                    Context.Guild.Id,
                                    createChannel.Id,
                                    posted.Id,
                                    accessLevel,
                                    Context.User.Username + "#" + Context.User.Id
                                );
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                    }

                    await FollowupAsync(
                        accessLevel switch
                        {
                            AccessRestriction.Open =>
                                "Verification mode set to **Open**.",
                            AccessRestriction.MembersOnly =>
                                "Verification mode set to **MembersOnly**.",
                            AccessRestriction.Restricted =>
                                "Verification mode set to **Restricted**.",
                            _ => $"Verification mode set to **{accessLevel}**."
                        },
                        ephemeral: true
                    );
                }
            }
        }
    }
}
