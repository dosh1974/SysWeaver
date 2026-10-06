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
    /// The cookies are parsed as "name=value" pairs separated by ';', names and values are trimmed (white space), the last value of a name is used.
    /// Values are not url decoded or unquoted.
    /// Enumerating (or Count, Keys, Values) creates a dictionary once.
    /// </summary>
    /// <remarks>
    /// Not used for the session cookie: <see cref="HttpServerBase"/> extracts that with its own scan, which uses the first occurrence instead of the last.
    /// </remarks>
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
        /// Reads the "name=value" pairs of a cookie header (a segment without '=' becomes part of the following name, parsing ends when no '=' remains).
        /// </summary>
        ref struct Reader
        {
            public Reader(ReadOnlySpan<Char> header)
            {
                Rest = header;
            }

            ReadOnlySpan<Char> Rest;

            /// <summary>
            /// Read the next pair.
            /// </summary>
            /// <param name="name">The trimmed name</param>
            /// <param name="value">The trimmed value</param>
            /// <returns>False if there are no more pairs</returns>
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

        /// <inheritdoc/>
        public bool TryGetValue(String key, [MaybeNullWhen(false)] out String value)
            => TryGetValue(Header, key, out value);

        /// <summary>
        /// Get the value of a cookie directly from a cookie header (same result as Create(header).TryGetValue(key, out value), without allocating a dictionary).
        /// </summary>
        /// <param name="header">The cookie header, may be null</param>
        /// <param name="key">The name of the cookie (case sensitive)</param>
        /// <param name="value">The value of the cookie (the last value if the cookie is present multiple times)</param>
        /// <returns>True if the cookie was found</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        public static bool TryGetValue(String header, String key, [MaybeNullWhen(false)] out String value)
        {
            ArgumentNullException.ThrowIfNull(key);
            var reader = new Reader(header);
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

        /// <inheritdoc/>
        public bool ContainsKey(String key)
        {
            ArgumentNullException.ThrowIfNull(key);
            var reader = new Reader(Header);
            while (reader.Next(out var n, out _))
                if (n.SequenceEqual(key))
                    return true;
            return false;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IEnumerable<String> Keys => GetAll().Keys;

        /// <inheritdoc/>
        public IEnumerable<String> Values => GetAll().Values;

        /// <inheritdoc/>
        public int Count => GetAll().Count;

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<String, String>> GetEnumerator() => GetAll().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetAll().GetEnumerator();
    }
}
