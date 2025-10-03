using Dockhound.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Services
{
    public interface IGuildSettingsService
    {
        Task<GuildConfig> GetAsync(ulong guildId, CancellationToken ct = default);
        Task UpdateAsync(ulong guildId, GuildConfig next, string? changedBy = null, CancellationToken ct = default);
        void Invalidate(ulong guildId);

        Task<GuildConfig> PatchAsync(ulong guildId, Action<GuildConfig> mutate, string? changedBy = null, CancellationToken ct = default);

        Task<GuildConfig.RestrictedAccessSettings> GetRestrictedAccessAsync(ulong guildId, CancellationToken ct = default);
        Task UpdateRestrictedAccessAsync(ulong guildId, ulong? channelId, ulong? messageId, string? changedBy = null, CancellationToken ct = default);

        Task<string?> GetGuildNameAsync(ulong guildId, CancellationToken ct = default);
        Task<string?> GetGuildTagAsync(ulong guildId, CancellationToken ct = default);
        Task<string?> GetGuildDisplayNameAsync(ulong guildId, CancellationToken ct = default);
    }
}
