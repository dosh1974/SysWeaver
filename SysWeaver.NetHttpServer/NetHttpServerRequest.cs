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
    public sealed class NetHttpServerRequest : HttpServerRequest
    {
        public NetHttpServerRequest(HttpListenerContext context, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart, bool didIndex, String newMethod = null) 
            : base(
                    newMethod ?? context.Request.HttpMethod,
                    url, prefix, server, host, queryStart, didIndex)
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


        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders
        {
            get
            {
                var h = ReqHeaders;
                foreach (var key in h.AllKeys)
                    yield return new KeyValuePair<String, IReadOnlyList<String>>(key, h.GetValues(key));
            }
        }

        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders
        {
            get
            {
                var h = ResHeaders;
                foreach (var key in h.AllKeys)
                    yield return new KeyValuePair<String, IReadOnlyList<String>>(key, h.GetValues(key));
            }
        }

        public override String IfNoneMatch => ReqHeaders["If-None-Match"]?.Trim();
        public override string AcceptEncoding => ReqHeaders["Accept-Encoding"];

        public override Stream InputStream => Req.InputStream;
        public override Stream OutputStream => Res.OutputStream;
        
        public override long ReqContentLength => Req.ContentLength64;
        public override String GetReqHeader(String name) => ReqHeaders[name];
        public override String GetResHeader(String name) => ResHeaders[name];

        public override String GetResMime() => ResContentType;

        String ResContentType;

        public override void SetResMime(String mime)
        {
            ResContentType = mime;
            Res.ContentType = mime;
            if (mime.FastEquals("image/png") && LocalUrl.FastEquals("Hypnos/Api/MemberCard.svg"))
                mime = null;

        }

        public override String ProtocolVersion => Req.ProtocolVersion.ToString();

        public override void SetResContentLength(long length) => Res.ContentLength64 = length;
        public override void SetResStatusCode(int statusCode) => Res.StatusCode = statusCode;

        public override int GetResStatusCode() => Res.StatusCode;


        IReadOnlyDictionary<String, String> Cookies;

        IReadOnlyDictionary<String, String> ReadCookies(String cookieString)
        {
            var t = HttpServerTools.ParseCookieString(cookieString ?? ReqHeaders["Cookie"]);
            Cookies = t;
            return t;
        }

        public override String GetReqCookie(String name, String cookieString = null)
        {
            (Cookies ?? ReadCookies(cookieString)).TryGetValue(name, out var cookie);
            return cookie;
        }

        public override void UpdateCookie(String str)
        {
            Res.AppendHeader("Set-Cookie", str);
        }

        public override void SetResBody(ReadOnlySpan<Byte> data)
        {
            var r = Res;
            r.ContentLength64 = data.Length;
            r.OutputStream.Write(data);
        }
        public override async Task SetResBodyAsync(ReadOnlyMemory<Byte> data)
        {
            var r = Res;
            r.ContentLength64 = data.Length;
            await r.OutputStream.WriteAsync(data).ConfigureAwait(false);
        }

        public override void SetResBody(Byte[] data, int offset, int length)
        {
            var r = Res;
            r.ContentLength64 = length;
            r.OutputStream.Write(data, offset, length);
        }
        public override Task SetResBodyAsync(Byte[] data, int offset, int length)
        {
            var r = Res;
            r.ContentLength64 = length;
            return r.OutputStream.WriteAsync(data, offset, length);
        }


        public override void SetResHeader(String header, String value)
            => Res.Headers[header] = value;

        bool InternalIsDead;

        public override IPAddress GetIP()
        {
            return Req.RemoteEndPoint.Address;
        }

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

        public override void Dispose()
        {
            OnDispose();
            base.Dispose();
        }

        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            var h = new NetHttpServerRequest(Context, newUrl, prefix, server, host, queryStart, false, newMethod);
            h.Init(Session);
            return h;
        }

        public override string GetRawQuery(string def = "")
        {
            var q = Req.Url.Query;
            if (String.IsNullOrEmpty(q))
                return def;
            return q.Substring(1);
        }
    }




}
