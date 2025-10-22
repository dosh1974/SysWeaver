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
            return new GenericHttpRequestHandler(statusCode, TextMime, contentEncoding.GetBytes(text), contentEncoding);
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

        sealed class DummyHandler : IHttpRequestHandler
        {
            /// <summary>
            /// Ignore, used internally
            /// </summary>
            public HttpServerRequest Redirected { get; set; }

            public int ClientCacheDuration => throw new NotImplementedException();
            public int RequestCacheDuration => throw new NotImplementedException();
            public bool UseStream => throw new NotImplementedException();
            public HttpCompressionPriority Compression => throw new NotImplementedException();
            public ICompDecoder Decoder => throw new NotImplementedException();
            public IReadOnlyList<string> Auth => throw new NotImplementedException();
            public ValueTask<string> GetCacheKey(HttpServerRequest request) => throw new NotImplementedException();
            public ReadOnlyMemory<byte> GetData(HttpServerRequest request) => throw new NotImplementedException();
            public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request) => throw new NotImplementedException();
            public string GetEtag(out bool useAsync, HttpServerRequest request) => throw new NotImplementedException();
            public Stream GetStream(HttpServerRequest request) => throw new NotImplementedException();
            public Task<Stream> GetStreamAsync(HttpServerRequest request) => throw new NotImplementedException();

       
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



    }



}
