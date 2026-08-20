using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.Net
{
    public sealed class ManualHttpServerRequest : HttpServerRequest
    {

        public ManualHttpServerRequest(String httpMethod, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart, bool didIndex = false)
            : base(httpMethod, url, prefix, server, host, queryStart, didIndex)
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
        public Headers ReqHeaders = new Headers();


        public sealed class Headers : HttpHeaders
        {
            public String this[String key] 
            {
                get => TryGetValues(key, out var vals) ? String.Join(';', vals) : null;
            }
        }

        public Stream _OutputStream;
        public int _ResStatusCode;
        public readonly Headers ResHeaders = new Headers();

        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders => ReqHeaders.Select(x => new KeyValuePair<String, IReadOnlyList<String>>(x.Key, x.Value.ToList()));
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders => ResHeaders.Select(x => new KeyValuePair<String, IReadOnlyList<String>>(x.Key, x.Value.ToList()));

        public override string IfNoneMatch => _IfNoneMatch;

        public override string AcceptEncoding => _AcceptEncoding;

        public override Stream InputStream => _InputStream;

        public override Stream OutputStream => _OutputStream;

        public override long ReqContentLength => _ReqContentLength;


        public override string ProtocolVersion => _ProtocolVersion;

        static readonly IReadOnlyDictionary<String, Action<ManualHttpServerRequest, IReadOnlyList<String>>> ResHeaderSetters = new Dictionary<String, Action<ManualHttpServerRequest, IReadOnlyList<String>>>(StringComparer.Ordinal)
        {
            { "Content-Length", (to, vals) => to.SetResContentLength(long.Parse(vals.First())) },
            { "Content-Type", (to, vals) => to.SetResMime(vals.First()) },
        }.Freeze();


        public override void SetResHeaders(int status, IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> headers, IReadOnlySet<String> ignore)
        {
            ignore = ignore ?? DefaultIgnoreHeaders;
            var ss = ResHeaderSetters;
            var to = this;
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
                if (!ProxyTools.AllowMultipleHeaders.Contains(k.FastToLower()))
                    ResHeaders.Remove(k);
                ResHeaders.TryAddWithoutValidation(k, vals);
            }
            _ResStatusCode = status;
        }

        public override IPAddress GetIP() => _IP;

        public override string GetReqCookie(string name, string cookieString = null)
            => ReqCookies.TryGetValue(name, out var v) ? v : null;

        public override string GetReqHeader(string name)
        {
            var val = ReqHeaders[name];
            //if (ReverseProxyTools.QuotedHeaders.Contains(name.FastToLower()))
                //val = val.RemoveQuotes();
            return val;

        }

        public override string GetResHeader(string name)
        { 
            var val = ResHeaders[name];
            //if (ReverseProxyTools.QuotedHeaders.Contains(name.FastToLower()))
                //val = val.RemoveQuotes();
            return val;
        }

        public override string GetResMime()
            => GetResHeader("Content-Type");

        public override bool IsDead()
            => false;

        public override void SetResBody(ReadOnlySpan<byte> data)
            => (_OutputStream ??= new ArrayPoolStream()).Write(data);

        public override async Task SetResBodyAsync(ReadOnlyMemory<byte> data)
            => await (_OutputStream ??= new ArrayPoolStream()).WriteAsync(data).ConfigureAwait(false);

        public override void SetResBody(Byte[] data, int offset, int length)
            => (_OutputStream ??= new ArrayPoolStream()).Write(data, offset, length);

        public override Task SetResBodyAsync(Byte[] data, int offset, int length)
            => (_OutputStream ??= new ArrayPoolStream()).WriteAsync(data, offset, length);

        public override void SetResContentLength(long length)
        {
            ResHeaders.Remove("Content-Length");
            ResHeaders.TryAddWithoutValidation("Content-Length", length.ToString());
        }

        public override void SetResHeader(string header, string value)
        {
            ResHeaders.Remove(header);
            if (String.IsNullOrEmpty(value))
                return;
            //if (ReverseProxyTools.QuotedHeaders.Contains(header.FastToLower()))
                //value = value.EnsureQuoted();
            ResHeaders.TryAddWithoutValidation(header, value);
        }

        public override void SetResMime(string mime)
        {
            ResHeaders.Remove("Content-Type");
            ResHeaders.TryAddWithoutValidation("Content-Type", mime);
        }

        public override void SetResStatusCode(int statusCode) => _ResStatusCode = statusCode;

        public override int GetResStatusCode() => _ResStatusCode;

        public override void UpdateCookie(string str)
        {
            ResHeaders.TryAddWithoutValidation("Set-Cookie", str);
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
                d.Add(x.Key, x.Value);
            h.Init(Session);
            return h;
        }


    }



}
