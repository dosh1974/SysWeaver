using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Compression;

namespace SysWeaver.Net
{

    /// <summary>
    /// A module that serves remote http(s) folders under local web folders (one or more end points, see <see cref="HttpProxyEndPoint"/>), with in-memory and on-disc caching.
    /// GET responses are cached, HEAD requests are served from the cache (if present), all other requests (and GET requests with a Range header) are passed through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The local url after the web root is appended to the source root with every path segment url encoded (so encoded '/', '?', '&amp;' and '#' chars stay data),
    /// a request with a "." or ".." segment is rejected (400), and the raw (still encoded) query string is appended as is.
    /// The client request headers are forwarded to the remote server, except Cookie and Authorization (see <see cref="HttpProxyParams.ForwardCookies"/> and <see cref="HttpProxyParams.ForwardAuthorization"/>).
    /// When fetching data for the cache, the client's conditional, range and Accept-Encoding headers are replaced by the proxy's own.
    /// The auth check is done while the module resolves handlers.
    /// </para>
    /// <para>
    /// Caching:
    /// Only 200 responses to GET requests without "no-store", "private" or "Vary: *" are cached. Set-Cookie headers are never cached (only the request that caused the fetch gets them).
    /// Cached data is kept exactly as served by the source (compressed data is never decompressed for storage), both in memory and on disc.
    /// If a client doesn't accept the encoding of the cached data, it's re-compressed (or decompressed) and that variant is kept in memory only (with the memory cached entry).
    /// Uncompressed data of a compressible mime type is compressed for clients that accept it (variants in memory only).
    /// Within <see cref="HttpProxyParams.CheckEtagSeconds"/> of the last validation cached data is served as is, after that a conditional request is made (If-None-Match / If-Modified-Since),
    /// a 304 extends the memory and disc durations.
    /// If the request forwards cookies or authorization to the source, the forwarded values are part of the cache key and such responses are never cached on disc.
    /// </para>
    /// <para>
    /// Disc cache:
    /// Files are distributed over a sub folder of <see cref="Folders.AllAppFolders"/> using <see cref="Folders.SelectFolder"/>, every entry has a small meta file (".m", status, headers, validators etc)
    /// and a data file (".d", the body as served by the source).
    /// The last write time of the meta file is the expiration time of the entry, a scheduled task (see <see cref="Scheduler"/>) deletes expired files every <see cref="HttpProxyParams.PruneMinutes"/>.
    /// </para>
    /// <para>
    /// Offline handling:
    /// If the source server can't be reached, times out (<see cref="HttpProxyParams.RequestTimeoutSeconds"/>) or responds with 502, 503 or 504 it's considered offline.
    /// While offline no requests are sent to it, stale cached data is served if allowed (<see cref="HttpProxyParams.ServeStale"/>), else a 503 is returned immediately.
    /// A periodic task checks if the server is online again (every <see cref="HttpProxyParams.OnlineCheckSeconds"/>).
    /// Before a server is detected as offline, a validation that takes longer than <see cref="HttpProxyParams.StaleWaitMs"/> serves stale data (if allowed) while the request completes in the background.
    /// </para>
    /// <para>
    /// Every response written by the proxy has an "X-Cache" header: HIT (memory), DISC, REVALIDATED (304 from the source), MISS (fetched), STALE (source offline), PASS (not cached) or OFFLINE.
    /// </para>
    /// </remarks>
    public sealed class HttpProxy : IHttpServerModule, IDisposable, IPerfMonitored, IHaveStats
    {

        /// <inheritdoc/>
        public String Name { get; init; }

        /// <inheritdoc/>
        public String[] OnlyForPrefixes { get; init; }

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>
        /// Create a http proxy.
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <exception cref="Exception">Thrown if there are no end points, or an end point has an empty web root or no source</exception>
        public HttpProxy(HttpProxyParams p) : this(p, nameof(HttpProxy))
        {
        }

