using Dockhound.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Dockhound.Services
{
    public partial class SteamService : ISteamService
    {
        private const string SteamCommunityBaseUrl = "https://steamcommunity.com";
        private const string ResolveVanityUrlEndpoint =
            "https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/";

        private readonly IOptionsMonitor<AppSettings> _options;
        private readonly IConfigurationRoot _configRoot;
        private readonly HttpClient _httpClient;

        public SteamService(
            IOptionsMonitor<AppSettings> options,
            IConfiguration config,
            HttpClient httpClient)
        {
            _options = options;
            _configRoot = (IConfigurationRoot)config;
            _httpClient = httpClient;
        }

        [GeneratedRegex(@"^\d{17}$", RegexOptions.Compiled)]
        private static partial Regex Steam64IdRegex();

        public bool IsSteam64Id(string? input)
        {
            return !string.IsNullOrWhiteSpace(input)
                   && Steam64IdRegex().IsMatch(input.Trim());
        }

        public bool TryParseSteam64Id(string? input, out ulong steam64Id)
        {
            steam64Id = 0;

            return IsSteam64Id(input)
                   && ulong.TryParse(input!.Trim(), out steam64Id);
        }

        public bool IsSteamProfileUrl(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
                return false;

            return IsSteamCommunityHost(uri)
                   && (
                       uri.AbsolutePath.StartsWith("/id/", StringComparison.OrdinalIgnoreCase)
                       || uri.AbsolutePath.StartsWith("/profiles/", StringComparison.OrdinalIgnoreCase)
                   );
        }

        public string BuildProfileUrl(ulong steam64Id)
        {
            return $"{SteamCommunityBaseUrl}/profiles/{steam64Id}";
        }

        public string? ExtractVanityName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Trim();

            // Allow users to enter just the vanity name, if you want.
            // Example: "someUserName"
            if (!input.Contains('/') && !input.Contains('\\') && !IsSteam64Id(input))
                return input;

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
                return null;

            if (!IsSteamCommunityHost(uri))
                return null;

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length < 2)
                return null;

            // https://steamcommunity.com/id/someName
            if (segments[0].Equals("id", StringComparison.OrdinalIgnoreCase))
                return segments[1];

            // /profiles/{steam64id} is not a vanity URL.
            return null;
        }

        public async Task<SteamResolveResult> ResolveSteam64IdAsync(
            string? input,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new SteamResolveResult(SteamResolveStatus.EmptyInput);

            input = input.Trim();

            if (TryParseSteam64Id(input, out var steam64Id))
            {
                return new SteamResolveResult(
                    SteamResolveStatus.Resolved,
                    steam64Id,
                    BuildProfileUrl(steam64Id));
            }

            // Also support https://steamcommunity.com/profiles/{steam64id}
            if (TryExtractProfileSteam64Id(input, out steam64Id))
            {
                return new SteamResolveResult(
                    SteamResolveStatus.Resolved,
                    steam64Id,
                    BuildProfileUrl(steam64Id));
            }

            var vanityName = ExtractVanityName(input);

            if (string.IsNullOrWhiteSpace(vanityName))
                return new SteamResolveResult(SteamResolveStatus.InvalidInput);

            return await ResolveVanityNameAsync(vanityName, cancellationToken);
        }

        private async Task<SteamResolveResult> ResolveVanityNameAsync(string vanityName, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[SteamService] Resolving vanity name: '{vanityName}'");

            var apiKey = _options.CurrentValue.Configuration.SteamAPIKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("[SteamService] Steam API key is missing.");
                return new SteamResolveResult(SteamResolveStatus.MissingApiKey);
            }

            Console.WriteLine("[SteamService] Steam API key found.");

            var requestUri = BuildResolveVanityUri(apiKey, vanityName);

            // Do NOT log requestUri directly, since it contains the API key.
            Console.WriteLine($"[SteamService] Calling Steam ResolveVanityURL API for vanity name: '{vanityName}'");

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            Console.WriteLine($"[SteamService] Steam API response status: {(int)response.StatusCode} {response.StatusCode}");

            if (response.StatusCode == (HttpStatusCode)429)
            {
                Console.WriteLine("[SteamService] Steam API rate limit hit.");
                return new SteamResolveResult(SteamResolveStatus.RateLimited);
            }

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.BadGateway
                or HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine("[SteamService] Steam API appears unavailable.");
                return new SteamResolveResult(SteamResolveStatus.SteamUnavailable);
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[SteamService] Steam API request failed with HTTP {(int)response.StatusCode}.");

                return new SteamResolveResult(
                    SteamResolveStatus.Failed,
                    Message: $"Steam returned HTTP {(int)response.StatusCode}.");
            }

            Console.WriteLine("[SteamService] Steam API request succeeded. Reading JSON response.");

            var payload = await response.Content.ReadFromJsonAsync<ResolveVanityResponse>(
                cancellationToken: cancellationToken);

            var result = payload?.Response;

            if (result is null)
            {
                Console.WriteLine("[SteamService] Steam API response did not contain a valid response object.");
                return new SteamResolveResult(SteamResolveStatus.Failed);
            }

            Console.WriteLine(
                $"[SteamService] Steam API result: Success={result.Success}, SteamId='{result.SteamId}', Message='{result.Message}'");

            // Steam ResolveVanityURL normally returns success = 1 for match and 42 for no match.
            if (result.Success == 1 && ulong.TryParse(result.SteamId, out var steam64Id))
            {
                Console.WriteLine($"[SteamService] Vanity name resolved successfully. Steam64Id={steam64Id}");

                return new SteamResolveResult(
                    SteamResolveStatus.Resolved,
                    steam64Id,
                    BuildProfileUrl(steam64Id));
            }

            if (result.Success == 42)
            {
                Console.WriteLine($"[SteamService] Vanity name not found: '{vanityName}'");

                return new SteamResolveResult(
                    SteamResolveStatus.NotFound,
                    Message: result.Message);
            }

            Console.WriteLine(
                $"[SteamService] Steam API returned an unexpected result. Success={result.Success}, Message='{result.Message}'");

            return new SteamResolveResult(
                SteamResolveStatus.Failed,
                Message: result.Message);
        }

        private static Uri BuildResolveVanityUri(string apiKey, string vanityName)
        {
            var builder = new UriBuilder(ResolveVanityUrlEndpoint);

            var query =
                $"key={Uri.EscapeDataString(apiKey)}" +
                $"&vanityurl={Uri.EscapeDataString(vanityName)}" +
                "&url_type=1";

            builder.Query = query;

            return builder.Uri;
        }

        private static bool TryExtractProfileSteam64Id(string input, out ulong steam64Id)
        {
            steam64Id = 0;

            if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
                return false;

            if (!IsSteamCommunityHost(uri))
                return false;

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length < 2)
                return false;

            if (!segments[0].Equals("profiles", StringComparison.OrdinalIgnoreCase))
                return false;

            return ulong.TryParse(segments[1], out steam64Id);
        }

        private static bool IsSteamCommunityHost(Uri uri)
        {
            return uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.Equals("www.steamcommunity.com", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ResolveVanityResponse
        {
            public ResolveVanityInnerResponse? Response { get; set; }
        }

        private sealed class ResolveVanityInnerResponse
        {
            public int Success { get; set; }

            public string? SteamId { get; set; }

            public string? Message { get; set; }
        }
    }
}