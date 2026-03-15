using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.ReverseProxy
{


    public sealed class ReverseProxyClient : IDisposable
    {
        public ReverseProxyClient(ServiceManager manager, ReverseProxyClientParams p)
        {
            var maxThreads = Environment.ProcessorCount;
            var threadCount = p.MaxConcurrentRequests;
            threadCount = threadCount > 0 ? threadCount : (maxThreads + threadCount);
            if (threadCount <= 0)
                threadCount = 1;
            Params = p;
            p.TimeoutInMilliSeconds = 15 * 60 * 1000;
            ClientId = EnvInfo.ResolveText(p.ClientId ?? "$(MachineName)");
            Manager = manager;
            if (!SetLocalServer(manager.TryGet<HttpServerBase>()))
                manager.OnServiceAdded += OnServiceAdded;
            FetchTasks = Enumerable.Range(0, threadCount).Select(x => new PeriodicTask(Fetch, 1)).ToArray();

        }

        readonly ReverseProxyClientParams Params;
        readonly AsyncLock CreateConnectionLock = new AsyncLock();

        async ValueTask<bool> Fetch(CancellationToken cancel)
        {
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
                {
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
                            await Task.Delay(15000, cancel).ConfigureAwait(false);
                            return true;
                        }
                    }
                }
            }
            try
            {
                var res = await server.GetReverseProxyRequest(ClientId).ConfigureAwait(false);
                if (res == null)
                    return true;
                //  Local (in process)
                var url = LocalPrefix + res.Url;
                var host = local.GetHost(out var prefix, out int qs, ref url);
                using var t = new ManualHttpServerRequest(res.Method.ToString(), url, prefix, local, host, qs);
                var data = res.Data;
                if (data != null)
                {
                    t._ReqContentLength = data.Length;
                    t._InputStream = new MemoryStream(data);
                }
                var h = new Dictionary<String, String>(StringComparer.Ordinal);
                foreach (var x in res.Headers.Nullable())
                    h[x.SplitFirst(':', out var rest)] = rest;
                var rh = h.Freeze();
                t.ReqHeaders = rh;
                rh.TryGetValue("Accept-Encoding", out t._AcceptEncoding);
                rh.TryGetValue("If-None-Match", out t._IfNoneMatch);
                if (rh.TryGetValue("Cookie", out var cookies))
                    t.ReqCookies = HttpServerTools.ParseCookieString(cookies);
                else
                    t.ReqCookies = ReadOnlyData.EmptyDictionary<String, String>();

                var dest = new ArrayPoolStream();
                t._OutputStream = dest;
                if (url.EndsWith("GetMessages"))
                    url = url;
                await local.Handle(t).ConfigureAwait(false);
                var resData = dest.ToArray();
                t.ResHeaders["Content-Length"] = resData.Length.ToString();
                //var mime = t.GetResMime();
                //if (!String.IsNullOrEmpty(mime))
                    //t.ResHeaders["Content-Type"] = mime;
                var response = new ReverseProxyResponse
                {
                    Data = resData,
                    ClientId = ClientId,
                    RequestId = res.RequestId,
                    Headers = t.ResHeaders.Select(x => String.Concat(x.Key, ':', x.Value)).ToArray(),
                    StatusCode = t._ResStatusCode,
                };
                await server.ReverseProxyResponse(response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
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
            }
            return true;
        }

        volatile IReverseProxyServer Server;

        readonly PeriodicTask[] FetchTasks;

        readonly String ClientId;

        bool SetLocalServer(HttpServerBase server)
        {
            if (server == null)
                return false;
            LocalPrefix = server.AllPrefixes.FirstOrDefault().Replace("*", "localhost").TrimEnd('/') + '/';
            Local = server;
            Manager.OnServiceAdded -= OnServiceAdded;
            return true;
        }

        void OnServiceAdded(object service, ServiceInfo info)
            => SetLocalServer(service as HttpServerBase);

        String LocalPrefix;
        HttpServerBase Local;
        readonly ServiceManager Manager;

        public void Dispose()
        {
            Manager.OnServiceAdded -= OnServiceAdded;
            Interlocked.Exchange(ref Server, null)?.Dispose();
            var t = FetchTasks;
            var ti = t.Length;
            while (ti > 0)
            {
                --ti;
                Interlocked.Exchange(ref t[ti], null)?.Dispose();
            }
        }



    }

}
