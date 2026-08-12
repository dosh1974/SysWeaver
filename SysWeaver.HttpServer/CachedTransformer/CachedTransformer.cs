using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.Net;
using SysWeaver.MicroService;
using System.Globalization;

namespace SysWeaver.HttpTransformer
{

    public partial class CachedTransformer : IHttpTransformerService, IDisposable, IPerfMonitored, IHaveStats
    {

        public const string TempExt = ".tmp";


        public readonly ICompType CompType;

        public readonly String CompExt;

        public static FileHttpRequestHandler[] GetValidSorted(IReadOnlyList<FileHttpRequestHandler> files, long originalLen)
            => files.OrderBy(x => x == null ? originalLen : x.Fi.Length).ToArray();

        public static readonly RequestOptions Options = new RequestOptions(0, 0, 0, null, null);



        protected CachedTransformer(CachedTransformerParams p = null)
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
        

        async ValueTask BuildOne(CachedTransformerJob job)
        {
            using var ___ = PerfMon.Track("BuildQueued");
            using var _ = await BuildLock.Lock().ConfigureAwait(false);
            using var __ = PerfMon.Track("Build");
            var e = job.Entry;
            var info = job.File;
            try
            {
                FileHttpRequestHandler[] files;
                var baseName = info.BaseName;
                var mime = info.Mime;
                await PathExt.EnsureCanWriteFileAsync(baseName).ConfigureAwait(false);
                using (var ____ = PerfMon.Track("Build." + mime))
                    files = await info.Handler.Build(this, info, job.Data, e).ConfigureAwait(false);
                if (files != null)
                    e.Files = files;
                e.Completed = true;
            }
            catch (Exception ex)
            {
                BuildErrors.OnException(ex);
                e.Completed = true;
            }
            ScheduledJobs.TryRemove(info.CacheKey, out var _);
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



        protected bool Add(String fileExtension, ICachedTransformer transformHandler)
        {
            return MimeHandlers.TryAdd(fileExtension.FastTrimStartToLower('.'), transformHandler);
        }

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
                    if (formats == null)
                        continue;
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
            if (!(mh.TryGetValue(mime.SplitFirst(';'), out var mimeHandler) || mh.TryGetValue(ext, out mimeHandler)))
                throw new Exception("Internal error!");
            var st = mimeHandler.BuildStrategy;
            var info = new CachedTransformerFile(mimeHandler, key, baseName, state);
            var e = mimeHandler.Validate(this, info);
            if (e != null)
                return e;

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
            e.OrgSize = data.Length;
            var job = new CachedTransformerJob(info, data, e);
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


        readonly ConcurrentQueue<CachedTransformerJob> BuildJobs = new ConcurrentQueue<CachedTransformerJob>();

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
            if (Directory.Exists(folder))
            {
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
            }
            return true;
        }


        public static async ValueTask SaveOrg(String baseName, long orgLength)
        {
            var name = baseName + ".org";
            var fi = new FileInfo(name);
            if (fi.Exists && (fi.Length > 0))
                return;
            var tempName = name + TempExt;
            try
            {
                await File.WriteAllTextAsync(tempName, orgLength.ToString()).ConfigureAwait(false);
                await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                return;
            }
            finally
            {
                await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
            }
        }


        public static long ReadOrg(String baseName)
        {
            var orgName = baseName + ".org";
            if (!File.Exists(orgName))
                return -1;
            var t = File.ReadAllText(orgName);
            if (!long.TryParse(t.Trim(), out var orgSize))
                return -1;
            if (orgSize <= 0)
                return -1;
            return orgSize;
        }

        #region DEBUG



        /// <summary>
        /// All active cached data transformers
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(29)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Http Server/{0}", "Cached transformers", null, "icons/world.svg")]
        public TableData CachedTransformersTable(TableDataRequest r)
            => TableDataTools.Get(r, 30000, MimeHandlers.Select(x => new MimeHandler(x)));

