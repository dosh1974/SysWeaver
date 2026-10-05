using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.Translation;

namespace SysWeaver.Net
{

    public enum HttpServerMethods
    {
        GET,
        POST,
        HEAD,
        Other,
    }


    public abstract class HttpServerRequest : ITranslationContext, IDisposable
    {

        public virtual void Dispose()
        {
            var c = Custom as IDisposable;
            if (c != null)
                c.Dispose();
        }

        public override string ToString() => Url;

        /// <summary>
        /// The http method used
        /// </summary>
        public readonly String Method;

        /// <summary>
        /// The http method used as an enum
        /// </summary>
        public readonly HttpServerMethods HttpMethod;

        /// <summary>
        /// True if this is a HEAD request
        /// </summary>
        public readonly bool IsHead;

        /// <summary>
        /// The absoulte url, prefer this over Uri.AbsolutePath since it has some overhead
        /// </summary>
        public readonly String Url;
        /// <summary>
        /// The prefix used (one of the listening prefixes), can be used for different behaviours
        /// </summary>
        public readonly String Prefix;
        /// <summary>
        /// The local url after stripping the prefix and query paramaters.
        /// </summary>
        public readonly String LocalUrl;
        /// <summary>
        /// The server instance
        /// </summary>
        public readonly HttpServerBase Server;
        
        
        /// <summary>
        /// The If-None-Match header value
        /// </summary>
        public abstract String IfNoneMatch { get; }
        
        /// <summary>
        /// The compression header
        /// </summary>
        public abstract String AcceptEncoding { get; }


        /// <summary>
        /// The accepted encoders
        /// </summary>
        public IReadOnlySet<String> AcceptedEncoders
        {
            get
            {
                var l = LazyAcceptedEncoders;
                if (l != null)
                    return l;
                l = HttpCompressionPriority.GetAcceptedEncoders(AcceptEncoding);
                LazyAcceptedEncoders = l;
                return l;
            }
        }

        IReadOnlySet<String> LazyAcceptedEncoders;



        /// <summary>
        /// The index in to the url string where the first query value is located, or 0 if there are no query parameters
        /// </summary>
        public readonly int QueryStringStart;


        /// <summary>
        /// Get the query string (everything after ?) or the supplied default if no no query is present.
        /// </summary>
        /// <returns></returns>
        public virtual String GetQuery(String def = "")
        {
            var i = QueryStringStart;
            if (i <= 0)
                return def;
            return Url.Substring(i);
        }

        /// <summary>
        /// Get the raw query string (everything after ?) or the supplied default if no no query is present.
        /// </summary>
        /// <returns></returns>
        public virtual String GetRawQuery(String def = "") => GetQuery(def);

        /// <summary>
        /// The compression encoder and level to use, set before calling WriteStream or GetData
        /// </summary>
        public Tuple<ICompEncoder, CompEncoderLevels> CompEncoder;

        /// <summary>
        /// Session, may be null
        /// </summary>
        public HttpSession Session { get; private set; }

        /// <summary>
        /// Host information
        /// </summary>
        public readonly HttpServerHostInfo Host;


        /// <summary>
        /// Map a http method to the enum (ordinal, a switch is a length check and a few char compares, no hashing)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static HttpServerMethods GetHttpMethod(String httpMethod) => httpMethod switch
        {
            "GET" => HttpServerMethods.GET,
            "POST" => HttpServerMethods.POST,
            "HEAD" => HttpServerMethods.HEAD,
            _ => HttpServerMethods.Other,
        };

