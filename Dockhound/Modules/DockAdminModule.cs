using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Config;
using Dockhound.Enums;
using Dockhound.Extensions;
using Dockhound.Logs;
using Dockhound.Models;
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
                private readonly IGuildSettingsProvider _guildSettingsProvider;
                private readonly IDbContextFactory<DockhoundContext> _dbFactory;

                public DockSettings(DockhoundContext dbContext, HttpClient httpClient, 
                                    IConfiguration config, IOptions<AppSettings> appSettings, 
                                    IOptionsMonitor<AppSettings> monitorSettings, IAppSettingsService appSettingsService, 
                                    IGuildSettingsProvider guildSettingsProvider,
                                    IDbContextFactory<DockhoundContext> dbFactory)
                {
                    _dbContext = dbContext;
                    _httpClient = httpClient;
                    _configuration = config;
                    _settings = appSettings.Value;
                    _appSettingsService = appSettingsService;
                    _monitorSettings = monitorSettings;
                    _guildSettingsProvider = guildSettingsProvider;
                    _dbFactory = dbFactory;
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("view", "Displays current app settings")]
                public async Task GetAppSettings()
                {
                    var node = JsonSerializer.SerializeToNode(
                        _monitorSettings.CurrentValue,
                        new JsonSerializerOptions { WriteIndented = true }
                    ) as JsonObject;

                    if (node is not null && node.ContainsKey("Configuration"))
                    {
                        node["Configuration"] = new JsonObject
                        {
                            ["_redacted"] = "Removed to prevent leakage"
                        };
                    }

                    var pretty = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";

                    // Keep embed under 4096 chars
                    const int max = 3900; // headroom for code fences
                    if (pretty.Length > max)
                        pretty = pretty.Substring(0, max) + "\n... (truncated)";

                    var embed = new EmbedBuilder()
                        .WithTitle("App Settings")
                        .WithDescription($"```\n{pretty}\n```")
                        .WithColor(Color.Blue)
                        .Build();

                    await RespondAsync(embed: embed, ephemeral: true);
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("viewguild", "Displays current guild settings")]
                public async Task GetGuildSettings()
                {
                    var cfg = await _guildSettingsProvider.GetAsync(Context.Guild.Id);

                    var node = JsonSerializer.SerializeToNode(
                        cfg,
                        new JsonSerializerOptions { WriteIndented = true }
                    ) as JsonObject;

                    var pretty = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";

                    // Keep embed under 4096 chars
                    const int max = 3900; // headroom for code fences
                    if (pretty.Length > max)
                        pretty = pretty.Substring(0, max) + "\n... (truncated)";

                    var embed = new EmbedBuilder()
                        .WithTitle("Guild Settings")
                        .WithDescription($"```\n{pretty}\n```")
                        .WithColor(Color.Blue)
                        .Build();

                    await RespondAsync(embed: embed, ephemeral: true);
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
                    cfg.SchemaVersion = cfg.SchemaVersion <= 0 ? 1 : cfg.SchemaVersion;
                    cfg.Verify ??= new GuildConfig.VerificationSettings();
                    cfg.Verify.RestrictedAccess ??= new GuildConfig.RestrictedAccessSettings();
                    cfg.Verify.RecruitAssignerRoles ??= new List<ulong>();
                    cfg.Verify.AllyAssignerRoles ??= new List<ulong>();
                    cfg.Verify.RestrictedAccess.AlwaysRestrictRoles ??= new List<ulong>();
                    cfg.Verify.RestrictedAccess.MemberOnlyRoles ??= new List<ulong>();

                    // Persist
                    try
                    {
                        await _guildSettingsProvider.UpdateAsync(
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
                    var saved = await _guildSettingsProvider.GetAsync(targetGuildId);
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
                        guild = new Guild { GuildId = guildId, CreatedAtUtc = DateTime.UtcNow };
                        db.Guilds.Add(guild);
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
                        .AddField("Name", guild.Name ?? "-", true)
                        .AddField("Tag", guild.Tag ?? "-", true)
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
                private readonly IGuildSettingsProvider _guildSettingsProvider;

                public VerifyAdminSetup(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings, IAppSettingsService appSettingsService, IGuildSettingsProvider guildSettingsProvider)
                {
                    _dbContext = dbContext;
                    _httpClient = httpClient;
                    _configuration = config;
                    _settings = appSettings.Value;
                    _appSettingsService = appSettingsService;
                    _guildSettingsProvider = guildSettingsProvider;
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("info", "Provides information on the verification process.")]
                public async Task VerifyInfo()
                {
                    var cfg = await _guildSettingsProvider.GetAsync(Context.Guild.Id);
                    string imageUrl = cfg.Verify.ImageUrl; //_configuration["VERIFY_IMAGEURL"];

                    var embed = new EmbedBuilder()
                        .WithTitle("Looking to Verify?")
                        .WithDescription("Follow the steps below to get yourself verified.")
                        .AddField("Steps to Verify", "1. Enter `/verify me`\n2. Upload your `MAP SCREEN Screenshot`\n3. Select `Colonial` or `Warden`", false)
                        .AddField("**Required Screenshot**", "Map Screenshot **ONLY**\nScreenshots from **Home Region** OR **Secure Map** will be **rejected**.", false)
                        .AddField("\u200B​", "\u200B", false)
                        .AddField("**How long will it take?**", "If you have given us the correct information, one of the officers will handle your request asap.", false)
                        .WithImageUrl(imageUrl)
                        .WithColor(Color.Gold)
                        .WithFooter("Brought to you by Dockhound")
                        .Build();

                    await RespondAsync(embed: embed);
                }

                //[RequireUserPermission(GuildPermission.Administrator)]
                //[SlashCommand("applicant_button", "Create Button for Applicants to use")]
                //public async Task ApplicantButton()
                //{
                //    var button = new ComponentBuilder()
                //        .WithButton("Assign Applicant", $"assign_applicant");

                //    await RespondAsync(
                //        text: "\u200B",
                //        components: button.Build()
                //    );
                //}

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("restrict", "Set the channel’s access mode")]
                public async Task Restrict([Summary("setting", "Restricted / MembersOnly / Open")] AccessRestriction accessLevel)
                {
                    await DeferAsync(ephemeral: true);

                    var cfg = await _guildSettingsProvider.GetAsync(Context.Guild.Id);

                    if (Context.Channel is not SocketTextChannel channel)
                    {
                        await FollowupAsync("This command can only be used in text channels.", ephemeral: true);
                        return;
                    }

                    var membersOnlyRoles = cfg.Verify.RestrictedAccess.MemberOnlyRoles.ToSet();
                    var alwaysDenyRoles = cfg.Verify.RestrictedAccess.AlwaysRestrictRoles.ToSet();

                    // Active allow list: Restricted has NO whitelist; MembersOnly allows configured roles EXCEPT always-deny
                    var activeAllow = accessLevel == AccessRestriction.MembersOnly
                        ? membersOnlyRoles.Except(alwaysDenyRoles).ToHashSet()
                        : new HashSet<ulong>();

                    // Helper: merge only the fields we manage (preserve others)
                    static OverwritePermissions Merge(OverwritePermissions? current,
                        PermValue? view = null, PermValue? send = null, PermValue? appCmds = null)
                    {
                        var c = current ?? new OverwritePermissions();
                        return c.Modify(
                            viewChannel: view ?? c.ViewChannel,
                            sendMessages: send ?? c.SendMessages,
                            useApplicationCommands: appCmds ?? c.UseApplicationCommands
                        );
                    }

                    var everyone = Context.Guild.EveryoneRole;

                    // @everyone: View always allowed; Send/AppCmd depends on mode
                    var everyoneSend = accessLevel == AccessRestriction.Open ? PermValue.Allow : PermValue.Deny;
                    var mergedEveryone = Merge(channel.GetPermissionOverwrite(everyone),
                                               view: PermValue.Allow, send: everyoneSend, appCmds: everyoneSend);
                    await channel.AddPermissionOverwriteAsync(everyone, mergedEveryone);

                    // We only manage overwrites for roles in our config lists; leave all other roles untouched
                    var managedRoleIds = new HashSet<ulong>(membersOnlyRoles.Concat(alwaysDenyRoles));

                    foreach (var roleId in managedRoleIds)
                    {
                        var role = Context.Guild.GetRole(roleId);
                        if (role is null) continue;

                        var cur = channel.GetPermissionOverwrite(role);

                        // Always-deny roles: hard deny View/Send/AppCmds in ALL modes
                        if (alwaysDenyRoles.Contains(roleId))
                        {
                            var merged = Merge(cur, view: PermValue.Deny, send: PermValue.Deny, appCmds: PermValue.Deny);
                            await channel.AddPermissionOverwriteAsync(role, merged);
                            continue;
                        }

                        // MembersOnly: allow Send/AppCmds for active roles; otherwise revert those fields to Inherit
                        if (accessLevel == AccessRestriction.MembersOnly && activeAllow.Contains(roleId))
                        {
                            var merged = Merge(cur, send: PermValue.Allow, appCmds: PermValue.Allow);
                            await channel.AddPermissionOverwriteAsync(role, merged);
                        }
                        else
                        {
                            if (cur.HasValue)
                            {
                                var merged = Merge(cur, send: PermValue.Inherit, appCmds: PermValue.Inherit);
                                await channel.AddPermissionOverwriteAsync(role, merged);
                            }
                        }
                    }

                    var displayName = await _guildSettingsProvider.GetGuildDisplayNameAsync(Context.Guild.Id) ?? "";

                    // ---------- Banner handling ----------
                    string? bannerContent = accessLevel switch
                    {
                        AccessRestriction.Restricted =>
                            "## 🔒 Verification is **Restricted**\nNo verification is allowed at this time!",
                        AccessRestriction.MembersOnly =>
                            $"## :warning: Pre-Verification is set to **Members Only**\nOnly {displayName} regiment members can verify.",
                        AccessRestriction.Open => null
                    };

                    // Always fetch the latest stored ids

                    

                    var ra = await _guildSettingsProvider.GetRestrictedAccessAsync(Context.Guild.Id);
                    var storedChannelId = ra.ChannelId;
                    var storedMsgId = ra.MessageId;

                    if (bannerContent is not null)
                    {
                        if (storedChannelId.HasValue && storedMsgId.HasValue)
                        {
                            try
                            {
                                var bannerChannel =
                                    Context.Guild.GetTextChannel(storedChannelId.Value) ??
                                    Context.Guild.GetTextChannel(channel.Id);

                                if (bannerChannel is not null)
                                {
                                    var oldMsg = await bannerChannel.GetMessageAsync(storedMsgId.Value) as IUserMessage;
                                    if (oldMsg is not null)
                                        await oldMsg.DeleteAsync();
                                }
                            }
                            catch
                            {
                                // ignore (message already gone / permissions / etc.)
                            }
                        }

                        var newBanner = await channel.SendMessageAsync(bannerContent);

                        await _guildSettingsProvider.UpdateRestrictedAccessAsync(Context.Guild.Id, channel.Id, newBanner.Id, Context.User.Username + "#" + Context.User.Id);
                    }
                    else
                    {
                        if (storedChannelId.HasValue && storedMsgId.HasValue)
                        {
                            try
                            {
                                var bannerChannel =
                                    Context.Guild.GetTextChannel(storedChannelId.Value) ??
                                    Context.Guild.GetTextChannel(channel.Id);

                                if (bannerChannel is not null)
                                {
                                    var oldMsg = await bannerChannel.GetMessageAsync(storedMsgId.Value) as IUserMessage;
                                    if (oldMsg is not null)
                                        await oldMsg.DeleteAsync();
                                }
                            }
                            catch { /* ignore */ }

                            await _guildSettingsProvider.UpdateRestrictedAccessAsync(Context.Guild.Id, null, null, Context.User.Username + "#" + Context.User.Id);
                        }
                    }
                    // ---------- end banner handling ----------

                    await FollowupAsync(
                        accessLevel switch
                        {
                            AccessRestriction.Open =>
                                "Channel mode set to **Open**. Everyone can send messages and use application commands (banner removed).",
                            AccessRestriction.MembersOnly =>
                                $"Channel mode set to **MembersOnly**. Only configured roles can send messages and use application commands ({activeAllow.Count} role(s)).",
                            AccessRestriction.Restricted =>
                                "Channel mode set to **Restricted**. Send messages and application commands are disabled for everyone (view remains allowed).",
                            _ => $"Channel mode set to **{accessLevel}**."
                        },
                        ephemeral: true
                    );
                }

            }
        }
    }
}
