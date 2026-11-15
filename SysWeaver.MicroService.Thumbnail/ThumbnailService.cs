using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Compression;
using SysWeaver.Media;
using SysWeaver.Net;
using SysWeaver.Net.IconModule;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// When added to a service, all files (server by the FileHttpServerModule) can be returned as a thumbnail, by adding "?Key" to the url.
    /// The Key consists of the prefix and the supported resolution (resolutions can be specified in the params), ex: "?Thumb32x32".
    /// The resolutions are only there for guidance and are not guaranteed.
    /// File-types that aren't recognized return their file extension icon instead (typically .svg).
    /// </summary>
    [IsMicroService]
    [RequiredDep<IconHttpServerModule, FileHttpServerModule>]
    public sealed class ThumbnailService : IDisposable, IFileTransformer, IPerfMonitored, IHttpServerModule, IHaveStats
    {
        /// <inheritdoc />
        public override string ToString()
        {
            var rs = MapRes;
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " resolution: " : " resolutions: ", String.Join(", ", rs.Select(x => String.Concat(x.Key.ToQuoted(), " @ ", x.Value.Item1, 'x', x.Value.Item2))));
        }

        /// <summary>
        /// Create the service and register a file transformer for each configured resolution
        /// </summary>
        /// <param name="manager">The service manager</param>
        /// <param name="p">Parameters, null uses defaults</param>
        public ThumbnailService(ServiceManager manager, ThumbnailParams p = null)
        {
            p = p ?? new ThumbnailParams();
            CachedAsyncHandler = HandleAsync;
            var pm = PerfMon;
            pm.Enabled = p.PerMon;
            IconTrackers = new PerfTrackers(pm, null, ".Icon");
            IconBuildTrackers = new PerfTrackers(pm, null, ".Icon.Build");
            AllTrackers = new PerfTrackers(pm, null, ".All");
            IconModule = manager.Get<IconHttpServerModule>();
            var fs = manager.Get<FileHttpServerModule>();
            Fs = fs;
            PngMime = MimeTypeMap.GetMimeType("png");
            SvgMime = MimeTypeMap.GetMimeType("svg");
            MediaInfo.RetryFailedSeconds = Math.Max(p.RetryFailedSeconds, 1);
            var cacheDuration = TimeSpan.FromSeconds(Math.Max(p.CacheSeconds, 1));
            CacheDuration = cacheDuration;
            var prefix = p.Prefix ?? "";
            Prefix = prefix;
            var res = MapRes;
            foreach (var r in p.Resolutions)
            {
                var x = r.Split('x');
                var w = int.Parse(x[0]);
                var h = int.Parse(x[1]);
                var rname = String.Concat(prefix, w, 'x', h);
                res.TryAdd(rname, Tuple.Create(w, h, 1L, new FastMemCache<string, MediaInfo>(cacheDuration, StringComparer.Ordinal)));
                if (!fs.AddFileTransformer(rname, this))
                    res.TryRemove(rname, out var _);
            }
            res.TryAdd(Prefix, Tuple.Create(0, 0, 1L, new FastMemCache<string, MediaInfo>(cacheDuration, StringComparer.Ordinal)));
            Comp = CompManager.GetFromHttp("deflate");
            Options = new RequestOptions(30, 30, 0, null, null);
        }
        readonly RequestOptions Options;

        /// <summary>
        /// The prefix used before the width and height of a thumbnail key, ex: "Thumb"
        /// </summary>
        public readonly String Prefix;
        readonly TimeSpan CacheDuration;
        readonly IconHttpServerModule IconModule;
        readonly Tuple<string, bool> PngMime;
        readonly Tuple<string, bool> SvgMime;
        readonly FileHttpServerModule Fs;
        readonly ICompType Comp;

        /// <summary>
        /// Remove all file transformers
        /// </summary>
        public void Dispose()
        {
            var fs = Fs;
            foreach (var x in MapRes)
                fs.RemoveFileTransformer(x.Key);
        }

        /// <summary>
        /// Get (cached) media information for a file, using the default key (<see cref="Prefix"/>, no fixed size)
        /// </summary>
        /// <param name="filename">The file to get information about</param>
        /// <param name="fill">If true the thumbnail fills the requested size</param>
        /// <param name="waitForResult">If true wait for the information to be built, else a result might not be available yet</param>
        /// <returns>The media information, null if the key isn't registered</returns>
        public async Task<MediaInfo> GetMediaInfo(String filename, bool fill = false, bool waitForResult = true)
        {
            using var perf = PerfMon.Track(nameof(GetMediaInfo));
            return await GetMediaInfo(Prefix, filename, fill, waitForResult).ConfigureAwait(false);
        }

        /// <summary>
        /// Get (cached) media information for a file and a thumbnail size
        /// </summary>
        /// <param name="key">The key of the thumbnail size, ex: "Thumb32x32"</param>
        /// <param name="filename">The file to get information about</param>
        /// <param name="fill">If true the thumbnail fills the requested size</param>
        /// <param name="waitForResult">If true wait for the information to be built, else a result might not be available yet</param>
        /// <returns>The media information, null if the key isn't registered</returns>
        public async Task<MediaInfo> GetMediaInfo(String key, String filename, bool fill = false, bool waitForResult = true)
        {
            using var perf = PerfMon.Track(nameof(GetMediaInfo));
            if (!MapRes.TryGetValue(key, out var x))
                return null;
            return await x.Item4.GetOrUpdateAsync(filename + (fill ? ":f" : ""), fn =>
            {
//                if (waitForResult)
                    return MediaInfo.GetAsync(filename, x.Item1, x.Item2, fill, PerfMon, key + ".Build");
//                TaskExt.StartNewAsyncChain(() => MediaInfo.GetAsync(filename, x.Item1, x.Item2, fill, PerfMon, key + ".Build"));
//                return NoInfoTask;
                }, waitForResult).ConfigureAwait(false);
        }

        static readonly Task<MediaInfo> NoInfoTask = Task.FromResult((MediaInfo)null);

        /// <summary>
        /// Forget the cached media information and thumbnails of a file (of every thumbnail size), ex: after the file was replaced by an upload,
        /// so that the next request reads the new file
        /// </summary>
        /// <param name="filename">The full path of the file</param>
        public void Invalidate(String filename)
        {
            foreach (var x in MapRes)
            {
                var c = x.Value.Item4;
                c.Remove(filename);
                c.Remove(filename + ":f");
            }
        }

        /// <summary>
        /// Perf trackers: key + ".Icon", key + ".Icon.Build", key + ".All"
        /// </summary>
        readonly PerfTrackers IconTrackers, IconBuildTrackers, AllTrackers;

        IHttpRequestHandler GetIcon(String key, String mimeCode)
        {
            using (IconTrackers.Track(key))
            {
                var cache = MimeCache;
                if (cache.TryGetValue(mimeCode, out var f))
                    return f;
                using (IconBuildTrackers.Track(key))
                {
                    var options = Options;
                    var data = IconModule.GetFromMime(mimeCode);
                    var cmp = Comp.GetCompressed(Encoding.UTF8.GetBytes(data), CompEncoderLevels.Best);
                    f = new StaticMemoryHttpRequestHandler(null, null, cmp, SvgMime.Item1, options.Compression, 30, 5, null, null, Comp, options.Auth);
                    cache.TryAdd(mimeCode, f);
                    return f;
                }
            }
        }

        /// <summary>
        /// Get a request handler for the thumbnail of a file
        /// </summary>
        /// <param name="key">The key of the thumbnail size, ex: "Thumb32x32"</param>
        /// <param name="filename">The file to get a thumbnail of</param>
        /// <param name="fill">If true the thumbnail fills the requested size</param>
        /// <returns>A handler for the thumbnail, the icon of the file type if there is no thumbnail, or null if the file has no extension</returns>
        public async Task<IHttpRequestHandler> GetThumb(String key, String filename, bool fill = false)
        {
            using var perf = PerfMon.Track(nameof(GetThumb));
            var m = await GetMediaInfo(key, filename, fill).ConfigureAwait(false);
            if (m?.IconFile != null)
                return new FileHttpRequestHandler(PngMime, new FileInfo(m.IconFile), Options, false, null);
            var i = filename.IndexOf('?');
            if (i >= 0)
                filename = filename.Substring(0, i);
            i = filename.LastIndexOf('.');
            if (i < 0)
                return null;
            var ext = i < 0 ? "" : filename.Substring(i + 1);
            var mime = MimeTypeMap.GetMimeType(ext, false);
            return GetIcon(key, mime.Item1);
        }

        /*
        public async Task<IHttpRequestHandler> GetThumbAsync(String key, String filename, bool fill = false)
        {
            using var perf = PerfMon.Track(nameof(GetThumbAsync));
            var m = await GetMediaInfo(key, filename, fill).ConfigureAwait(false);
            if (m == null)
                return null;
            if (m.IconFile == null)
                return null;
            return new FileHttpRequestHandler(PngMime, new FileInfo(m.IconFile), Options, false, null);
        }*/




        /// <summary>
        /// Not supported (requests are handled by <see cref="AsyncHandler"/>)
        /// </summary>
        /// <param name="context">The incoming request</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotImplementedException">Always</exception>
        public IHttpRequestHandler Handler(HttpServerRequest context)
            => throw new NotImplementedException();

        /// <inheritdoc />
        public String[] OnlyForPrefixes { get; } = ["thumbnail/"];

        /// <summary>
        /// Async handler for "thumbnail/[resolution name].png?[source url]" requests
        /// </summary>
        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler => CachedAsyncHandler;

        readonly Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> CachedAsyncHandler;

        /// <summary>
        /// Get the resolution name from a local url "thumbnail/{name}.png"
        /// </summary>
        /// <param name="url">The local url</param>
        /// <returns>The resolution name or null if the url is invalid</returns>
        static String GetResolutionName(String url)
        {
            //if (!url.FastStartsWith("thumbnail/"))
                //return null;
            var name = url.AsSpan(10);
            var d = name.IndexOf('.');
            if (d < 0)
                return null;
            if (!name.Slice(d + 1).SequenceEqual("png"))
                return null;
            return name.Slice(0, d).ToString();
        }

        async ValueTask<IHttpRequestHandler> HandleAsync(HttpServerRequest context)
        {
            var rname = GetResolutionName(context.LocalUrl);
            if (rname == null)
                return null;
            //  The query string is the source url (Url is already decoded), QueryStringStart is 0 if there is no query
            var qs = context.QueryStringStart;
            if (qs <= 0)
                return null;
            var url = context.Url;
            var sl = url.Length - qs;
            if (sl <= 0)
                return null;
            var f = url[qs];
            String src;
            if ((f == '"') || (f == '\''))
            {
                if (sl <= 2)
                    return null;
                if (url[url.Length - 1] != f)
                    return null;
                src = url.Substring(qs + 1, sl - 2);
            }
            else
            {
                src = url.Substring(qs);
            }
            if (!FileHash.IsWeb(src))
                return null;
            var m = await GetMediaInfo(rname, src, false).ConfigureAwait(false);
            if (m == null)
                return null;
            if (m.IconFile == null)
                return null;
            return new FileHttpRequestHandler(PngMime, new FileInfo(m.IconFile), Options, false, null);
        }

        /// <inheritdoc />
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        /// <summary>
        /// Add a new thumbnail size to support
        /// </summary>
        /// <param name="width">The max width in pixels</param>
        /// <param name="height">The max height in pixels</param>
        /// <param name="key">An optional key, defaults to the prefix followed by the size, ex: "Thumb64x64"</param>
        /// <returns>The key to use to get this thumbnail "?Key", ex: "?Thumb64x64", or null if the file transformer could not be registered.
        /// Adding an existing key with the same size increases its use count (see <see cref="RemoveThumbSize"/>)</returns>
        /// <exception cref="ArgumentException">The key is already registered with another size</exception>
        public String AddThumbSize(int width, int height, String key = null)
        {
            var c = MapRes;
            key = key ?? String.Concat(Prefix, width, 'x', height);
            lock (c)
            {
                if (c.TryGetValue(key, out var existing))
                {
                    //  Keep the existing registration (and its cached items), the size must match
                    if ((existing.Item1 != width) || (existing.Item2 != height))
                        throw new ArgumentException(String.Concat("The thumbnail key \"", key, "\" is already registered with the size ", existing.Item1, 'x', existing.Item2, ", can't register it with the size ", width, 'x', height, '!'), nameof(key));
                    c[key] = Tuple.Create(existing.Item1, existing.Item2, existing.Item3 + 1, existing.Item4);
                }
                else
                {
                    c.TryAdd(key, Tuple.Create(width, height, 1L, new FastMemCache<string, MediaInfo>(CacheDuration, StringComparer.Ordinal)));
                    if (!Fs.AddFileTransformer(key, this))
                    {
                        c.TryRemove(key, out var _);
                        return null;
                    }
                }
            }
            return key;
        }

        /// <summary>
        /// Remove a previously added thumbnail size (the size is removed when it has been removed as many times as it was added)
        /// </summary>
        /// <param name="key">The key returned by the AddThumbSize call</param>
        public void RemoveThumbSize(String key)
        {
            var c = MapRes;
            lock (c)
            {
                if (c.TryGetValue(key, out var existing))
                {
                    var newCount = existing.Item3 - 1;
                    if (newCount <= 0)
                    {
                        c.TryRemove(key, out var _);
                        Fs.RemoveFileTransformer(key);
                    }
                    else
                    {
                        c[key] = Tuple.Create(existing.Item1, existing.Item2, newCount, existing.Item4);
                    }
                }
            }

        }
        

        readonly ConcurrentDictionary<String, Tuple<int, int, long, FastMemCache<String, MediaInfo>>> MapRes = new ConcurrentDictionary<string, Tuple<int, int, long, FastMemCache<String, MediaInfo>>>(StringComparer.Ordinal);

        /// <inheritdoc />
        public async ValueTask<IHttpRequestHandler> Modify(string key, Tuple<string, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder, bool updateAccessTime, bool isDynamic)
        {
            using (AllTrackers.Track(key))
            {
                var m = await GetMediaInfo(key, fi.FullName, false).ConfigureAwait(false);
                if (m?.IconFile != null)
                    return new FileHttpRequestHandler(PngMime, new FileInfo(m.IconFile), options, false, null, updateAccessTime, isDynamic);
                return GetIcon(key, GetMimeCode(mime.Item1));
            }
        }

        /// <summary>
        /// Get the mime type without any parameters, ex: "text/plain; charset=UTF-8" => "text/plain"
        /// </summary>
        /// <param name="mime">The mime type</param>
        /// <returns>The part before the first ';' with trailing white spaces removed</returns>
        static String GetMimeCode(String mime)
        {
            var i = mime.IndexOf(';');
            if (i < 0)
                return mime.TrimEnd();
            return mime.AsSpan(0, i).TrimEnd().ToString();
        }

        /// <inheritdoc />
        public IEnumerable<Stats> GetStats()
        {
            long h = 0;
            long s = 0;
            long m = 0;
            long size = 0;
            foreach (var x in MapRes)
            {
                x.Value.Item4.GetStats(out var hitCount, out var semiHitCount, out var missCount, out var ss);
                h += hitCount;
                s += semiHitCount;
                m += missCount;
                size += ss;
            }
            var tot = h + s + m;
            var totOrg = tot;
            if (tot <= 0)
                tot = 1;
            String system = nameof(ThumbnailService);
            yield return new Stats(system, "Size", size, "Number of items in the cache");
            yield return new Stats(system, "Total count", totOrg, "Number of times an item have been requested");
            yield return new Stats(system, "Hit ratio", (double)(((Decimal)h) * 100M / (Decimal)tot), "The ratio of cache hits (returns an existing item)", Data.TableDataNumberAttribute.Percentage);
            yield return new Stats(system, "Semi hit ratio", (double)(((Decimal)s) * 100M / (Decimal)tot), "The ratio of semi cache hits (returns an existing item, but had to take a lock to get it, so less optimal)", Data.TableDataNumberAttribute.Percentage);
            yield return new Stats(system, "Miss ratio", (double)(((Decimal)m) * 100M / (Decimal)tot), "The ratio of cache misses (doesn't have an item, and a new one have to be created)", Data.TableDataNumberAttribute.Percentage);
        }

        readonly ConcurrentDictionary<String, IHttpRequestHandler> MimeCache = new ConcurrentDictionary<string, IHttpRequestHandler>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(ThumbnailService));
    }
}
