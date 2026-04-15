using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Remote.Connection;

namespace SysWeaver.ReverseProxy
{


    public sealed partial class ReverseProxyClient : IDisposable, IHaveStats
    {
        static readonly Char[] TrimChars = " \t\r\n/".ToCharArray();

        public static void ValidateEndPointName(String s)
        {
            foreach (var c in s)
            {
                if ((c >= 'a') && (c <= 'z'))
                    continue;
                if ((c >= 'A') && (c <= 'Z'))
                    continue;
                if ((c >= '0') && (c <= '9'))
                    continue;
                throw new Exception(String.Concat("Invalid char '", c, "' found in end point name \"" + s + "\", only a-z, A-Z or 0-9 is allowed!"));
            }
        }
        public static String SanitizeClientId(String s)
        {
            var l = s.Length;
            Span<Char> temp = stackalloc Char[l];
            int o = 0;
            for (int i = 0; i < l; ++ i)
            {
                var c = s[i];
                if ((c >= 'a') && (c <= 'z'))
                {
                    temp[o] = c;
                    ++o;
                    continue;
                }
                if ((c >= 'A') && (c <= 'Z'))
                {
                    temp[o] = c;
                    ++o;
                    continue;
                }
                if ((c >= '0') && (c <= '9'))
                {
                    temp[o] = c;
                    ++o;
                    continue;
                }
            }
            if (o == 0)
                throw new Exception(String.Concat("Invalid client id \"", s, "\", only a-z, A-Z or 0-9 is allowed!"));
            if (o == l)
                return s;
            return new string(temp[..o]);
        }

        public ReverseProxyClient(ServiceManager manager, ReverseProxyClientParams p)
        {
            var maxThreads = Environment.ProcessorCount;
            var threadCount = p.MaxConcurrentRequests;
            threadCount = threadCount > 0 ? threadCount : (maxThreads + threadCount);
            if (threadCount <= 0)
                threadCount = 1;
            Params = p;
            p.TimeoutInMilliSeconds = 15 * 60 * 1000;
            var testId = EnvInfo.ResolveText(p.ClientId ?? "$(MachineName)");
            var clientId = SanitizeClientId(testId);
            if (!testId.FastEquals(clientId))
                manager.AddMessage(String.Concat("Evaluated client id \"", testId, "\" was reduced to \"", clientId, "\", only a-z, A-Z or 0-9 is allowed!"), MessageLevels.Warning);
            else
                manager.AddMessage(String.Concat("Reverse proxy client id: \"", clientId, '"'));
            ClientId = clientId;
            Manager = manager;
            if (!SetLocalServer(manager.TryGet<HttpServerBase>()))
                manager.OnServiceAdded += OnServiceAdded;
            Dictionary<String, LocalEndPoint> endPoints = new Dictionary<string, LocalEndPoint>(StringComparer.Ordinal);
            using (var _ = manager.Tab())
            {
                foreach (var exp in p.EndPoints.Nullable())
                {
                    var name = exp.SplitFirst(':', out var ep).Trim(TrimChars);
                    ValidateEndPointName(name);
                    endPoints.Add(name, new LocalEndPoint(name, ep));
                    manager.AddMessage(String.Concat('"', name, "\" => \"", ep, '"'));
                }
            }
            if (endPoints.Count <= 0)
                endPoints.Add("", new LocalEndPoint("", ""));
            EndPoints = endPoints.Freeze();
            if (endPoints.Count == 1)
                SingleEndPoint = endPoints.FirstOrDefault().Value;
            ServerBaseUrl = String.Concat(p.BaseUrl.TrimEnd('/'), '/', p.ServerBaseUrl ?? "ReverseProxyFiles", '/', clientId, '-');


            Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> certValid = null;
            if (p.IgnoreCertErrors)
                certValid = (requestMessage, certificate, chain, sslErrors) => true;

            var handler = new HttpClientTimeoutHandler
            {
                DefaultTimeout = TimeSpan.FromMinutes(6),
                InnerHandler = new HttpClientHandler
                {
                    MaxConnectionsPerServer = threadCount,
                    ClientCertificateOptions = certValid == null ? ClientCertificateOption.Automatic : ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = certValid,
                    AutomaticDecompression = System.Net.DecompressionMethods.None,
                }
            };
            ClientHandler = handler;
            Client = new HttpClient(handler);
            ConnectionTasks = Enumerable.Range(0, threadCount).Select(x => new PeriodicTask(Connection, 1)).ToArray();
        }

        HttpClient Client;
        DelegatingHandler ClientHandler;
        readonly String ServerBaseUrl;
        readonly LocalEndPoint SingleEndPoint;
        readonly IReadOnlyDictionary<String, LocalEndPoint> EndPoints;