        /*
         
        /// <summary>
        /// Local urls are cached (most requests are for a limited set of urls), so that the same local url doesn't allocate a new string for every request
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, String> LocalUrlCache = new LowAllocConcurrentDictionary<String, String>(MaxCachedLocalUrls);
        static readonly LowAllocConcurrentDictionary<String, String>.AlternateLookup<ReadOnlySpan<Char>> LocalUrlLookup = LocalUrlCache.GetAlternateLookup<ReadOnlySpan<Char>>();
        static int LocalUrlCacheCount;

        /// <summary>
        /// The max number of cached local urls (a bound, since the urls are controlled by the clients)
        /// </summary>
        const int MaxCachedLocalUrls = 4096;

        /// <summary>
        /// Longer local urls are never cached
        /// </summary>
        const int MaxCachedLocalUrlLength = 256;

        /// <summary>
        /// Get a part of the url, a cached string if the same local url has been seen before (same result as Substring)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static String GetLocalUrl(String url, int start, int length)
        {
            if (((uint)start > (uint)url.Length) || ((uint)length > (uint)(url.Length - start)))
                return url.Substring(start, length);
            if ((start == 0) && (length == url.Length))
                return url;
            if (length == 0)
                return String.Empty;
            var s = url.AsSpan(start, length);
            if (length > MaxCachedLocalUrlLength)
                return new String(s);
            if (LocalUrlLookup.TryGetValue(s, out var cached))
                return cached;
            var n = new String(s);
            //  When the cache is full it's cleared, so that it adapts to the urls in use (and can't be filled with junk urls forever)
            if (Volatile.Read(ref LocalUrlCacheCount) >= MaxCachedLocalUrls)
            {
                LocalUrlCache.Clear();
                Volatile.Write(ref LocalUrlCacheCount, 0);
            }
            if (LocalUrlCache.TryAdd(n, n))
                Interlocked.Increment(ref LocalUrlCacheCount);
            return n;
        }
        */

        /// <summary>
        /// True if "index.html" was added automatically
        /// </summary>
        public readonly bool DidIndex;


        /// <summary>
        /// The raw url, uri encoded, zero processing from input.
        /// </summary>
        public readonly String RawUrl;

        protected HttpServerRequest(String httpMethod, String rawUrl, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart, bool didIndex)
        {
            RawUrl = rawUrl;
            DidIndex = didIndex;
            Method = httpMethod;
            var m = GetHttpMethod(httpMethod);
            HttpMethod = m;
            IsHead = m == HttpServerMethods.HEAD;
            Url = url;
            Prefix = prefix;
            var pl = prefix.Length;
            QueryStringStart = queryStart + 1;
            var l = (queryStart < 0 ? url.Length : queryStart) - pl;
            //LocalUrl = GetLocalUrl(url, pl, l); // Use string cache
            LocalUrl = url.Substring(pl, l);
            Server = server;
            Host = host;
        }
       

        /// <summary>
        /// Get all request headers
        /// </summary>
        public abstract IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders { get; }


        /// <summary>
        /// Get all response headers
        /// </summary>
        public abstract IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders { get; }


        public void Init(HttpSession session)
        {
            Session = session;
        }

        /// <summary>
        /// Custom data
        /// </summary>
        public Object Custom;


        NameValueCollection IQP;
        IReadOnlyDictionary<String, String> IqpL;

        /// <summary>
        /// Parse and return query paramaters.
        /// URL encoded characters in the values are decoded.
        /// </summary>
        public NameValueCollection QueryParameters => IQP ?? (IQP = GetQueryParameters());

        NameValueCollection GetQueryParameters()
        {
            var u = RawUrl;
            var l = u.IndexOf('?');
            if (l < 0)
                return new NameValueCollection();
            return HttpUtility.ParseQueryString(u.Substring(l + 1));
        }

        /// <summary>
        /// Parse and return query parameter dictionary (keys are all lowercase), only the last value is set if multiple keys are found.
        /// URL encoded characters in the values are decoded.
        /// </summary>
        public IReadOnlyDictionary<String, String> QueryParamsLowercase => IqpL ?? (IqpL = HttpServerTools.GetQueryParamsLowerKey(QueryParameters));



        /// <summary>
        /// Throw an exception if the client have been lost
        /// </summary>
        /// <exception cref="HttpListenerException"></exception>
        public void ThrowIfDead()
        {
            if (IsDead())
                throw new HttpListenerException(1, "Client connection lost!");
        }


        public abstract Stream InputStream { get; }
        public abstract Stream OutputStream { get; }

        public abstract long ReqContentLength { get; }
        public abstract String GetReqHeader(String name);
        public abstract String GetResHeader(String name);

        public abstract String ProtocolVersion { get; } 

