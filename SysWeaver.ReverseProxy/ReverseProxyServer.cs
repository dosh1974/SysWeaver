using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.ReverseProxy
{

    [WebApiUrl("../ReverseProxy")]
    public sealed partial class ReverseProxyServer : IHttpServerRawModule, IHaveStats
    {
        public override string ToString() => BaseUrl;

        public ReverseProxyServer(ReverseProxyServerParams p, IMessageHost msg = null)
        {
            p = p ?? new ReverseProxyServerParams();
            HashSet<String> ignoreDomains = new HashSet<String>(StringComparer.Ordinal);
            foreach (var x in p.AllButDomains.Nullable())
            {
                var t = x?.Trim();
                if (!String.IsNullOrEmpty(t))
                    ignoreDomains.Add(t.FastToLower());
            }
            bool haveDomains = ignoreDomains.Count > 0;
            var baseUrl = p.BaseUrl?.Trim('/');
            if (String.IsNullOrEmpty(baseUrl) && (!haveDomains))
                throw new Exception("Invalid base url (must supply a unique URL since ALL requests will be redirected)");
            if (!String.IsNullOrEmpty(baseUrl))
            {
                if (baseUrl.FastEquals("ReverseProxy"))
                    throw new Exception("Invalid base url (must supply a unique URL since ALL requests will be redirected)");
                baseUrl += '/';
                BaseUrl = baseUrl;
                BaseUrlLen = baseUrl.Length;
                msg?.AddMessage(MessageLevels.Info, $"Client url routing: \"{baseUrl}[ClientId]<-EndPoint>/<..url..>\"");
            }
            if (haveDomains)
            {
                IgnoreDomains = ignoreDomains.Freeze();
                msg?.AddMessage("Client domain routing: \"http{s}://[ClientId]<-EndPoint>.*.*/<..url..>\"");
            }
            else
            {
                OnlyForPrefixes = [baseUrl];
            }
        }

        readonly IReadOnlySet<String> IgnoreDomains;
        readonly String BaseUrl;
        readonly int BaseUrlLen;

        readonly ConcurrentDictionary<String, Client> Clients = new (StringComparer.Ordinal);
        public String[] OnlyForPrefixes { get; init; }
        public Task<bool> Handle(HttpServerRequest r)
        {
            var localUrl = r.LocalUrl;
            if (r.DidIndex)
                localUrl = localUrl.Substring(0, localUrl.Length - 10);

            var baseUrlLen = BaseUrlLen;
            var ignoreDomains = IgnoreDomains;
            String clientId, endPoint;
            if (ignoreDomains != null)
            {
                var t = new Uri(r.Url);
                var host = t.Host;
                if (!ignoreDomains.Contains(host.FastToLower()))
                {
                    //  Domain switch
                    clientId = host.SplitFirst('.');
                    clientId = clientId.SplitFirst('-', out endPoint);
                    return HandleClient(r, clientId, endPoint, localUrl, r.Host.Len);
                }
                if ((baseUrlLen <= 0) || (!localUrl.FastStartsWith(BaseUrl)))
                    return TaskExt.FalseTask;
            }
            //  Sub path switch
            var clientUrl = localUrl.Substring(baseUrlLen);
            clientId = clientUrl.SplitFirst('/', out clientUrl);
            if (String.IsNullOrEmpty(clientId))
                throw new HttpResponseException(404);
            if (String.IsNullOrEmpty(clientUrl))
                clientUrl = "";
            var segment = clientId;
            clientId = clientId.SplitFirst('-', out endPoint);
            //  The cookies of a client are scoped to its sub path
            String rootPath = "/";
            if (Uri.TryCreate(r.Prefix, UriKind.Absolute, out var pu))
                rootPath = pu.AbsolutePath;
            if (!rootPath.EndsWith('/'))
                rootPath += '/';
            return HandleClient(r, clientId, endPoint, clientUrl, 1 + baseUrlLen + r.Host.Len + clientId.Length, String.Concat(rootPath, BaseUrl, segment, "/"));
        }

        async Task<bool> HandleClient(HttpServerRequest r, String clientId, String endPoint, String clientUrl, int prefixLength, String cookiePath = null)
        {
            if (!Clients.TryGetValue(clientId, out var client))
                throw new HttpResponseException(503);
            var qs = r.QueryStringStart;
            if (qs > 0)
                clientUrl = String.Concat(clientUrl, '?', r.GetRawQuery());
            var cookiePrefix = GetCookiePrefix(clientId);
            var secure = r.Prefix?.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ?? false;
            await client.Cache.HandleAsync(r, clientUrl, async (url, data) =>
            {
                var req = new ReverseProxyRequest
                {
                    ClientId = clientId,
                    EndPoint = endPoint,
                    RequestId = GetRequestGuid(),
                    Url = clientUrl,
                    Method = data.Method,
                    Headers = FilterRequestCookies(data.Headers, cookiePrefix),
                    Data = data.Data,
                };
                var res = await client.MakeRequest(req).ConfigureAwait(false);
                if (res == null)
                    throw new HttpResponseException(503);
                return new ProxyData(data.Method, RewriteResponseCookies(res.Headers, cookiePrefix, cookiePath, secure), res.Data, res.StatusCode);
            }).ConfigureAwait(false);
            return true;
        }

        #region Cookies

        /// <summary>
        /// The prefix that is added to the names of the cookies that a client sets: "rp.[ClientId]." (only letters, digits, '-' and '_' of the client id are used).
        /// All clients (and the proxy server itself) share the origin of the proxy server in the browser, the prefix keeps their cookies apart:
        /// a client only gets the cookies that it has set (without the prefix), never the cookies of the proxy server (ex: its session cookie) or of other clients.
        /// </summary>
        /// <param name="clientId">The client id</param>
        /// <returns>The cookie name prefix</returns>
        static String GetCookiePrefix(String clientId)
        {
            var sb = new StringBuilder(clientId.Length + 4);
            sb.Append("rp.");
            foreach (var c in clientId)
            {
                if (Char.IsAsciiLetterOrDigit(c) || (c == '-') || (c == '_'))
                    sb.Append(c);
            }
            sb.Append('.');
            return sb.ToString();
        }

        /// <summary>
        /// Only forward the cookies that the client has set (named with the client's prefix, the prefix is removed), the cookies of the proxy server
        /// (session, device id etc) and of other clients are never sent to a client.
        /// </summary>
        /// <param name="headers">The request headers ("Name:value")</param>
        /// <param name="cookiePrefix">The cookie name prefix of the client (see <see cref="GetCookiePrefix"/>)</param>
        /// <returns>The headers to forward</returns>
        static String[] FilterRequestCookies(String[] headers, String cookiePrefix)
        {
            if (headers == null)
                return null;
            List<String> res = null;
            StringBuilder cookies = null;
            var hl = headers.Length;
            for (int i = 0; i < hl; ++i)
            {
                var h = headers[i];
                if (!h.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                {
                    res?.Add(h);
                    continue;
                }
                if (res == null)
                {
                    res = new List<String>(hl);
                    for (int j = 0; j < i; ++j)
                        res.Add(headers[j]);
                }
                foreach (var part in h.Substring(7).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!part.StartsWith(cookiePrefix, StringComparison.Ordinal))
                        continue;
                    var c = part.Substring(cookiePrefix.Length);
                    if (c.Length <= 0 || (c[0] == '='))
                        continue;
                    if (cookies == null)
                        cookies = new StringBuilder(h.Length);
                    else
                        cookies.Append("; ");
                    cookies.Append(c);
                }
            }
            if (res == null)
                return headers;
            if (cookies != null)
                res.Add("Cookie:" + cookies.ToString());
            return res.ToArray();
        }

        /// <summary>
        /// Rewrite the Set-Cookie headers of a client response: the cookie name gets the client's prefix, the path is scoped to the client's sub path
        /// (if any), any Domain attribute is removed and "Secure" is added when the proxy is used over https.
        /// </summary>
        /// <param name="headers">The response headers ("Name:value")</param>
        /// <param name="cookiePrefix">The cookie name prefix of the client (see <see cref="GetCookiePrefix"/>)</param>
        /// <param name="cookiePath">The path of the client on the proxy server, ex: "/ReverseProxyFiles/devtest/", null to keep the path (domain routing)</param>
        /// <param name="secure">True if the proxy request uses https</param>
        /// <returns>The headers</returns>
        static String[] RewriteResponseCookies(String[] headers, String cookiePrefix, String cookiePath, bool secure)
        {
            if (headers == null)
                return null;
            String[] res = null;
            var hl = headers.Length;
            for (int i = 0; i < hl; ++i)
            {
                var h = headers[i];
                if (!h.StartsWith("Set-Cookie:", StringComparison.OrdinalIgnoreCase))
                    continue;
                var parts = h.Substring(11).Split(';', StringSplitOptions.TrimEntries);
                if ((parts.Length <= 0) || (parts[0].Length <= 0))
                    continue;
                var sb = new StringBuilder(h.Length + cookiePrefix.Length + (cookiePath?.Length ?? 0) + 16);
                sb.Append("Set-Cookie:").Append(cookiePrefix).Append(parts[0]);
                bool havePath = false;
                bool haveSecure = false;
                var pl = parts.Length;
                for (int j = 1; j < pl; ++j)
                {
                    var a = parts[j];
                    if (a.Length <= 0)
                        continue;
                    if (a.StartsWith("Domain", StringComparison.OrdinalIgnoreCase) && ((a.Length == 6) || (a[6] == '=') || (a[6] == ' ')))
                        continue;
                    if (a.StartsWith("Path", StringComparison.OrdinalIgnoreCase) && ((a.Length == 4) || (a[4] == '=') || (a[4] == ' ')))
                    {
                        havePath = true;
                        if (cookiePath != null)
                        {
                            var ei = a.IndexOf('=');
                            var p = ei < 0 ? "" : a.Substring(ei + 1).Trim().TrimStart('/');
                            sb.Append(";Path=").Append(cookiePath).Append(p);
                            continue;
                        }
                    }
                    if (a.Equals("Secure", StringComparison.OrdinalIgnoreCase))
                        haveSecure = true;
                    sb.Append(';').Append(a);
                }
                if ((!havePath) && (cookiePath != null))
                    sb.Append(";Path=").Append(cookiePath);
                if (secure && !haveSecure)
                    sb.Append(";Secure");
                res ??= (String[])headers.Clone();
                res[i] = sb.ToString();
            }
            return res ?? headers;
        }

        #endregion//Cookies

        [WebApi]
        [WebApiAuth(Roles.Service)]
        public async Task<ReverseProxyRequest> GetReverseProxyRequest(ReverseProxyResponse response, HttpServerRequest r)
        {
            var clientId = response.ClientId;
            if (response.RequestId != null)
                ReverseProxyResponse(response);
            var clients = Clients;
            if (!clients.TryGetValue(clientId, out var client))
            {
                client = new Client();
                if (!clients.TryAdd(clientId, client))
                    client = clients[clientId];
            }
            Interlocked.Increment(ref client.Connections);
            try
            {
                var now = DateTime.UtcNow;
                var end = now.AddSeconds(30);
                Interlocked.Exchange(ref client.Ip, r.GetIpAddress());
                Interlocked.Exchange(ref client.LastConnection, now.Ticks);
                var p = client.Pending;
                var w = client.Waiter;
                long cc = 0;
                for (; ; )
                {
                    if (p.TryDequeue(out var res))
                    {
                        Interlocked.Increment(ref client.TotalSent);
                        Interlocked.Increment(ref client.InProgress);
                        return res;
                    }
                    var wait = (int)(end - DateTime.UtcNow).TotalMilliseconds ;
                    if (wait < 1)
                        return null;
                    cc = await w.WaitForChange(cc, wait).ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Decrement(ref client.Connections);
            }
        }

        long UnknownClientResponses;
        long UnknownRequestResponses;

        void ReverseProxyResponse(ReverseProxyResponse response)
        {
            var clients = Clients;
            if (!clients.TryGetValue(response.ClientId, out var client))
            {
                Interlocked.Increment(ref UnknownClientResponses);
                return;
            }
            if (!client.Responses.TryAdd(response.RequestId, response))
            {
                Interlocked.Increment(ref UnknownRequestResponses);
                return;
            }
            Interlocked.Increment(ref client.TotalCompleted);
            Interlocked.Decrement(ref client.InProgress);
            client.ResponseWaiter.Change();
        }


        long RequestId = (DateTime.UtcNow - new DateTime(2026, 1, 1)).Ticks;

        String GetRequestGuid()
        {
            var t = Interlocked.Increment(ref RequestId);
            return CompactAsciiString.Secure.Encode(t);
        }

        /// <summary>
        /// Test a reverse proxy connection manually
        /// </summary>
        /// <param name="r"></param>
        /// <exception cref="HttpResponseException"></exception>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        public async Task<ReverseProxyResponse> DebugReverseProxyRequest(ReverseProxyRequest r)
        {
            if (!Clients.TryGetValue(r.ClientId, out var c))
                throw new HttpResponseException(503);
            r.RequestId = GetRequestGuid();
            return await c.MakeRequest(r).ConfigureAwait(false);
        }

        /// <summary>
        /// Get brief information about all reverse proxy clients
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        public TypedTableData<ReverseProxyClientBrief> GetClientBriefs(TableDataRequest r)
        {
            var b = BaseUrl;
            return TableDataTools.GetTyped(r, 2000, Clients.Select(x => new ReverseProxyClientBrief(x, b)));
        }


        /// <summary>
        /// Get detailed information about all reverse proxy clients
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Operations/{0}", "Reverse proxy clients", null, "icons/world.svg", 32)]
        public TypedTableData<ReverseProxyClientDetail> GetClientDetails(TableDataRequest r)
        {
            var b = BaseUrl;
            return TableDataTools.GetTyped(r, 2000, Clients.Select(x => new ReverseProxyClientDetail(x, b)));
        }

        public IEnumerable<Stats> GetStats()
        {
            const String sys = nameof(ReverseProxyServer);
            var t = Interlocked.Read(ref UnknownClientResponses);
            if (t > 0)
                yield return new Stats(sys, "UnknownClientResponses", t, "Number of repsonse messages (sent from a client) that contained an unknown client id");
            t = Interlocked.Read(ref UnknownRequestResponses);
            if (t > 0)
                yield return new Stats(sys, "UnknownRequestResponses", t, "Number of repsonse messages (sent from a client) that contained an unknown response id");
        }
    }

}
