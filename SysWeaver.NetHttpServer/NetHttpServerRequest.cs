using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Collections.Generic;

namespace SysWeaver.Net
{
    public sealed class NetHttpServerRequest : HttpServerRequest
    {
        public NetHttpServerRequest(HttpListenerContext context, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart, String newMethod = null) 
            : base(
                    newMethod ?? context.Request.HttpMethod,
                    url, prefix, server, host, queryStart)
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


        public override IEnumerable<KeyValuePair<String, String>> AllReqHeaders
        {
            get
            {
                var h = ReqHeaders;
                foreach (var key in h.AllKeys)
                    yield return new KeyValuePair<String, String>(key, h.Get(key));
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
        }

        public override String ProtocolVersion => Req.ProtocolVersion.ToString();

        public override void SetResContentLength(long length) => Res.ContentLength64 = length;
        public override void SetResStatusCode(int statusCode) => Res.StatusCode = statusCode;



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

        public override void UpdateCookie(String n, String value, DateTime exp, String path = "/;HttpOnly")
        {
            var now = DateTime.UtcNow;
            var maxDate = now.AddYears(1);
            if (exp > maxDate)
                exp = maxDate;
            var maxAge = (long)(exp - now).TotalSeconds;
            var str = maxAge <= 0 ? HttpServerTools.MakeCookie(n, "", 0, path) : HttpServerTools.MakeCookie(n, value, maxAge, path);
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

        public override void CopyHeaders(HttpServerRequest toData)
        {
            var s = Res;
            var to = (toData as NetHttpServerRequest).Res;
            if (s == to)
                return;
            foreach (String h in s.Headers)
            {
                if (!h.FastEquals("Set-Cookie"))
                    to.Headers[h] = s.Headers[h];
            }
            to.ContentLength64 = s.ContentLength64;
            to.ContentEncoding = s.ContentEncoding;
            to.StatusCode = s.StatusCode;
        }


        public override void Dispose()
        {
            OnDispose();
            base.Dispose();
        }

        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            var h = new NetHttpServerRequest(Context, newUrl, prefix, server, host, queryStart, newMethod);
            h.Init(Session);
            return h;
        }
    }




}
