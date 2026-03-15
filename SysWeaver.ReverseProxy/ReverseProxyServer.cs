using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.ReverseProxy
{
    public sealed class ReverseProxyServerParams
    {
        public String BaseUrl = "ReverseProxyFile";
    }

    [WebApiUrl("../ReverseProxy")]
    public sealed partial class ReverseProxyServer : IHttpServerRawModule
    {

        public async ValueTask<bool> Handle(HttpServerRequest r)
        {
            var m = r.HttpMethod;
            if (m == HttpServerMethods.Other)
                throw new HttpResponseException(404);
            var url = r.LocalUrl.Substring(BaseUrlLen);
            var clientId = url.SplitFirst('/', out url);
            if (String.IsNullOrEmpty(clientId))
                throw new HttpResponseException(404);
            if (String.IsNullOrEmpty(url))
                url = "";
            if (!Clients.TryGetValue(clientId, out var client))
                throw new HttpResponseException(503);
            Byte[] data = null;
            if (m == HttpServerMethods.POST)
                data = await r.InputStream.ReadAllBytesAsync().ConfigureAwait(false);
            var req = new ReverseProxyRequest
            {
                ClientId = clientId,
                RequestId = GetRequestGuid(),
                Url = url,
                Method = m,
                Data = data,
                Headers = r.AllReqHeaders.Select(x => String.Join(':', x.Key, x.Value)).ToArray(),
            };
            var res = await client.MakeRequest(req).ConfigureAwait(false);
            if (res == null)
                throw new HttpResponseException(503);
            foreach (var h in res.Headers.Nullable())
            {
                var key = h.SplitFirst(':', out var value);
                if (key.FastEquals("Content-Type"))
                    r.SetResMime(value);
                else
                    r.SetResHeader(key, value);
            }
            r.SetResStatusCode(res.StatusCode);
            data = res.Data;
            if (data != null)
                await r.SetResBodyAsync(data).ConfigureAwait(false);
            return true;
        }
        
        public String[] OnlyForPrefixes { get; init; }

        public override string ToString() => BaseUrl;

        public ReverseProxyServer(ReverseProxyServerParams p)
        {
            p = p ?? new ReverseProxyServerParams();
            var baseUrl = p.BaseUrl?.Trim('/');
            if (String.IsNullOrEmpty(baseUrl))
                throw new Exception("Invalid base url (must supply a unique URL since ALL requests will be redirected)");
            if (baseUrl.FastEquals("ReverseProxy"))
                throw new Exception("Invalid base url (must supply a unique URL since ALL requests will be redirected)");
            baseUrl += '/';
            BaseUrl = baseUrl;
            BaseUrlLen = baseUrl.Length;
            OnlyForPrefixes = [baseUrl];
        }

        readonly String BaseUrl;
        readonly int BaseUrlLen;

        readonly ConcurrentDictionary<String, Client> Clients = new (StringComparer.Ordinal);


        [WebApi]
        [WebApiAuth(Roles.Service)]
        public async Task<ReverseProxyRequest> GetReverseProxyRequest(string clientId, HttpServerRequest r)
        {
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

        [WebApi]
        [WebApiAuth(Roles.Service)]
        public Task ReverseProxyResponse(ReverseProxyResponse response)
        {
            var clients = Clients;
            if (!clients.TryGetValue(response.ClientId, out var client))
                return Task.CompletedTask;
            if (!client.Responses.TryAdd(response.RequestId, response))
                return Task.CompletedTask;
            Interlocked.Increment(ref client.TotalCompleted);
            Interlocked.Decrement(ref client.InProgress);
            client.ResponseWaiter.Change();
            return Task.CompletedTask;
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
        /// All mime types that the web server recognizes
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Http Server/{0}", "Reverse connections", null, "icons/world.svg")]
        public TableData ReverseConnectionsTable(TableDataRequest r)
            => TableDataTools.Get(r, 2000, Clients.Select(x => new Data(x)));



    }

}
