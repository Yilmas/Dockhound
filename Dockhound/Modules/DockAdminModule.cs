using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Enums;
using Dockhound.Extensions;
using Dockhound.Logs;
using Dockhound.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                private readonly WllTrackerContext _dbContext;
                private readonly HttpClient _httpClient;
                private readonly IConfiguration _configuration;
                private readonly AppSettings _settings;

                public DockSettings(WllTrackerContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
                {
                    _dbContext = dbContext;
                    _httpClient = httpClient;
                    _configuration = config;
                    _settings = appSettings.Value;
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("view", "Displays current app settings")]
                public async Task GetAppSettings()
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(
                        _settings,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );

                    var embed = new EmbedBuilder()
                        .WithTitle("App Settings")
                        .WithDescription($"```\n{json}\n```")
                        .WithColor(Color.Blue)
                        .Build();

                    await RespondAsync(embed: embed, ephemeral: true);
                }


                [RequireUserPermission(GuildPermission.ViewAuditLog | GuildPermission.ManageMessages)]
                [SlashCommand("log", "Display audit log for the bot.")]
                public async Task TrackerLog(string query = "all", DateTime? startDate = null, DateTime? endDate = null)
                {

                    var lookup = await LogFilter.LookupLogEventsAsync(_dbContext, query, startDate, endDate);

                    var memoryStream = LogFilter.ConvertToMemoryStream(lookup);

                    string start = startDate?.ToString("yyyyMMdd_HHmmss") ?? "start";
                    string end = endDate?.ToString("yyyyMMdd_HHmmss") ?? "now";

                    await RespondWithFileAsync(memoryStream, $"logs_{start}_to_{end}.json", "Here are the filtered log entries.", ephemeral: true);

                    try
                    {
                        var msg = await GetOriginalResponseAsync();

                        var log = new LogEvent(
                            eventName: "Log Retrieval",
                            messageId: msg.Id,
                            username: Context.User.Username,
                            userId: Context.User.Id
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

            [Group("verify", "Admin root for Verify Module")]
            public class VerifyAdminSetup : InteractionModuleBase<SocketInteractionContext>
            {
                private readonly WllTrackerContext _dbContext;
                private readonly HttpClient _httpClient;
                private readonly IConfiguration _configuration;
                private readonly AppSettings _settings;

                public VerifyAdminSetup(WllTrackerContext dbContext, HttpClient httpClient, IConfiguration config, IOptions<AppSettings> appSettings)
                {
                    _dbContext = dbContext;
                    _httpClient = httpClient;
                    _configuration = config;
                    _settings = appSettings.Value;
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("info", "Provides information on the verification process.")]
                public async Task VerifyInfo()
                {
                    string imageUrl = _configuration["VERIFY_IMAGEURL"];

                    var embed = new EmbedBuilder()
                        .WithTitle("Looking to Verify?")
                        .WithDescription("Follow the steps below to get yourself verified.")
                        .AddField("Steps to Verify", "1. Enter `/verify me`\n2. Upload your `MAP SCREEN Screenshot`\n3. Select `Colonial` or `Warden`", false)
                        .AddField("**Required Screenshot**", "Map Screenshot **ONLY**\nScreenshots from **Home Region** OR **Secure Map** will be **rejected**.", false)
                        .AddField("\u200B​", "\u200B", false)
                        .AddField("**How long will it take?**", "If you have given us the correct information, one of the officers will handle your request asap.", false)
                        .WithImageUrl(imageUrl)
                        .WithColor(Color.Gold)
                        .WithFooter("Brought to you by WLL Cannonsmoke")
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

                    if (Context.Channel is not SocketTextChannel channel)
                    {
                        await FollowupAsync("This command can only be used in text channels.", ephemeral: true);
                        return;
                    }

                    var membersOnlyRoles = _configuration["RESTRICT_MEMBERONLY_ROLES"].ParseRoleIds();
                    var alwaysDenyRoles = _configuration["RESTRICT_ALWAYS_DENY_ROLES"].ParseRoleIds(); // NEW: multiple role IDs

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

                    // ---------- Banner handling ----------
                    string? bannerContent = accessLevel switch
                    {
                        AccessRestriction.Restricted =>
                            "## 🔒 Verification is **Restricted**\nNo verification is allowed at this time!",
                        AccessRestriction.MembersOnly =>
                            "## :warning: Pre-Verification is set to **Members Only**\nOnly HvL regiment members can verify.",
                        AccessRestriction.Open => null
                    };

                    // Reuse the same stored message ID for both Restricted/MembersOnly
                    var storedMsgId = _settings.Verify.RestrictedAccess?.ChannelId;
                    if (bannerContent is not null)
                    {
                        IUserMessage? banner = null;
                        if (storedMsgId.HasValue)
                        {
                            try { banner = await channel.GetMessageAsync(storedMsgId.Value) as IUserMessage; } catch { /* ignore */ }
                        }

                        if (banner is null)
                        {
                            banner = await channel.SendMessageAsync(bannerContent);
                            AppSettingsService.UpdateRestrictedAccess(channel.Id, banner.Id);
                        }
                        else
                        {
                            await banner.ModifyAsync(m => m.Content = bannerContent);
                            AppSettingsService.UpdateRestrictedAccess(channel.Id, banner.Id);
                        }
                    }
                    else
                    {
                        if (storedMsgId.HasValue)
                        {
                            try
                            {
                                if (await channel.GetMessageAsync(storedMsgId.Value) is IUserMessage toDelete)
                                    await toDelete.DeleteAsync();
                            }
                            catch { /* ignore */ }
                            AppSettingsService.UpdateRestrictedAccess(); // clear stored state
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
