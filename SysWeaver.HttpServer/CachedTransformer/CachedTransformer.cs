using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    public sealed partial class CachedTransformer : IHttpTransformerService, IDisposable, IPerfMonitored, IHaveStats
    {

        public const string TempExt = ".tmp";


        public readonly ICompType CompType;

        public readonly String CompExt;

        public static FileHttpRequestHandler[] GetValidSorted(IReadOnlyList<FileHttpRequestHandler> files, long originalLen)
            => files.OrderBy(x => x == null ? originalLen : x.Fi.Length).ToArray();

        public static readonly RequestOptions Options = new RequestOptions(0, 0, 0, null, null);



        public CachedTransformer(CachedTransformerParams p = null)
        {
            p = p ?? new CachedTransformerParams();

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


            DataFolders = p.Folders ?? Folders.AllAppFolders.Convert(x => Path.Combine(x, "TransformerCache"));
            BuildLock = new AsyncLock(threadCount);
            BuildTasks = Enumerable.Range(0, threadCount).Select(x => new PeriodicTask(Build, 100)).ToArray();
            PruneTask = new PeriodicTask(Prune, 15 * 60 * 1000, true);
            RemoveAfterDays = Math.Min(365 * 100, Math.Max(p.RemoveAfterDays, 1));
        }


        readonly int RemoveAfterDays;
        readonly AsyncLock BuildLock;

        PeriodicTask PruneTask;
        readonly PeriodicTask[] BuildTasks;

        public void Dispose()
        {
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
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
                    files = await job.Handler.Build(this, job.BaseName, job.Mime, job.Data, job.Ext, job.IsSupported, job.Decoder).ConfigureAwait(false);
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



        public bool Add(String mimeOrExtension, ICachedTransformer transformHandler)
            => MimeHandlers.TryAdd(mimeOrExtension, transformHandler);

        readonly SemiFrozenDictionary<String, ICachedTransformer> MimeHandlers = new SemiFrozenDictionary<string, ICachedTransformer>(StringComparer.Ordinal);

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
                if (file == null)
                    return false;
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



        readonly ConcurrentDictionary<String, CachedTransformerEntry> ScheduledJobs = new (StringComparer.Ordinal);


        bool TryStartBuild(String key, out CachedTransformerEntry e)
        {
            var n = new CachedTransformerEntry();
            var sj = ScheduledJobs;
            while (!sj.TryAdd(key, n))
            {
                if (sj.TryGetValue(key, out e))
                    return false;
            }
            e = n;
            return true;
        }

        async ValueTask<CachedTransformerEntry> GetFromCache(String key, HttpRequestTransformerState state)
        {
            var name = HashTools.GetHashString(key);
            var baseName = Path.Combine(Folders.SelectFolder(DataFolders, name), name);
            var mime = state.Mime;
            var ext = state.Ext;
            var mh = MimeHandlers;
            if (!(mh.TryGetValue(mime, out var mimeHandler) || mh.TryGetValue(ext, out mimeHandler)))
                throw new Exception("Internal error!");
            var e = mimeHandler.Validate(this, baseName, mimeHandler.BuildStrategy != CachedTransformerBuildStrategies.AlwaysDirect);
            if (e != null)
                return e;
            var st = mimeHandler.BuildStrategy;
            bool defer = st != CachedTransformerBuildStrategies.AlwaysDirect;
            if (st == CachedTransformerBuildStrategies.CheckAccept)
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


        public IEnumerable<Stats> GetStats()
        {
            const string sys = nameof(CachedTransformer);
            foreach (var x in BuildErrors.GetStats(sys, "BuildEx."))
                yield return x;
            foreach (var x in PrunerErrors.GetStats(sys, "PruneEx."))
                yield return x;
            foreach (var x in Cache.GetStats(sys, "Cache."))
                yield return x;
            yield return new Stats(sys, "Deleted files", Interlocked.Read(ref DeletedFiles), "Number of old files pruned (deleted)");
        }


        readonly ConcurrentQueue<BuildJob> BuildJobs = new ConcurrentQueue<BuildJob>();

        readonly FastMemCache<String, CachedTransformerEntry> Cache = new (TimeSpan.FromHours(1), StringComparer.Ordinal);

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(CachedTransformer));



        int FolderIndex;

        sealed class Group
        {
            public DateTime LastAccess = DateTime.MinValue;
            public readonly List<String> Files = new List<string>(16);
        }

        long DeletedFiles;
        readonly ExceptionTracker PrunerErrors = new ExceptionTracker();

        async ValueTask<bool> Prune()
        {
            using var _ = PerfMon.Track(nameof(Prune));
            var fs = DataFolders;
            var fi = FolderIndex;
            ++fi;
            fi %= fs.Count;
            FolderIndex = fi;
            var folder = fs[fi];
            var old = DateTime.UtcNow.AddDays(-RemoveAfterDays);
            var tempOld = DateTime.UtcNow.AddHours(-1);
            var files = Directory.GetFiles(folder);
            Dictionary<String, Group> groups = new Dictionary<string, Group>(files.Length >> 2);
            foreach (var file in files)
            {
                var at = new FileInfo(file).LastAccessTimeUtc;
                var fn = Path.GetFileName(file);
                if (fn.FastEndsWith(TempExt))
                {
                    if (at < tempOld)
                    {
                        var ex = await PathExt.TryDeleteFileAsync(file).ConfigureAwait(false);
                        if (ex == null)
                            Interlocked.Increment(ref DeletedFiles);
                        else
                            PrunerErrors.OnException(ex);
                    }
                    continue;
                }
                if (fn.Length <= 26)
                    continue;
                var groupName = fn.Substring(0, 26);
                if (!groups.TryGetValue(groupName, out var group))
                {
                    group = new Group();
                    groups.Add(groupName, group);
                }
                group.Files.Add(file);
                var ea = group.LastAccess;
                group.LastAccess = at > ea ? at : ea;
            }
            foreach (var g in groups.Values)
            {
                if (g.LastAccess < old)
                {
                    foreach (var file in g.Files)
                    {
                        var ex = await PathExt.TryDeleteFileAsync(file).ConfigureAwait(false);
                        if (ex == null)
                            Interlocked.Increment(ref DeletedFiles);
                        else
                            PrunerErrors.OnException(ex);
                    }
                }
            }
            return true;
        }


    }

}
