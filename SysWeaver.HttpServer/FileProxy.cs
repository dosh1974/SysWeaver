using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// A module that serves a remote http(s) folder under a local web folder, proxying GET, HEAD and POST requests.
    /// Kept for existing configurations, prefer <see cref="HttpProxy"/> (multiple end points, disc caching, offline handling etc).
    /// </summary>
    /// <remarks>
    /// Implemented using a <see cref="HttpProxy"/> with a single end point:
    /// GET responses are cached in memory and re-validated (using the ETag / Last-Modified of the source) once the max-age of the source's Cache-Control header has passed (every request if there is no max-age),
    /// no disc caching, and no stale data is served if the source is offline.
    /// The local url after the web root is appended to the source root with every path segment url encoded (so encoded '/', '?', '&amp;' and '#' chars stay data),
    /// a request with a "." or ".." segment is rejected (400), and the raw (still encoded) query string is appended as is.
    /// The client request headers are forwarded to the remote server, except Cookie and Authorization (see <see cref="FileProxyParams.ForwardCookies"/> and <see cref="FileProxyParams.ForwardAuthorization"/>).
    /// The auth check is done while the module resolves handlers.
    /// </remarks>
    public sealed class FileProxy : IHttpServerModule, IDisposable, IPerfMonitored, IHaveStats
    {

        /// <inheritdoc/>
        public String Name => Proxy.Name;

        /// <inheritdoc/>
        public String[] OnlyForPrefixes => Proxy.OnlyForPrefixes;

        /// <summary>
        /// Create a file proxy.
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <exception cref="Exception">Thrown if the web root or the source root is null or empty</exception>
        public FileProxy(FileProxyParams p)
        {
            if (String.IsNullOrEmpty(p.WebRoot))
                throw new Exception("Web root may not be empty!");
            if (String.IsNullOrEmpty(p.SourceRoot))
                throw new Exception("Source root may not be empty!");
            Proxy = new HttpProxy(new HttpProxyParams
            {
                CredFile = p.CredFile,
                User = p.User,
                Password = p.Password,
                UseTor = p.UseTor,
                IgnoreCertErrors = p.IgnoreCertErrors,
                Auth = p.Auth,
                ForwardCookies = p.ForwardCookies,
                ForwardAuthorization = p.ForwardAuthorization,
                ServeStale = false,
                CheckEtagSeconds = -1,
                DiscCacheDurationSeconds = 0,
                EndPoints =
                [
                    new HttpProxyEndPoint
                    {
                        WebRoot = p.WebRoot,
                        SourceRoot = p.SourceRoot,
                    }
                ],
            }, nameof(FileProxy));
        }

        readonly HttpProxy Proxy;

        /// <summary>
        /// Dispose the proxy (and the http client if one was created for it).
        /// </summary>
        public void Dispose() => Proxy.Dispose();

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <inheritdoc/>
        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler => Proxy.AsyncHandler;

        /// <inheritdoc/>
        public PerfMonitor PerfMon => Proxy.PerfMon;

        /// <summary>
        /// Url encode every segment of a decoded relative path (so that decoded '/', '?', '&amp;', '#' etc can't change the structure of the upstream url).
        /// </summary>
        /// <param name="path">The decoded path</param>
        /// <returns>The encoded path</returns>
        /// <exception cref="HttpResponseException">Thrown (400) if the path contains a "." or ".." segment (also when separated using '\')</exception>
        public static String EncodePath(String path) => HttpProxy.EncodePath(path);

        /// <inheritdoc/>
        public IEnumerable<Stats> GetStats() => Proxy.GetStats();

    }

}
