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
    /// File-types that aren't recognized return their file extension icon instead (tyipcally .svg).
    /// </summary>
    [IsMicroService]
    [RequiredDep<IconHttpServerModule, FileHttpServerModule>]
    public sealed class ThumbnailService : IDisposable, IFileTransformer, IPerfMonitored, IHttpServerModule, IHaveStats
    {
        public override string ToString()
        {
            var rs = MapRes;
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " resolution: " : " resolutions: ", String.Join(", ", rs.Select(x => String.Concat(x.Key.ToQuoted(), " @ ", x.Value.Item1, 'x', x.Value.Item2))));
        }

        public ThumbnailService(ServiceManager manager, ThumbnailParams p = null)
        {
            p = p ?? new ThumbnailParams();
            PerfMon.Enabled = p.PerMon;
            IconModule = manager.Get<IconHttpServerModule>();
            var fs = manager.Get<FileHttpServerModule>();
            Fs = fs;
            PngMime = MimeTypeMap.GetMimeType("png");
            SvgMime = MimeTypeMap.GetMimeType("svg");
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

        public readonly String Prefix;
        readonly TimeSpan CacheDuration;
        readonly IconHttpServerModule IconModule;
        readonly Tuple<string, bool> PngMime;
        readonly Tuple<string, bool> SvgMime;
        readonly FileHttpServerModule Fs;
        readonly ICompType Comp;

        public void Dispose()
        {
            var fs = Fs;
            foreach (var x in MapRes)
                fs.RemoveFileTransformer(x.Key);
        }

        public async Task<MediaInfo> GetMediaInfo(String filename, bool fill = false, bool waitForResult = true)
        {
            using var perf = PerfMon.Track(nameof(GetMediaInfo));
            return await GetMediaInfo(Prefix, filename, fill, waitForResult).ConfigureAwait(false);
        }

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

        IHttpRequestHandler GetIcon(String key, String mimeCode)
        {
            var mon = PerfMon;
            var monPre = key + ".";
            using (mon.Track(monPre + "Icon"))
            {
                var cache = MimeCache;
                if (cache.TryGetValue(mimeCode, out var f))
                    return f;
                using (mon.Track(monPre + "Icon.Build"))
                {
                    var options = Options;
                    var data = IconModule.GetFromMime(mimeCode);
                    var cmp = Comp.GetCompressed(Encoding.UTF8.GetBytes(data), CompEncoderLevels.Best);
                    f = new StaticMemoryHttpRequestHandler(null, null, cmp, SvgMime.Item1, options.Compression, 30, 5, null, Comp, options.Auth);
                    cache.TryAdd(mimeCode, f);
                    return f;
                }
            }
        }

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




        public IHttpRequestHandler Handler(HttpServerRequest context)
            => throw new NotImplementedException();

        public String[] OnlyForPrefixes { get; } = ["thumbnail/"];

        /// <summary>
        /// An optional async handler
        /// </summary>
        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler => async context =>
        {
            var url = context.LocalUrl;
            //if (!url.FastStartsWith("thumbnail/"))
                //return null;
            var name = url.Substring(10);
            var ext = name.Split('.');
            if (ext.Length != 2)
                return null;
            if (ext[1] != "png")
                return null;
            var rname = ext[0];
            var src = HttpUtility.UrlDecode(context.Url.Substring(context.QueryStringStart));
            var sl = src.Length;
            if (sl <= 0)
                return null;
            var f = src[0];
            if ((f == '"') || (f == '\''))
            {
                if (sl <= 2)
                    return null;
                if (src[sl - 1] != f)
                    return null;
                src = src.Substring(1, sl - 2);
            }
            if (!FileHash.IsWeb(src))
                return null;
            var m = await GetMediaInfo(rname, src, false).ConfigureAwait(false);
            if (m == null)
                return null;
            if (m.IconFile == null)
                return null;
            return new FileHttpRequestHandler(PngMime, new FileInfo(m.IconFile), Options, false, null);
        };

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        /// <summary>
        /// Add a new thumbnail size to support
        /// </summary>
        /// <param name="width">The max width in pixels</param>
        /// <param name="height">The max height in pixels</param>
        /// <param name="key">An optional key</param>
        /// <returns>The key to use to get this thumbnail "?Key", ex: "?Thumb64x64"</returns>
        public String AddThumbSize(int width, int height, String key = null)
        {
            var c = MapRes;
            key = key ?? String.Concat(Prefix, width, 'x', height);
            lock (c)
            {
                if (c.TryGetValue(key, out var existing))
                {
                    c[key] = Tuple.Create(width, height, existing.Item3 + 1, new FastMemCache<string, MediaInfo>(CacheDuration, StringComparer.Ordinal));
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
        /// Remove a previously added thumbnail size
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

        public async Task<IHttpRequestHandler> Modify(string key, Tuple<string, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder, bool updateAccessTime)
        {
            var mon = PerfMon;
            var monPre = key + ".";
            using (mon.Track(monPre + "All"))
            {
                var m = await GetMediaInfo(key, fi.FullName, false).ConfigureAwait(false);
                if (m?.IconFile != null)
                    return new FileHttpRequestHandler(PngMime, new FileInfo(m.IconFile), options, false, null, updateAccessTime);
                return GetIcon(key, mime.Item1.Split(';')[0].TrimEnd());
            }
        }

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

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(ThumbnailService));
    }
}
