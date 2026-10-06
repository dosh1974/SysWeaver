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
    /// <summary>
    /// An in-memory request, used to run requests through the server pipeline from code (see <see cref="HttpServerBase.InternalRead(String, HttpSession, String)"/>).
    /// Request data is set using the public fields, the response is captured in <see cref="ResHeaders"/>, <see cref="_ResStatusCode"/> and <see cref="_OutputStream"/>
    /// (an <see cref="ArrayPoolStream"/> created on first write).
    /// </summary>
    /// <remarks>
    /// Not thread safe. Disposing the request disposes the input and output streams.
    /// The client is never "dead" and the IP defaults to the loopback address.
    /// </remarks>
    public sealed class ManualHttpServerRequest : HttpServerRequest
    {

        /// <summary>
        /// Create a request.
        /// </summary>
        /// <param name="httpMethod">The http method, ex: "GET"</param>
        /// <param name="url">The absolute (decoded) url, also used as the raw url</param>
        /// <param name="prefix">The prefix of the url (from <see cref="HttpServerBase.GetHost"/>)</param>
        /// <param name="server">The server</param>
        /// <param name="host">The host</param>
        /// <param name="queryStart">The index of the '?' in the url, -1 if there is no query string</param>
        /// <param name="didIndex">True if "index.html" was added to the url</param>
        public ManualHttpServerRequest(String httpMethod, String url, String prefix, HttpServerBase server, HttpServerHostInfo host, int queryStart, bool didIndex = false)
            : base(httpMethod, url, url, prefix, server, host, queryStart, didIndex)
        {
        }

        /// <summary>
        /// Dispose the custom data, the input stream and the output stream.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            Interlocked.Exchange(ref _InputStream, null)?.Dispose();
            Interlocked.Exchange(ref _OutputStream, null)?.Dispose();
        }

        /// <summary>
        /// The request body, may be null.
        /// </summary>
        public Stream _InputStream;
        
        /// <summary>
        /// The request content length.
        /// </summary>
        public long _ReqContentLength;
        /// <summary>
        /// The http protocol version.
        /// </summary>
        public string _ProtocolVersion = "1.1";
        /// <summary>
        /// The Accept-Encoding request header, null means no compression.
        /// </summary>
        public string _AcceptEncoding;
        /// <summary>
        /// The If-None-Match request header.
        /// </summary>
        public string _IfNoneMatch;

        /// <summary>
        /// The client IP address.
        /// </summary>
        public IPAddress _IP = IPAddress.Loopback;
        /// <summary>
        /// The request cookies, must be set before <see cref="GetReqCookie"/> is used (null throws).
        /// </summary>
        public IReadOnlyDictionary<String, String> ReqCookies;
        /// <summary>
        /// The request headers.
        /// </summary>
        public Headers ReqHeaders = new Headers();


        /// <summary>
        /// A header collection (no validation).
        /// </summary>
        public sealed class Headers : HttpHeaders
        {
            /// <summary>
            /// Get the values of a header joined with ';', null if the header isn't present.
            /// </summary>
            /// <param name="key">The header name (case insensitive)</param>
            public String this[String key] 
            {
                get => TryGetValues(key, out var vals) ? String.Join(';', vals) : null;
            }
        }

        /// <summary>
        /// The response body, created on first write. Shared (not copied) by <see cref="ReplaceUrl"/>.
        /// </summary>
        public Stream _OutputStream;
        /// <summary>
        /// The response status code.
        /// </summary>
        public int _ResStatusCode;
        /// <summary>
        /// The response headers.
        /// </summary>
        public readonly Headers ResHeaders = new Headers();

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllReqHeaders => ReqHeaders.Select(x => new KeyValuePair<String, IReadOnlyList<String>>(x.Key, x.Value.ToList()));
        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> AllResHeaders => ResHeaders.Select(x => new KeyValuePair<String, IReadOnlyList<String>>(x.Key, x.Value.ToList()));

        /// <inheritdoc/>
        public override string IfNoneMatch => _IfNoneMatch;

        /// <inheritdoc/>
        public override string AcceptEncoding => _AcceptEncoding;

        /// <inheritdoc/>
        public override Stream InputStream => _InputStream;

        /// <inheritdoc/>
        public override Stream OutputStream => _OutputStream;

        /// <inheritdoc/>
        public override long ReqContentLength => _ReqContentLength;


        /// <inheritdoc/>
        public override string ProtocolVersion => _ProtocolVersion;

        static readonly IReadOnlyDictionary<String, Action<ManualHttpServerRequest, IReadOnlyList<String>>> ResHeaderSetters = new Dictionary<String, Action<ManualHttpServerRequest, IReadOnlyList<String>>>(StringComparer.Ordinal)
        {
            { "Content-Length", (to, vals) => to.SetResContentLength(long.Parse(vals.First())) },
            { "Content-Type", (to, vals) => to.SetResMime(vals.First()) },
        }.Freeze();


        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override IPAddress GetIP() => _IP;

        /// <summary>
        /// Get a cookie from <see cref="ReqCookies"/> (the cookie string is ignored).
        /// </summary>
        /// <param name="name">The cookie name</param>
        /// <param name="cookieString">Ignored</param>
        /// <returns>The value or null if not found</returns>
        /// <exception cref="NullReferenceException">Thrown if <see cref="ReqCookies"/> is null</exception>
        public override string GetReqCookie(string name, string cookieString = null)
            => ReqCookies.TryGetValue(name, out var v) ? v : null;

        /// <inheritdoc/>
        public override string GetReqHeader(string name)
        {
            var val = ReqHeaders[name];
            //if (ReverseProxyTools.QuotedHeaders.Contains(name.FastToLower()))
                //val = val.RemoveQuotes();
            return val;

        }

        /// <inheritdoc/>
        public override string GetResHeader(string name)
        { 
            var val = ResHeaders[name];
            //if (ReverseProxyTools.QuotedHeaders.Contains(name.FastToLower()))
                //val = val.RemoveQuotes();
            return val;
        }

        /// <inheritdoc/>
        public override string GetResMime()
            => GetResHeader("Content-Type");

        /// <inheritdoc/>
        public override bool IsDead()
            => false;

        /// <inheritdoc/>
        public override void SetResBody(ReadOnlySpan<byte> data)
            => (_OutputStream ??= new ArrayPoolStream()).Write(data);

        /// <inheritdoc/>
        public override async Task SetResBodyAsync(ReadOnlyMemory<byte> data)
            => await (_OutputStream ??= new ArrayPoolStream()).WriteAsync(data).ConfigureAwait(false);

        /// <inheritdoc/>
        public override void SetResBody(Byte[] data, int offset, int length)
            => (_OutputStream ??= new ArrayPoolStream()).Write(data, offset, length);

        /// <inheritdoc/>
        public override Task SetResBodyAsync(Byte[] data, int offset, int length)
            => (_OutputStream ??= new ArrayPoolStream()).WriteAsync(data, offset, length);

        /// <inheritdoc/>
        public override void SetResContentLength(long length)
        {
            ResHeaders.Remove("Content-Length");
            ResHeaders.TryAddWithoutValidation("Content-Length", length.ToString());
        }

        /// <inheritdoc/>
        public override void SetResHeader(string header, string value)
        {
            ResHeaders.Remove(header);
            if (String.IsNullOrEmpty(value))
                return;
            //if (ReverseProxyTools.QuotedHeaders.Contains(header.FastToLower()))
                //value = value.EnsureQuoted();
            ResHeaders.TryAddWithoutValidation(header, value);
        }

        /// <inheritdoc/>
        public override void SetResMime(string mime)
        {
            ResHeaders.Remove("Content-Type");
            ResHeaders.TryAddWithoutValidation("Content-Type", mime);
        }

        /// <inheritdoc/>
        public override void SetResStatusCode(int statusCode) => _ResStatusCode = statusCode;

        /// <inheritdoc/>
        public override int GetResStatusCode() => _ResStatusCode;

        /// <inheritdoc/>
        public override void UpdateCookie(string str)
        {
            ResHeaders.TryAddWithoutValidation("Set-Cookie", str);
        }


        /// <summary>
        /// Create a new manual request for another url, sharing the request data, cookies, headers (same instance), session, input and output streams.
        /// </summary>
        /// <param name="newUrl">The new (decoded) url</param>
        /// <param name="host">The host of the new url</param>
        /// <param name="prefix">The prefix of the new url</param>
        /// <param name="queryStart">The index of the '?' in the new url, -1 if there is no query string</param>
        /// <param name="server">The server</param>
        /// <param name="newMethod">The new http method, null to keep the current</param>
        /// <returns>The new request (didIndex is always false)</returns>
        /// <remarks>Disposing the new request also disposes the shared streams.</remarks>
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
