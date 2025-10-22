using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public sealed class NetHttpServerRequest : HttpServerRequest, IDisposable
    {
        public NetHttpServerRequest(HttpListenerContext context, String url, String prefix, HttpServerBase server, Uri uri, HttpServerHostInfo host, String newMethod = null) 
            : base(
                    newMethod ?? context.Request.HttpMethod,
                    //context.Request.Headers["If-Modified-Since"]?.Trim(),
                    context.Request.Headers["If-None-Match"]?.Trim(),
                    context.Request.Headers["Accept-Encoding"],
                    url, prefix, server, uri, host)
        {
            Context = context;
            Req = context.Request;
            Res = context.Response;
        }

        internal readonly HttpListenerContext Context;
        internal readonly HttpListenerRequest Req;
        internal readonly HttpListenerResponse Res;

        public override Stream InputStream => Req.InputStream;
        public override Stream OutputStream => Res.OutputStream;
        
        public override long ReqContentLength => Req.ContentLength64;
        public override String GetReqHeader(String name) => Req.Headers[name];
        public override String GetResHeader(String name) => Res.Headers[name];

        public override String GetResMime() => Res.ContentType;

        public override void SetResMime(String mime) => Res.ContentType = mime;

        public override String ProtocolVersion => Req.ProtocolVersion.ToString();

        public override void SetResContentLength(long length) => Res.ContentLength64 = length;
        public override void SetResStatusCode(int statusCode) => Res.StatusCode = statusCode;


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

        public static String MakeCookie(String name, String value, long maxAge, String path)
        {
            return String.Concat(
                name,
                '=',
                value,
                ";Max-Age=",
                maxAge,
                ";Path=",
                path);
        }

        public override void UpdateCookie(String n, String value, DateTime exp, String path = "/;HttpOnly")
        {
            var now = DateTime.UtcNow;
            var maxDate = now.AddYears(1);
            if (exp > maxDate)
                exp = maxDate;
            var maxAge = (long)(exp - now).TotalSeconds;
            var str = maxAge <= 0 ? MakeCookie(n, "", 0, path) : MakeCookie(n, value, maxAge, path);
            Res.AppendHeader("Set-Cookie", str);
        }
        public override void SetResBody(ReadOnlySpan<Byte> data)
        {
            var r = Res;
            r.ContentLength64 = data.Length;
            r.OutputStream.Write(data);
        }
        public override ValueTask SetResBodyAsync(ReadOnlyMemory<Byte> data)
        {
            var r = Res;
            r.ContentLength64 = data.Length;
            return r.OutputStream.WriteAsync(data);
        }

        public override void SetResHeader(String header, String value) => Res.Headers[header] = value;

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

        public override void CopyHeaders(HttpServerRequest toData)
        {
            var s = Res;
            var to = (toData as NetHttpServerRequest).Res;
            if (s == to)
                return;
            to.ContentLength64 = s.ContentLength64;
            to.ContentEncoding = s.ContentEncoding;
            to.StatusCode = s.StatusCode;
            foreach (String h in s.Headers)
                to.Headers[h] = s.Headers[h];
            foreach (Cookie c in s.Cookies)
                to.Cookies.Add(c);
        }


        public void Dispose()
        {
            OnDispose();
        }

    }




}
