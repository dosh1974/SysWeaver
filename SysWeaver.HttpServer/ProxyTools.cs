using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    public class ProxyData
    {
        #if DEBUG
        public override string ToString() => String.Concat(Method, " (", StatusCode, ") @ ", Data?.Length ?? 0, " bytes");
        
        #endif//DEBUG

        public HttpServerMethods Method;
        public String[] Headers;
        public Byte[] Data;
        public int StatusCode;
        public ProxyData()
        {
        }

        public ProxyData(HttpServerMethods method, string[] headers, byte[] data = null, int statusCode = 0)
        {
            Method = method;
            Headers = headers;
            Data = data;
            StatusCode = statusCode;
        }
    }

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



        public static String[] EncodeHeaders(params IEnumerable<KeyValuePair<string, IEnumerable<string>>>[] headers)
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
        /// Get headers and other data required to proxy a request
        /// </summary>
        /// <param name="r"></param>
        /// <param name="prefixLength"></param>
        /// <returns></returns>
        /// <exception cref="HttpResponseException"></exception>
        public static async ValueTask<ProxyData> GetFromRequest(HttpServerRequest r, int? prefixLength = 0)
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
        /// Ser response headers from the result of a proxied request
        /// </summary>
        /// <param name="r"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static ValueTask SetToRequest(HttpServerRequest r, ProxyData data)
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
            return d == null ? TaskExt.CompValTask : r.SetResBodyAsync(d);
        }

        /// <summary>
        /// Make a proxied request 
        /// </summary>
        /// <param name="c"></param>
        /// <param name="url">The url to do the request against</param>
        /// <param name="data">The input data</param>
        /// <returns></returns>
        public static async ValueTask<ProxyData> ProxyRequest(HttpClient c, String url, ProxyData data)
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
                var resData = Encoding.UTF8.GetBytes(ex.Message + " [500]");
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
