using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// A request or response that is passed through a proxy: method, headers ("Name:value" strings), body and status code.
    /// </summary>
    public class ProxyData
    {
        #if DEBUG
        public override string ToString() => String.Concat(Method, " (", StatusCode, ") @ ", Data?.Length ?? 0, " bytes");
        
        #endif//DEBUG

        /// <summary>
        /// The http method.
        /// </summary>
        public HttpServerMethods Method;
        /// <summary>
        /// The headers as "Name:value" strings (see <see cref="ProxyTools.EncodeHeaders{T}"/>), may be null.
        /// </summary>
        public String[] Headers;
        /// <summary>
        /// The body, may be null.
        /// </summary>
        public Byte[] Data;
        /// <summary>
        /// The http status code (responses only).
        /// </summary>
        public int StatusCode;
        /// <summary>
        /// Create an empty instance (for serialization).
        /// </summary>
        public ProxyData()
        {
        }

        /// <summary>
        /// Create an instance.
        /// </summary>
        /// <param name="method">The http method</param>
        /// <param name="headers">The headers as "Name:value" strings</param>
        /// <param name="data">The body</param>
        /// <param name="statusCode">The http status code</param>
        public ProxyData(HttpServerMethods method, string[] headers, byte[] data = null, int statusCode = 0)
        {
            Method = method;
            Headers = headers;
            Data = data;
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// Helpers for proxying http requests (read a request, forward it using an <see cref="HttpClient"/> and write the response back).
    /// </summary>
    /// <remarks>
    /// All request headers except Host, Upgrade-Insecure-Requests and Transfer-Encoding are forwarded, including Cookie and Authorization.
    /// No X-Forwarded-For / Forwarded headers are added.
    /// </remarks>
    public static class ProxyTools
    {

        static readonly IReadOnlySet<String> ContentHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "content-length",
            "content-type",
            "content-encoding"
        );


        static readonly IReadOnlySet<String> IgnoreHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "host",
/*            "sec-ch-ua",
            "sec-ch-ua-mobile",
            "sec-ch-ua-platform",
            "sec-fetch-site",
            "sec-fetch-dest",
            "sec-fetch-mode",
            "sec-fetch-user",
*/            "upgrade-insecure-requests",
            "transfer-encoding"
        );


        static readonly IReadOnlySet<String> QuotedHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "if-none-match",
            "etag"
        );

        /// <summary>
        /// Lower cased names of headers that may occur multiple times (each value is kept as a separate header).
        /// </summary>
        public static readonly IReadOnlySet<String> AllowMultipleHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "set-cookie"
        );

        static readonly IReadOnlyDictionary<String, Action<HttpServerRequest, String>> SpecialHeaders = new Dictionary<String, Action<HttpServerRequest, String>>(StringComparer.Ordinal)
            {
                { "Content-Type", (req, value) => req.SetResMime(value) },
                { "Content-Length", (req, value) => req.SetResContentLength(long.Parse(value)) },
                { "Set-Cookie", (req, value) => req.UpdateCookie(value) },
            }.Freeze();



        static readonly IEnumerable<KeyValuePair<string, IEnumerable<string>>> EmptyHeaders = Array.Empty<KeyValuePair<string, IEnumerable<string>>>();



        /// <summary>
        /// Encode header collections into "Name:value" strings, ignored headers (host etc) are skipped.
        /// Multiple values are joined with ',' except for headers in <see cref="AllowMultipleHeaders"/>.
        /// </summary>
        /// <typeparam name="T">The type of the header values</typeparam>
        /// <param name="headers">The header collections, null collections are skipped</param>
        /// <returns>The encoded headers</returns>
        public static String[] EncodeHeaders<T>(params IEnumerable<KeyValuePair<string, T>>[] headers) where T : IEnumerable<String>
        {
            List<String> h = new List<string>(16);
            var l = headers.Length;
            var ih = IgnoreHeaders;
            var am = AllowMultipleHeaders;
            for (int i = 0; i < l; ++i)
            {
                var hlist = headers[i];
                if (hlist == null)
                    continue;
                foreach (var kv in hlist)
                {
                    var key = kv.Key;
                    var kl = key.FastToLower();
                    if (ih.Contains(kl))
                        continue;
                    if (am.Contains(kl))
                    {
                        foreach (var v in kv.Value)
                            h.Add(String.Concat(key, ':', v));
                        continue;
                    }
                    h.Add(String.Concat(key, ':', String.Join(',', kv.Value)));
                }
            }
            return h.ToArray();
        }



        /// <summary>
        /// Get headers and other data required to proxy a request (the body is read for POST requests).
        /// </summary>
        /// <param name="r">The request</param>
        /// <param name="prefixLength">Number of chars to remove from the start of the Referer header value, null to use the length of the request's host prefix (making it relative).
        /// The default 0 keeps the Referer as is.</param>
        /// <returns>The request data</returns>
        /// <exception cref="HttpResponseException">Thrown (404) if the method isn't GET, HEAD or POST</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the Referer is shorter than the prefix length</exception>
        public static async Task<ProxyData> GetFromRequest(HttpServerRequest r, int? prefixLength = 0)
        {
            var m = r.HttpMethod;
            if (m == HttpServerMethods.Other)
                throw new HttpResponseException(404);
            Byte[] data = null;
            if (m == HttpServerMethods.POST)
                data = await r.InputStream.ReadAllBytesAsync().ConfigureAwait(false);
            var headers = ProxyTools.EncodeHeaders(r.AllReqHeaders);
            var hl = headers.Length;
            var pl = prefixLength ?? r.Host.Len;
            for (int i = 0; i < hl; ++i)
            {
                var h = headers[i];
                if (h.StartsWith("Referer:", StringComparison.OrdinalIgnoreCase))
                {
                    var t = h.Substring(8).Trim().Substring(pl);
                    headers[i] = "Referer:" + t;
                }
            }
            return new ProxyData(m, headers, data);
        }

        /// <summary>
        /// Set the response status, headers (Content-Type, Content-Length and Set-Cookie are handled specially) and body of a request from a proxied response.
        /// </summary>
        /// <param name="r">The request to write the response to</param>
        /// <param name="data">The proxied response</param>
        /// <returns>A task that completes when the body has been written</returns>
        public static Task SetToRequest(HttpServerRequest r, ProxyData data)
        {
            var sh = SpecialHeaders;
            foreach (var h in data.Headers.Nullable())
            {
                var key = h.SplitFirst(':', out var value);
                if (sh.TryGetValue(key, out var fn))
                    fn(r, value);
                else
                    r.SetResHeader(key, value);
            }
            r.SetResStatusCode(data.StatusCode);
            var d = data.Data;
            return d == null ? Task.CompletedTask: r.SetResBodyAsync(d, 0, d.Length);
        }

        /// <summary>
        /// Make a proxied request.
        /// </summary>
        /// <param name="c">The http client to use</param>
        /// <param name="url">The url to do the request against</param>
        /// <param name="data">The input data (method, headers and body)</param>
        /// <returns>The response (the whole body is read into memory).
        /// Exceptions are not thrown, they are returned as a 500 response with the exception message (sensitive information removed, see <see cref="ExceptionExt.SafeMessage"/>) as the body.</returns>
        public static async Task<ProxyData> ProxyRequest(HttpClient c, String url, ProxyData data)
        {
            var httpMethod = data.Method;
            var method = new HttpMethod(httpMethod.ToString());
            using var localRequest = new HttpRequestMessage(method, url);
            HttpContent content = null;
            try
            {
                if (httpMethod == HttpServerMethods.POST)
                {
                    var postData = data.Data;
                    if (postData != null)
                    {
                        content = new ReadOnlyMemoryContent(postData);
                        localRequest.Content = content;
                    }
                }
                var h = localRequest.Headers;
                var ch = ProxyTools.ContentHeaders;
                // TODO: Set: X-Forwarded-For:
                foreach (var x in data.Headers.Nullable())
                {
                    var key = x.SplitFirst(':', out var value);
                    if (ch.Contains(key.FastToLower()))
                        content?.Headers?.TryAddWithoutValidation(key, value);
                    else
                        h.TryAddWithoutValidation(key, value);
                }
                using var localResponse = await c.SendAsync(localRequest).ConfigureAwait(false);
                var resData = await localResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return new ProxyData(data.Method, EncodeHeaders(localResponse.Headers, localResponse.Content?.Headers), resData, (int)localResponse.StatusCode);
            }
            catch (Exception ex)
            {
                //  Sensitive information (internal host names, private ips etc) is removed from the message
                var resData = Encoding.UTF8.GetBytes(ex.SafeMessage() + " [500]");
                return new ProxyData(data.Method, new String[]
                    {
                        "Content-Length:" + resData.Length,
                        "Content-Type:" + MimeTypeMap.PlainText
                    }, resData, 500);
            }
            finally
            {
                content?.Dispose();
            }
        }

    }

}
