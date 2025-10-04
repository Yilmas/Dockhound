using Discord;
using Dockhound.Config;
using Dockhound.Enums;
using Dockhound.Models;   // Faction enum
using Dockhound.Services; // IGuildSettingsService
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Dockhound.Services.VerificationHistoryService;

namespace Dockhound.Models
{
    public static class DiscordRolesList
    {
        public static async Task<List<ulong>> GetDeltaRoleIdListAsync(IGuildSettingsService settings, IGuildUser user, Faction faction, CancellationToken ct = default)
        {
            var cfg = await settings.GetAsync(user.GuildId, ct);
            var roles = cfg.Roles ?? new List<GuildConfig.RoleSet>();
            return ComputeDelta(roles, user, faction);
        }

        public static Task<List<ulong>> GetDeltaRoleIdListAsync(IGuildSettingsService settings, IGuildUser user, string faction, CancellationToken ct = default)
        {
            var parsed = FactionParser.Parse(faction); // throws if invalid
            return GetDeltaRoleIdListAsync(settings, user, parsed, ct);
        }

        public static async Task<string> GetDeltaRoleMentionsAsync(IGuildSettingsService settings, IGuildUser user, Faction faction, CancellationToken ct = default)
        {
            var ids = await GetDeltaRoleIdListAsync(settings, user, faction, ct);
            return string.Join(", ", ids.Select(id => $"<@&{id}>"));
        }

        public static async Task<string> GetDeltaRoleMentionsAsync(IGuildSettingsService settings, IGuildUser user, string faction, CancellationToken ct = default)
        {
            var ids = await GetDeltaRoleIdListAsync(settings, user, faction, ct);
            return string.Join(", ", ids.Select(id => $"<@&{id}>"));
        }

        private static List<ulong> ComputeDelta(IEnumerable<GuildConfig.RoleSet> roleSets, IGuildUser user, Faction faction)
        {
            var result = new List<ulong>();
            var userRoles = user.RoleIds?.ToHashSet() ?? new HashSet<ulong>();

            foreach (var r in roleSets)
            {
                // Normalize name once
                var name = r.Name?.Trim() ?? string.Empty;

                ulong? roleToAssign = null;

                if (string.Equals(name, "Faction", StringComparison.OrdinalIgnoreCase))
                {
                    roleToAssign = faction switch
                    {
                        Faction.Colonial => r.Colonial ?? r.Generic,
                        Faction.Warden => r.Warden ?? r.Generic,
                        _ => r.Generic
                    };
                }
                else
                {
                    // Only assign faction variant if user already has the generic base role
                    var hasGeneric = r.Generic.HasValue && userRoles.Contains(r.Generic.Value);
                    if (hasGeneric)
                    {
                        var factionRole = faction switch
                        {
                            Faction.Colonial => r.Colonial,
                            Faction.Warden => r.Warden,
                            _ => r.Generic
                        };
                        roleToAssign = factionRole ?? r.Generic; // fallback if needed
                    }
                }

                // Add only if a valid role id and user doesn't already have it
                if (roleToAssign.HasValue &&
                    roleToAssign.Value != 0 &&
                    !userRoles.Contains(roleToAssign.Value))
                {
                    result.Add(roleToAssign.Value);
                }
            }

            return result;
        }
    }
}
