using Dockhound.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Services
{
    public sealed record VerificationBrief(Faction Faction, DateTime ApprovedAtUtc);

    public interface IVerificationHistoryService
    {
        Task LogApprovalAsync(ulong guildId, ulong userId, Faction faction, string? imageUrl, ulong? approvedByUserId,CancellationToken ct = default);

        Task<IReadOnlyList<VerificationBrief>> GetTrackRecordAsync(ulong userId, int take = 5, CancellationToken ct = default);

        Task<Faction?> GetMostRecentFactionAsync(ulong userId, CancellationToken ct = default);
    }
}