        public abstract void SetResMime(String mime);

        public abstract String GetResMime();

        public abstract void SetResContentLength(long length);
        public abstract void SetResStatusCode(int statusCode);

        public abstract int GetResStatusCode();

        public abstract void SetResHeader(String header, String value);
        
        /// <summary>
        /// Prefer the Byte[] overload if possible, ASP net stream DO not implement the Span and Memory version as of now!
        /// TODO: Check if .NET11 fixes this! If so revert code that uses workarounds!
        /// </summary>
        /// <param name="data"></param>
        public abstract void SetResBody(ReadOnlySpan<Byte> data);

        /// <summary>
        /// Prefer the Byte[] overload if possible, ASP net stream DO not implement the Span and Memory version as of now!
        /// TODO: Check if .NET11 fixes this! If so revert code that uses workarounds!
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public abstract Task SetResBodyAsync(ReadOnlyMemory<Byte> data);

        public abstract void SetResBody(Byte[] data, int offset, int length);
        public abstract Task SetResBodyAsync(Byte[] data, int offset, int length);


        public abstract bool IsDead();

        public abstract String GetReqCookie(String name, String cookieString = null);

        public abstract void UpdateCookie(String str);

        /// <summary>
        ///  Get the IP of the current client connection (closest to the server)
        /// </summary>
        /// <returns></returns>
        public abstract IPAddress GetIP();

        /// <summary>
        /// Get the resolved client IP address (before any proxies)
        /// </summary>
        /// <returns></returns>
        public String GetIpAddress()
        {
            // TODO: Use "Forwarded" (https://datatracker.ietf.org/doc/html/rfc7239) and "X-Forwarded-For" (https://en.wikipedia.org/wiki/X-Forwarded-For) from trusted proxies.
            // The headers were read but not used, the lookups are removed until implemented.
            var ip = GetIP()?.ToString();
            if (ip == null)
                return "?";
            bool isV6 = ip.StartsWith('[');
            if (isV6)
            {
                var e = ip.IndexOf(']');
                ip = e < 0 ? ip.Substring(1) : ip.Substring(1, e - 1);
            }
            else
            {
                var t = ip.LastIndexOf(':');
                if (t >= 0)
                {

                    if ((t == 0) || (ip[t - 1] != ':'))
                        ip = ip.Substring(0, t);
                }
            }
            return ip;
        }


