using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLL_Tracker.Extensions
{
    public static class StringExtensions
    {
        public static string ToTitleCase(this string source) => ToTitleCase(source, null);

        public static string ToTitleCase(this string source, CultureInfo culture)
        {
            culture = culture ?? CultureInfo.CurrentUICulture;
            return culture.TextInfo.ToTitleCase(source.ToLower());
        }
    }
}
