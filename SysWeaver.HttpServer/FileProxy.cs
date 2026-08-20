using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Compression;

namespace SysWeaver.Net
{

    public sealed class FileProxy : IHttpServerModule, IDisposable, IPerfMonitored, IHaveStats
    {

        public String Name { get; init; }

        public String[] OnlyForPrefixes { get; init; }

        public FileProxy(FileProxyParams p)
        {
            var root = p.WebRoot;
            var sourceRoot = p.SourceRoot;
            if (String.IsNullOrEmpty(root))
                throw new Exception("Web root may not be empty!");
            if (String.IsNullOrEmpty(sourceRoot))
                throw new Exception("Source root may not be empty!");
            if (root.FastIndexOf("://") >= 0)
            {
                root = root.TrimEnd('/') + '/';
                ForPrefix = root;
            }
            else
            {
                WebRootLen = root.Length;
            }
            Name = String.Concat("FileProxy ", root, " => ", sourceRoot);
            SourceRoot = sourceRoot;
            PerfMon = new PerfMonitor(Name);
            if ((ForPrefix == null) && (!root.FastEquals("/")))
                OnlyForPrefixes = [root];
            if (p.GetUserPassword(out var user, out var password, false))
            {
                OwnClient = true;
                var c = WebTools.CreateHttpClient(p.UseTor, p.IgnoreCertErrors, false);
                Client = c;
                if (user.FastToLower().FastEquals("bearer"))
                {
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", password);
                }
                else
                {
                    var byteArray = Encoding.ASCII.GetBytes(String.Join(":", user, password));
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                }
            }
            else
            {
                Client = WebTools.GetSharedHttpClient(p.UseTor, p.IgnoreCertErrors, false);
            }
            AsyncHandler = HandleAsync;
            Auth = Authorization.GetRequiredTokens(p.Auth);
        }

        readonly IReadOnlyList<String> Auth;

        public void Dispose()
        {
            if (OwnClient)
                Client.Dispose();
        }

        readonly String ForPrefix;
        readonly bool OwnClient;
        readonly HttpClient Client;

        public override string ToString() => Name;

        readonly int WebRootLen;
        readonly String SourceRoot;

        public Func<HttpServerRequest, Task<IHttpRequestHandler>> AsyncHandler { get; init; }

        public PerfMonitor PerfMon { get; init;  }

        readonly ProxyRequestCache Cache = new ProxyRequestCache();

        async Task<ProxyData> DownstreamRequest(String url, ProxyData data)
        {
            using var __ = PerfMon.Track(nameof(DownstreamRequest));
            return await ProxyTools.ProxyRequest(Client, url, data).ConfigureAwait(false);
        }

        async Task<IHttpRequestHandler> HandleAsync(HttpServerRequest context)
        {
            using var __ = PerfMon.Track(nameof(HandleAsync));
            if (!(context.Session?.IsValid(Auth) ?? true))
                throw new UserNotAllowedException();
            var fp = ForPrefix;
            if (fp != null)
            {
                if (!context.Prefix.FastEquals(fp))
                    return null;
            }
            var u = context.LocalUrl;
            var req = SourceRoot + u.Substring(WebRootLen);
            var p = context.QueryStringStart;
            if (p > 0)
                req = String.Concat(req, '?', context.Url.Substring(p));
            return await Cache.HandleAsync(context, req, DownstreamRequest).ConfigureAwait(false);
        }

        public IEnumerable<Stats> GetStats()
        {
            const String sys = nameof(FileProxy);
            foreach (var x in Cache.GetCacheStats(sys, "Cache GET."))
                yield return x;
            foreach (var x in Cache.HeadCacheStats(sys, "Cache HEAD."))
                yield return x;
        }

    }






}
