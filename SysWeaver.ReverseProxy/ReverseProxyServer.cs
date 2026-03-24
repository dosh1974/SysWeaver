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
                if (!String.IsNullOrEmpty(x))
                    ignoreDomains.Add(x.FastToLower());
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
                msg?.AddMessage(String.Concat("Client url routing: \"", baseUrl, "[ClientId]<-EndPoint>/<..url..>\""));
            }
            if (haveDomains)
            {
                IgnoreDomains = ignoreDomains.Freeze();
                msg?.AddMessage(String.Concat("Client domain routing: \"http{s}://[ClientId]<-EndPoint>.*.*/<..url..>\""));
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
        public ValueTask<bool> Handle(HttpServerRequest r)
        {
            var localUrl = r.LocalUrl;
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
                    return HandleClient(r, clientId, endPoint, localUrl, r.Host.Len, r.Host.Name);
                }
                if ((baseUrlLen <= 0) || (!localUrl.FastStartsWith(BaseUrl)))
                    return TaskExt.FalseValueTask;
            }
            //  Sub path switch
            var clientUrl = localUrl.Substring(baseUrlLen);
            clientId = clientUrl.SplitFirst('/', out clientUrl);
            var referrer = String.Concat(r.Host, clientId, '/');
            if (String.IsNullOrEmpty(clientId))
                throw new HttpResponseException(404);
            if (String.IsNullOrEmpty(clientUrl))
                clientUrl = "";
            clientId = clientId.SplitFirst('-', out endPoint);
            return HandleClient(r, clientId, endPoint, clientUrl, 1 + baseUrlLen + r.Host.Len + clientId.Length, referrer);
        }

        async ValueTask<bool> HandleClient(HttpServerRequest r, String clientId, String endPoint, String clientUrl, int prefixLength, String referrer)
        {
            var m = r.HttpMethod;
            if (m == HttpServerMethods.Other)
                throw new HttpResponseException(404);

            if (!Clients.TryGetValue(clientId, out var client))
                throw new HttpResponseException(503);

            var qs = r.QueryStringStart;
            if (qs > 0)
                clientUrl += r.Url.Substring(qs - 1);

            Byte[] data = null;
            if (m == HttpServerMethods.POST)
                data = await r.InputStream.ReadAllBytesAsync().ConfigureAwait(false);
            var headers = ReverseProxyTools.EncodeHeaders(r.AllReqHeaders);
            var hl = headers.Length;
            for (int i = 0; i < hl; ++i)
            {
                var h = headers[i];
                if (h.StartsWith("Referer:", StringComparison.OrdinalIgnoreCase))
                {
                    var t = h.Substring(8).Trim().Substring(prefixLength);
                    headers[i] = "Referer:" + t;
                }
            }
            var req = new ReverseProxyRequest
            {
                ClientId = clientId,
                EndPoint = endPoint,
                RequestId = GetRequestGuid(),
                Url = clientUrl,
                Method = m,
                Data = data,
                Headers = headers,
            };
            var res = await client.MakeRequest(req).ConfigureAwait(false);
            if (res == null)
                throw new HttpResponseException(503);
            var sh = SpecialHeaders;
            foreach (var h in res.Headers.Nullable())
            {
                var key = h.SplitFirst(':', out var value);
                if (sh.TryGetValue(key, out var fn))
                    fn(r, value);
                else
                    r.SetResHeader(key, value);
            }
            r.SetResStatusCode(res.StatusCode);
            data = res.Data;
            if (data != null)
                await r.SetResBodyAsync(data).ConfigureAwait(false);
            return true;

        }


        static readonly IReadOnlyDictionary<String, Action<HttpServerRequest, String>> SpecialHeaders = new Dictionary<String, Action<HttpServerRequest, String>>(StringComparer.Ordinal)
            {
                { "Content-Type", (req, value) => req.SetResMime(value) },
                { "Content-Length", (req, value) => req.SetResContentLength(long.Parse(value)) },
                { "Set-Cookie", (req, value) => req.UpdateCookie(value) },
            }.Freeze();



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
        /// <param name="r">Paramaters</param>
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
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Debug/Http Server/{0}", "Reverse proxy clients", null, "icons/world.svg")]
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
