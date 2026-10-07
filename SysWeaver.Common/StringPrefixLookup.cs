using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// An immutable lookup that finds the longest string (from a set of strings) that a text starts with, using ordinal (case sensitive) compares.
    /// Optimized for a few (up to ~32) short strings (4 - 16 chars), where most searches doesn't match, like the web page sub paths of special modules.
    /// Empty strings are not supported, they can't be added (throws an <see cref="ArgumentException"/>) and can't be searched for (throws in debug builds).
    /// See <see cref="StringPrefixLookupBase"/> for the implementation. Immutable and thread safe.
    /// Used by the http server for the path prefixes of modules and redirects (and as a faster drop in replacement for a case sensitive <see cref="FrozenStringTree"/>).
    /// </summary>
    public sealed class StringPrefixLookup : StringPrefixLookupBase, IStringTree
    {
        /// <summary>
        /// Create a lookup of some strings
        /// </summary>
        /// <param name="strings">The strings, may not contain null, empty strings or duplicates (ordinal)</param>
        /// <exception cref="ArgumentNullException">A string is null (or <paramref name="strings"/> is null)</exception>
        /// <exception cref="ArgumentException">A string is empty</exception>
        /// <exception cref="Exception">A string is a duplicate</exception>
        public StringPrefixLookup(IEnumerable<String> strings) : this(strings.ToArray())
        {
        }

        /// <summary>
        /// Create a lookup where the leaf of a string is the string itself
        /// </summary>
        StringPrefixLookup(String[] strings) : base(strings, strings)
        {
        }

        /// <summary>
        /// Find the longest string, that the text starts with
        /// </summary>
        /// <param name="text">The text to match against the strings, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end returns null)</param>
        /// <returns>The longest found match (the stored string instance) or null if no match is found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String StartsWithAny(String text, int start = 0) => Unsafe.As<String>(StartsWithAnyLeaf(text, start));

        /// <summary>
        /// Create a lookup of strings with values, where a string can have more than one value (a drop in replacement for a case sensitive FrozenStringTreeList)
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="strings">The strings and values, the strings may not be null or empty (duplicate strings are grouped)</param>
        /// <returns>A lookup where the value of a string is the values of that string (in the order they where added)</returns>
        /// <exception cref="ArgumentNullException">A string is null</exception>
        public static StringPrefixLookup<IReadOnlyList<T>> BuildList<T>(IEnumerable<Tuple<String, T>> strings)
            => new(strings
                .GroupBy(x => x.Item1, StringComparer.Ordinal)
                .Select(g => KeyValuePair.Create(g.Key, (IReadOnlyList<T>)g.Select(x => x.Item2).ToArray())));

        /// <summary>
        /// Create a lookup of strings with values, where a string can have more than one value (a drop in replacement for a case sensitive FrozenStringTreeList)
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="strings">The strings and values, the strings may not be null or empty (duplicate strings are grouped)</param>
        /// <returns>A lookup where the value of a string is the values of that string (in the order they where added)</returns>
        /// <exception cref="ArgumentNullException">A string is null</exception>
        public static StringPrefixLookup<IReadOnlyList<T>> BuildList<T>(IEnumerable<KeyValuePair<String, T>> strings)
            => BuildList(strings.Select(x => Tuple.Create(x.Key, x.Value)));

        /// <summary>
        /// Create a lookup of values, where a string can have more than one value (a drop in replacement for a case sensitive FrozenStringTreeList)
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="values">The values</param>
        /// <param name="getKey">Function that extracts the string key (may not return null or an empty string, values with the same key are grouped)</param>
        /// <returns>A lookup where the value of a string is the values of that string (in the order they where added)</returns>
        /// <exception cref="ArgumentNullException">A key is null</exception>
        public static StringPrefixLookup<IReadOnlyList<T>> BuildList<T>(IEnumerable<T> values, Func<T, String> getKey)
            => BuildList(values.Select(x => Tuple.Create(getKey(x), x)));
    }

    /// <summary>
    /// An immutable lookup that finds the value of the longest string (from a set of strings with a value each) that a text starts with, using ordinal (case sensitive) compares.
    /// Optimized for a few (up to ~32) short strings (4 - 16 chars), where most searches doesn't match, like the web page sub paths of special modules.
    /// Empty strings are not supported, they can't be added (throws an <see cref="ArgumentException"/>) and can't be searched for (throws in debug builds).
    /// See <see cref="StringPrefixLookupBase"/> for the implementation. Immutable and thread safe (as long as the values are).
    /// </summary>
    /// <typeparam name="T">The type of the values</typeparam>
    public sealed class StringPrefixLookup<T> : StringPrefixLookupBase
    {
        /// <summary>
        /// Create a lookup of strings with values
        /// </summary>
        /// <param name="strings">The strings and values, the strings may not be null, empty or duplicates (ordinal)</param>
        /// <exception cref="ArgumentNullException">A string is null (or <paramref name="strings"/> is null)</exception>
        /// <exception cref="ArgumentException">A string is empty</exception>
        /// <exception cref="Exception">A string is a duplicate</exception>
        public StringPrefixLookup(IEnumerable<KeyValuePair<String, T>> strings) : this(strings.ToArray())
        {
        }

        /// <summary>
        /// Create a lookup of values
        /// </summary>
        /// <param name="values">The values</param>
        /// <param name="getKey">Function that extracts the string key (may not return null, an empty string or duplicates)</param>
        /// <exception cref="ArgumentNullException">A key is null (or <paramref name="values"/> is null)</exception>
        /// <exception cref="ArgumentException">A key is empty</exception>
        /// <exception cref="Exception">A key is a duplicate</exception>
        public StringPrefixLookup(IEnumerable<T> values, Func<T, String> getKey) : this(values.Select(x => KeyValuePair.Create(getKey(x), x)).ToArray())
        {
        }

        /// <summary>
        /// Create a lookup where the leaf of a string is an <see cref="Entry"/> with the value and the precomputed prefix values
        /// </summary>
        StringPrefixLookup(KeyValuePair<String, T>[] strings) : base(strings.Select(x => x.Key).ToArray(), Entries(strings))
        {
        }

        /// <summary>
        /// Find the value of the longest string, that the text starts with
        /// </summary>
        /// <param name="text">The text to match against the strings, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end finds nothing)</param>
        /// <returns>The value of the longest found match or default if no match is found (use <see cref="TryStartsWithAny(out T, string, int)"/> if default is a valid value)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T StartsWithAny(String text, int start = 0)
        {
            var e = Unsafe.As<Entry>(StartsWithAnyLeaf(text, start));
            return e == null ? default : e.Value;
        }

        /// <summary>
        /// Find the value of the longest string, that the text starts with
        /// </summary>
        /// <param name="value">The value of the longest found match or default if no match is found</param>
        /// <param name="text">The text to match against the strings, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end finds nothing)</param>
        /// <returns>True if a match was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryStartsWithAny(out T value, String text, int start = 0)
        {
            var e = Unsafe.As<Entry>(StartsWithAnyLeaf(text, start));
            if (e == null)
            {
                value = default;
                return false;
            }
            value = e.Value;
            return true;
        }

        /// <summary>
        /// Find the values of all strings, that is a prefix of the text
        /// </summary>
        /// <param name="text">The text to find prefixes for, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end finds nothing)</param>
        /// <returns>The values of the matches, ordered from shortest match to longest match (an empty list if there are none). The returned list is shared (by all searches with the same result), it must not be modified (it's an array)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<T> PrefixesOf(String text, int start = 0)
        {
            // All strings that are a prefix of the text are prefixes of the longest one, so the result is precomputed for every string
            var e = Unsafe.As<Entry>(StartsWithAnyLeaf(text, start));
            return e == null ? NoPrefixes : e.Prefixes;
        }

        #region Implementation

        /// <summary>
        /// The (shared) result of <see cref="PrefixesOf(string, int)"/> when nothing matches
        /// </summary>
        static readonly T[] NoPrefixes = [];

        /// <summary>
        /// The leaf of a string
        /// </summary>
        sealed class Entry
        {
            /// <summary>
            /// The value of the string
            /// </summary>
            public T Value;

            /// <summary>
            /// The values of all strings that are a prefix of this string (including this), shortest first
            /// </summary>
            public T[] Prefixes;
        }

        /// <summary>
        /// Create the leafs (an <see cref="Entry"/> for every string), O(n^2) in the number of strings
        /// </summary>
        static Object[] Entries(KeyValuePair<String, T>[] strings)
        {
            var res = new Object[strings.Length];
            for (int i = 0; i < strings.Length; ++i)
            {
                var s = strings[i].Key;
                // Null strings are validated by the base class
                res[i] = new Entry
                {
                    Value = strings[i].Value,
                    Prefixes = s == null ? [] : strings
                        .Where(x => (x.Key != null) && s.StartsWith(x.Key, StringComparison.Ordinal))
                        .OrderBy(x => x.Key.Length)
                        .Select(x => x.Value)
                        .ToArray(),
                };
            }
            return res;
        }

        #endregion
    }
}
