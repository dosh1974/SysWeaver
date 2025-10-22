using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver
{
    /// <summary>
    /// Provides a fast way to determine what a string starts with (from a given fixed set), the string to test MUST start with one in the set
    /// </summary>
    public static class PrefixFinder
    {

        static String StripPrefix(String s)
        {
            int i = s.IndexOf("://", StringComparison.Ordinal);
            if (i < 0)
                return null;
            i = s.IndexOf('/', i + 3);
            return i < 0 ? "" : s.Substring(i);
        }

        /// <summary>
        /// Creates a prefix finder
        /// </summary>
        /// <param name="prefixes">The prefixes that we should match against, ex: ["http://", "https://", "ftp://", "sftp://"]</param>
        /// <param name="caseSensitive">True if the comparision should be case sensitive, else false</param>
        /// <returns>A function that given a string, returns the prefix string that it starts with.
        /// The given string must start with one of the pre-defined prefixes or the behaviour is undefined (can't throw exceptions or return String.Empty etc)</returns>
        public static Func<String, String> Create(String[] prefixes, bool caseSensitive = true)
        {
            prefixes = new HashSet<String>(prefixes.Select(x => StripPrefix(x))).ToArray();


            var l = prefixes.Length;
            if (l == 0)
                return t => String.Empty;
            if (l == 1)
            {
                var v = prefixes[0];
                return t => v;
            }
            var minLen = prefixes.Min(x => x.Length);
            int firstUnMatched = -1;
            if (minLen > 0)
            {
                for (int testLen = 0; testLen < minLen; ++testLen)
                {
                    HashSet<char> seen = new HashSet<char>();
                    foreach (var p in prefixes)
                    {
                        var c = p[testLen];
                        if (!caseSensitive)
                            c = CharExt.FastLower(c);
                        seen.Add(c);
                    }
                    if (seen.Count == l)
                    {
                        firstUnMatched = testLen;
                        break;
                    }
                }
                if (firstUnMatched >= 0)
                {
                    Dictionary<Char, String> all = new Dictionary<char, string>();
                    foreach (var p in prefixes)
                    {
                        var c = p[firstUnMatched];
                        all[c] = p;
                        if (!caseSensitive)
                        {
                            all[CharExt.FastUpper(c)] = p;
                            all[CharExt.FastLower(c)] = p;
                        }
                    }
                    String Get(Char c) => all.TryGetValue(c, out var l) ? l : String.Empty;
                    var min = (int)all.Keys.Min();
                    var max = (int)all.Keys.Max();
                    var count = max - min + 1;
                    if (count < 256)
                    {
                        var a = GC.AllocateUninitializedArray<String>(count);
                        for (int i = 0; i < count; ++i)
                            a[i] = Get((Char)(i + min));
                        return t => a[t[firstUnMatched] - min];
                    }
                    else
                    {
                        return t => Get(t[firstUnMatched]);
                    }
                }
            }

            var tree = new TernaryTree<String>(caseSensitive);
            foreach (var x in prefixes)
                tree.Add(x, x);
            return t => tree.TryFindStart(out var v, t) ? (v ?? String.Empty) : String.Empty;
/*            var sorted = prefixes.ToArray();
            Array.Sort(sorted, (a, b) => b.Length - a.Length);
            if (caseSensitive)
            {
                return t =>
                {
                    foreach (var x in sorted)
                    {
                        if (t.StartsWith(x))
                            return x;

                    }
                    return String.Empty;
                };
            }
            return t =>
            {
                foreach (var x in sorted)
                {
                    if (t.StartsWith(x, true, null))
                        return x;

                }
                return String.Empty;
            };
*/
        }

    }
}
