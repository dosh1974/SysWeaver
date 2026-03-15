using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.ReverseProxy
{
    public sealed class ManualHttpServerRequest : HttpServerRequest
    {

        public ManualHttpServerRequest(String httpMethod, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart)
            : base(httpMethod, url, prefix, server, host, queryStart)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            Interlocked.Exchange(ref _InputStream, null)?.Dispose();
            Interlocked.Exchange(ref _OutputStream, null)?.Dispose();
        }

        public Stream _InputStream;
        
        public long _ReqContentLength;
        public string _ProtocolVersion = "1.1";
        public string _AcceptEncoding;
        public string _IfNoneMatch;

        public IPAddress _IP = IPAddress.Loopback;
        public IReadOnlyDictionary<String, String> ReqCookies;
        public IReadOnlyDictionary<String, String> ReqHeaders;


        public Stream _OutputStream;
        public int _ResStatusCode;
        public readonly Dictionary<String, String> ResHeaders = new(StringComparer.Ordinal);

        public override IEnumerable<KeyValuePair<String, String>> AllReqHeaders => ReqHeaders;

        public override string IfNoneMatch => _IfNoneMatch;

        public override string AcceptEncoding => _AcceptEncoding;

        public override Stream InputStream => _InputStream;

        public override Stream OutputStream => _OutputStream;

        public override long ReqContentLength => _ReqContentLength;


        public override string ProtocolVersion => _ProtocolVersion;


        public override void CopyHeaders(HttpServerRequest to)
        {
            var t = to as ManualHttpServerRequest;
            if (t == null)
                throw new Exception("Invalid types!");
            var tr = t.ResHeaders;
            foreach (var x in ResHeaders)
                tr[x.Key] = x.Value;
        }

        public override IPAddress GetIP() => _IP;

        public override string GetReqCookie(string name, string cookieString = null)
            => ReqCookies.TryGetValue(name, out var v) ? v : null;

        public override string GetReqHeader(string name)
            => ReqHeaders.TryGetValue(name, out var v) ? v : null;

        public override string GetResHeader(string name)
            => ResHeaders.TryGetValue(name, out var v) ? v : null;

        public override string GetResMime()
            => ResHeaders.TryGetValue("Content-Type", out var v) ? v : null;

        public override bool IsDead()
            => false;

        public override void SetResBody(ReadOnlySpan<byte> data)
            => OutputStream.Write(data);

        public override ValueTask SetResBodyAsync(ReadOnlyMemory<byte> data)
            => OutputStream.WriteAsync(data);

        public override void SetResContentLength(long length)
        {
            ResHeaders["Content-Length"] = length.ToString();
        }

        public override void SetResHeader(string header, string value)
        {
            if (String.IsNullOrEmpty(value))
                ResHeaders.TryRemove(header, out var _);
            else
                ResHeaders[header] = value;
        }

        public override void SetResMime(string mime)
        {
            ResHeaders["Content-Type"] = mime;
        }

        public override void SetResStatusCode(int statusCode)
        {
            _ResStatusCode = statusCode;
        }

        public override void UpdateCookie(string name, string value, DateTime exp, string path = "/;HttpOnly")
        {
            var now = DateTime.UtcNow;
            var maxDate = now.AddYears(1);
            if (exp > maxDate)
                exp = maxDate;
            var maxAge = (long)(exp - now).TotalSeconds;
            var str = maxAge <= 0 ? HttpServerTools.MakeCookie(name, "", 0, path) : HttpServerTools.MakeCookie(name, value, maxAge, path);
            var h = ResHeaders;
            if (h.TryGetValue("Set-Cookie", out var t))
                t = String.Concat(t, ';', str);
            else
                t = str;
            h["Set-Cookie"] = t;
        }


        public override HttpServerRequest ReplaceUrl(string newUrl, HttpServerHostInfo host, String prefix, int queryStart, HttpServerBase server, String newMethod = null)
        {
            var h = new ManualHttpServerRequest(newMethod ?? Method, newUrl, prefix, server, host, queryStart);
            h._InputStream = _InputStream;
            h._ReqContentLength = _ReqContentLength;
            h._ProtocolVersion = _ProtocolVersion;
            h._AcceptEncoding = _AcceptEncoding;
            h._IfNoneMatch = _IfNoneMatch;
            h._IP = _IP;
            h.ReqCookies = ReqCookies;
            h.ReqHeaders = ReqHeaders;
            h._OutputStream = _OutputStream;
            h._ResStatusCode = _ResStatusCode;
            var s = ResHeaders;
            var d = h.ResHeaders;
            foreach (var x in s)
                d[x.Key] = x.Value;
            h.Init(Session);
            return h;
        }


    }



}
