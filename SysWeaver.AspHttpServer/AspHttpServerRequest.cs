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
    public sealed class AspHttpServerRequest : HttpServerRequest, IDisposable
    {
        public AspHttpServerRequest(HttpContext context, String url, String prefix, AspHttpServer server, Uri uri, HttpServerHostInfo host, String newMethod = null)
            : base(
                    newMethod ?? context.Request.Method,
                    url, prefix, server, uri, host)
        {
            Context = context;
            Req = context.Request;
            Res = context.Response;
        }

        internal readonly HttpContext Context;
        internal readonly HttpRequest Req;
        internal readonly HttpResponse Res;

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

        static readonly IReadOnlyDictionary<String, String> Empty = new Dictionary<String, String>(StringComparer.Ordinal).Freeze();


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


        IReadOnlyDictionary<String, String> ReadCookies()
        {
            var s = Req.Headers.Cookie;
            var c = s.Count;
            if (c <= 0)
            {
                var e = Empty;
                Cookies = e;
                return e;
            }
            var t = new Dictionary<String, String>(c, StringComparer.Ordinal);
            foreach (var x in s)
            {
                int start = 0;
                for (; ; )
                {
                    var e = x.IndexOf('=', start);
                    if (e < 0)
                        break;
                    var key = Trimmed(x, start, e);
                    start = e + 1;
                    e = x.IndexOf(';', start);
                    if (e < 0)
                    {
                        var value = Trimmed(x, start, x.Length);
                        t[key] = value;
                        break;
                    }
                    var val = Trimmed(x, start, e);
                    t[key] = val;
                    start = e + 1;
                }
            }
            Cookies = t;
            return t;
        }

        public override String GetReqCookie(String name)
        {
            var cookies = Cookies;
            if (cookies == null)
                cookies = ReadCookies();
            cookies.TryGetValue(name, out var cookie);
            return cookie;
        }

        const String DefPath = "/;HttpOnly";

        static readonly CookieOptions DefCock = new CookieOptions
        {
            Path = DefPath,
        };

        public override void UpdateCookie(String n, String value, DateTime exp, String path = DefPath)
        {
            var now = DateTime.UtcNow;
            var maxDate = now.AddYears(1);
            if (exp > maxDate)
                exp = maxDate;
            var maxAge = (long)(exp - now).TotalSeconds;
            var str = maxAge <= 0 ? HttpServerTools.MakeCookie(n, "", 0, path) : HttpServerTools.MakeCookie(n, value, maxAge, path);
            Res.Headers.Append("Set-Cookie", str);
            Cok[n] = str;
        }



        readonly Dictionary<String, String> Cok = new (StringComparer.Ordinal);
        readonly Dictionary<String, String> Head = new (StringComparer.Ordinal);

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
            Head[header] = value;  
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
            pool.Return(toDelete);
            foreach (var h in Head)
                toh.Append(h.Key, h.Value);
            to.ContentLength = Cl;
            to.ContentType = Mime;
            to.StatusCode = Status;
        }


        public void Dispose()
        {
            OnDispose();
        }

    }



}
