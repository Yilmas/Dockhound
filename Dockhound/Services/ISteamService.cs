using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Services
{
    public interface ISteamService
    {
        bool IsSteam64Id(string? input);

        bool TryParseSteam64Id(string? input, out ulong steam64Id);

        bool IsSteamProfileUrl(string? input);

        string? ExtractVanityName(string? input);

        string BuildProfileUrl(ulong steam64Id);

        Task<SteamResolveResult> ResolveSteam64IdAsync(
            string? input,
            CancellationToken cancellationToken = default);
    }

    public enum SteamResolveStatus
    {
        Resolved,
        EmptyInput,
        InvalidInput,
        MissingApiKey,
        NotFound,
        RateLimited,
        SteamUnavailable,
        Failed
    }

    public sealed record SteamResolveResult(
        SteamResolveStatus Status,
        ulong? Steam64Id = null,
        string? ProfileUrl = null,
        string? Message = null);
}
