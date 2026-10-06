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

    /// <summary>
    /// The http methods that the server distinguishes.
    /// </summary>
    public enum HttpServerMethods
    {
        /// <summary>
        /// A GET request.
        /// </summary>
        GET,
        /// <summary>
        /// A POST request.
        /// </summary>
        POST,
        /// <summary>
        /// A HEAD request (no response body is sent).
        /// </summary>
        HEAD,
        /// <summary>
        /// Any other method (see <see cref="HttpServerRequest.Method"/> for the actual method).
        /// </summary>
        Other,
    }


    /// <summary>
    /// A request being handled by an <see cref="HttpServerBase"/>, an abstraction over the listener's request and response (HttpListener, Kestrel or in-memory).
    /// </summary>
    /// <remarks>
    /// <see cref="Url"/> and <see cref="LocalUrl"/> are url decoded (path and query), <see cref="RawUrl"/> is the url as received from the listener.
    /// Not thread safe unless stated, a request is normally handled by a single logical flow.
    /// </remarks>
    public abstract class HttpServerRequest : ITranslationContext, IDisposable
    {

        /// <summary>
        /// Dispose <see cref="Custom"/> if it's disposable.
        /// </summary>
        public virtual void Dispose()
        {
            var c = Custom as IDisposable;
            if (c != null)
                c.Dispose();
        }

        /// <inheritdoc/>
        public override string ToString() => Url;

        /// <summary>
        /// The http method used, ex: "GET".
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
        /// The absolute url (url decoded, with "index.html" inserted for directory requests), ex: "https://host/folder/index.html?x=1".
        /// </summary>
        public readonly String Url;
        /// <summary>
        /// The prefix used (one of the listening prefixes with any wildcard replaced by the requested host name), ex: "https://host/".
        /// For wildcard prefixes the host part comes from the client (the Host header), so don't trust it for building links outside of the request.
        /// </summary>
        public readonly String Prefix;
        /// <summary>
        /// The local url after stripping the prefix and query parameters (url decoded), ex: "folder/index.html".
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
        /// The accepted encoders (lower cased encoding names from the Accept-Encoding header, computed on first use).
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
        /// The index in to the <see cref="Url"/> string where the first query char is located (after the '?'), or 0 if there are no query parameters.
        /// </summary>
        public readonly int QueryStringStart;


        /// <summary>
        /// Get the query string (everything after '?', url decoded) or the supplied default if no query is present.
        /// </summary>
        /// <param name="def">The value to return if there is no query string</param>
        /// <returns>The query string</returns>
        public virtual String GetQuery(String def = "")
        {
            var i = QueryStringStart;
            if (i <= 0)
                return def;
            return Url.Substring(i);
        }

        /// <summary>
        /// Get the raw query string (everything after '?') or the supplied default if no query is present.
        /// The default implementation returns the decoded query (same as <see cref="GetQuery"/>).
        /// </summary>
        /// <param name="def">The value to return if there is no query string</param>
        /// <returns>The query string</returns>
        public virtual String GetRawQuery(String def = "") => GetQuery(def);

        /// <summary>
        /// The compression encoder and level selected for the response (set by the server before the handler data is read), null if not compressed.
        /// </summary>
        public Tuple<ICompEncoder, CompEncoderLevels> CompEncoder;

        /// <summary>
        /// The session, set by the server (see <see cref="Init"/>) before any module handler is called. May be null for raw modules and manually created requests.
        /// </summary>
        public HttpSession Session { get; private set; }

        /// <summary>
        /// Host information, may be null for manually created requests.
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
        /// The url as received from the listener (not url decoded by the server), used for parsing <see cref="QueryParameters"/>.
        /// </summary>
        public readonly String RawUrl;

        /// <summary>
        /// Create a request.
        /// </summary>
        /// <param name="httpMethod">The http method, ex: "GET"</param>
        /// <param name="rawUrl">The url as received from the listener</param>
        /// <param name="url">The decoded url (see <see cref="HttpServerBase.GetHost"/>)</param>
        /// <param name="prefix">The prefix of the url, must not be longer than the url (path part)</param>
        /// <param name="server">The server</param>
        /// <param name="host">The host</param>
        /// <param name="queryStart">The index of the '?' in the url, -1 if there is no query string</param>
        /// <param name="didIndex">True if "index.html" was added to the url</param>
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


        /// <summary>
        /// Set the session of the request (used by the server).
        /// </summary>
        /// <param name="session">The session</param>
        public void Init(HttpSession session)
        {
            Session = session;
        }

        /// <summary>
        /// Custom data, disposed (if disposable) when the request is disposed.
        /// </summary>
        public Object Custom;


        NameValueCollection IQP;
        IReadOnlyDictionary<String, String> IqpL;

        /// <summary>
        /// Parse (on first use) and return the query parameters (from <see cref="RawUrl"/>).
        /// URL encoded characters in the names and values are decoded.
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
        /// Parse and return a query parameter dictionary (keys are all lowercase), if a key occurs multiple times the values are comma separated, if keys only differ in case the last one is used.
        /// URL encoded characters in the values are decoded.
        /// </summary>
        public IReadOnlyDictionary<String, String> QueryParamsLowercase => IqpL ?? (IqpL = HttpServerTools.GetQueryParamsLowerKey(QueryParameters));



        /// <summary>
        /// Throw an exception if the client connection has been lost.
        /// </summary>
        /// <exception cref="HttpListenerException">Thrown if <see cref="IsDead"/> returns true</exception>
        public void ThrowIfDead()
        {
            if (IsDead())
                throw new HttpListenerException(1, "Client connection lost!");
        }


        /// <summary>
        /// The request body.
        /// </summary>
        public abstract Stream InputStream { get; }
        /// <summary>
        /// The response body stream (write after the status code and headers have been set).
        /// </summary>
        public abstract Stream OutputStream { get; }

        /// <summary>
        /// The Content-Length of the request (implementation specific value if unknown).
        /// </summary>
        public abstract long ReqContentLength { get; }
        /// <summary>
        /// Get a request header.
        /// </summary>
        /// <param name="name">The header name (case insensitive)</param>
        /// <returns>The value, null if not present</returns>
        public abstract String GetReqHeader(String name);
        /// <summary>
        /// Get a response header that has been set.
        /// </summary>
        /// <param name="name">The header name (case insensitive)</param>
        /// <returns>The value, null if not set</returns>
        public abstract String GetResHeader(String name);

        /// <summary>
        /// The http protocol version of the request, ex: "1.1".
        /// </summary>
        public abstract String ProtocolVersion { get; } 

        /// <summary>
        /// Set the Content-Type of the response.
        /// </summary>
        /// <param name="mime">The mime type</param>
        public abstract void SetResMime(String mime);

        /// <summary>
        /// Get the Content-Type of the response.
        /// </summary>
        /// <returns>The mime type, null if not set</returns>
        public abstract String GetResMime();

        /// <summary>
        /// Set the Content-Length of the response.
        /// </summary>
        /// <param name="length">The length in bytes</param>
        public abstract void SetResContentLength(long length);
        /// <summary>
        /// Set the status code of the response.
        /// </summary>
        /// <param name="statusCode">The http status code</param>
        public abstract void SetResStatusCode(int statusCode);

        /// <summary>
        /// Get the status code of the response.
        /// </summary>
        /// <returns>The http status code</returns>
        public abstract int GetResStatusCode();

        /// <summary>
        /// Set (replace) a response header.
        /// </summary>
        /// <param name="header">The header name</param>
        /// <param name="value">The value (implementations may remove the header for null or empty values)</param>
        public abstract void SetResHeader(String header, String value);
        
        /// <summary>
        /// Write data to the response body.
        /// Prefer the Byte[] overload if possible, ASP.NET streams do NOT implement the Span and Memory versions efficiently as of now!
        /// TODO: Check if .NET11 fixes this! If so revert code that uses workarounds!
        /// </summary>
        /// <param name="data">The data to write</param>
        public abstract void SetResBody(ReadOnlySpan<Byte> data);

        /// <summary>
        /// Write data to the response body.
        /// Prefer the Byte[] overload if possible, ASP.NET streams do NOT implement the Span and Memory versions efficiently as of now!
        /// TODO: Check if .NET11 fixes this! If so revert code that uses workarounds!
        /// </summary>
        /// <param name="data">The data to write</param>
        /// <returns>A task that completes when the data has been written</returns>
        public abstract Task SetResBodyAsync(ReadOnlyMemory<Byte> data);

        /// <summary>
        /// Write data to the response body.
        /// </summary>
        /// <param name="data">The buffer</param>
        /// <param name="offset">The offset of the first byte to write</param>
        /// <param name="length">The number of bytes to write</param>
        public abstract void SetResBody(Byte[] data, int offset, int length);
        /// <summary>
        /// Write data to the response body.
        /// </summary>
        /// <param name="data">The buffer</param>
        /// <param name="offset">The offset of the first byte to write</param>
        /// <param name="length">The number of bytes to write</param>
        /// <returns>A task that completes when the data has been written</returns>
        public abstract Task SetResBodyAsync(Byte[] data, int offset, int length);


        /// <summary>
        /// Check if the client connection has been lost.
        /// </summary>
        /// <returns>True if the client is gone</returns>
        public abstract bool IsDead();

        /// <summary>
        /// Get a request cookie.
        /// </summary>
        /// <param name="name">The cookie name</param>
        /// <param name="cookieString">The Cookie header, if already read (avoids reading it again), else null</param>
        /// <returns>The value, null if not present</returns>
        public abstract String GetReqCookie(String name, String cookieString = null);

        /// <summary>
        /// Add a Set-Cookie header to the response.
        /// </summary>
        /// <param name="str">The Set-Cookie value (see <see cref="HttpServerTools.MakeCookie(String, String, DateTime, String)"/>)</param>
        public abstract void UpdateCookie(String str);

        /// <summary>
        /// Get the IP of the current client connection (closest to the server, proxies are not resolved).
        /// </summary>
        /// <returns>The address</returns>
        public abstract IPAddress GetIP();

        /// <summary>
        /// Get the client IP address as text, "?" if unknown.
        /// Forwarded / X-Forwarded-For headers are not used (yet), so this is the address of the closest peer.
        /// </summary>
        /// <returns>The address as text</returns>
        /// <remarks>IPv4 addresses mapped to IPv6 (ex: "::ffff:192.168.1.5") are returned as IPv4 addresses (ex: "192.168.1.5").</remarks>
        public String GetIpAddress()
        {
            // TODO: Use "Forwarded" (https://datatracker.ietf.org/doc/html/rfc7239) and "X-Forwarded-For" (https://en.wikipedia.org/wiki/X-Forwarded-For) from trusted proxies.
            // The headers were read but not used, the lookups are removed until implemented.
            var ip = GetIP();
            if (ip == null)
                return "?";
            return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
        }


        /// <summary>
        /// Set the response mime type and write a text as UTF-8 to the response body.
        /// </summary>
        /// <param name="text">The text, must not be null</param>
        /// <param name="mime">The mime type</param>
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

        /// <summary>
        /// Headers that <see cref="SetResHeaders"/> ignores by default (Set-Cookie, so that cached responses never replay cookies).
        /// </summary>
        protected static readonly IReadOnlySet<String> DefaultIgnoreHeaders = ReadOnlyData.Set("Set-Cookie");

        /// <summary>
        /// Set the status code and headers of the response (used when sending cached responses).
        /// </summary>
        /// <param name="status">The http status code</param>
        /// <param name="headers">The headers to set</param>
        /// <param name="ignore">Headers to skip, null for <see cref="DefaultIgnoreHeaders"/></param>
        public abstract void SetResHeaders(int status, IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> headers, IReadOnlySet<String> ignore = null);

        /// <summary>
        /// Dispose the cancellation token source (if created), implementations should call this when the request is disposed.
        /// </summary>
        protected void OnDispose()
        {
            Interlocked.Exchange(ref Ts, null)?.Dispose();
        }

        /// <summary>
        /// Get a cancellation token that is cancelled when the client connection is lost (polled every 250 ms using <see cref="IsDead"/>).
        /// Thread safe, created on first use.
        /// </summary>
        /// <returns>The cancellation token</returns>
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
        /// A dictionary that can be used for storing request data (to pass between functions etc), thread safe, created on first use.
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


        /// <summary>
        /// Get the folder part of the local url (without the trailing '/'), empty for files in the root.
        /// </summary>
        /// <returns>The local folder</returns>
        public String GetLocalUrl()
        {
            var l = LocalUrl;
            var t = l.LastIndexOf('/');
            return t < 0 ? "" : l.Substring(0, t);
        }



        /// <summary>
        /// Make a path that is relative to the folder of the local url absolute ("../" segments are resolved).
        /// Paths containing "://" are returned as is.
        /// </summary>
        /// <param name="path">The relative path</param>
        /// <returns>The absolute url</returns>
        /// <exception cref="Exception">Thrown if the path goes above the root</exception>
        /// <remarks>For requests in the root folder the result contains a double slash after the prefix (ex: "https://host//path").</remarks>
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
        /// If the url is relative, make it absolute (with respect to the current request <see cref="Url"/>).
        /// Only leading "./" and "../" segments are resolved.
        /// </summary>
        /// <param name="url">An absolute or relative url</param>
        /// <returns>An absolute url</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the url has more leading "../" segments than the request url has segments</exception>
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
        /// Custom per request data can be added here (thread safe, created on first use, separate from <see cref="Properties"/>).
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
        /// Add a data table to some storage and get a reference to it.
        /// </summary>
        /// <param name="scope">The scope of the availability of this data</param>
        /// <param name="data">The table data to add</param>
        /// <param name="lifeTimeInSeconds">The life time of this data in seconds (will be removed after this many seconds)</param>
        /// <returns>A reference to the table (meta data)</returns>
        public TableDataReference AddData(DataScopes scope, BaseTableData data, int lifeTimeInSeconds = 5 * 60)
            => Server.AddData(this, scope, data, lifeTimeInSeconds);

        /// <summary>
        /// Get the reference to a data table from a given id.
        /// </summary>
        /// <param name="dataRefId">The id of the data</param>
        /// <returns>The reference, null if not found or expired</returns>
        public TableDataReference GetTableData(String dataRefId)
            => Server.GetTableData(this, dataRefId);

        /// <summary>
        /// Get the data of a data table from a given id.
        /// </summary>
        /// <param name="dataRefId">The id of the data</param>
        /// <returns>The data, null if not found or expired</returns>
        public BaseTableData ResolveTableData(String dataRefId)
            => Server.GetTableData(this, dataRefId)?.Get();


        #endregion//Data References

        /// <summary>
        /// Get the translator to use if any translation is to be done (null if auto translation isn't enabled).
        /// </summary>
        public ITranslator Translator => Server.Translator;

        /// <summary>
        /// The language to use (the session language, "en" if there is no session).
        /// </summary>
        public String Language => Session?.Language ?? "en";

        /// <summary>
        /// Additional etag data, appended to the handler's etag (use when the response depends on more than the handler data).
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
        /// Create a request for another url that shares the connection, headers and session of this request (used internally for internal redirects).
        /// </summary>
        /// <param name="newUrl">The new (decoded) url</param>
        /// <param name="host">The host of the new url</param>
        /// <param name="prefix">The prefix of the new url</param>
        /// <param name="queryStart">The index of the '?' in the new url, -1 if there is no query string</param>
        /// <param name="server">The server</param>
        /// <param name="newMethod">The new http method, null to keep the current</param>
        /// <returns>The new request</returns>
        public abstract HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null);

    }

}
