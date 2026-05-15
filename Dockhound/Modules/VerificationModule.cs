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

    [SlashCommand("me", "Start the verification process.")]
    public async Task VerifyAsync()
    {
        var steamRequired = false;

        if (_guildSettingsService.TryGetCached(Context.Guild.Id, out var cfg))
            steamRequired = cfg?.Verify?.IsSteamRequired == true;

        if (steamRequired)
        {
            await RespondWithModalAsync<VerifyMeSteamRequiredModal>("verify_me_required");
        }
        else
        {
            await RespondWithModalAsync<VerifyMeSteamOptionalModal>("verify_me_optional");
        }
    }
}

