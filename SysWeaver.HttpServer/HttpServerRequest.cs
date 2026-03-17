using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using SysWeaver.Data;
using SysWeaver.Translation;
using System.Buffers;

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
        /// The local url after stripping the prefix and query paramaters
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
        /// Get the raw query string (everything after ?) or an empty string if no query is present.
        /// </summary>
        /// <returns></returns>
        public String GetRawQuery(String def = "")
        {
            var i = QueryStringStart;
            if (i <= 0)
                return def;
            return Url.Substring(i);
        }

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


        static readonly IReadOnlyDictionary<String, HttpServerMethods> IntMethods = new Dictionary<String, HttpServerMethods>(StringComparer.Ordinal)
        {
            { "GET", HttpServerMethods.GET },
            { "POST", HttpServerMethods.POST },
            { "HEAD", HttpServerMethods.HEAD },
        }.Freeze();

        protected HttpServerRequest(String httpMethod, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart)
        {
            Method = httpMethod;
            var m = IntMethods.TryGetValue(httpMethod ?? "", out var hm) ? hm : HttpServerMethods.Other;
            HttpMethod = m;
            IsHead = m == HttpServerMethods.HEAD;
            Url = url;
            Prefix = prefix;
            var pl = prefix.Length;
            QueryStringStart = queryStart + 1;
            LocalUrl = queryStart < 0 ? url.Substring(pl) : url.Substring(pl, queryStart - pl);
            Server = server;
            Host = host;
        }
       

        /// <summary>
        /// Get all request headers
        /// </summary>
        public abstract IEnumerable<KeyValuePair<String, IEnumerable<String>>> AllReqHeaders { get; }



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
        public NameValueCollection QueryParameters => IQP ?? (IQP = HttpUtility.ParseQueryString(Url.Substring(QueryStringStart)));



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
        public abstract void SetResHeader(String header, String value);
        public abstract void SetResBody(ReadOnlySpan<Byte> data);
        public abstract ValueTask SetResBodyAsync(ReadOnlyMemory<Byte> data);

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

            var fw = GetReqHeader("Forwarded");
            if (fw != null)
            {
                // "Forwarded"      https://datatracker.ietf.org/doc/html/rfc7239

            }
            fw = GetReqHeader("X-Forwarded-For");
            if (fw != null)
            {
                //"X-Forwarded-For"   https://en.wikipedia.org/wiki/X-Forwarded-For
            }
            var ip = GetIP()?.ToString();
            if (ip == null)
                return "?";
            bool isV6 = ip.StartsWith('[');
            if (isV6)
            {
                ip = ip.Split(']')[0].Substring(1);
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
            if (tl < 1024)
            {
                Span<Byte> tt = stackalloc Byte[al];
                if (Encoding.UTF8.TryGetBytes(text, tt, out var wl))
                {
                    SetResBody(tt.Slice(0, wl));
                    return;
                }
            }
            var buf = ArrayPoolStream.Rent(al);
            try
            {
                var t = buf.AsSpan();
                if (Encoding.UTF8.TryGetBytes(text, t, out var l))
                {
                    SetResBody(t.Slice(0, l));
                    return;
                }
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }

        public abstract void CopyHeaders(HttpServerRequest to);

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
            if (path.IndexOf("://") >= 0)
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
            if (url.IndexOf("://") > 0)
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