        public IEnumerable<Stats> GetStats()
        {
            const String sys = nameof(ReverseProxyClient);
            foreach (var x in ConnectFails.GetStats(sys, "ConnectionFails."))
                yield return x;
            foreach (var x in ProxyFails.GetStats(sys, "ProxyFails."))
                yield return x;
            foreach (var x in EndPointFails.GetStats(sys, "EndPointFails."))
                yield return x;
        }

        readonly ReverseProxyClientParams Params;
        readonly AsyncLock CreateConnectionLock = new AsyncLock();
        readonly ExceptionTracker ConnectFails = new ExceptionTracker();
        readonly ExceptionTracker ProxyFails = new ExceptionTracker();
        readonly ExceptionTracker EndPointFails = new ExceptionTracker();




        void SetErrorResponse(ReverseProxyResponse response, String requestId, String message, int statusCode)
        {
            var resData = Encoding.UTF8.GetBytes(message);
            response.Data = resData;
            response.RequestId = requestId;
            response.Headers = [
                "Content-Length:" + resData.Length,
                "Content-Type:" + MimeTypeMap.PlainText,
                ];
            response.StatusCode = statusCode;
        }

        async ValueTask<bool> Connection(CancellationToken cancel)
        {
            if (IsDisposing)
                return false;
            var local = Local;
            if (local == null)
            {
                await Task.Delay(100, cancel).ConfigureAwait(false);
                return true;
            }
            var server = Server;
            if (server == null)
            {
                using var _ = await CreateConnectionLock.Lock().ConfigureAwait(false);
                if (IsDisposing)
                    return false;
                server = Server;
                if (server == null)
                {
                    try
                    {
                        server = Params.Create<IReverseProxyServer>();
                        Server = server;
                    }
                    catch (Exception ex)
                    {
                        ConnectFails.OnException(ex);
                        if (IsDisposing)
                            return false;
                        await Task.Delay(15000, cancel).ConfigureAwait(false);
                        return true;
                    }
                }
            }
            ReverseProxyResponse response = new ReverseProxyResponse
            {
                ClientId = ClientId,
            };
            var eps = EndPoints;
            for (; ; )
            {
                ReverseProxyRequest res;
                try
                {
                    res = await server.GetReverseProxyRequest(response).ConfigureAwait(false);
                    if (res == null)
                        return true;
                }
                catch (Exception ex)
                {
                    ProxyFails.OnException(ex);
                    if (IsDisposing)
                        return false;
                    await Task.Delay(5000, cancel).ConfigureAwait(false);
                    var aa = ex as HttpResponseException;
                    if (aa == null)
                    {
                        //  TODO: Only recreate connection on certain errors?
                        if (Interlocked.Exchange(ref Server, null) == server)
                        {
                            try
                            {
                                server.Dispose();
                            }
                            catch
                            {
                            }
                        }
                    }
                    return true;
                }
                LocalEndPoint endPoint = SingleEndPoint;
                String url = res.Url;
                //  Find end point
                var epName = res.EndPoint;
                if (!String.IsNullOrEmpty(epName))
                    eps.TryGetValue(epName, out endPoint);
                if (endPoint == null)
                {
                    SetErrorResponse(response, res.RequestId, "Not Found - The server cannot find the requested resource.", 404);
                    continue;
                }
                try
                {
                    Interlocked.Increment(ref endPoint.InProgress);
                    var baseUrl = endPoint.BaseUrl;
                    url = baseUrl + url;
                    var headers = res.Headers;
                    var hl = headers.Length;
                    for (int i = 0; i < hl; ++ i)
                    {
                        var h = headers[i];
                        if (h.StartsWith("Referer:", StringComparison.OrdinalIgnoreCase))
                        {
                            var t = baseUrl + h.Substring(8);
                            headers[i] = "Referer:" + t;
                        }

                    }
                    if (endPoint.IsInternal)
                    {
                        //  Local (in process)
                        url = local.LocalUri + url;
                        var host = local.GetHost(out var prefix, out int qs, out var didIndex, ref url);
                        using var t = new ManualHttpServerRequest(res.Method.ToString(), url, prefix, local, host, qs);
                        var data = res.Data;
                        if (data != null)
                        {
                            t._ReqContentLength = data.Length;
                            t._InputStream = new MemoryStream(data);
                        }
                        var h = new ManualHttpServerRequest.Headers();
                        foreach (var x in res.Headers.Nullable())
                        {
                            var key = x.SplitFirst(':', out var value);
//                            if (ReverseProxyTools.QuotedHeaders.Contains(key.FastToLower()))
//                                value = value.EnsureQuoted();
                            h.TryAddWithoutValidation(key, value);
                        }
                        t.ReqHeaders = h;
                        t._AcceptEncoding = t.GetReqHeader("Accept-Encoding");
                        t._IfNoneMatch = t.GetReqHeader("If-None-Match");
                        var cookies = t.GetReqHeader("Cookie");
                        if (cookies != null)
                            t.ReqCookies = HttpServerTools.ParseCookieString(cookies);
                        else
                            t.ReqCookies = ReadOnlyData.EmptyDictionary<String, String>();
                        var dest = new ArrayPoolStream();
                        t._OutputStream = dest;
                        await local.Handle(t).ConfigureAwait(false);
                        var resData = dest.ToArray();
                        t.SetResContentLength(resData.Length);
                        response.Data = resData;
                        response.RequestId = res.RequestId;
                        response.Headers = ProxyTools.EncodeHeaders(t.ResHeaders);
                        response.StatusCode = t._ResStatusCode;
                    }else
                    {
                        var ret = await ProxyTools.ProxyRequest(Client, url, new ProxyData(res.Method, res.Headers, res.Data)).ConfigureAwait(false);
                        response.Data = ret.Data;
                        response.RequestId = res.RequestId;
                        response.Headers = ret.Headers;
                        response.StatusCode = ret.StatusCode;




/*                        var c = Client;
                        var method = new HttpMethod(res.Method.ToString());
                        using var localRequest = new HttpRequestMessage(method, url);
                        HttpContent content = null;
                        try
                        {
                            if (res.Method == HttpServerMethods.POST)
                            {
                                var postData = res.Data;
                                if (postData != null)
                                {
                                    content = new ReadOnlyMemoryContent(postData);
                                    localRequest.Content = content;
                                }
                            }
                            var h = localRequest.Headers;
                            var ch = ReverseProxyTools.ContentHeaders;
                            // TODO: Set: X-Forwarded-For:
                            foreach (var x in res.Headers.Nullable())
                            {
                                var key = x.SplitFirst(':', out var value);
                                if (ch.Contains(key.FastToLower()))
                                    content?.Headers?.TryAddWithoutValidation(key, value);
                                else
                                    h.TryAddWithoutValidation(key, value);
                            }
                            using var localResponse = await c.SendAsync(localRequest).ConfigureAwait(false);
                            var resData = await localResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            response.Data = resData;
                            response.RequestId = res.RequestId;
                            response.Headers = ReverseProxyTools.EncodeHeaders(localResponse.Headers, localResponse.Content?.Headers);
                            response.StatusCode = (int)localResponse.StatusCode;
                        }
                        finally
                        {
                            content?.Dispose();
                        }
*/
                    }
                }
                catch (Exception ex)
                {

                    EndPointFails.OnException(ex);
                    endPoint?.Fails?.OnException(ex);
                    SetErrorResponse(response, res.RequestId, "Internal Server Error: " + ex.Message, 500);
                    continue;
                }
                finally
                {
                    Interlocked.Decrement(ref endPoint.InProgress);
                    Interlocked.Increment(ref endPoint.Completed);
                }

            }
        }


