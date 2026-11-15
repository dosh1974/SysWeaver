using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.Net
{
    /// <summary>
    /// A request handled by <see cref="NetHttpServer"/>, a wrapper around an <see cref="HttpListenerContext"/>.
    /// </summary>
    public sealed class NetHttpServerRequest : HttpServerRequest
    {
        /// <summary>
        /// Create a request from a listener context (created by <see cref="NetHttpServer"/>).
        /// </summary>
        /// <param name="context">The listener context.</param>
        /// <param name="rawUrl">The raw url (including the query string).</param>
        /// <param name="url">The url.</param>
        /// <param name="prefix">The prefix the request was received on.</param>
        /// <param name="server">The server that handles the request.</param>
        /// <param name="host">The host info of the request.</param>
        /// <param name="queryStart">The index of the query start in the url.</param>
        /// <param name="didIndex">True if the url was changed to an index document.</param>
        /// <param name="newMethod">Optional method to use instead of the method of the context.</param>
        public NetHttpServerRequest(HttpListenerContext context, String rawUrl, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart, bool didIndex, String newMethod = null) 
            : base(
                    newMethod ?? context.Request.HttpMethod,
                    rawUrl, url, prefix, server, host, queryStart, didIndex)
        {
            Context = context;
            var req = context.Request;
            var res = context.Response;
            Req = req;
            Res = res;
            ReqHeaders = req.Headers;
            ResHeaders = res.Headers;
        }

        internal readonly HttpListenerContext Context;
        internal readonly HttpListenerRequest Req;
        internal readonly HttpListenerResponse Res;
        readonly NameValueCollection ReqHeaders;
        readonly NameValueCollection ResHeaders;


        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders
        {
            get
            {
                var h = ReqHeaders;
                foreach (var key in h.AllKeys)
                    yield return new KeyValuePair<String, IReadOnlyList<String>>(key, h.GetValues(key));
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders
        {
            get
            {
                var h = ResHeaders;
                foreach (var key in h.AllKeys)
                    yield return new KeyValuePair<String, IReadOnlyList<String>>(key, h.GetValues(key));
            }
        }

        /// <inheritdoc/>
        public override String IfNoneMatch => ReqHeaders["If-None-Match"]?.Trim();
        /// <inheritdoc/>
        public override string AcceptEncoding => ReqHeaders["Accept-Encoding"];

        /// <inheritdoc/>
        public override Stream InputStream => Req.InputStream;
        /// <inheritdoc/>
        public override Stream OutputStream => Res.OutputStream;
        
        /// <inheritdoc/>
        public override long ReqContentLength => Req.ContentLength64;
        /// <inheritdoc/>
        public override String GetReqHeader(String name) => ReqHeaders[name];
        /// <inheritdoc/>
        public override String GetResHeader(String name) => ResHeaders[name];

        /// <inheritdoc/>
        public override String GetResMime() => ResContentType;

        String ResContentType;

        /// <inheritdoc/>
        public override void SetResMime(String mime)
        {
            ResContentType = mime;
            Res.ContentType = mime;
        }

        /// <inheritdoc/>
        public override String ProtocolVersion => Req.ProtocolVersion.ToString();

        /// <inheritdoc/>
        public override void SetResContentLength(long length) => Res.ContentLength64 = length;
        /// <inheritdoc/>
        public override void SetResStatusCode(int statusCode) => Res.StatusCode = statusCode;

        /// <inheritdoc/>
        public override int GetResStatusCode() => Res.StatusCode;


        IReadOnlyDictionary<String, String> Cookies;

        IReadOnlyDictionary<String, String> ReadCookies(String cookieString)
        {
            var t = HttpServerTools.ParseCookieString(cookieString ?? ReqHeaders["Cookie"]);
            Cookies = t;
            return t;
        }

        /// <inheritdoc/>
        public override String GetReqCookie(String name, String cookieString = null)
        {
            (Cookies ?? ReadCookies(cookieString)).TryGetValue(name, out var cookie);
            return cookie;
        }

        /// <inheritdoc/>
        public override void UpdateCookie(String str)
        {
            Res.AppendHeader("Set-Cookie", str);
        }

        /// <inheritdoc/>
        public override void SetResBody(ReadOnlySpan<Byte> data)
        {
            var r = Res;
            r.ContentLength64 = data.Length;
            r.OutputStream.Write(data);
        }
#if NET11_0_OR_GREATER
        /// <summary>
        /// Bodies up to this size are written synchronously (see <see cref="SetResBodyAsync(ReadOnlyMemory{byte})"/>).
        /// </summary>
        const int SyncWriteMaxSize = 64 << 10;
#endif//NET11_0_OR_GREATER

        /// <inheritdoc/>
        /// <remarks>
        /// .NET 11+: a body up to <see cref="SyncWriteMaxSize"/> is written synchronously (http.sys copies it, the write doesn't wait for the client).
        /// An async HttpListener write allocates and frees a native overlapped, and freeing it (ThreadPoolBoundHandle.FreeNativeOverlapped) is contended on .NET 11:
        /// under load, requests waited for whole timer ticks (15.6 ms) and the throughput dropped to a third of .NET 10.
        /// </remarks>
        public override async Task SetResBodyAsync(ReadOnlyMemory<Byte> data)
        {
            var r = Res;
            r.ContentLength64 = data.Length;
#if NET11_0_OR_GREATER
            if (data.Length <= SyncWriteMaxSize)
            {
                r.OutputStream.Write(data.Span);
                return;
            }
#endif//NET11_0_OR_GREATER
            await r.OutputStream.WriteAsync(data).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override void SetResBody(Byte[] data, int offset, int length)
        {
            var r = Res;
            r.ContentLength64 = length;
            r.OutputStream.Write(data, offset, length);
        }
        /// <inheritdoc/>
        /// <remarks>.NET 11+: a body up to <see cref="SyncWriteMaxSize"/> is written synchronously, see <see cref="SetResBodyAsync(ReadOnlyMemory{byte})"/></remarks>
        public override Task SetResBodyAsync(Byte[] data, int offset, int length)
        {
            var r = Res;
            r.ContentLength64 = length;
#if NET11_0_OR_GREATER
            if (length <= SyncWriteMaxSize)
            {
                try
                {
                    r.OutputStream.Write(data, offset, length);
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }
#endif//NET11_0_OR_GREATER
            return r.OutputStream.WriteAsync(data, offset, length);
        }


        /// <inheritdoc/>
        public override void SetResHeader(String header, String value)
            => Res.Headers[header] = value;

        bool InternalIsDead;

        /// <inheritdoc/>
        public override IPAddress GetIP()
        {
            return Req.RemoteEndPoint.Address;
        }

        /// <inheritdoc/>
        public override bool IsDead()
        {
            if (InternalIsDead)
                return true;
            try
            {
                var b = Req.IsLocal;
                return false;
            }
            catch
            {
                InternalIsDead = true;
                return true;
            }
        }


        static readonly IReadOnlyDictionary<String, Action<HttpListenerResponse, IReadOnlyList<String>>> ResHeaderSetters = new Dictionary<String, Action<HttpListenerResponse, IReadOnlyList<String>>>(StringComparer.Ordinal)
        {
            { "Content-Length", (to, vals) => to.ContentLength64 = long.Parse(vals.First()) },
            { "Content-Type", (to, vals) => to.ContentType = vals.First() },
        }.Freeze();


        /// <inheritdoc/>
        public override void SetResHeaders(int status, IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> headers, IReadOnlySet<String> ignore)
        {
            ignore = ignore ?? DefaultIgnoreHeaders;
            var ss = ResHeaderSetters;
            var to = Res;
            foreach (var h in headers)
            {
                var k = h.Key;
                if (ignore.Contains(k))
                    continue;
                var vals = h.Value;
                var vc = vals.Count;
                if (vc <= 0)
                    continue;
                if (ss.TryGetValue(k, out var set))
                {
                    set(to, vals);
                    continue;
                }
                to.Headers[k] = vals[0];
                for (int i = 1; i < vc; ++i)
                    to.AppendHeader(k, vals[i]);
            }
            to.StatusCode = status;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            OnDispose();
            base.Dispose();
        }

        /// <inheritdoc/>
        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            var h = new NetHttpServerRequest(Context, newUrl, newUrl, prefix, server, host, queryStart, false, newMethod);
            h.Init(Session);
            return h;
        }

        /// <inheritdoc/>
        public override string GetRawQuery(string def = "")
        {
            var q = Req.Url.Query;
            if (String.IsNullOrEmpty(q))
                return def;
            return q.Substring(1);
        }
    }




}
