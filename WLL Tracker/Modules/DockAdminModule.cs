using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
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

                public VerifyAdminSetup(WllTrackerContext dbContext, HttpClient httpClient, IConfiguration config)
                {
                    _dbContext = dbContext;
                    _httpClient = httpClient;
                    _configuration = config;
                }

                [RequireUserPermission(GuildPermission.ManageMessages)]
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

                [RequireUserPermission(GuildPermission.ManageMessages)]
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
            }
        }
    }
}
