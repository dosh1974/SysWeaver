using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net;
using System.Buffers;

namespace SysWeaver.Net
{
    public sealed class AspHttpServerRequest : HttpServerRequest
    {
        public AspHttpServerRequest(HttpContext context, String url, String prefix, AspHttpServer server, HttpServerHostInfo host, int queryStart, String newMethod = null)
            : base(
                    newMethod ?? context.Request.Method,
                    url, prefix, server, host, queryStart)
        {
            Context = context;
            Req = context.Request;
            Res = context.Response;
        }

        internal readonly HttpContext Context;
        internal readonly HttpRequest Req;
        internal readonly HttpResponse Res;

        public override IEnumerable<KeyValuePair<String, String>> AllReqHeaders => Req.Headers.Select(x => new KeyValuePair<String, String>(x.Key, x.Value.FirstOrDefault()?.Trim()));

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

        public override void SetResStatusCode(int statusCode)
        {
            Res.StatusCode = statusCode;
            Status = statusCode;
        }
        int Status;



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

        const String DefPath = "/;HttpOnly";

        public override void UpdateCookie(String n, String value, DateTime exp, String path = DefPath)
        {
            var now = DateTime.UtcNow;
            var maxDate = now.AddYears(1);
            if (exp > maxDate)
                exp = maxDate;
            var maxAge = (long)(exp - now).TotalSeconds;
            var str = maxAge <= 0 ? HttpServerTools.MakeCookie(n, "", 0, path) : HttpServerTools.MakeCookie(n, value, maxAge, path);
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

        static readonly ArrayPool<String> Pool = ArrayPool<String>.Shared;

        public override void CopyHeaders(HttpServerRequest toData)
        {
            var s = Res;
            var to = (toData as AspHttpServerRequest).Res;
            var toh = to.Headers;
            var count = toh.Count;
            var pool = Pool;
            var toDelete = pool.Rent(count);
            int delCount = 0;
            foreach (var n in toh.Keys)
            {
                if (!n.FastEquals("Set-Cookie"))
                {
                    toDelete[delCount] = n;
                    ++delCount;
                }
            }
            if (delCount == count)
            {
                toh.Clear();
            }else
            {
                while (delCount > 0)
                {
                    --delCount;
                    toh.Remove(toDelete[delCount]);
                }
            }
#if DEBUG
            pool.Return(toDelete, true);
#else//DEBUG
            pool.Return(toDelete);
#endif//DEBUG
            var hs = Head;
            if (hs != null)
                foreach (var h in hs)
                    toh.Append(h.Key, h.Value); 
            to.ContentLength = Cl;
            to.ContentType = Mime;
            to.StatusCode = Status;
        }

        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            var h = new AspHttpServerRequest(Context, newUrl, prefix, server as AspHttpServer, host, queryStart, newMethod);
            h.Init(Session);
            return h;
        }

        public override void Dispose()
        {
            OnDispose();
            base.Dispose();
        }

    }



}