        sealed class MimeHandler
        {
            public MimeHandler(KeyValuePair<String, ICachedTransformer> d)
            {
                var mime = d.Key;
                if (mime.IndexOf('/') < 0)
                {
                    Ext = mime;
                    EI = mime;
                }else
                {
                    Mime = mime;
                }
                var t = d.Value;
                Strategy = t.BuildStrategy.ToString().RemoveCamelCase();
                Type = t.GetType().Name.RemoveCamelCase();
                Info = t.Info;
            }

            /// <summary>
            /// The mime that this transformer will be applied to.
            /// If null the file extension is used instead.
            /// </summary>
            [TableDataMime]
            public String Mime;

            /// <summary>
            /// The file extension that this transformer will be applied to.
            /// If null the mime is used instead.
            /// </summary>
            [TableDataFileExtension]
            public String Ext;

            [TableDataFileExtensionImage]
            public String EI;

            /// <summary>
            /// The strategy as to how build resources
            /// </summary>
            public String Strategy;

            /// <summary>
            /// The type of transformer
            /// </summary>
            public String Type;

            /// <summary>
            /// Transformer specific information
            /// </summary>
            [TableDataTags]
            public String Info;
        }


        /// <summary>
        /// All cached transformed files that have been accessed
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(2)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Http Server/{0}", "Cached recent files", null, "icons/world.svg")]
        public TableData CachedRecentFilesTable(TableDataRequest r)
            => TableDataTools.Get(r, 2000, Cache.Select(x => new CachedFile(x)));


        sealed class CachedFile
        {
            public CachedFile(ValueTuple<DateTime, String, CachedTransformerEntry> d)
            {
                var time = d.Item1;
                var x = d.Item2.Split('\n');
                var url = x[0];
                var etag = x[1];
                var e = d.Item3;
                Etag = etag;
                Url = url;
                Ext = url.Substring(url.LastIndexOf('.') + 1);
                Expires = time;
                Completed = e.Completed;
                var orgSize = e.OrgSize;
                OrgSize = e.OrgSize;
                var files = e.Files;
                if (files != null)
                {
                    var l = files.Length;
                    if (l > 0)
                    {
                        List<String> tags = new List<string>(l);
                        String location = null;
                        for (int i = 0; i < l; ++ i)
                        {
                            var f = files[i];
                            if (f == null)
                                continue;
                            var fi = f.Fi;
                            var len = fi.Length;
                            var name = fi.Name;
                            var es = name.IndexOf('.');
                            var size = (100M * len) / Math.Max(1M, orgSize);
                            tags.Add(String.Concat(name.Substring(es), " @ ", size.ToString("0.00", CultureInfo.InvariantCulture), '%'));
                            if (location == null)
                                location = Path.Combine(fi.DirectoryName, name.Substring(0, es));
                        }
                        BaseName = location;
                        Order = String.Join(',', tags);
                    }
                }
            }

            /// <summary>
            /// The etag for the original file (version)
            /// </summary>
            [TableDataUrl("{0}", "../{1}?raw", "Click to open the original file:\n\"{3}\"")]
            public String Etag;

            /// <summary>
            /// The url to the file
            /// </summary>
            [TableDataUrl("{0}", "../{0}")]
            public String Url;

            /// <summary>
            /// File extension
            /// </summary>
            [TableDataFileExtensionImage]
            public String Ext;

            /// <summary>
            /// When this entry is removed from the memory cache (not disc)
            /// </summary>
            public DateTime Expires;

            /// <summary>
            /// The size of tthe original file
            /// </summary>
            [TableDataByteSize]
            public long OrgSize;

            /// <summary>
            /// If true, the cache build have been completed
            /// </summary>
            public bool Completed;

            /// <summary>
            /// Order of optimized versions
            /// </summary>
            [TableDataTags]
            public String Order;

            /// <summary>
            /// The base name of the cached assets (directory and base file name)
            /// </summary>
            [TableDataText(64)]
            public String BaseName;

        }


        #endregion//DEBUG


    }

}
