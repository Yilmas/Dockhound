using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WLL_Tracker.Models;

namespace WLL_Tracker.Modules
{
    public class DockAdminModule : InteractionModuleBase<SocketInteractionContext>
    {
        [CommandContextType(InteractionContextType.Guild)]
        [Group("dockadmin", "Root command of Dockhound Admin")]
        public class DockAdminSetup : InteractionModuleBase<SocketInteractionContext>
        {
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

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("applicant_button", "Create Button for Applicants to use")]
                public async Task ApplicantButton()
                {
                    var button = new ComponentBuilder()
                        .WithButton("Assign Applicant", $"assign_applicant");

                    await RespondAsync(
                        text: "\u200B",
                        components: button.Build()
                    );
                }

                [RequireUserPermission(GuildPermission.Administrator)]
                [SlashCommand("restrict", "Toggles restriction for verifications")]
                public async Task RestrictToggle()
                {
                    await DeferAsync(ephemeral: true);

                    var channel = Context.Channel as SocketTextChannel;
                    if (channel == null)
                    {
                        await FollowupAsync("This command can only be used in text channels.", ephemeral: true);
                        return;
                    }

                    if (_settings.Verify.RestrictedAccess.Whitelist == null)
                    {
                        await FollowupAsync("No whitelisted roles!", ephemeral: true);
                        return;
                    }

                    var everyoneRole = Context.Guild.EveryoneRole;
                    var currentOverwrite = channel.GetPermissionOverwrite(everyoneRole);
                    bool isRestricted = currentOverwrite?.SendMessages == PermValue.Deny;

                    if (!isRestricted)
                    {
                        // Restricting...
                        await channel.AddPermissionOverwriteAsync(everyoneRole, new OverwritePermissions(
                            viewChannel: PermValue.Allow,
                            sendMessages: PermValue.Deny,
                            useApplicationCommands: PermValue.Deny));

                        foreach (var roleId in _settings.Verify.RestrictedAccess.Whitelist)
                        {
                            var role = Context.Guild.GetRole(roleId);
                            if (role != null)
                            {
                                await channel.AddPermissionOverwriteAsync(role, new OverwritePermissions(
                                    sendMessages: PermValue.Allow,
                                    useApplicationCommands: PermValue.Allow));
                            }
                        }

                        var restrictionMessage = await channel.SendMessageAsync("## 🔒 Pre-Verification for the upcoming war is ONLY open for WLL personnel 🔒\nNon-WLL can verify 1 hour after war start.");
                        AppSettingsService.UpdateRestrictedAccess(channel.Id, restrictionMessage.Id);

                        await FollowupAsync("Restrictions applied.", ephemeral: true);
                    }
                    else
                    {
                        // Unrestricting...
                        await channel.AddPermissionOverwriteAsync(everyoneRole, new OverwritePermissions(
                            viewChannel: PermValue.Allow,
                            sendMessages: PermValue.Allow,
                            useApplicationCommands: PermValue.Allow));

                        foreach (var roleId in _settings.Verify.RestrictedAccess.Whitelist)
                        {
                            var role = Context.Guild.GetRole(roleId);
                            if (role != null)
                            {
                                await channel.RemovePermissionOverwriteAsync(role);
                            }
                        }

                        var msgId = _settings.Verify.RestrictedAccess.ChannelId;
                        if (msgId != null)
                        {
                            try
                            {
                                var msg = await channel.GetMessageAsync(msgId.Value);
                                await msg.DeleteAsync();
                            }
                            catch { /* silently ignore errors */ }

                            AppSettingsService.UpdateRestrictedAccess();
                        }

                        await FollowupAsync("Restrictions removed.", ephemeral: true);
                    }
                }

            }
        }
    }
}
