using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net;
using System.Buffers;
using Microsoft.Extensions.Primitives;

namespace SysWeaver.Net
{
    public sealed class AspHttpServerRequest : HttpServerRequest
    {
        public AspHttpServerRequest(HttpContext context, String url, String prefix, AspHttpServer server, HttpServerHostInfo host, int queryStart, bool didIndex, String newMethod = null)
            : base(
                    newMethod ?? context.Request.Method,
                    url, prefix, server, host, queryStart, didIndex)
        {
            Context = context;
            Req = context.Request;
            Res = context.Response;
        }

        internal readonly HttpContext Context;
        internal readonly HttpRequest Req;
        internal readonly HttpResponse Res;

        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders => Req.Headers.Select(x => new KeyValuePair<String, IReadOnlyList<String>>(x.Key, x.Value.ToList()));
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders => Res.Headers.Select(x => new KeyValuePair<String, IReadOnlyList<String>>(x.Key, x.Value.ToList()));


        public override String IfNoneMatch => Req.Headers["If-None-Match"].FirstOrDefault()?.Trim();
        public override string AcceptEncoding => Req.Headers["Accept-Encoding"];

        public override Stream InputStream => Req.Body;
        public override Stream OutputStream => Res.Body;

        public override long ReqContentLength => Req.ContentLength ?? 0;
        public override String GetReqHeader(String name) => Req.Headers[name];

        public override String GetResHeader(String name) => Res.Headers[name];

        public override String GetResMime() => Res.ContentType;

        public override void SetResMime(String mime)
        {
            Res.ContentType = mime;
            Mime = mime;
        }

        String Mime;
       
        public override String ProtocolVersion => Req.Protocol;

        public override void SetResContentLength(long length)
        {
            Res.ContentLength = length;
            Cl = length;
        }

        long Cl;

        public override void SetResStatusCode(int statusCode) => Res.StatusCode = statusCode;
        public override int GetResStatusCode() => Res.StatusCode;



        IReadOnlyDictionary<String, String> Cookies;

        IReadOnlyDictionary<String, String> ReadCookies(String cookieString)
        {
            var t = HttpServerTools.ParseCookieString(cookieString ?? Req.Headers.Cookie.FirstOrDefault());
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
            Res.Headers.Append("Set-Cookie", str);
        }



        Dictionary<String, String> Head;

        public override void SetResBody(ReadOnlySpan<Byte> data)
        {
            var r = Res;
            r.ContentLength = data.Length;
            r.Body.Write(data);
        }
        public override ValueTask SetResBodyAsync(ReadOnlyMemory<Byte> data)
        {
            var r = Res;
            r.ContentLength = data.Length;
            return r.Body.WriteAsync(data);
        }

        public override void SetResHeader(String header, String value)
        {
            Res.Headers[header] = value;
            var h = Head;
            if (h == null)
            {
                h = new Dictionary<string, string>(StringComparer.Ordinal);
                Head = h;
            }
            h[header] = value;  
        }

        public override IPAddress GetIP()
        {
            var a = Context.Connection.RemoteIpAddress;
            return a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a;
        }

        bool InternalIsDead;

        public override bool IsDead()
        {
            if (InternalIsDead)
                return true;
            try
            {
                return false;
            }
            catch
            {
                InternalIsDead = true;
                return true;
            }
        }

        static readonly IReadOnlyDictionary<String, Action<HttpResponse, IReadOnlyList<String>>> ResHeaderSetters = new Dictionary<String, Action<HttpResponse, IReadOnlyList<String>>>(StringComparer.Ordinal)
        {
            { "Content-Length", (to, vals) => to.ContentLength = long.Parse(vals.First()) },
            { "Content-Type", (to, vals) => to.ContentType = vals.First() },
        }.Freeze();

        public override void SetResHeaders(int status, IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> headers, IReadOnlySet<String> ignore)

        {
            ignore = ignore ?? DefaultIgnoreHeaders;
            var to = Res;
            var ss = ResHeaderSetters;
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
                to.Headers[k] = new StringValues(vals.ToArray());
            }
            to.StatusCode = status;
        }

        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            var h = new AspHttpServerRequest(Context, newUrl, prefix, server as AspHttpServer, host, queryStart, false, newMethod);
            h.Init(Session);
            return h;
        }

        public override void Dispose()
        {
            OnDispose();
            base.Dispose();
        }

        public override string GetRawQuery(string def = "")
        {
            var qs = Req.QueryString;
            if (!qs.HasValue)
                return def;
            return qs.Value.Substring(1);
        }
    }



}
