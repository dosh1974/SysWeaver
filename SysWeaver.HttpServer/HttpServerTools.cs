using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlTypes;
using System.Diagnostics;
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
    public static class HttpServerTools
    {
        public const String DefPath = "/;HttpOnly";

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
        /// Escape non-ascii using \uXXXX
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
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
        /// Get the etag for an assembly (based last write time)
        /// </summary>
        /// <param name="asm"></param>
        /// <returns>The etag or null if it can't be found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String GetAssemblyEtag(Assembly asm)
            => ToEtag(asm.GetLastWriteTimerUtc());

        /// <summary>
        /// Create an etag from a DateTime
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToEtag(DateTime t) => CompactAsciiString.Secure.Encode((t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime()).Ticks);

        /// <summary>
        /// Create an etag from a long
        /// </summary>
        /// <param name="l"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToEtag(long l) => CompactAsciiString.Secure.Encode(l);


        /// <summary>
        /// Create an etag from some data (using a hash)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
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
        /// Decode a time stamp string
        /// </summary>
        /// <param name="lm"></param>
        /// <returns></returns>
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
        /// Remove things like "../" in paths, ex "Api/../Test/Func" becomes "Test/Func"
        /// </summary>
        /// <param name="p">A path</param>
        /// <returns>A cleaned up path</returns>
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
        /// Merges url paths, ignoring empty parts
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



        public const String TextMimeSuffix = "; charset=UTF-8";

        public const String TextMime = "text/plain" + TextMimeSuffix;
        public const String JsonMime = "application/json" + TextMimeSuffix;
        public const String HtmlMime = "text/html" + TextMimeSuffix;
        public const String SvgMime = "image/svg+xml" + TextMimeSuffix;


        /// <summary>
        /// Get a plain text handler
        /// </summary>
        /// <param name="text">The text to repsond with</param>
        /// <param name="statusCode">The status code to use</param>
        /// <param name="contentEncoding">Content encoding methof to use, default is UTF8</param>
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


        public const String PreventCacheKey = "";

        public const int MaxRequestCache = 60 * 60 * 24 * 366 * 50;

        public static readonly ValueTask<IHttpRequestHandler> NullHttpRequestHandlerTask = ValueTask.FromResult<IHttpRequestHandler>(null);
        public static readonly ValueTask<IHttpRequestHandler> NullHttpRequestHandlerValueTask = ValueTask.FromResult<IHttpRequestHandler>(null);


        public static readonly IReadOnlyList<IHttpServerEndPoint> NoEndPoints = new List<IHttpServerEndPoint>();

        public static readonly IHttpRequestHandler AlreadyHandled = new DummyHandler();
        public static readonly ValueTask<IHttpRequestHandler> AlreadyHandledValueTask = ValueTask.FromResult(AlreadyHandled);

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
        /// Take a NameValueCollection and turn it into a dictionary with all keys lower-cased
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String MakeCookie(String name, String value, long maxAge, String path)
        {
            return String.Concat(
                name,
                '=',
                value,
                ";Max-Age=",
                maxAge,
                ";Path=",
                path);
        }


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



        /// <summary>
        /// Parses the cookies found in the supplied strings and created a dictionary
        /// </summary>
        /// <param name="newCookieStr"></param>
        //public static IReadOnlyDictionary<String, String> ParseCookieString(String newCookieStr)
            //=> CookieCache.GetOrUpdate(newCookieStr ?? "", IntParseCookieString);

        public static unsafe IReadOnlyDictionary<String, String> ParseCookieString(String newCookieStr)
        {
            var cookies = new Dictionary<String, String>(StringComparer.Ordinal);
            var sp = newCookieStr.AsSpan();
            fixed (Char* s = sp)
            {
                var start = s;
                var end = s + sp.Length;
                for (; ; )
                {
                    var e = CharPtrTools.IndexOf('=', start, end);
                    if (e == null)
                        break;
                    var key = CharPtrTools.ToTrimmedString(start, e);
                    start = e + 1;
                    e = CharPtrTools.IndexOf(';', start, end);
                    if (e == null)
                    {
                        var value = CharPtrTools.ToTrimmedString(start, end);
                        cookies[key] = value;
                        break;
                    }
                    var val = CharPtrTools.ToTrimmedString(start, e);
                    cookies[key] = val;
                    start = e + 1;
                }
            }
            return cookies.Freeze();
        }


        // Cached delegate: allocated once per type load, not per UrlDecode call.
        static readonly SpanAction<char, string> s_urlDecodeAction = UrlDecodeToSpan;

        /// <summary>
        /// Allocation-free, except for the final string created by string.Create.
        /// Mimics HttpUtility.UrlDecode(string) using UTF-8 percent-decoding.
        /// </summary>
        public static string UrlDecode(string value)
        {
            if (value is null)
                return null;

            if (value.Length == 0)
                return value;

            // ------------------------------------------------------------------
            // First pass:
            // Dry run. No destination span. Only counts the decoded UTF-16 length
            // and determines whether any decoding is actually needed.
            // ------------------------------------------------------------------
            var counter = new Utf8UrlDecoder(Span<char>.Empty, dryRun: true);
            bool needsDecoding = false;

            Process(value, ref counter, ref needsDecoding);

            int decodedLength = counter.Length;

            if (!needsDecoding)
            {
                Debug.Assert(decodedLength == value.Length);
                return value;
            }

            // ------------------------------------------------------------------
            // Second pass:
            // Allocate only the resulting string and decode into its span.
            // ------------------------------------------------------------------
            return string.Create(decodedLength, value, s_urlDecodeAction);
        }

        static void UrlDecodeToSpan(Span<char> destination, string source)
        {
            var writer = new Utf8UrlDecoder(destination, dryRun: false);
            bool unused = false;

            Process(source, ref writer, ref unused);

            Debug.Assert(writer.Length == destination.Length);
        }

        static void Process(string source, ref Utf8UrlDecoder decoder, ref bool needsDecoding)
        {
            int i = 0;
            int length = source.Length;

            while (i < length)
            {
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
