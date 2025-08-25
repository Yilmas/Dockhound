using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Extensions
{
    public static class ConfigParsingExtensions
    {
        /// <summary>
        /// Parse a comma/semicolon/whitespace/newline-separated list of ulongs into a HashSet.
        /// Safe for null/empty input.
        /// </summary>
        public static HashSet<ulong> ParseRoleIds(this string? csv)
        {
            var result = new HashSet<ulong>();
            if (string.IsNullOrWhiteSpace(csv))
                return result;

            foreach (var token in csv.Split(new[] { ',', ';', '\n', '\r', ' ' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (ulong.TryParse(token, out var id))
                    result.Add(id);
            }

            return result;
        }

        /// <summary>
        /// Convenience: reads a config key and parses it using ParseRoleIds().
        /// </summary>
        public static HashSet<ulong> GetRoleIds(this IConfiguration config, string key)
            => config[key].ParseRoleIds();
    }
}
