using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Extensions
{
    public static class StringExtensions
    {
        public static string ToTitleCase(this string source) => ToTitleCase(source, null);

        public static string ToTitleCase(this string source, CultureInfo culture)
        {
            culture = culture ?? CultureInfo.CurrentUICulture;
            return culture.TextInfo.ToTitleCase(source.ToLower());
        }

        /// <summary>
        /// Computes the Levenshtein edit distance between this string and <paramref name="target"/>.
        /// </summary>
        /// <param name="source">The source string (this).</param>
        /// <param name="target">The target string.</param>
        /// <param name="ignoreCase">If true, compares characters case-insensitively.</param>
        /// <returns>The number of edits (insertions, deletions, substitutions) required.</returns>
        public static int LevenshteinDistance(this string source, string target, bool ignoreCase = false)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));

            if (ReferenceEquals(source, target)) return 0;
            if (source.Length == 0) return target.Length;
            if (target.Length == 0) return source.Length;

            ReadOnlySpan<char> s = source;
            ReadOnlySpan<char> t = target;

            var v0 = new int[t.Length + 1];
            var v1 = new int[t.Length + 1];

            for (int i = 0; i < v0.Length; i++) v0[i] = i;

            if (!ignoreCase)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    v1[0] = i + 1;
                    for (int j = 0; j < t.Length; j++)
                    {
                        int cost = s[i] == t[j] ? 0 : 1;
                        v1[j + 1] = Math.Min(
                            Math.Min(v1[j] + 1, v0[j + 1] + 1),
                            v0[j] + cost);
                    }
                    Array.Copy(v1, v0, v0.Length);
                }
            }
            else
            {
                for (int i = 0; i < s.Length; i++)
                {
                    v1[0] = i + 1;
                    for (int j = 0; j < t.Length; j++)
                    {
                        // Ordinal, but case-insensitive using invariant upper
                        char a = char.ToUpperInvariant(s[i]);
                        char b = char.ToUpperInvariant(t[j]);
                        int cost = a == b ? 0 : 1;

                        v1[j + 1] = Math.Min(
                            Math.Min(v1[j] + 1, v0[j + 1] + 1),
                            v0[j] + cost);
                    }
                    Array.Copy(v1, v0, v0.Length);
                }
            }

            return v1[t.Length];
        }

        /// <summary>
        /// Returns a similarity score between 0 and 1, where 1 means identical.
        /// </summary>
        public static double LevenshteinSimilarity(this string source, string target, bool ignoreCase = false)
        {
            int distance = source.LevenshteinDistance(target, ignoreCase);
            int maxLen = Math.Max(source?.Length ?? 0, target?.Length ?? 0);
            return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
        }
    }
}