        public void SetResText(String text, String mime = "text/plain; charset=UTF-8")
        {
            var tl = text.Length;
            var al = tl << 2;
            SetResMime(mime);
            /*
            if (tl < 1024)
            {
                Span<Byte> tt = stackalloc Byte[al];
                if (Encoding.UTF8.TryGetBytes(text, tt, out var wl))
                {
                    SetResBody(tt.Slice(0, wl));
                    return;
                }
            }
            */
            var buf = ArrayPoolStream.Rent(al);
            try
            {
                var t = buf.AsSpan();
                if (Encoding.UTF8.TryGetBytes(text, t, out var l))
                {
                    //SetResBody(t.Slice(0, l));
                    SetResBody(buf, 0, l);
                    return;
                }
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }

        protected static readonly IReadOnlySet<String> DefaultIgnoreHeaders = ReadOnlyData.Set("Set-Cookie");

        public abstract void SetResHeaders(int status, IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> headers, IReadOnlySet<String> ignore = null);

        protected void OnDispose()
        {
            Interlocked.Exchange(ref Ts, null)?.Dispose();
        }

        public CancellationToken GetRequestCancellationToken()
        {
            var ts = Ts;
            if (ts != null)
                return ts.Token;
            lock (this)
            {
                ts = Ts;
                if (ts != null)
                    return ts.Token;
                ts = new PeriodicCancellationTokenSource(IsDead, 250);
                Ts = ts;
            }
            return ts.Token;
        }

        PeriodicCancellationTokenSource Ts;



        volatile ConcurrentDictionary<String, Object> InternalCustomData;

        /// <summary>
        /// A dictionary that can be used for storing request data (to pass between functions etc)
        /// </summary>
        public ConcurrentDictionary<String, Object> Properties
        {
            get
            {
                var t = InternalCustomData;
                if (t != null)
                    return t;
                lock (this)
                {
                    t = InternalCustomData;
                    if (t != null)
                        return t;
                    t = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
                    InternalCustomData = t;
                }
                return t;
            }
        }


        public String GetLocalUrl()
        {
            var l = LocalUrl;
            var t = l.LastIndexOf('/');
            return t < 0 ? "" : l.Substring(0, t);
        }



        /// <summary>
        /// Make any relative path an absolute path
        /// </summary>
        /// <param name="path">The relative path</param>
        /// <returns></returns>
        public String MakeAbsolute(String path)
        {
            if (path.FastIndexOf("://") >= 0)
                return path;
            var l = GetLocalUrl();
            if (l.Length < 0)
                return HttpServerTools.CleanupPaths(Prefix + path);
            return HttpServerTools.CleanupPaths(String.Concat(Prefix, l, '/', path));
        }


        /// <summary>
        /// If the url is relative, make it absoulte (with respect to the current request URL).
        /// </summary>
        /// <param name="url">An absolute or relative url</param>
        /// <returns>An absolute url</returns>
        public String MakeRequestAbsolute(String url)
        {
            if (url.FastIndexOf("://") > 0)
                return url;
            var b = Url.Split('/');
            int i = b.Length - 1;
            var a = url.Split('/');
            var al = a.Length;
            int j = 0;
            for (; j < al; ++j)
            {
                var p = a[j];
                if (p.FastEquals(".."))
                {
                    --i;
                    continue;
                }
                if (p.FastEquals("."))
                    continue;
                break;
            }
            return String.Join('/', String.Join('/', b, 0, i), String.Join('/', a, j, al - j));
        }


        /// <summary>
        /// Custom per request data can be added here
        /// </summary>
        public ConcurrentDictionary<String, Object> Data
        {
            get
            {
                var d = InternalData;
                if (d != null) 
                    return d;    
                lock (this)
                {
                    d = InternalData;
                    if (d != null)
                        return d;
                    d = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
                    InternalData = d;
                    return d;
                }
            }
        }

        ConcurrentDictionary<String, Object> InternalData;

        #region Data References

        /// <summary>
        /// Add a data table to some storage and get a reference to it
        /// </summary>
        /// <param name="scope">The scope of the availability of this data</param>
        /// <param name="data">The table data to add</param>
        /// <param name="lifeTimeInSeconds">The life time of this data in seconds (will be removed after this many seconds)</param>
        /// <returns>A reference to the table (meta data)</returns>
        public TableDataReference AddData(DataScopes scope, BaseTableData data, int lifeTimeInSeconds = 5 * 60)
            => Server.AddData(this, scope, data, lifeTimeInSeconds);

        /// <summary>
        /// Get the reference to a data table from a given id
        /// </summary>
        /// <param name="dataRefId">The id of the data</param>
        /// <returns></returns>
        public TableDataReference GetTableData(String dataRefId)
            => Server.GetTableData(this, dataRefId);

        public BaseTableData ResolveTableData(String dataRefId)
            => Server.GetTableData(this, dataRefId)?.Get();


        #endregion//Data References

        /// <summary>
        /// Get the translator to use if any translation is to be done (null if no translation is requested)
        /// </summary>
        public ITranslator Translator => Server.Translator;

        /// <summary>
        /// The language to user
        /// </summary>
        public String Language => Session?.Language ?? "en";

        /// <summary>
        /// Additional etag data
        /// </summary>
        public String Etag;

        /// <summary>
        /// Optional per request client cache override.
        /// The duration in seconds that the client should keep the response cached (basically setting up the Cache header in the response)
        /// </summary>
        public int? ClientCacheDuration;

        /// <summary>
        /// Optional per request server cache override.
        /// The duration in seconds that the same request should be cached on the server, i.e the WriteStream / GetData for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).
        /// If negative, the per session cache is used (else a global cache is used).
        /// </summary>
        public int? RequestCacheDuration;


        /// <summary>
        /// Replace the url (used internally)
        /// </summary>
        /// <returns></returns>
        public abstract HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null);

    }

}
