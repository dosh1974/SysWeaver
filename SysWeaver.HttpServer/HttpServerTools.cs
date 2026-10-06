using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Net
{
    /// <summary>
    /// Helpers used by the http server and its modules: cookies, etags, url paths, url decoding, mime constants and shared handler instances.
    /// </summary>
    public static class HttpServerTools
    {
        /// <summary>
        /// The default cookie path and attributes ("/" and HttpOnly, no Secure or SameSite).
        /// </summary>
        public const String DefPath = "/;HttpOnly";

        /// <summary>
        /// Make a Set-Cookie value that expires at a given time (capped to one year from now).
        /// </summary>
        /// <param name="name">The cookie name</param>
        /// <param name="value">The cookie value, inserted as is (must not contain ';' or control chars)</param>
        /// <param name="exp">When the cookie expires (UTC), if not in the future a cookie deletion (empty value, max age 0) is made</param>
        /// <param name="path">The path, optionally followed by attributes, ex: "/;HttpOnly;SameSite=None;Secure"</param>
        /// <returns>The Set-Cookie value</returns>
        public static String MakeCookie(String name, String value, DateTime exp, String path = DefPath)
        {
            var now = DateTime.UtcNow;
            var maxDate = now.AddYears(1);
            if (exp > maxDate)
                exp = maxDate;
            var maxAge = (long)(exp - now).TotalSeconds;
            var str = maxAge <= 0 ? MakeCookie(name, "", 0, path) : MakeCookie(name, value, maxAge, path);
            return str;
        }



        /// <summary>
        /// Escape non-ASCII chars using \uXXXX (and backslashes as "\\").
        /// </summary>
        /// <param name="value">The value, must not be null</param>
        /// <returns>The escaped value (the same instance if nothing was escaped)</returns>
        public static string EncodeNonAsciiCharacters(this string value)
        {
            var len = value.Length;
            StringBuilder sb = new StringBuilder((len << 1) + 128);
            var h = SpanExt.HexChars;
            for (int i = 0; i < len; ++ i)
            {
                var c = value[i];
                if (c < 0x80)
                {
                    sb.Append(c);
                    if (c == '\\')
                        sb.Append(c);
                    continue;
                }
                sb.Append("\\u");
                uint val = (uint)c;
                sb.Append(h[val >> 12]);
                sb.Append(h[(val >> 8) & 0xf]);
                sb.Append(h[(val >> 4) & 0xf]);
                sb.Append(h[val & 0xf]);
            }
            var res = sb.Length == len ? value : sb.ToString();
#if DEBUG
            if (!res.IsAsciiOnly())
                throw new Exception();
#endif//DEBUG
            return res;
        }


        /// <summary>
        /// Get the etag for an assembly (based on its last write time).
        /// </summary>
        /// <param name="asm">The assembly</param>
        /// <returns>The etag</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String GetAssemblyEtag(Assembly asm)
            => ToEtag(asm.GetLastWriteTimerUtc());

        /// <summary>
        /// Create an etag from a DateTime (local and unspecified times are converted to UTC first).
        /// </summary>
        /// <param name="t">The time</param>
        /// <returns>The etag (can be decoded using <see cref="TryGetDateTimeFromETag"/>)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToEtag(DateTime t) => CompactAsciiString.Secure.Encode((t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime()).Ticks);

        /// <summary>
        /// Create an etag from a long.
        /// </summary>
        /// <param name="l">The value</param>
        /// <returns>The etag</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToEtag(long l) => CompactAsciiString.Secure.Encode(l);


        /// <summary>
        /// Create an etag from some data (using an MD5 hash, not for security purposes).
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>The etag</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToEtag(ReadOnlySpan<Byte> data)
        {
            Span<Byte> hash = stackalloc Byte[16];
            MD5.HashData(data, hash);
            var l0 = BitConverter.ToUInt64(hash[..8]);
            var l1 = BitConverter.ToUInt64(hash[8..]);
            return CompactAsciiString.Secure.Encode(l0) + CompactAsciiString.Secure.Encode(l1);
        }


        /// <summary>
        /// Decode a time stamp etag (made by <see cref="ToEtag(DateTime)"/>), anything after the first space is ignored.
        /// </summary>
        /// <param name="lm">The etag, may be null</param>
        /// <returns>The UTC time, or null if the value can't be decoded or the year is outside 1900 - 2500</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime? TryGetDateTimeFromETag(String lm)
        {
            try
            {
                var l = CompactAsciiString.Secure.DecodeInt64(lm.SplitFirst(' '));
                var dt = new DateTime(l, DateTimeKind.Utc);
                var y = dt.Year;
                if (y < 1900)
                    return null;
                if (y > 2500)
                    return null;
                return dt;
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// The default last write time to use for endpoints
        /// </summary>
        public static readonly DateTime StartedTime = EnvInfo.AppStart;

        /// <summary>
        /// The text to write to the last modfied response header for static responses
        /// </summary>
        public static readonly String StartedETag = ToEtag(StartedTime);

        /// <summary>
        /// Merges url paths, ignoring empty parts
        /// </summary>
        /// <param name="paths">The paths to merge, if a path is null or empty it's ignored</param>
        /// <returns>The merged path</returns>
        public static String CombinePaths(params String[] paths) => String.Join('/', paths.Where(x => !String.IsNullOrEmpty(x)));


        /// <summary>
        /// Remove "./" and resolve "../" segments in paths, ex "Api/../Test/Func" becomes "Test/Func".
        /// Empty segments (double slashes) are kept.
        /// </summary>
        /// <param name="p">A path</param>
        /// <returns>A cleaned up path (the same instance if nothing changed)</returns>
        /// <exception cref="Exception">Thrown if a "../" segment goes above the start of the path</exception>
        public static String CleanupPaths(String p)
        {
            var ps = p.Split('/');
            var l = ps.Length;
            int o = 0;
            for (int i = 0; i < l; ++i)
            {
                var t = ps[i];
                if (t == ".")
                    continue;
                if (t == "..")
                {
                    if (o == 0)
                        throw new Exception("Invalid path " + p.ToQuoted());
                    --o;
                    continue;
                }
                ps[o] = t;
                ++o;
            }
            if (o == l)
                return p;
            return String.Join('/', ps, 0, o);
        }


        /// <summary>
        /// Merges url paths, ignoring empty parts, and adds a trailing '/' if the result isn't empty.
        /// </summary>
        /// <param name="paths">The paths to merge, if a path is null or empty it's ignored</param>
        /// <returns>The merged path</returns>
        public static String CombinePathsAndAddTrailingSlash(params String[] paths)
        {
            var t = CombinePaths(paths);
            return t.Length <= 0 ? t : (t + '/');
        }

        /// <summary>
        /// Make sure that a non-empty root ends with a /
        /// </summary>
        /// <param name="root">A root</param>
        /// <returns>A root that is either empty or ends with a /</returns>
        public static String FixEnumRoot(String root) => ((root.Length <= 0) || root.EndsWith('/')) ? root : (root + '/');



        /// <summary>
        /// The charset suffix for text mime types.
        /// </summary>
        public const String TextMimeSuffix = "; charset=UTF-8";

        /// <summary>
        /// Plain UTF-8 text.
        /// </summary>
        public const String TextMime = "text/plain" + TextMimeSuffix;
        /// <summary>
        /// UTF-8 json.
        /// </summary>
        public const String JsonMime = "application/json" + TextMimeSuffix;
        /// <summary>
        /// UTF-8 html.
        /// </summary>
        public const String HtmlMime = "text/html" + TextMimeSuffix;
        /// <summary>
        /// UTF-8 svg.
        /// </summary>
        public const String SvgMime = "image/svg+xml" + TextMimeSuffix;


        /// <summary>
        /// Get a plain text handler.
        /// </summary>
        /// <param name="text">The text to respond with</param>
        /// <param name="statusCode">The status code to use</param>
        /// <param name="contentEncoding">Content encoding method to use, default is UTF8 (the mime type always says UTF-8)</param>
        /// <returns>A handler</returns>
        public static GenericHttpRequestHandler GetPlainTextHandler(String text, int statusCode = 200, Encoding contentEncoding = null)
        {
            contentEncoding ??= Encoding.UTF8;
            return new GenericHttpRequestHandler(statusCode, TextMime, contentEncoding.GetBytes(text));
        }

        /// <summary>
        /// A generic 404 handler
        /// </summary>
        public static readonly IHttpRequestHandler Generic404 = GetPlainTextHandler("It's a 404, blame the devs", 404);


        static ulong CacheUrl;


        const String CacheChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

        /// <summary>
        /// Get a new unique (per process) cache key, ":" followed by a counter in base 64 chars.
        /// </summary>
        /// <returns>A completed task with the key</returns>
        public static ValueTask<String> GetStaticCacheUrl()
        {
            var num = Interlocked.Increment(ref CacheUrl);
            var chars = CacheChars;
            var b = new StringBuilder(32);
            b.Append(':');
            while (num > 0)
            {
                b.Append(chars[(int)(num & 63)]);
                num >>= 6;
            }
            return ValueTask.FromResult(b.ToString());
        }


        /// <summary>
        /// Return this from <see cref="IHttpRequestHandler.GetCacheKey"/> to prevent the response from being cached.
        /// </summary>
        public const String PreventCacheKey = "";

        /// <summary>
        /// The maximum request cache duration in seconds (about 50 years, "forever").
        /// </summary>
        public const int MaxRequestCache = 60 * 60 * 24 * 366 * 50;

        /// <summary>
        /// A completed task with a null handler (same as <see cref="NullHttpRequestHandlerValueTask"/>).
        /// </summary>
        public static readonly ValueTask<IHttpRequestHandler> NullHttpRequestHandlerTask = ValueTask.FromResult<IHttpRequestHandler>(null);
        /// <summary>
        /// A completed task with a null handler (no handler found).
        /// </summary>
        public static readonly ValueTask<IHttpRequestHandler> NullHttpRequestHandlerValueTask = ValueTask.FromResult<IHttpRequestHandler>(null);


        /// <summary>
        /// An empty end point list.
        /// </summary>
        public static readonly IReadOnlyList<IHttpServerEndPoint> NoEndPoints = new List<IHttpServerEndPoint>();

        /// <summary>
        /// Return this handler when the response has already been written to the request (the server does nothing more).
        /// </summary>
        public static readonly IHttpRequestHandler AlreadyHandled = new DummyHandler();
        /// <summary>
        /// A completed task with <see cref="AlreadyHandled"/>.
        /// </summary>
        public static readonly ValueTask<IHttpRequestHandler> AlreadyHandledValueTask = ValueTask.FromResult(AlreadyHandled);

        /// <summary>
        /// The type of <see cref="AlreadyHandled"/>, all members except <see cref="IHttpRequestHandler.GetCacheKey"/> and <see cref="IHttpRequestHandler.Redirected"/> throw.
        /// </summary>
        sealed class DummyHandler : IHttpRequestHandler
        {
            /// <summary>
            /// Ignore, used internally
            /// </summary>
            public HttpServerRequest Redirected { get; set; }

            public int ClientCacheDuration => throw new NotImplementedException();
            public int RequestCacheDuration => throw new NotImplementedException();
            public HttpCompressionPriority Compression => throw new NotImplementedException();
            public ICompDecoder Decoder => throw new NotImplementedException();
            public IReadOnlyList<string> Auth => throw new NotImplementedException();
			public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;
			public HttpRequestData Get(HttpServerRequest request) => throw new NotImplementedException();
            public Task<HttpRequestData> GetAsync(HttpServerRequest request) => throw new NotImplementedException();
            public string GetEtag(out bool useAsync, HttpServerRequest request) => throw new NotImplementedException();

       
        }


        /// <summary>
        /// Take a NameValueCollection and turn it into a frozen dictionary with all keys lower-cased (null keys are skipped).
        /// Multiple values of a key are comma separated, if keys only differ in case the last one is used.
        /// </summary>
        /// <param name="q">The collection</param>
        /// <returns>The dictionary</returns>
        public static IReadOnlyDictionary<String, String> GetQueryParamsLowerKey(NameValueCollection q)
        {
            if (q.Count <= 0)
                return ReadOnlyData.EmptyDictionary<String, String>();
            var d = new Dictionary<String, String>(StringComparer.Ordinal);
            foreach (String x in q)
            {
                if (x == null)
                    continue;
                var v = q.Get(x);
                d[x.FastToLower()] = v;
            }
            return d.Freeze();
        }

        /// <summary>
        /// Make a Set-Cookie value: "name=value;Max-Age=maxAge;Path=path".
        /// Built on the stack, the only allocation is the returned string.
        /// </summary>
        /// <param name="name">The cookie name</param>
        /// <param name="value">The cookie value, inserted as is (must not contain ';' or control chars)</param>
        /// <param name="maxAge">The max age in seconds, 0 deletes the cookie</param>
        /// <param name="path">The path, optionally followed by attributes, ex: "/;HttpOnly"</param>
        /// <returns>The Set-Cookie value</returns>
        [SkipLocalsInit]
        public static String MakeCookie(String name, String value, long maxAge, String path)
        {
            var h = new DefaultInterpolatedStringHandler(16, 4, CultureInfo.InvariantCulture, stackalloc Char[256]);
            h.AppendFormatted(name);
            h.AppendLiteral("=");
            h.AppendFormatted(value);
            h.AppendLiteral(";Max-Age=");
            h.AppendFormatted(maxAge);
            h.AppendLiteral(";Path=");
            h.AppendFormatted(path);
            return h.ToStringAndClear();
        }

        /// <summary>
        /// Get the value of a cookie directly from a cookie header string, without parsing or allocating anything but the returned value.
        /// Same result as ParseCookieString(cookieHeader).TryGetValue(name, out var value).
        /// </summary>
        /// <param name="cookieHeader">The cookie header, may be null</param>
        /// <param name="name">The name of the cookie</param>
        /// <returns>The value of the cookie (the last value if the cookie is present multiple times) or null if the cookie wasn't found</returns>
        public static String GetCookie(String cookieHeader, String name)
            => CookieStringDictionary.TryGetValue(cookieHeader, name, out var value) ? value : null;


        /*
        /// <summary>
        /// Parses the cookies found in the supplied strings and add's it key values to the dictionary
        /// </summary>
        /// <param name="cookies"></param>
        /// <param name="newCookieStr"></param>
        public static void AddCookieString(Dictionary<String, String> cookies, String newCookieStr)
        {
            int start = 0;
            for (; ; )
            {
                var e = newCookieStr.IndexOf('=', start);
                if (e < 0)
                    break;
                var key = Trimmed(newCookieStr, start, e);
                start = e + 1;
                e = newCookieStr.IndexOf(';', start);
                if (e < 0)
                {
                    var value = Trimmed(newCookieStr, start, newCookieStr.Length);
                    cookies[key] = value;
                    break;
                }
                var val = Trimmed(newCookieStr, start, e);
                cookies[key] = val;
                start = e + 1;
            }
        }

        static String Trimmed(String s, int start, int end)
        {
            while (start < end)
            {
                if (!Char.IsWhiteSpace(s[start]))
                    break;
                ++start;
            }
            while (end > start)
            {
                --end;
                if (!Char.IsWhiteSpace(s[end]))
                {
                    ++end;
                    break;
                }
            }
            return s.Substring(start, end - start);
        }

        */


        //static readonly FastMemCache<String, IReadOnlyDictionary<String, String>> CookieCache = new(TimeSpan.FromMinutes(1), StringComparer.Ordinal);



        //public static IReadOnlyDictionary<String, String> ParseCookieString(String newCookieStr)
            //=> CookieCache.GetOrUpdate(newCookieStr ?? "", IntParseCookieString);

        /// <summary>
        /// Get the cookies of a cookie header as a dictionary.
        /// </summary>
        /// <param name="newCookieStr">The Cookie header, may be null</param>
        /// <returns>The cookies (the last value is used if a name occurs multiple times)</returns>
        /// <remarks>Nothing is parsed up front, looking up a cookie scans the header and only allocates the value (see <see cref="CookieStringDictionary"/>)</remarks>
        public static IReadOnlyDictionary<String, String> ParseCookieString(String newCookieStr)
            => CookieStringDictionary.Create(newCookieStr);


        /// <summary>
        /// Urls up to this length are decoded on the stack (longer urls use a pooled buffer)
        /// </summary>
        const int MaxStackDecodeChars = 512;

        /// <summary>
        /// Url decode a value (UTF-8 percent-decoding, "+" becomes a space, "%uXXXX" is supported, invalid escapes are kept as is and invalid UTF-8 becomes U+FFFD).
        /// Allocation-free, except for the final string (only if the value needs decoding).
        /// Mimics HttpUtility.UrlDecode(string).
        /// </summary>
        /// <param name="value">The value, may be null</param>
        /// <returns>The decoded value (the same instance if nothing needed decoding), null if <paramref name="value"/> is null</returns>
        [SkipLocalsInit]
        public static string UrlDecode(string value)
        {
            if (value is null)
                return null;
            // Most values doesn't need decoding (vectorized check)
            var l = value.Length;
            if (!value.AsSpan().ContainsAny('%', '+'))
                return value;
            if (l <= MaxStackDecodeChars)
            {
                Span<char> buffer = stackalloc char[l];
                var n = UrlDecode(value, buffer, out var needsDecoding);
                return needsDecoding ? new string(buffer[..n]) : value;
            }
            var rented = ArrayPool<char>.Shared.Rent(l);
            try
            {
                var n = UrlDecode(value, rented, out var needsDecoding);
                return needsDecoding ? new string(rented, 0, n) : value;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Url decode a value into a span in a single pass (same decoding as <see cref="UrlDecode(string)"/>).
        /// The decoded value is never longer than the value.
        /// </summary>
        /// <param name="value">The value to decode</param>
        /// <param name="destination">The destination, must be at least as long as the value</param>
        /// <param name="needsDecoding">True if the value needed decoding, else the decoded value is identical to the value</param>
        /// <returns>The number of chars written to the destination</returns>
        /// <exception cref="ArgumentException">Thrown if the destination is shorter than the value</exception>
        public static int UrlDecode(ReadOnlySpan<char> value, Span<char> destination, out bool needsDecoding)
        {
            if (destination.Length < value.Length)
                throw new ArgumentException("The destination must be at least as long as the value", nameof(destination));
            var writer = new Utf8UrlDecoder(destination, dryRun: false);
            needsDecoding = false;
            Process(value, ref writer, ref needsDecoding);
            return writer.Length;
        }

        /// <summary>
        /// Chars that are copied as is (all ASCII except the ones that needs decoding)
        /// </summary>
        static readonly SearchValues<char> PlainUrlChars = SearchValues.Create(
            Enumerable.Range(0, 128).Select(x => (char)x).Where(x => (x != '%') && (x != '+')).ToArray());

        static void Process(ReadOnlySpan<char> source, ref Utf8UrlDecoder decoder, ref bool needsDecoding)
        {
            int i = 0;
            int length = source.Length;
            var plain = PlainUrlChars;

            while (i < length)
            {
                // Copy runs of plain ASCII chars at once (vectorized search)
                var run = source[i..].IndexOfAnyExcept(plain);
                if (run != 0)
                {
                    if (run < 0)
                        run = length - i;
                    decoder.AddAsciiRun(source.Slice(i, run));
                    i += run;
                    if (i >= length)
                        break;
                }

                char c = source[i];

                // HttpUtility.UrlDecode decodes '+' to space.
                if (c == '+')
                {
                    needsDecoding = true;
                    decoder.AddByte((byte)' ');
                    i++;
                    continue;
                }

                if (c == '%')
                {
                    // ------------------------------------------------------------
                    // Legacy %uXXXX support.
                    // Remove this block if you only want strict RFC 3986 %XX.
                    // ------------------------------------------------------------
                    if (i + 6 <= length)
                    {
                        char maybeU = source[i + 1];

                        if (maybeU == 'u' || maybeU == 'U')
                        {
                            int h1 = HexToInt(source[i + 2]);
                            int h2 = HexToInt(source[i + 3]);
                            int h3 = HexToInt(source[i + 4]);
                            int h4 = HexToInt(source[i + 5]);

                            if (h1 >= 0 && h2 >= 0 && h3 >= 0 && h4 >= 0)
                            {
                                needsDecoding = true;

                                int codeUnit = (h1 << 12) | (h2 << 8) | (h3 << 4) | h4;
                                decoder.AddUtf16CodeUnit((char)codeUnit);

                                i += 6;
                                continue;
                            }
                        }
                    }

                    // ------------------------------------------------------------
                    // Normal %XX percent-encoding.
                    // ------------------------------------------------------------
                    if (i + 3 <= length)
                    {
                        int hi = HexToInt(source[i + 1]);
                        int lo = HexToInt(source[i + 2]);

                        if (hi >= 0 && lo >= 0)
                        {
                            needsDecoding = true;

                            byte b = (byte)((hi << 4) | lo);
                            decoder.AddByte(b);

                            i += 3;
                            continue;
                        }
                    }

                    // Invalid percent escape: keep the '%' literally and continue
                    // with the next character, similar to HttpUtility.UrlDecode.
                    decoder.AddByte((byte)'%');
                    i++;
                    continue;
                }

                if (c < 0x80)
                {
                    // ASCII characters are treated as decoded bytes.
                    decoder.AddByte((byte)c);
                }
                else
                {
                    // Non-ASCII literal UTF-16 code units are passed through,
                    // after flushing any incomplete UTF-8 percent-encoded sequence.
                    decoder.AddUtf16CodeUnit(c);
                }

                i++;
            }

            decoder.Flush();
        }

        static int HexToInt(char c)
        {
            if ((uint)(c - '0') <= 9u)
                return c - '0';

            if ((uint)(c - 'a') <= 5u)
                return c - 'a' + 10;

            if ((uint)(c - 'A') <= 5u)
                return c - 'A' + 10;

            return -1;
        }

        /// <summary>
        /// Incremental UTF-8 decoder/output writer.
        /// In dry-run mode it only counts UTF-16 chars.
        /// In write mode it writes into the destination span.
        /// </summary>
        ref struct Utf8UrlDecoder
        {
            readonly Span<char> _output;
            readonly bool _dryRun;

            // In dry-run mode: number of UTF-16 chars that would be produced.
            // In write mode: current write index.
            int _pos;

            // UTF-8 sequence state.
            int _remaining;
            uint _value;
            uint _minimum;

            public Utf8UrlDecoder(Span<char> output, bool dryRun)
            {
                _output = output;
                _dryRun = dryRun;
                _pos = 0;

                _remaining = 0;
                _value = 0;
                _minimum = 0;
            }

            public int Length => _pos;

            public void AddByte(byte b)
            {
                while (true)
                {
                    if (_remaining == 0)
                    {
                        if (b < 0x80)
                        {
                            EmitScalar(b);
                            return;
                        }

                        if ((b & 0xE0) == 0xC0)
                        {
                            StartSequence(totalBytes: 2, initialValue: (uint)(b & 0x1F), minimumValue: 0x80u);
                            return;
                        }

                        if ((b & 0xF0) == 0xE0)
                        {
                            StartSequence(totalBytes: 3, initialValue: (uint)(b & 0x0F), minimumValue: 0x800u);
                            return;
                        }

                        if ((b & 0xF8) == 0xF0)
                        {
                            StartSequence(totalBytes: 4, initialValue: (uint)(b & 0x07), minimumValue: 0x10000u);
                            return;
                        }

                        // Invalid lead byte.
                        EmitReplacement();
                        return;
                    }

                    if ((b & 0xC0) != 0x80)
                    {
                        // Invalid continuation byte.
                        // Replace the partial sequence, then retry current byte as a new lead byte.
                        EmitReplacement();
                        Reset();
                        continue;
                    }

                    _value = (_value << 6) | (uint)(b & 0x3F);
                    _remaining--;

                    if (_remaining != 0)
                        return;

                    uint scalar = _value;
                    uint minimum = _minimum;
                    Reset();

                    // Reject overlong sequences, UTF-16 surrogates, and out-of-range scalars.
                    if (scalar < minimum || scalar > 0x10FFFFu || (scalar >= 0xD800u && scalar <= 0xDFFFu))
                    {
                        EmitReplacement();
                    }
                    else
                    {
                        EmitScalar(scalar);
                    }

                    return;
                }
            }

            /// <summary>
            /// Add plain ASCII chars (no '%' or '+'), same as calling AddByte for each char
            /// </summary>
            public void AddAsciiRun(ReadOnlySpan<char> run)
            {
                if (_remaining != 0)
                {
                    // An incomplete UTF-8 sequence, handle the bytes one by one (invalid continuation bytes etc)
                    foreach (var c in run)
                        AddByte((byte)c);
                    return;
                }
                if (!_dryRun)
                    run.CopyTo(_output[_pos..]);
                _pos += run.Length;
            }

            public void AddUtf16CodeUnit(char c)
            {
                Flush();
                EmitChar(c);
            }

            public void Flush()
            {
                if (_remaining != 0)
                {
                    EmitReplacement();
                    Reset();
                }
            }

            void StartSequence(int totalBytes, uint initialValue, uint minimumValue)
            {
                _remaining = totalBytes - 1;
                _value = initialValue;
                _minimum = minimumValue;
            }

            void Reset()
            {
                _remaining = 0;
                _value = 0;
                _minimum = 0;
            }

            void EmitScalar(uint scalar)
            {
                if (scalar <= 0xFFFFu)
                {
                    EmitChar((char)scalar);
                    return;
                }

                // Supplementary Unicode scalar: emit UTF-16 surrogate pair.
                uint v = scalar - 0x10000u;

                EmitChar((char)(0xD800u + (v >> 10)));
                EmitChar((char)(0xDC00u + (v & 0x3FFu)));
            }

            void EmitReplacement() => EmitChar('\uFFFD');

            void EmitChar(char c)
            {
                if (!_dryRun)
                {
                    Debug.Assert((uint)_pos < (uint)_output.Length);
                    _output[_pos] = c;
                }

                _pos++;
            }
        }
    





}



}
