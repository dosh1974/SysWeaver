using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net;

namespace SysWeaver.Net
{
    public sealed class AspHttpServerRequest : HttpServerRequest, IDisposable
    {
        public AspHttpServerRequest(HttpContext context, String url, String prefix, AspHttpServer server, Uri uri, HttpServerHostInfo host, String newMethod = null)
            : base(
                    newMethod ?? context.Request.Method,
                    //context.Request.Headers["If-Modified-Since"].FirstOrDefault()?.Trim(),
                    context.Request.Headers["If-None-Match"].FirstOrDefault()?.Trim(),
                    context.Request.Headers["Accept-Encoding"],
                    url, prefix, server, uri, host)
        {
            Context = context;
            Req = context.Request;
            Res = context.Response;
        }

        internal readonly HttpContext Context;
        internal readonly HttpRequest Req;
        internal readonly HttpResponse Res;

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


        public override String GetReqCookie(String name)
        {
            String s = Req.Headers["Cookie"];
            if (s == null)
                return null;
            name += "=";
            var i = s.IndexOf(name, StringComparison.Ordinal);
            if (i < 0)
                return null;
            i += name.Length;
            var end = s.IndexOf(';', i);
            if (end < 0)
                end = s.Length;
            return s.Substring(i, end - i);
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
            var opt = exp <= now ?
                new CookieOptions
                {
                    Path = path,
                    MaxAge = TimeSpan.Zero,
                }
                :
                new CookieOptions
                {
                    Path = path,
                    Expires = exp,
                };
            Res.Cookies.Append(n, value, opt);
            Cok[n] = Tuple.Create(value, opt);
        }



        readonly Dictionary<String, Tuple<String, CookieOptions>> Cok = new Dictionary<string, Tuple<string, CookieOptions>>(StringComparer.Ordinal);
        readonly Dictionary<String, String> Head = new Dictionary<string, string>(StringComparer.Ordinal);

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

        public override void CopyHeaders(HttpServerRequest toData)
        {
            var s = Res;
            var to = (toData as AspHttpServerRequest).Res;
            to.ContentLength = Cl;
            to.StatusCode = Status;
            to.ContentType = Mime;
            var toh = to.Headers;
            foreach (var h in Head)
                toh.Append(h.Key, h.Value);
            var toc = to.Cookies;
            foreach (var c in Cok)
            {
                var v = c.Value;
                toc.Append(c.Key, v.Item1, v.Item2);
            }
        }


        public void Dispose()
        {
            OnDispose();
        }

    }



}
