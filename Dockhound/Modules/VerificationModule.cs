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

    public VerificationModule(DockhoundContext dbContext, HttpClient httpHttpClient, IGuildSettingsService guildSettingsService, IVerificationHistoryService verificationHistoryService)
    {
        _dbContext = dbContext;
        _httpClient = httpHttpClient;
        _guildSettingsService = guildSettingsService;
        _verificationHistory = verificationHistoryService;
    }

    [SlashCommand("me", "Start the verification process.")]
    public async Task VerifyAsync()
    {
        var cfg = await _guildSettingsService.GetAsync(Context.Guild.Id);

        var steamRequired = cfg?.Verify?.IsSteamRequired == true;
        var restrictionLevel = cfg?.Verify?.RestrictedAccess?.CurrentRestrictionLevel ?? AccessRestriction.Open;

        // Always attempt to log diagnostic info (best-effort) before any early returns
        try
        {
            var member = Context.User as SocketGuildUser;
            var userRoleNames = member is not null
                ? member.Roles.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
                : new List<string>();

            string allowedRolesDisplay;
            if (restrictionLevel == AccessRestriction.MembersOnly)
            {
                var memberOnlyRoles = cfg?.Verify?.RestrictedAccess?.MemberOnlyRoles ?? new List<ulong>();
                var alwaysRestrictRoles = cfg?.Verify?.RestrictedAccess?.AlwaysRestrictRoles ?? new List<ulong>();
                var allowed = memberOnlyRoles.Except(alwaysRestrictRoles).ToList();
                var allowedNames = allowed.Select(id => Context.Guild?.GetRole(id)?.Name ?? id.ToString()).ToList();
                allowedRolesDisplay = allowedNames.Any() ? string.Join(", ", allowedNames) : "(none)";
            }
            else if (restrictionLevel == AccessRestriction.Open)
            {
                allowedRolesDisplay = "(all)";
            }
            else // Restricted
            {
                allowedRolesDisplay = "(none)";
            }

            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [VERIFY] User={Context.User.Username}({Context.User.Id}) Roles=[{string.Join(", ", userRoleNames)}] Mode={restrictionLevel} AllowedRoles=[{allowedRolesDisplay}]");
        }
        catch
        {
            // keep logging best-effort and never block the user flow
        }

        if (restrictionLevel == AccessRestriction.Restricted)
        {
            await RespondAsync(
                "Verification is currently restricted. Please try again later.",
                ephemeral: true);

            return;
        }

        if (restrictionLevel == AccessRestriction.MembersOnly)
        {
            var member = Context.User as SocketGuildUser;

            if (member is null)
            {
                await RespondAsync(
                    "This can only be used inside a server.",
                    ephemeral: true);

                return;
            }

            var memberOnlyRoles = cfg?.Verify?.RestrictedAccess?.MemberOnlyRoles?.ToHashSet()
                ?? new HashSet<ulong>();

            var alwaysRestrictRoles = cfg?.Verify?.RestrictedAccess?.AlwaysRestrictRoles?.ToHashSet()
                ?? new HashSet<ulong>();

            var allowedRoles = memberOnlyRoles
                .Except(alwaysRestrictRoles)
                .ToHashSet();

            var userRoleIds = member.Roles
                .Select(role => role.Id);

            var hasAllowedRole = allowedRoles.Overlaps(userRoleIds);
            var isAlwaysRestricted = alwaysRestrictRoles.Overlaps(userRoleIds);

            if (isAlwaysRestricted || !hasAllowedRole)
            {
                await RespondAsync(
                    "Verification is currently limited to configured allowed roles.",
                    ephemeral: true);

                return;
            }
        }

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

