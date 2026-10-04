using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SysWeaver.Net
{
    /// <summary>
    /// The cookies of a request, read directly from the cookie header string (parsed for every request, typically only one or two cookies are read).
    /// Nothing is parsed or allocated up front, a lookup scans the header and only allocates the value that is returned.
    /// The cookies are parsed exactly like before: "name=value" pairs separated by ';', names and values are trimmed (white space), the last value of a name is used.
    /// Enumerating (or Count, Keys, Values) creates a dictionary once.
    /// </summary>
    sealed class CookieStringDictionary : IReadOnlyDictionary<String, String>
    {
        CookieStringDictionary(String header)
        {
            Header = header;
        }

        /// <summary>
        /// Get the cookies of a cookie header
        /// </summary>
        /// <param name="header">The cookie header, may be null</param>
        /// <returns>The cookies</returns>
        public static IReadOnlyDictionary<String, String> Create(String header)
            => String.IsNullOrEmpty(header) ? Empty : new CookieStringDictionary(header);

        static readonly IReadOnlyDictionary<String, String> Empty = new Dictionary<String, String>(StringComparer.Ordinal).Freeze();

        readonly String Header;

        /// <summary>
        /// All cookies (created on first use, only needed for enumeration)
        /// </summary>
        IReadOnlyDictionary<String, String> All;

        /// <summary>
        /// Reads the "name=value" pairs of a cookie header
        /// </summary>
        ref struct Reader
        {
            public Reader(ReadOnlySpan<Char> header)
            {
                Rest = header;
            }

            ReadOnlySpan<Char> Rest;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Next(out ReadOnlySpan<Char> name, out ReadOnlySpan<Char> value)
            {
                var r = Rest;
                var e = r.IndexOf('=');
                if (e < 0)
                {
                    name = default;
                    value = default;
                    return false;
                }
                name = r[..e].Trim();
                r = r[(e + 1)..];
                e = r.IndexOf(';');
                if (e < 0)
                {
                    // The last pair
                    value = r.Trim();
                    Rest = default;
                    return true;
                }
                value = r[..e].Trim();
                Rest = r[(e + 1)..];
                return true;
            }
        }

        public bool TryGetValue(String key, [MaybeNullWhen(false)] out String value)
        {
            ArgumentNullException.ThrowIfNull(key);
            var reader = new Reader(Header);
            ReadOnlySpan<Char> found = default;
            bool any = false;
            while (reader.Next(out var n, out var v))
            {
                // The last value of a name is used
                if (n.SequenceEqual(key))
                {
                    found = v;
                    any = true;
                }
            }
            value = any ? found.ToString() : null;
            return any;
        }

        public bool ContainsKey(String key)
        {
            ArgumentNullException.ThrowIfNull(key);
            var reader = new Reader(Header);
            while (reader.Next(out var n, out _))
                if (n.SequenceEqual(key))
                    return true;
            return false;
        }

        public String this[String key]
            => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException("The given key '" + key + "' was not present in the dictionary.");

        /// <summary>
        /// Get a dictionary with all cookies (created once)
        /// </summary>
        IReadOnlyDictionary<String, String> GetAll()
        {
            var a = All;
            if (a != null)
                return a;
            var d = new Dictionary<String, String>(StringComparer.Ordinal);
            var reader = new Reader(Header);
            while (reader.Next(out var n, out var v))
                d[n.ToString()] = v.ToString();
            a = d.Freeze();
            All = a;
            return a;
        }

        public IEnumerable<String> Keys => GetAll().Keys;

        public IEnumerable<String> Values => GetAll().Values;

        public int Count => GetAll().Count;

        public IEnumerator<KeyValuePair<String, String>> GetEnumerator() => GetAll().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetAll().GetEnumerator();
    }
}