        /// <summary>
        /// Create a http proxy.
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="typeName">The type name used as a prefix of the module name (and for stats)</param>
        /// <exception cref="Exception">Thrown if there are no end points, or an end point has an empty web root or no source</exception>
        internal HttpProxy(HttpProxyParams p, String typeName)
        {
            ArgumentNullException.ThrowIfNull(p);
            var eps = p.EndPoints;
            if ((eps == null) || (eps.Length <= 0))
                throw new Exception("At least one end point is required!");
            TypeName = typeName;
            //  A configured credentials file must exist and contain valid credentials (fail loudly on a misconfiguration)
            if (p.GetUserPassword(out var user, out var password, !String.IsNullOrEmpty(p.CredFile)))
            {
                OwnClient = true;
                var c = WebTools.CreateHttpClient(p.UseTor, p.IgnoreCertErrors, false);
                Client = c;
                if (user.FastToLower().FastEquals("bearer"))
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", password);
                else
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Join(":", user, password))));
            }
            else
            {
                Client = WebTools.GetSharedHttpClient(p.UseTor, p.IgnoreCertErrors, false);
            }
            Timeout = TimeSpan.FromSeconds(Math.Max(1, p.RequestTimeoutSeconds));
            StaleWait = TimeSpan.FromMilliseconds(Math.Max(0, p.StaleWaitMs));
            MaxMemItemSize = p.MaxMemItemSize;
            var onlineCheckMs = Math.Max(1, p.OnlineCheckSeconds) * 1000;
            List<Ep> endPoints = new List<Ep>(eps.Length);
            bool anyDisc = false;
            bool needAll = false;
            List<String> prefixes = new List<string>();
            List<String> names = new List<string>();
            foreach (var x in eps)
            {
                if (x == null)
                    continue;
                var e = new Ep(p, x, this, onlineCheckMs);
                endPoints.Add(e);
                anyDisc |= e.DiscTicks > 0;
                if (e.ForPrefix != null)
                {
                    needAll = true;
                    names.Add(String.Concat(e.ForPrefix, " => ", e.Source));
                }
                else
                {
                    if (e.WebRoot.Length <= 0)
                        needAll = true;
                    else
                        prefixes.Add(e.WebRoot);
                    names.Add(String.Concat(e.WebRoot, " => ", e.Source));
                }
            }
            if (endPoints.Count <= 0)
                throw new Exception("At least one end point is required!");
            //  Absolute prefixes first, then the longest web root first (most specific match)
            EndPoints = endPoints.OrderBy(x => x.ForPrefix == null ? 1 : 0).ThenByDescending(x => x.WebRoot.Length).ToArray();
            Name = String.Concat(typeName, ' ', String.Join(", ", names));
            if (!needAll)
                OnlyForPrefixes = prefixes.Distinct(StringComparer.Ordinal).ToArray();
            PerfMon = new PerfMonitor(Name);
            AsyncHandler = HandleAsync;
            if (anyDisc)
            {
                //  A stable sub folder per configuration (the same config uses the same files after a restart)
                DiscFolders = Folders.Append(Folders.AllAppFolders, Path.Combine("HttpProxy", HashName(Name).Substring(0, 16)));
                var pm = Math.Max(1, p.PruneMinutes);
                PruneTask = Scheduler.AddTask(DateTime.UtcNow.AddMinutes(pm), Prune, "Prune " + Name, t => t.AddMinutes(pm));
            }
        }

        /// <summary>
        /// Stop the background tasks, dispose the http client (if one was created for this proxy).
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            foreach (var h in Hosts.Values)
                h.Dispose();
            if (OwnClient)
                Client.Dispose();
        }

        #region Members

        readonly String TypeName;
        readonly bool OwnClient;
        readonly HttpClient Client;
        readonly TimeSpan Timeout;
        readonly TimeSpan StaleWait;
        readonly int MaxMemItemSize;
        readonly Ep[] EndPoints;
        readonly IReadOnlyList<String> DiscFolders;
        IDisposable PruneTask;

        /// <summary>
        /// The state of every source host (scheme + authority).
        /// </summary>
        readonly ConcurrentDictionary<String, HostState> Hosts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The memory cache (all end points share it)
        /// </summary>
        readonly FastMemCache<String, CacheEntry> Mem = new(e => new DateTime(Interlocked.Read(ref e.MemExpires), DateTimeKind.Utc), StringComparer.Ordinal);

        /// <summary>
        /// Running cache loads (only one load per key at a time)
        /// </summary>
        readonly ConcurrentDictionary<String, Task<LoadResult>> Loading = new(StringComparer.Ordinal);

        /// <summary>
        /// Refreshes that continues in the background (stale data was served because the source was slow to respond)
        /// </summary>
        readonly ConcurrentDictionary<String, Task<LoadResult>> Refreshing = new(StringComparer.Ordinal);

        /// <summary>
        /// The preferred compression when re-compressing data for a client.
        /// </summary>
        static readonly HttpCompressionPriority Compression = HttpCompressionPriority.GetSupportedEncoders();

        /// <inheritdoc/>
        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get; init; }

        /// <inheritdoc/>
        public PerfMonitor PerfMon { get; init; }

        #endregion//Members

        #region End points

        /// <summary>
        /// A resolved end point.
        /// </summary>
        sealed class Ep
        {
            public Ep(HttpProxyParams p, HttpProxyEndPoint e, HttpProxy owner, int onlineCheckMs)
            {
                var root = e.WebRoot;
                if (String.IsNullOrEmpty(root))
                    throw new Exception("Web root may not be empty!");
                if (root.FastIndexOf("://") >= 0)
                {
                    ForPrefix = root.TrimEnd('/') + '/';
                    WebRoot = "";
                }
                else
                {
                    //  "/" means everything
                    WebRoot = root.FastEquals("/") ? "" : root;
                }
                Source = CombineSource(p.SourceServer, e.SourceRoot);
                if (!Uri.TryCreate(Source, UriKind.Absolute, out var su))
                    throw new Exception("Invalid source \"" + Source + "\" for web root \"" + root + "\"");
                //  "-" on an end point means no auth (Authorization.GetRequiredTokens treats "-" as "no one")
                var ea = e.Auth?.Trim();
                Auth = ea == "-" ? null : Authorization.GetRequiredTokens(e.Auth ?? p.Auth);
                ForwardCookies = e.ForwardCookies ?? p.ForwardCookies;
                ForwardAuthorization = e.ForwardAuthorization ?? p.ForwardAuthorization;
                ServeStale = e.ServeStale ?? p.ServeStale;
                var check = e.CheckEtagSeconds ?? p.CheckEtagSeconds;
                CheckTicks = check < 0 ? -1 : check * TimeSpan.TicksPerSecond;
                MemTicks = Math.Max(0, e.MemCacheDurationSeconds ?? p.MemCacheDurationSeconds) * TimeSpan.TicksPerSecond;
                DiscTicks = Math.Max(0, e.DiscCacheDurationSeconds ?? p.DiscCacheDurationSeconds) * TimeSpan.TicksPerSecond;
                Caching = (MemTicks > 0) || (DiscTicks > 0);
                var hostKey = su.GetLeftPart(UriPartial.Authority) + '/';
                Host = owner.Hosts.GetOrAdd(hostKey, k => new HostState(owner, k, onlineCheckMs));
            }

            /// <summary>
            /// Absolute listener prefix (ends with '/'), or null for a relative web root.
            /// </summary>
            public readonly String ForPrefix;
            /// <summary>
            /// The relative web root, empty if <see cref="ForPrefix"/> is used or for "everything"
            /// </summary>
            public readonly String WebRoot;
            /// <summary>
            /// The full source url to append the local path to
            /// </summary>
            public readonly String Source;
            public readonly IReadOnlyList<String> Auth;
            public readonly bool ForwardCookies;
            public readonly bool ForwardAuthorization;
            public readonly bool ServeStale;
            /// <summary>
            /// Number of ticks between validations, -1 to use the max-age of the source
            /// </summary>
            public readonly long CheckTicks;
            public readonly long MemTicks;
            public readonly long DiscTicks;
            public readonly bool Caching;
            public readonly HostState Host;
        }

        /// <summary>
        /// Combine the source server and a source root.
        /// </summary>
        /// <param name="server">The source server (may be null if the root is absolute)</param>
        /// <param name="root">The source root, if it contains "://" it's used as is</param>
        /// <returns>The source url</returns>
        static String CombineSource(String server, String root)
        {
            if ((root != null) && (root.FastIndexOf("://") >= 0))
                return root;
            if (String.IsNullOrEmpty(server))
                throw new Exception("A source server is required (or an absolute source root)!");
            if (String.IsNullOrEmpty(root))
                return server;
            return String.Concat(server.TrimEnd('/'), "/", root.TrimStart('/'));
        }

        /// <summary>
        /// Find the end point for a request.
        /// </summary>
        Ep FindEndPoint(HttpServerRequest context, out String rest)
        {
            var u = context.LocalUrl;
            foreach (var e in EndPoints)
            {
                var fp = e.ForPrefix;
                if (fp != null)
                {
                    if (!context.Prefix.FastEquals(fp))
                        continue;
                    rest = u;
                    return e;
                }
                var wr = e.WebRoot;
                if (u.StartsWith(wr, StringComparison.Ordinal))
                {
                    rest = u.Substring(wr.Length);
                    return e;
                }
            }
            rest = null;
            return null;
        }

        #endregion//End points

        #region Host state (online / offline)

        /// <summary>
        /// The online / offline state of a source host.
        /// </summary>
        sealed class HostState : IDisposable
        {
            public HostState(HttpProxy owner, String probeUrl, int checkMs)
            {
                Owner = owner;
                ProbeUrl = probeUrl;
                //  Always running, does nothing while online (no start / stop races)
                Probe = new PeriodicTask(CheckOnline, checkMs, true, true, true);
            }

            readonly HttpProxy Owner;
            public readonly String ProbeUrl;
            readonly PeriodicTask Probe;
            int OfflineFlag;
            public long OfflineCount;
            public long LastOfflineTicks;

            public bool IsOffline => Volatile.Read(ref OfflineFlag) != 0;

            public void SetOffline()
            {
                if (Interlocked.Exchange(ref OfflineFlag, 1) != 0)
                    return;
                Interlocked.Increment(ref OfflineCount);
                Interlocked.Exchange(ref LastOfflineTicks, DateTime.UtcNow.Ticks);
            }

            public void SetOnline() => Volatile.Write(ref OfflineFlag, 0);

            async Task<bool> CheckOnline(CancellationToken cancel)
            {
                if (!IsOffline)
                    return true;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                cts.CancelAfter(Owner.Timeout);
                try
                {
                    var r = await ProxyTools.SendRequest(Owner.Client, ProbeUrl, new ProxyData(HttpServerMethods.HEAD, null), cts.Token).ConfigureAwait(false);
                    if (!IsServerDown(r.StatusCode))
                        SetOnline();
                }
                catch
                {
                }
                return true;
            }

            public void Dispose() => Probe.Dispose();
        }

        /// <summary>
        /// Status codes that indicates that the source server (or the server behind a gateway) is down.
        /// </summary>
        static bool IsServerDown(int statusCode) => statusCode is 502 or 503 or 504;

        #endregion//Host state (online / offline)

        #region Request handling

        /// <summary>
        /// Proxy a request.
        /// </summary>
        /// <exception cref="UserNotAllowedException">Thrown if the session lacks the required tokens</exception>
        async ValueTask<IHttpRequestHandler> HandleAsync(HttpServerRequest context)
        {
            using var __ = PerfMon.Track(nameof(HandleAsync));
            var ep = FindEndPoint(context, out var rest);
            if (ep == null)
                return null;
            if (!(context.Session?.IsValid(ep.Auth) ?? true))
                throw new UserNotAllowedException();
            var url = ep.Source + EncodePath(rest);
            if (context.QueryStringStart > 0)
                url = String.Concat(url, '?', context.GetRawQuery());
            var req = await ProxyTools.GetFromRequest(context).ConfigureAwait(false);
            req.Headers = FilterClientHeaders(req.Headers, ep.ForwardCookies, ep.ForwardAuthorization, out var cred, out var haveRange);
            Interlocked.Increment(ref RequestCount);
            var m = context.HttpMethod;
            if (ep.Caching && (!haveRange) && ((m == HttpServerMethods.GET) || (m == HttpServerMethods.HEAD)))
                await HandleCached(context, ep, url, req, cred, m == HttpServerMethods.HEAD).ConfigureAwait(false);
            else
                await WriteResponse(context, await PassThrough(ep, url, req).ConfigureAwait(false)).ConfigureAwait(false);
            return HttpServerTools.AlreadyHandled;
        }

        async Task HandleCached(HttpServerRequest context, Ep ep, String url, ProxyData req, String cred, bool head)
        {
            var key = cred == null ? url : String.Concat(url, "\n", cred);
            bool disc = (ep.DiscTicks > 0) && (cred == null) && (DiscFolders != null);
            CacheEntry e = null;
            //  Fast path
            if ((ep.MemTicks > 0) && Mem.TryGet(key, out e) && IsFresh(e, ep))
            {
                Interlocked.Increment(ref MemHits);
                await Serve(context, e, null, head, "HIT").ConfigureAwait(false);
                return;
            }
            if (head)
            {
                //  HEAD requests doesn't populate the cache
                if ((e == null) && disc)
                    e = await ReadDisc(key).ConfigureAwait(false);
                if ((e != null) && (IsFresh(e, ep) || (ep.ServeStale && ep.Host.IsOffline)))
                {
                    await Serve(context, e, null, true, IsFresh(e, ep) ? "HIT" : "STALE").ConfigureAwait(false);
                    return;
                }
                await WriteResponse(context, await PassThrough(ep, url, req).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }
            //  Only one load per key at a time
            var tcs = new TaskCompletionSource<LoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var running = Loading.GetOrAdd(key, tcs.Task);
            LoadResult res;
            bool waiter = running != tcs.Task;
            if (waiter)
            {
                res = await running.ConfigureAwait(false);
            }
            else
            {
                try
                {
                    res = await Load(ep, key, url, req, disc).ConfigureAwait(false);
                    tcs.SetResult(res);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                    throw;
                }
                finally
                {
                    Loading.TryRemove(new KeyValuePair<String, Task<LoadResult>>(key, tcs.Task));
                }
            }
            if (res.Entry != null)
            {
                //  Only the request that caused the fetch gets any Set-Cookie headers
                await Serve(context, res.Entry, waiter ? null : res.Fresh, false, res.Status).ConfigureAwait(false);
                return;
            }
            //  Not cacheable, a waiting request makes its own request (responses may be per request)
            await WriteResponse(context, waiter ? await PassThrough(ep, url, req).ConfigureAwait(false) : res.Response).ConfigureAwait(false);
        }

        /// <summary>
        /// The result of a cache load.
        /// </summary>
        sealed class LoadResult
        {
            public LoadResult(CacheEntry entry, String status, ProxyData fresh = null)
            {
                Entry = entry;
                Status = status;
                Fresh = fresh;
            }

            public LoadResult(ProxyData response)
            {
                Response = response;
            }

            /// <summary>
            /// The entry to serve, if null <see cref="Response"/> is used.
            /// </summary>
            public readonly CacheEntry Entry;
            /// <summary>
            /// The X-Cache status
            /// </summary>
            public readonly String Status;
            /// <summary>
            /// The source response that created the entry (if any)
            /// </summary>
            public readonly ProxyData Fresh;
            /// <summary>
            /// A response to write as is (not cached)
            /// </summary>
            public readonly ProxyData Response;
        }

        /// <summary>
        /// Load (or refresh) a cache entry, only one load per key at a time.
        /// </summary>
        async Task<LoadResult> Load(Ep ep, String key, String url, ProxyData req, bool disc)
        {
            using var __ = PerfMon.Track(nameof(Load));
            CacheEntry e = null;
            bool fromDisc = false;
            if ((ep.MemTicks > 0) && Mem.TryGet(key, out e) && IsFresh(e, ep))
            {
                //  Refreshed by a previous load
                Interlocked.Increment(ref MemHits);
                return new LoadResult(e, "HIT");
            }
            if ((e == null) && disc)
            {
                e = await ReadDisc(key).ConfigureAwait(false);
                if (e != null)
                {
                    fromDisc = true;
                    if (IsFresh(e, ep))
                    {
                        Interlocked.Increment(ref DiscHits);
                        PutMem(ep, e);
                        return new LoadResult(e, "DISC");
                    }
                }
            }
            if (ep.Host.IsOffline)
                return StaleOrOffline(ep, e, fromDisc, null);
            bool canStale = (e != null) && ep.ServeStale;
            var rf = Refreshing;
            if (rf.TryGetValue(key, out var running))
            {
                //  A background refresh (started when stale data was served) is still running
                if (canStale)
                    return StaleOrOffline(ep, e, fromDisc, null);
                return await running.ConfigureAwait(false);
            }
            var t = Refresh(ep, key, url, req, e, disc, fromDisc);
            if (canStale && (!t.IsCompleted) && (StaleWait < Timeout))
            {
                if (await Task.WhenAny(t, Task.Delay(StaleWait)).ConfigureAwait(false) != t)
                {
                    //  Serve stale data, let the refresh complete in the background
                    rf[key] = t;
                    _ = t.ContinueWith(x =>
                    {
                        rf.TryRemove(new KeyValuePair<String, Task<LoadResult>>(key, x));
                        if (x.Exception != null)
                            FetchErrors.OnException(x.Exception);
                    }, TaskContinuationOptions.ExecuteSynchronously);
                    return StaleOrOffline(ep, e, fromDisc, null);
                }
            }
            return await t.ConfigureAwait(false);
        }

        /// <summary>
        /// Validate or fetch data from the source, updating the cache.
        /// </summary>
        async Task<LoadResult> Refresh(Ep ep, String key, String url, ProxyData req, CacheEntry e, bool disc, bool fromDisc)
        {
            var host = ep.Host;
            var upHeaders = UpstreamHeaders(req.Headers, e);
            ProxyData r;
            using (var cts = new CancellationTokenSource(Timeout))
            {
                try
                {
                    using var _ = PerfMon.Track("Fetch");
                    r = await ProxyTools.SendRequest(Client, url, new ProxyData(HttpServerMethods.GET, upHeaders), cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    FetchErrors.OnException(ex);
                    host.SetOffline();
                    return StaleOrOffline(ep, e, fromDisc, ErrorResponse(ex, cts.IsCancellationRequested));
                }
            }
            if (IsServerDown(r.StatusCode))
            {
                host.SetOffline();
                return StaleOrOffline(ep, e, fromDisc, r);
            }
            if (r.StatusCode == 304)
            {
                if (e == null)
                    return new LoadResult(r);
                Interlocked.Increment(ref Revalidated);
                if (ep.CheckTicks < 0)
                {
                    GetCacheInfo(r.Headers, out var maxAge, out _);
                    if (maxAge >= 0)
                        e.MaxAge = maxAge;
                }
                Interlocked.Exchange(ref e.Checked, DateTime.UtcNow.Ticks);
                PutMem(ep, e);
                if (disc)
                    await TouchDisc(ep, e).ConfigureAwait(false);
                return new LoadResult(e, "REVALIDATED");
            }
            var ne = TryCreateEntry(key, r);
            if (ne != null)
            {
                Interlocked.Increment(ref Fetched);
                PutMem(ep, ne);
                if (disc)
                    await WriteDisc(ep, ne, true).ConfigureAwait(false);
                return new LoadResult(ne, "MISS", r);
            }
            //  Not cacheable (anymore)
            if (e != null)
            {
                Mem.Remove(key);
                if (disc)
                    DeleteDisc(key);
            }
            Interlocked.Increment(ref PassThroughs);
            return new LoadResult(r);
        }

        /// <summary>
        /// The source is offline, serve stale data if allowed, else the given response or a 503.
        /// </summary>
        LoadResult StaleOrOffline(Ep ep, CacheEntry e, bool fromDisc, ProxyData response)
        {
            if ((e != null) && ep.ServeStale)
            {
                Interlocked.Increment(ref StaleServed);
                if (fromDisc)
                    PutMem(ep, e);
                return new LoadResult(e, "STALE");
            }
            Interlocked.Increment(ref OfflineResponses);
            return new LoadResult(response ?? OfflineResponse());
        }

        /// <summary>
        /// Pass a request through (no caching).
        /// </summary>
        async Task<ProxyData> PassThrough(Ep ep, String url, ProxyData req)
        {
            using var __ = PerfMon.Track(nameof(PassThrough));
            var host = ep.Host;
            if (host.IsOffline)
            {
                Interlocked.Increment(ref OfflineResponses);
                return OfflineResponse();
            }
            Interlocked.Increment(ref PassThroughs);
            using var cts = new CancellationTokenSource(Timeout);
            try
            {
                var r = await ProxyTools.SendRequest(Client, url, req, cts.Token).ConfigureAwait(false);
                if (IsServerDown(r.StatusCode))
                    host.SetOffline();
                return r;
            }
            catch (Exception ex)
            {
                FetchErrors.OnException(ex);
                host.SetOffline();
                return ErrorResponse(ex, cts.IsCancellationRequested);
            }
        }

        ProxyData OfflineResponse()
        {
            var d = OfflineBody;
            return new ProxyData(HttpServerMethods.GET, new String[]
            {
                "Content-Type:" + MimeTypeMap.PlainText,
                "Content-Length:" + d.Length,
                "Cache-Control:no-store",
                "Retry-After:" + Math.Max(1, (int)Timeout.TotalSeconds),
                "X-Cache:OFFLINE",
            }, d, 503);
        }

        static readonly Byte[] OfflineBody = Encoding.UTF8.GetBytes("The source server is offline [503]");

        static ProxyData ErrorResponse(Exception ex, bool timeOut)
        {
            //  Sensitive information (internal host names, private ips etc) is removed from the message
            var status = timeOut ? 504 : 502;
            var d = Encoding.UTF8.GetBytes(String.Concat(timeOut ? "The source server timed out" : ex.SafeMessage(), " [", status, "]"));
            return new ProxyData(HttpServerMethods.GET, new String[]
            {
                "Content-Type:" + MimeTypeMap.PlainText,
                "Content-Length:" + d.Length,
                "Cache-Control:no-store",
            }, d, status);
        }

        /// <summary>
        /// Write a non cached response.
        /// </summary>
        static Task WriteResponse(HttpServerRequest context, ProxyData r)
        {
            context.SetResHeader("X-Cache", "PASS");
            return ProxyTools.SetToRequest(context, r);
        }

        /// <summary>
        /// Serve a cache entry.
        /// </summary>
        Task Serve(HttpServerRequest context, CacheEntry e, ProxyData fresh, bool head, String status)
        {
            if (fresh != null)
            {
                foreach (var h in fresh.Headers.Nullable())
                    if (h.StartsWith("Set-Cookie:", StringComparison.OrdinalIgnoreCase))
                        context.UpdateCookie(h.Substring(11));
            }
            var etag = e.ETag;
            context.SetResHeader("X-Cache", status);
            if (etag.FastEquals(context.IfNoneMatch))
            {
                context.SetResHeader("ETag", etag);
                context.SetResStatusCode(304);
                return Task.CompletedTask;
            }
            var data = GetVariant(e, context, out var enc);
            var src = e.Headers;
            var sl = src.Length;
            var headers = new String[sl + (enc == null ? 3 : 4)];
            Array.Copy(src, headers, sl);
            headers[sl] = "ETag:" + etag;
            headers[sl + 1] = "Vary:Accept-Encoding";
            headers[sl + 2] = "Content-Length:" + data.Length;
            if (enc != null)
                headers[sl + 3] = "Content-Encoding:" + enc;
            return ProxyTools.SetToRequest(context, new ProxyData(context.HttpMethod, headers, head ? null : data, e.StatusCode));
        }

        /// <summary>
        /// Get the data to send to a client (using an encoding that the client accepts).
        /// </summary>
        /// <param name="e">The entry</param>
        /// <param name="context">The request</param>
        /// <param name="enc">The content encoding of the returned data, null for none</param>
        /// <returns>The data</returns>
        Byte[] GetVariant(CacheEntry e, HttpServerRequest context, out String enc)
        {
            var srcEnc = e.Encoding;
            var accepted = context.AcceptedEncoders;
            if ((srcEnc != null) && (accepted?.Contains(srcEnc) ?? false))
            {
                enc = srcEnc;
                return e.Data;
            }
            if ((srcEnc == null) && !e.Compressible)
            {
                enc = null;
                return e.Data;
            }
            var target = Compression?.GetEncoder(context.AcceptEncoding, accepted);
            var code = target?.Item1.HttpCode ?? "";
            if (code.Length <= 0)
            {
                if (srcEnc == null)
                {
                    enc = null;
                    return e.Data;
                }
            }
            enc = code.Length <= 0 ? null : code;
            if (e.Variants.TryGetValue(code, out var v))
                return v;
            using var __ = PerfMon.Track("Recompress");
            Byte[] raw = e.Data;
            if (srcEnc != null)
            {
                var dec = CompManager.GetFromHttp(srcEnc);
                if (dec == null)
                {
                    //  Can't decode, send as is
                    enc = srcEnc;
                    return e.Data;
                }
                raw = dec.GetDecompressedArray(raw);
            }
            v = code.Length <= 0 ? raw : target.Item1.GetCompressed(raw, target.Item2, true).ToArray();
            Interlocked.Increment(ref Recompressed);
            e.Variants.TryAdd(code, v);
            return v;
        }

        #endregion//Request handling

        #region Headers

        /// <summary>
        /// Client headers that are never sent to the source when fetching data for the cache (the proxy sets its own).
        /// </summary>
        static readonly IReadOnlySet<String> NotForwardedWhenCaching = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            "If-None-Match",
            "If-Modified-Since",
            "If-Match",
            "If-Unmodified-Since",
            "If-Range",
            "Range",
            "Accept-Encoding",
            "Cache-Control",
            "Pragma",
            "Connection",
            "Keep-Alive",
            "TE",
        };

        /// <summary>
        /// Source response headers that are not stored (the proxy sets its own, or they are hop-by-hop).
        /// </summary>
        static readonly IReadOnlySet<String> NotStored = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            "Content-Length",
            "Content-Encoding",
            "Transfer-Encoding",
            "Connection",
            "Keep-Alive",
            "Proxy-Connection",
            "Trailer",
            "Upgrade",
            "Set-Cookie",
            "Date",
            "Age",
            "Vary",
            "ETag",
            "X-Cache",
        };

        /// <summary>
        /// Remove Cookie / Authorization headers that shouldn't be forwarded, get the forwarded credentials (part of the cache key) and if there is a Range header.
        /// </summary>
        static String[] FilterClientHeaders(String[] headers, bool forwardCookies, bool forwardAuth, out String cred, out bool haveRange)
        {
            cred = null;
            haveRange = false;
            if (headers == null)
                return null;
            var l = new List<String>(headers.Length);
            foreach (var h in headers)
            {
                if (h.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!forwardCookies)
                        continue;
                    cred = cred == null ? h : String.Concat(cred, "\r", h);
                }
                else if (h.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!forwardAuth)
                        continue;
                    cred = cred == null ? h : String.Concat(cred, "\r", h);
                }
                else if (h.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                {
                    haveRange = true;
                }
                l.Add(h);
            }
            if (cred != null)
                cred = HashName(cred);
            return l.ToArray();
        }

        /// <summary>
        /// Get the headers to send to the source when fetching data for the cache.
        /// </summary>
        static String[] UpstreamHeaders(String[] headers, CacheEntry e)
        {
            var l = new List<String>((headers?.Length ?? 0) + 3);
            var nf = NotForwardedWhenCaching;
            foreach (var h in headers.Nullable())
            {
                var key = h.SplitFirst(':');
                if (!nf.Contains(key.Trim()))
                    l.Add(h);
            }
            l.Add("Accept-Encoding:" + AcceptEncodingValue());
            if (e != null)
            {
                if (e.SourceETag != null)
                    l.Add("If-None-Match:" + e.SourceETag);
                if (e.SourceLastModified != null)
                    l.Add("If-Modified-Since:" + e.SourceLastModified);
            }
            return l.ToArray();
        }

        /// <summary>
        /// The Accept-Encoding header to send to the source (all encodings that we can decode, so data can always be re-compressed for a client).
        /// </summary>
        static String AcceptEncodingValue()
        {
            var codes = CompManager.HttpCodes.ToArray();
            var c = CachedAccept;
            if ((c != null) && (c.Item1 == codes.Length))
                return c.Item2;
            Array.Sort(codes, StringComparer.Ordinal);
            var v = String.Join(", ", codes);
            CachedAccept = Tuple.Create(codes.Length, v);
            return v;
        }

        static Tuple<int, String> CachedAccept;

        /// <summary>
        /// Get the caching information from response headers.
        /// </summary>
        /// <param name="headers">The headers</param>
        /// <param name="maxAge">The s-maxage or max-age (in seconds), -1 if none</param>
        /// <param name="noStore">True if the response may not be stored in a shared cache ("no-store", "private" or "Vary: *")</param>
        static void GetCacheInfo(String[] headers, out int maxAge, out bool noStore)
        {
            maxAge = -1;
            int sMaxAge = -1;
            noStore = false;
            foreach (var header in headers.Nullable())
            {
                if (header.StartsWith("Cache-Control:", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var y in header.Substring(14).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (y.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(y.AsSpan(8).Trim('"'), out var x) && (x > maxAge))
                                maxAge = x;
                            continue;
                        }
                        if (y.StartsWith("s-maxage=", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(y.AsSpan(9).Trim('"'), out var x) && (x > sMaxAge))
                                sMaxAge = x;
                            continue;
                        }
                        //  "private" may have field names (private="Set-Cookie"), treat as private
                        if (y.StartsWith("private", StringComparison.OrdinalIgnoreCase) || y.Equals("no-store", StringComparison.OrdinalIgnoreCase))
                            noStore = true;
                    }
                    continue;
                }
                if (header.StartsWith("Vary:", StringComparison.OrdinalIgnoreCase))
                {
                    if (header.AsSpan(5).Trim().SequenceEqual("*"))
                        noStore = true;
                }
            }
            if (sMaxAge >= 0)
                maxAge = sMaxAge;
        }

        #endregion//Headers

        #region Cache entries

        /// <summary>
        /// A cached source response.
        /// </summary>
        sealed class CacheEntry
        {
            public CacheEntry(String key, int statusCode, String[] headers, String encoding, Byte[] data, String sourceETag, String sourceLastModified, String etag, int maxAge, bool compressible, long checkedTicks)
            {
                Key = key;
                StatusCode = statusCode;
                Headers = headers;
                Encoding = encoding;
                Data = data;
                SourceETag = sourceETag;
                SourceLastModified = sourceLastModified;
                ETag = etag;
                MaxAge = maxAge;
                Compressible = compressible;
                Checked = checkedTicks;
            }

            public readonly String Key;
            public readonly int StatusCode;
            /// <summary>
            /// The stored headers ("Name:value"), without Content-Length, Content-Encoding, ETag etc
            /// </summary>
            public readonly String[] Headers;
            /// <summary>
            /// The content encoding of <see cref="Data"/> (lower case), null if not compressed
            /// </summary>
            public readonly String Encoding;
            /// <summary>
            /// The body as served by the source
            /// </summary>
            public readonly Byte[] Data;
            public readonly String SourceETag;
            public readonly String SourceLastModified;
            /// <summary>
            /// The etag sent to clients (the source etag or a hash of the data)
            /// </summary>
            public readonly String ETag;
            /// <summary>
            /// The max-age of the source (seconds), -1 if none
            /// </summary>
            public int MaxAge;
            /// <summary>
            /// True if the mime type is compressible
            /// </summary>
            public readonly bool Compressible;
            /// <summary>
            /// The time (UTC ticks) of the last fetch or validation
            /// </summary>
            public long Checked;
            /// <summary>
            /// When the entry expires from the memory cache (UTC ticks)
            /// </summary>
            public long MemExpires;
            /// <summary>
            /// The data using other encodings (memory only), "" = identity
            /// </summary>
            public readonly ConcurrentDictionary<String, Byte[]> Variants = new(StringComparer.Ordinal);
        }

        static bool IsFresh(CacheEntry e, Ep ep)
        {
            var check = ep.CheckTicks;
            if (check < 0)
                check = Math.Max(0, e.MaxAge) * TimeSpan.TicksPerSecond;
            return (DateTime.UtcNow.Ticks - Interlocked.Read(ref e.Checked)) < check;
        }

        void PutMem(Ep ep, CacheEntry e)
        {
            if ((ep.MemTicks <= 0) || (e.Data.Length > MaxMemItemSize))
                return;
            Interlocked.Exchange(ref e.MemExpires, DateTime.UtcNow.Ticks + ep.MemTicks);
            Mem.Set(e.Key, e);
        }

        /// <summary>
        /// Create a cache entry from a source response, null if the response isn't cacheable.
        /// </summary>
        static CacheEntry TryCreateEntry(String key, ProxyData r)
        {
            if ((r.StatusCode != 200) || (r.Method != HttpServerMethods.GET))
                return null;
            var headers = r.Headers ?? Array.Empty<String>();
            GetCacheInfo(headers, out var maxAge, out var noStore);
            if (noStore)
                return null;
            String encoding = null;
            String etag = null;
            String lastModified = null;
            String mime = null;
            var keep = new List<String>(headers.Length);
            var ns = NotStored;
            foreach (var h in headers)
            {
                var name = h.SplitFirst(':', out var value).Trim();
                value = value?.Trim();
                if (name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    if (String.IsNullOrEmpty(value) || value.Equals("identity", StringComparison.OrdinalIgnoreCase))
                        continue;
                    //  Multiple encodings are not supported
                    if (value.IndexOf(',') >= 0)
                        return null;
                    encoding = value.ToLowerInvariant();
                    continue;
                }
                if (name.Equals("ETag", StringComparison.OrdinalIgnoreCase))
                {
                    etag ??= value;
                    continue;
                }
                if (name.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase))
                    lastModified ??= value;
                else if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    mime ??= value;
                if (ns.Contains(name))
                    continue;
                keep.Add(h);
            }
            var data = r.Data ?? Array.Empty<Byte>();
            bool compressible = false;
            if ((encoding == null) && (mime != null))
            {
                var m = mime.SplitFirst(';').Trim().ToLowerInvariant();
                if (MimeTypeMap.TryGetMimeType(m, out var mt, false))
                    compressible = mt.Item2;
                else
                    compressible = m.StartsWith("text/", StringComparison.Ordinal);
            }
            var clientEtag = etag ?? String.Concat("\"px", HttpServerTools.ToEtag(data), "\"");
            return new CacheEntry(key, r.StatusCode, keep.ToArray(), encoding, data, etag, lastModified, clientEtag, maxAge, compressible, DateTime.UtcNow.Ticks);
        }

        #endregion//Cache entries

        #region Disc cache

        const int MetaMagic = 0x31585048; // "HPX1"
        const String MetaExt = ".m";
        const String DataExt = ".d";
        const String TempExt = ".tmp";

        /// <summary>
        /// Get a hex hash of a string (for file / folder names).
        /// </summary>
        static String HashName(String s)
        {
            Span<Byte> h = stackalloc Byte[32];
            SHA256.HashData(Encoding.UTF8.GetBytes(s), h);
            return Convert.ToHexString(h.Slice(0, 16));
        }

        String GetDiscBase(String key)
        {
            var name = HashName(key);
            return Path.Combine(Folders.SelectFolder(DiscFolders, name), name);
        }

        /// <summary>
        /// Read an entry from disc, null if missing, expired or invalid.
        /// </summary>
        async Task<CacheEntry> ReadDisc(String key)
        {
            using var __ = PerfMon.Track(nameof(ReadDisc));
            var b = GetDiscBase(key);
            var metaFile = b + MetaExt;
            try
            {
                var fi = new FileInfo(metaFile);
                if (!fi.Exists)
                    return null;
                if (fi.LastWriteTimeUtc <= DateTime.UtcNow)
                    return null;
                var meta = await File.ReadAllBytesAsync(metaFile).ConfigureAwait(false);
                using var ms = new MemoryStream(meta, false);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                if (r.ReadInt32() != MetaMagic)
                    return null;
                var k = r.ReadString();
                if (!k.FastEquals(key))
                    return null;
                var status = r.ReadInt32();
                var enc = NullIfEmpty(r.ReadString());
                var setag = NullIfEmpty(r.ReadString());
                var slm = NullIfEmpty(r.ReadString());
                var etag = r.ReadString();
                var maxAge = r.ReadInt32();
                var compressible = r.ReadBoolean();
                var checkedTicks = r.ReadInt64();
                var size = r.ReadInt64();
                var hc = r.ReadInt32();
                var headers = new String[hc];
                for (int i = 0; i < hc; ++i)
                    headers[i] = r.ReadString();
                var dataFile = b + DataExt;
                var df = new FileInfo(dataFile);
                if ((!df.Exists) || (df.Length != size))
                    return null;
                var data = await File.ReadAllBytesAsync(dataFile).ConfigureAwait(false);
                if (data.Length != size)
                    return null;
                return new CacheEntry(key, status, headers, enc, data, setag, slm, etag, maxAge, compressible, checkedTicks);
            }
            catch (Exception ex)
            {
                DiscErrors.OnException(ex);
                return null;
            }
        }

        static String NullIfEmpty(String s) => String.IsNullOrEmpty(s) ? null : s;

        static Byte[] GetMeta(CacheEntry e)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(MetaMagic);
                w.Write(e.Key);
                w.Write(e.StatusCode);
                w.Write(e.Encoding ?? "");
                w.Write(e.SourceETag ?? "");
                w.Write(e.SourceLastModified ?? "");
                w.Write(e.ETag);
                w.Write(e.MaxAge);
                w.Write(e.Compressible);
                w.Write(Interlocked.Read(ref e.Checked));
                w.Write((long)e.Data.Length);
                var h = e.Headers;
                w.Write(h.Length);
                foreach (var x in h)
                    w.Write(x);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Write a file using a temp file (the final file is replaced atomically).
        /// </summary>
        static async Task WriteFile(String fileName, Byte[] data, DateTime? lastWriteUtc)
        {
            var temp = String.Concat(fileName, ".", Guid.NewGuid().ToString("N"), TempExt);
            try
            {
                try
                {
                    await File.WriteAllBytesAsync(temp, data).ConfigureAwait(false);
                }
                catch (DirectoryNotFoundException)
                {
                    //  The folder was deleted while running (Folders only creates a folder the first time it's seen)
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    await File.WriteAllBytesAsync(temp, data).ConfigureAwait(false);
                }
                //  Set before moving, so the pruner never sees the final file with a wrong time
                if (lastWriteUtc != null)
                    File.SetLastWriteTimeUtc(temp, lastWriteUtc.Value);
                File.Move(temp, fileName, true);
            }
            catch
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                }
                throw;
            }
        }

        /// <summary>
        /// Write an entry to disc.
        /// </summary>
        /// <param name="ep">The end point (for the duration)</param>
        /// <param name="e">The entry</param>
        /// <param name="newData">True to write the data, false to only update the meta data (if the data file is still there)</param>
        async Task WriteDisc(Ep ep, CacheEntry e, bool newData)
        {
            using var __ = PerfMon.Track(nameof(WriteDisc));
            var b = GetDiscBase(e.Key);
            var metaFile = b + MetaExt;
            var dataFile = b + DataExt;
            try
            {
                if (newData)
                {
                    //  Remove the meta data first, so a crash never leaves meta data pointing to the wrong data
                    try
                    {
                        File.Delete(metaFile);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        //  Created by WriteFile
                    }
                    await WriteFile(dataFile, e.Data, null).ConfigureAwait(false);
                }
                await WriteFile(metaFile, GetMeta(e), new DateTime(DateTime.UtcNow.Ticks + ep.DiscTicks, DateTimeKind.Utc)).ConfigureAwait(false);
                Interlocked.Increment(ref DiscWrites);
            }
            catch (Exception ex)
            {
                DiscErrors.OnException(ex);
            }
        }

        /// <summary>
        /// Update the meta data and extend the disc duration (writes the data too if it was pruned).
        /// </summary>
        Task TouchDisc(Ep ep, CacheEntry e)
        {
            var b = GetDiscBase(e.Key);
            var newData = !File.Exists(b + MetaExt) || !File.Exists(b + DataExt);
            return WriteDisc(ep, e, newData);
        }

        void DeleteDisc(String key)
        {
            var b = GetDiscBase(key);
            try
            {
                File.Delete(b + MetaExt);
                File.Delete(b + DataExt);
            }
            catch (Exception ex)
            {
                DiscErrors.OnException(ex);
            }
        }

        /// <summary>
        /// Delete expired files from the disc cache (a scheduled task).
        /// </summary>
        async Task Prune()
        {
            using var __ = PerfMon.Track(nameof(Prune));
            await Task.Yield();
            var now = DateTime.UtcNow;
            var orphanTime = now.AddHours(-1);
            foreach (var folder in DiscFolders)
            {
                try
                {
                    var di = new DirectoryInfo(folder);
                    if (!di.Exists)
                        continue;
                    foreach (var f in di.EnumerateFiles())
                    {
                        try
                        {
                            var name = f.Name;
                            if (name.EndsWith(MetaExt, StringComparison.Ordinal))
                            {
                                if (f.LastWriteTimeUtc > now)
                                    continue;
                                f.Delete();
                                File.Delete(Path.ChangeExtension(f.FullName, DataExt));
                                Interlocked.Increment(ref DiscPruned);
                                continue;
                            }
                            if (f.LastWriteTimeUtc > orphanTime)
                                continue;
                            if (name.EndsWith(DataExt, StringComparison.Ordinal))
                            {
                                //  Data without meta data
                                if (File.Exists(Path.ChangeExtension(f.FullName, MetaExt)))
                                    continue;
                                f.Delete();
                                Interlocked.Increment(ref DiscPruned);
                                continue;
                            }
                            if (name.EndsWith(TempExt, StringComparison.Ordinal))
                                f.Delete();
                        }
                        catch (Exception ex)
                        {
                            DiscErrors.OnException(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiscErrors.OnException(ex);
                }
            }
        }

        #endregion//Disc cache

        #region Path encoding

        /// <summary>
        /// Url encode every segment of a decoded relative path (so that decoded '/', '?', '&amp;', '#' etc can't change the structure of the upstream url).
        /// </summary>
        /// <param name="path">The decoded path</param>
        /// <returns>The encoded path</returns>
        /// <exception cref="HttpResponseException">Thrown (400) if the path contains a "." or ".." segment (also when separated using '\')</exception>
        public static String EncodePath(String path)
        {
            if (String.IsNullOrEmpty(path))
                return "";
            var parts = path.Split('/');
            var l = parts.Length;
            for (int i = 0; i < l; ++i)
            {
                var s = parts[i];
                if (s.Length <= 0)
                    continue;
                if (HaveDotSegment(s))
                    throw new HttpResponseException(400, "The path may not contain \".\" or \"..\" segments");
                parts[i] = Uri.EscapeDataString(s);
            }
            return String.Join('/', parts);
        }

        /// <summary>
        /// Check if any '\' separated part of a path segment is "." or ".." (same as s.Split('\\') without the allocations)
        /// </summary>
        /// <param name="s">The path segment</param>
        /// <returns>True if any part is "." or ".."</returns>
        static bool HaveDotSegment(ReadOnlySpan<Char> s)
        {
            for (; ; )
            {
                var k = s.IndexOf('\\');
                var x = k < 0 ? s : s.Slice(0, k);
                if (x is "." or "..")
                    return true;
                if (k < 0)
                    return false;
                s = s.Slice(k + 1);
            }
        }

        #endregion//Path encoding

        #region Stats

        long RequestCount;
        long MemHits;
        long DiscHits;
        long Revalidated;
        long Fetched;
        long StaleServed;
        long OfflineResponses;
        long PassThroughs;
        long Recompressed;
        long DiscWrites;
        long DiscPruned;
        readonly ExceptionTracker FetchErrors = new ExceptionTracker();
        readonly ExceptionTracker DiscErrors = new ExceptionTracker();

        /// <inheritdoc/>
        public IEnumerable<Stats> GetStats()
        {
            var sys = TypeName;
            yield return new Stats(sys, "Requests", Interlocked.Read(ref RequestCount), "Number of requests handled");
            yield return new Stats(sys, "MemHits", Interlocked.Read(ref MemHits), "Number of requests served from memory without contacting the source");
            yield return new Stats(sys, "DiscHits", Interlocked.Read(ref DiscHits), "Number of requests served from disc without contacting the source");
            yield return new Stats(sys, "Revalidated", Interlocked.Read(ref Revalidated), "Number of times the source responded with a 304 (cached data is unchanged)");
            yield return new Stats(sys, "Fetched", Interlocked.Read(ref Fetched), "Number of cacheable responses fetched from the source");
            yield return new Stats(sys, "Stale", Interlocked.Read(ref StaleServed), "Number of times stale data was served because the source was offline");
            yield return new Stats(sys, "Offline", Interlocked.Read(ref OfflineResponses), "Number of error responses because the source was offline");
            yield return new Stats(sys, "PassThrough", Interlocked.Read(ref PassThroughs), "Number of requests passed through to the source (not cached)");
            yield return new Stats(sys, "Recompressed", Interlocked.Read(ref Recompressed), "Number of times cached data was re-compressed for a client");
            yield return new Stats(sys, "DiscWrites", Interlocked.Read(ref DiscWrites), "Number of disc cache writes");
            yield return new Stats(sys, "DiscPruned", Interlocked.Read(ref DiscPruned), "Number of disc cache files pruned");
            foreach (var h in Hosts.Values)
            {
                yield return new Stats(sys, "Host." + h.ProbeUrl + ".Offline", h.IsOffline, "True if the source host is currently considered offline");
                yield return new Stats(sys, "Host." + h.ProbeUrl + ".OfflineCount", Interlocked.Read(ref h.OfflineCount), "Number of times the source host went offline");
            }
            foreach (var x in Mem.GetStats(sys, "Mem."))
                yield return x;
            foreach (var x in FetchErrors.GetStats(sys, "FetchEx."))
                yield return x;
            foreach (var x in DiscErrors.GetStats(sys, "DiscEx."))
                yield return x;
        }

        #endregion//Stats

    }

}
