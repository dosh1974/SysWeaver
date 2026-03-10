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
    }

    public sealed partial class MediaTransformerService : IHttpTransformerService, IDisposable
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


            var t = new Dictionary<string, IHandler>()
            {
                { "image/png", new ImageHandler([
                    new ImageFormat(MagickFormat.Avif, ".avif"),
                    new ImageFormat(MagickFormat.WebP, ".webp"),
                    new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                    ] ) },
                { "image/webp", new ImageHandler([
                    new ImageFormat(MagickFormat.Avif, ".avif"),
                    new ImageFormat(MagickFormat.Png, ".png"),
                    new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                    ] ) },
                { "image/avif", new ImageHandler([
                    new ImageFormat(MagickFormat.WebP, ".webp"),
                    new ImageFormat(MagickFormat.Png, ".png"),
                    new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                    ] ) },
                { "image/jpeg", new ImageHandler([
                    new ImageFormat(".jpg"),
                    new ImageFormat(MagickFormat.Avif, ".avif", 95),
                    new ImageFormat(MagickFormat.WebP, ".webp", 95),
                    new ImageFormat(MagickFormat.Png, ".png"),
                    ] ) },
                { ".psd", new ImageHandler([
                    new ImageFormat(MagickFormat.Avif, ".avif"),
                    new ImageFormat(MagickFormat.WebP, ".webp"),
                    new ImageFormat(MagickFormat.Png, ".png"),
                    new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                    ] ) },

            };
            MimeHandlers = t.Freeze();
            DataFolders = p.Folders ?? Folders.AllAppFolders;


            BuildTasks = Enumerable.Range(0, threadCount).Select(x => new PeriodicTask(Build, 100)).ToArray();
        }

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

        async ValueTask<bool> Build()
        {
            var b = BuildJobs;
            while (b.TryDequeue(out var job))
            {
                var files = await job.Handler.Build(this, job.BaseName, job.Mime, job.Data).ConfigureAwait(false);
                if (files != null)
                    job.Entry.Files = files;
                await Task.Delay(1).ConfigureAwait(false);
            }
            return true;
        }

        readonly IReadOnlyList<String> DataFolders;

        readonly IReadOnlyDictionary<String, IHandler> MimeHandlers;


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


        async ValueTask<CacheEntry> GetFromCache(String key, HttpRequestTransformerState state)
        {
            var name = HashTools.GetHashString(key);
            var baseName = Path.Combine(Folders.SelectFolder(DataFolders, name), "TransformedCache", "Media", name);
            var mime = state.Mime;
            var mimeHandler = MimeHandlers[mime];
            var e = mimeHandler.Validate(this, baseName);
            if (e != null)
                return e;
            e = new CacheEntry();
            var data = await state.ReadAllData().ConfigureAwait(false);
            BuildJobs.Enqueue(new BuildJob(mimeHandler, e, data, mime, baseName));
            return e;
        }



 

        readonly ICompType CompType;

        readonly String CompExt;

        static FileHttpRequestHandler[] GetValidSorted(FileHttpRequestHandler[] files)
            => files.Where(x => x != null).OrderBy(x => x.Fi.Length).ToArray();


        static readonly RequestOptions Options = new RequestOptions(0, 0, 0, null, null);

        readonly ConcurrentQueue<BuildJob> BuildJobs = new ConcurrentQueue<BuildJob>();

        readonly FastMemCache<String, CacheEntry> Cache = new (TimeSpan.FromHours(1), StringComparer.Ordinal);



    }

}
