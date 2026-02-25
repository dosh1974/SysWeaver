using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Net
{
    public static class HttpServerTools
    {
        /// <summary>
        /// Create a time stamp string from a DateTime
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static String ToEtag(DateTime t) => CompactAsciiString.Secure.Encode((t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime()).Ticks);
        //        public static String ToTimeStampString(DateTime t) => (t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime()).ToString("r");



        /// <summary>
        /// Decode a time stamp string
        /// </summary>
        /// <param name="lm"></param>
        /// <returns></returns>
        public static DateTime FromTimeStampString(String lm)
        {
            var l = CompactAsciiString.Secure.DecodeInt64(lm.SplitFirst(' '));
            return new DateTime(l, DateTimeKind.Utc);
        }
            
        /// <summary>
        /// The text to write to the last modfied response header for static responses
        /// </summary>
        public static readonly String StartedText = ToEtag(EnvInfo.AppStart);


        public static readonly DateTime StartedTime = EnvInfo.AppStart;


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

        public static readonly Task<String> NullStringTask = Task.FromResult<String>(null);
        public static readonly ValueTask<String> NullStringValueTask = ValueTask.FromResult<String>(null);

        public static readonly Task<IHttpRequestHandler> NullHttpRequestHandlerTask = Task.FromResult<IHttpRequestHandler>(null);
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
            public ValueTask<string> GetCacheKey(HttpServerRequest request) => throw new NotImplementedException();
            public HttpRequestData Get(HttpServerRequest request) => throw new NotImplementedException();
            public ValueTask<HttpRequestData> GetAsync(HttpServerRequest request) => throw new NotImplementedException();
            public string GetEtag(out bool useAsync, HttpServerRequest request) => throw new NotImplementedException();

       
        }


        /// <summary>
        /// Take a NameValueCollection and turn it into a dictionary with all keys lower-cased
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        public static IReadOnlyDictionary<String, String> GetQueryParamsLowerKey(NameValueCollection q)
        {
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





    }



}