        volatile IReverseProxyServer Server;

        readonly PeriodicTask[] ConnectionTasks;

        readonly String ClientId;

        bool SetLocalServer(HttpServerBase server)
        {
            if (server == null)
                return false;
            Local = server;
            Manager.OnServiceAdded -= OnServiceAdded;
            return true;
        }

        void OnServiceAdded(object service, ServiceInfo info)
            => SetLocalServer(service as HttpServerBase);

        HttpServerBase Local;
        readonly ServiceManager Manager;

        volatile bool IsDisposing;

        public void Dispose()
        {
            IsDisposing = true;
            Manager.OnServiceAdded -= OnServiceAdded;
            (Server as Remote.IRemoteApi)?.Cancel();
            var t = ConnectionTasks;
            var ti = t.Length;
            while (ti > 0)
            {
                --ti;
                Interlocked.Exchange(ref t[ti], null)?.Dispose();
            }
            Interlocked.Exchange(ref Server, null)?.Dispose();

            Interlocked.Exchange(ref Client, null)?.Dispose();
            Interlocked.Exchange(ref ClientHandler, null)?.Dispose();
        }

        /// <summary>
        /// All local reverse proxy endpoints
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Http Server/{0}", "Reverse proxy end points", null, "icons/network.svg")]
        public TableData ReverseProxyEndPointsTable(TableDataRequest r, HttpServerRequest context)
        {
            var b = ServerBaseUrl;
            var lp = context.Host.Name;
            return TableDataTools.Get(r, 2000, EndPoints.Values.Select(x => new Data(b, lp, x)));
        }


    }

}
