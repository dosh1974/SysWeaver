using ImageMagick;
using ImageMagick.Formats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    public class MediaTransformerParams
    {

        /// <summary>
        /// Maximum number of threads to use.
        /// Zero or negative to use relative to processior count
        /// </summary>
        public int BuildThreads = 1;


        /// <summary>
        /// Optionally specify where to store transformed media
        /// </summary>
        public String[] Folders;

        /// <summary>
        /// If true, optimization is performed on legacy files (bmp, tif, jpg, png)
        /// </summary>
        public bool Optimize = true;

        /// <summary>
        /// If true, new formats avif, webp are getting fallback to legacy
        /// </summary>
        public bool SupportNew = true;

        /// <summary>
        /// File extensions to support
        /// </summary>
        public String[] Support =
            [
                "psd",
                "tga",
                "dds",
                "exr",
                "jfif",
                "jp2",
                "jxl",
                "pcx",
                "pict",
            ];
    }

    public sealed partial class MediaTransformerService : IHttpTransformerService, IDisposable, IPerfMonitored, IHaveStats
    {

        public MediaTransformerService(MediaTransformerParams p = null)
        {
            p = p ?? new MediaTransformerParams();

            var maxThreads = Environment.ProcessorCount;
            var threadCount = p.BuildThreads;
            threadCount = threadCount > 0 ? threadCount : (maxThreads + threadCount);
            maxThreads >>= 1;
            if (threadCount > maxThreads)
                threadCount = maxThreads;
            if (threadCount <= 0)
                threadCount = 1;




            var compMethod = CompManager.GetFromHttp("br");
            CompType = compMethod;
            CompExt = '.' + compMethod.FileExtensions.FirstOrDefault().TrimStart('.');


            var t = new Dictionary<string, IMediaTransformHandler>();
            if (p.Optimize)
            {
                t.Add("image/png",
                        new ImageHandler(
                            MediaTransformerBuilds.AlwaysDefer,
                            [
                                new ImageFormat(MagickFormat.Avif, ".avif"),
                            new ImageFormat(MagickFormat.WebP, ".webp", 95),
                            new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                            ]));
                t.Add("image/bmp", new ImageHandler(MediaTransformerBuilds.AlwaysDefer, [
                    new ImageFormat(MagickFormat.Avif, ".avif"),
                    new ImageFormat(MagickFormat.WebP, ".webp", 95),
                    new ImageFormat(MagickFormat.Png, ".png"),
                    new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                    ]));
                t.Add("image/tiff", new ImageHandler(MediaTransformerBuilds.AlwaysDefer, [
                    new ImageFormat(MagickFormat.Avif, ".avif"),
                    new ImageFormat(MagickFormat.WebP, ".webp", 95),
                    new ImageFormat(MagickFormat.Png, ".png"),
                    new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                    ]));
                t.Add("image/jpeg", new ImageHandler(MediaTransformerBuilds.AlwaysDefer, [
                    new ImageFormat(".jpg"),
                    new ImageFormat(MagickFormat.Avif, ".avif", 95),
                    new ImageFormat(MagickFormat.WebP, ".webp", 100),
                    new ImageFormat(MagickFormat.Png, ".png"),
                    ]));
            }
            if (p.SupportNew)
            {
                t.Add("image/webp", new ImageHandler(MediaTransformerBuilds.CheckAccept, [
                    new ImageFormat(MagickFormat.Avif, ".avif"),
                        new ImageFormat(MagickFormat.Png, ".png"),
                        new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                        ]));
                t.Add("image/avif", new ImageHandler(MediaTransformerBuilds.CheckAccept, [
                    new ImageFormat(MagickFormat.WebP, ".webp", 95),
                        new ImageFormat(MagickFormat.Png, ".png"),
                        new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                        ]));
            }
            var s = p.Support;
            if (s != null)
            {
                foreach (var x in s)
                {
                    t.Add(x, new ImageHandler(
                        MediaTransformerBuilds.AlwaysDirect, [
                            new ImageFormat(MagickFormat.Avif, ".avif"),
                            new ImageFormat(MagickFormat.WebP, ".webp", 95),
                            new ImageFormat(MagickFormat.Png, ".png"),
                            new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                        ]));
                }
            }
            MimeHandlers = t.Freeze();
            DataFolders = p.Folders ?? Folders.AllAppFolders;
            BuildLock = new AsyncLock(threadCount);
            BuildTasks = Enumerable.Range(0, threadCount).Select(x => new PeriodicTask(Build, 100)).ToArray();
        }

        readonly AsyncLock BuildLock;

        readonly PeriodicTask[] BuildTasks;

        public void Dispose()
        {
            var t = BuildTasks;
            var tl = t.Length;
            while (tl > 0)
            {
                --tl;
                Interlocked.Exchange(ref t[tl], null)?.Dispose();
            }
        }

        readonly ExceptionTracker BuildErrors = new ();
        

        async ValueTask BuildOne(BuildJob job)
        {
            using var ___ = PerfMon.Track("BuildQueued");
            using var _ = await BuildLock.Lock().ConfigureAwait(false);
            using var __ = PerfMon.Track("Build");
            var e = job.Entry;
            try
            {
                FileHttpRequestHandler[] files;
                using (var ____ = PerfMon.Track("Build." + job.Mime))
                    files = await job.Handler.Build(this, job.BaseName, job.Mime, job.Data, job.Ext, job.IsSupported).ConfigureAwait(false);
                if (files != null)
                    e.Files = files;
                e.Completed = true;
            }
            catch (Exception ex)
            {
                BuildErrors.OnException(ex);
                e.Completed = true;
            }
            ScheduledJobs.TryRemove(job.CacheKey, out var _);
}

        async ValueTask<bool> Build()
        {
            var b = BuildJobs;
            while (b.TryDequeue(out var job))
            {
                await BuildOne(job).ConfigureAwait(false);
                await Task.Delay(1).ConfigureAwait(false);
            }
            return true;
        }

        readonly IReadOnlyList<String> DataFolders;

        readonly IReadOnlyDictionary<String, IMediaTransformHandler> MimeHandlers;


        public IEnumerable<KeyValuePair<string, Func<HttpRequestTransformerState, ValueTask<bool>>>> GetTransformers()
            => MimeHandlers.Select(x => new KeyValuePair<string, Func<HttpRequestTransformerState, ValueTask<bool>>>(x.Key, Handle));


        async ValueTask<bool> Handle(HttpRequestTransformerState state)
        {
            var data = state.Request;
            var key = String.Join('\n', data.LocalUrl, state.ETag);
            var c = await Cache.GetOrUpdateValueAsync(key, GetFromCache, state).ConfigureAwait(false);
            var files = c.Files;
            if (files == null)
                return false;
            var req = state.Request;
            String formats = null; 
            int l = files.Length;
            for (int i = 0; i < l; ++ i)
            {
                var file = files[i];
                //  Check if compression is accepted
                var dec = file.Decoder;
                if (dec != null)
                    if (!req.AcceptedEncoders.Contains(dec.HttpCode))
                        continue;
                var mime = file.Mime;
                if (AcceptMimeChecks.Contains(mime))
                {
                    formats = formats ?? req.GetReqHeader("Accept");
                    var mp = formats.IndexOf(mime);
                    if (mp < 0)
                        continue;
                }
                //  TODO: Check for file support
                state.Mime = mime;
                state.UseAsync = false;
                state.Handler = file;
                return true;
            }
            return false;
        }

        static readonly IReadOnlySet<String> AcceptMimeChecks = ReadOnlyData.Set<String>(
            "image/webp", "image/avif"
            );



        readonly ConcurrentDictionary<String, MediaTransformCacheEntry> ScheduledJobs = new (StringComparer.Ordinal);


        bool TryStartBuild(String key, out MediaTransformCacheEntry e)
        {
            var n = new MediaTransformCacheEntry();
            var sj = ScheduledJobs;
            while (!sj.TryAdd(key, n))
            {
                if (sj.TryGetValue(key, out e))
                    return false;
            }
            e = n;
            return true;
        }

        async ValueTask<MediaTransformCacheEntry> GetFromCache(String key, HttpRequestTransformerState state)
        {
            var name = HashTools.GetHashString(key);
            var baseName = Path.Combine(Folders.SelectFolder(DataFolders, name), "TransformedCache", "Media", name);
            var mime = state.Mime;
            var ext = state.Ext;
            var mh = MimeHandlers;
            if (!(mh.TryGetValue(mime, out var mimeHandler) || mh.TryGetValue(ext, out mimeHandler)))
                throw new Exception("Internal error!");
            var e = mimeHandler.Validate(this, baseName);
            if (e != null)
                return e;
            var st = mimeHandler.BuildStrategy;
            bool defer = st != MediaTransformerBuilds.AlwaysDirect;
            if (st == MediaTransformerBuilds.CheckAccept)
            {
                var req = state.Request;
                var acc = req.GetReqHeader("Accept") ?? "";
                defer = acc.IndexOf(state.Mime, StringComparison.Ordinal) >= 0;
            }
            if (!TryStartBuild(key, out e))
            {
                if (!defer)
                {
                    while (!e.Completed)
                        await Task.Delay(100).ConfigureAwait(false);
                }
                return e;
            }
            var data = await state.ReadAllData().ConfigureAwait(false);
            var job = new BuildJob(mimeHandler, key, e, data, baseName, state);
            if (defer)
            {
                BuildJobs.Enqueue(job);
                return e;
            }
            await BuildOne(job).ConfigureAwait(false);
            return e;
        }



 

        readonly ICompType CompType;

        readonly String CompExt;

        static FileHttpRequestHandler[] GetValidSorted(FileHttpRequestHandler[] files)
            => files.Where(x => x != null).OrderBy(x => x.Fi.Length).ToArray();

        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in BuildErrors.GetStats(nameof(MediaTransformerService), "BuildEx."))
                yield return x;
            foreach (var x in Cache.GetStats(nameof(MediaTransformerService), "Cache."))
                yield return x;
        }

        static readonly RequestOptions Options = new RequestOptions(0, 0, 0, null, null);

        readonly ConcurrentQueue<BuildJob> BuildJobs = new ConcurrentQueue<BuildJob>();

        readonly FastMemCache<String, MediaTransformCacheEntry> Cache = new (TimeSpan.FromHours(1), StringComparer.Ordinal);

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(MediaTransformerService));
    }

}
