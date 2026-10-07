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

    /// <summary>
    /// A module that serves a remote http(s) folder under a local web folder, proxying GET, HEAD and POST requests (GET and HEAD responses are cached, see <see cref="ProxyRequestCache"/>).
    /// </summary>
    /// <remarks>
    /// The local url after the web root is appended to the source root with every path segment url encoded (so encoded '/', '?', '&amp;' and '#' chars stay data),
    /// a request with a "." or ".." segment is rejected (400), and the raw (still encoded) query string is appended as is.
    /// The client request headers are forwarded to the remote server, except Cookie and Authorization (see <see cref="FileProxyParams.ForwardCookies"/> and <see cref="FileProxyParams.ForwardAuthorization"/>).
    /// The auth check is done while the module resolves handlers, before the request is matched against the web root.
    /// </remarks>
    public sealed class FileProxy : IHttpServerModule, IDisposable, IPerfMonitored, IHaveStats
    {

        /// <inheritdoc/>
        public String Name { get; init; }

        /// <inheritdoc/>
        public String[] OnlyForPrefixes { get; init; }

        /// <summary>
        /// Create a file proxy.
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <exception cref="Exception">Thrown if the web root or the source root is null or empty</exception>
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
            //  A configured credentials file must exist and contain valid credentials (fail loudly on a misconfiguration)
            if (p.GetUserPassword(out var user, out var password, !String.IsNullOrEmpty(p.CredFile)))
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
            Cache = new ProxyRequestCache
            {
                ForwardCookies = p.ForwardCookies,
                ForwardAuthorization = p.ForwardAuthorization,
            };
        }

        readonly IReadOnlyList<String> Auth;

        /// <summary>
        /// Dispose the http client (if one was created for this proxy).
        /// </summary>
        public void Dispose()
        {
            if (OwnClient)
                Client.Dispose();
        }

        readonly String ForPrefix;
        readonly bool OwnClient;
        readonly HttpClient Client;

        /// <inheritdoc/>
        public override string ToString() => Name;

        readonly int WebRootLen;
        readonly String SourceRoot;

        /// <inheritdoc/>
        public Func<HttpServerRequest, Task<IHttpRequestHandler>> AsyncHandler { get; init; }

        /// <inheritdoc/>
        public PerfMonitor PerfMon { get; init;  }

        readonly ProxyRequestCache Cache;

        async Task<ProxyData> DownstreamRequest(String url, ProxyData data)
        {
            using var __ = PerfMon.Track(nameof(DownstreamRequest));
            return await ProxyTools.ProxyRequest(Client, url, data).ConfigureAwait(false);
        }

        /// <summary>
        /// Proxy a request.
        /// </summary>
        /// <exception cref="UserNotAllowedException">Thrown if the session lacks the required tokens</exception>
        async Task<IHttpRequestHandler> HandleAsync(HttpServerRequest context)
        {
            using var __ = PerfMon.Track(nameof(HandleAsync));
            var fp = ForPrefix;
            if (fp != null)
            {
                if (!context.Prefix.FastEquals(fp))
                    return null;
            }
            if (!(context.Session?.IsValid(Auth) ?? true))
                throw new UserNotAllowedException();
            var u = context.LocalUrl;
            var req = SourceRoot + EncodePath(u.Substring(WebRootLen));
            var p = context.QueryStringStart;
            if (p > 0)
                req = String.Concat(req, '?', context.GetRawQuery());
            return await Cache.HandleAsync(context, req, DownstreamRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Url encode every segment of a decoded relative path (so that decoded '/', '?', '&amp;', '#' etc can't change the structure of the upstream url).
        /// </summary>
        /// <param name="path">The decoded path</param>
        /// <returns>The encoded path</returns>
        /// <exception cref="HttpResponseException">Thrown (400) if the path contains a "." or ".." segment (also when separated using '\')</exception>
        public static String EncodePath(String path)
        {
            var parts = path.Split('/');
            var l = parts.Length;
            for (int i = 0; i < l; ++i)
            {
                var s = parts[i];
                if (s.Length <= 0)
                    continue;
                foreach (var x in s.Split('\\'))
                    if (x.FastEquals(".") || x.FastEquals(".."))
                        throw new HttpResponseException(400, "Bad Request - Relative path segments are not allowed [400]");
                parts[i] = Uri.EscapeDataString(s);
            }
            return String.Join('/', parts);
        }

        /// <inheritdoc/>
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
