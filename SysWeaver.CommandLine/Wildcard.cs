using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// File name wildcard matching (* and ?) using Windows or Unix style rules.
    /// </summary>
    /// <remarks>Each call builds and evaluates a <see cref="Regex"/> (no caching), so avoid it in hot loops.</remarks>
    public static class Wildcard
    {

        /// <summary>
        /// <para>Tests if a file name matches the given wildcard pattern, uses the same rule as shell commands.</para>
        /// </summary>
        /// <param name="fileName">The file name to test, without folder.</param>
        /// <param name="pattern">A wildcard pattern which can use char * to match any amount of characters; or char ? to match one character.</param>
        /// <param name="unixStyle">
        /// If true, use the *nix style wildcard rules (case sensitive, * matches anything, ? exactly one character);
        /// otherwise use Windows style rules (case insensitive, ? matches zero or one non-dot character, special handling of trailing dots and ".*").
        /// NOTE: the Unix style implementation currently swaps the file name and pattern internally, see the remarks.
        /// </param>
        /// <returns>true if the file name matches the pattern, false otherwise.</returns>
        /// <remarks>
        /// With <paramref name="unixStyle"/> true, <paramref name="fileName"/> is currently used as the pattern and <paramref name="pattern"/> as the text,
        /// so a call like Match("a.txt", "*.txt", true) returns false.
        /// </remarks>
        public static bool Match(string fileName, string pattern, bool unixStyle = false)
            => unixStyle ? WildcardMatchesUnixStyle(fileName, pattern) : WildcardMatchesWindowsStyle(fileName, pattern);

        static bool WildcardMatchesWindowsStyle(string fileName, string pattern)
        {
            var dotdot = pattern.FastIndexOf("..");
            if (dotdot >= 0)
            {
                for (var i = dotdot; i < pattern.Length; i++)
                    if (pattern[i] != '.')
                        return false;
            }

            var normalized = Regex.Replace(pattern, @"\.+$", "");
            var endsWithDot = normalized.Length != pattern.Length;

            var endWeight = 0;
            if (endsWithDot)
            {
                var lastNonWildcard = normalized.Length - 1;
                for (; lastNonWildcard >= 0; lastNonWildcard--)
                {
                    var c = normalized[lastNonWildcard];
                    if (c == '*')
                        endWeight += short.MaxValue;
                    else if (c == '?')
                        endWeight += 1;
                    else
                        break;
                }

                if (endWeight > 0)
                    normalized = normalized.Substring(0, lastNonWildcard + 1);
            }

            var endsWithWildcardDot = endWeight > 0;
            var endsWithDotWildcardDot = endsWithWildcardDot && normalized.EndsWith(".");
            if (endsWithDotWildcardDot)
                normalized = normalized.Substring(0, normalized.Length - 1);

            normalized = Regex.Replace(normalized, @"(?!^)(\.\*)+$", @".*");

            var escaped = Regex.Escape(normalized);
            string head, tail;

            if (endsWithDotWildcardDot)
            {
                head = "^" + escaped;
                tail = @"(\.[^.]{0," + endWeight + "})?$";
            }
            else if (endsWithWildcardDot)
            {
                head = "^" + escaped;
                tail = "[^.]{0," + endWeight + "}$";
            }
            else
            {
                head = "^" + escaped;
                tail = "$";
            }

            if (head.EndsWith(@"\.\*") && head.Length > 5)
            {
                head = head.Substring(0, head.Length - 4);
                tail = @"(\..*)?" + tail;
            }

            var regex = head.Replace(@"\*", ".*").Replace(@"\?", "[^.]?") + tail;
            return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase);
        }

        private static bool WildcardMatchesUnixStyle(string pattern, string text)
        {
            var regex = "^" + Regex.Escape(pattern)
                                   .Replace("\\*", ".*")
                                   .Replace("\\?", ".")
                        + "$";

            return Regex.IsMatch(text, regex);
        }

    }
}
