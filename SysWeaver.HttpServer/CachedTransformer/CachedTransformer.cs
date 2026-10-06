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

    /// <summary>
    /// Base class for transformer services that convert served files (by mime type or file extension) into alternative,
    /// typically smaller, variants (pre-compressed, re-encoded images etc) that are cached on disc and selected per request.
    /// </summary>
    /// <remarks>
    /// Derived classes register <see cref="ICachedTransformer"/> handlers using <see cref="Add"/>.
    /// The server invokes <see cref="GetTransformers"/> handlers for matching files; an in-memory cache (1 hour) keyed by local url and etag
    /// maps to a <see cref="CachedTransformerEntry"/>; misses are validated against disc and otherwise built (in the background or directly,
    /// see <see cref="CachedTransformerBuildStrategies"/>).
    /// Files are named by a 26 character hash of the key, distributed over the data folders, and pruned when not accessed for
    /// <see cref="CachedTransformerParams.RemoveAfterDays"/> days (one folder is scanned every 15 minutes).
    /// Thread safe.
    /// </remarks>
    public partial class CachedTransformer : IHttpTransformerService, IDisposable, IPerfMonitored, IHaveStats
    {

        /// <summary>
        /// Extension appended to files while they are being written (stale temp files older than an hour are pruned).
        /// </summary>
        public const string TempExt = ".tmp";


        /// <summary>
        /// The compression used for disc cached files that should be stored compressed (brotli).
        /// </summary>
        public readonly ICompType CompType;

        /// <summary>
        /// The file extension (including the leading dot) used for files compressed with <see cref="CompType"/>.
        /// </summary>
        public readonly String CompExt;

        /// <summary>
        /// Sort variants by file size, smallest first.
        /// </summary>
        /// <param name="files">The variants, a null element represents the original.</param>
        /// <param name="originalLen">The size of the original, used as the sort key for null elements.</param>
        /// <returns>A new sorted array.</returns>
        public static FileHttpRequestHandler[] GetValidSorted(IReadOnlyList<FileHttpRequestHandler> files, long originalLen)
            => files.OrderBy(x => x == null ? originalLen : x.Fi.Length).ToArray();

        /// <summary>
        /// Request options used for the file handlers of cached variants (no client or request caching, no on-the-fly compression, no auth of its own).
        /// </summary>
        public static readonly RequestOptions Options = new RequestOptions(0, 0, 0, null, null);



        /// <summary>
        /// Create the transformer service and start the background build and prune tasks.
        /// </summary>
        /// <param name="p">Parameters, null to use defaults.</param>
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


            var pf = p.Folders;
            DataFolders = (pf?.Length > 0) ? pf : Folders.AllAppFolders.Convert(x => Path.Combine(x, "TransformerCache"));
            BuildLock = new AsyncLock(threadCount);
            BuildTasks = Enumerable.Range(0, threadCount).Select(x => new PeriodicTask(Build, 100)).ToArray();
            PruneTask = new PeriodicTask(Prune, 15 * 60 * 1000, true);
            RemoveAfterDays = Math.Min(365 * 100, Math.Max(p.RemoveAfterDays, 1));
        }


        readonly int RemoveAfterDays;
        readonly AsyncLock BuildLock;

        PeriodicTask PruneTask;
        readonly PeriodicTask[] BuildTasks;

        /// <summary>
        /// Stop the background build and prune tasks (queued builds are abandoned).
        /// </summary>
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
        

        /// <summary>
        /// Build one job (limited by <see cref="BuildLock"/>), always completes the entry and removes it from the scheduled jobs.
        /// </summary>
        async Task BuildOne(CachedTransformerJob job)
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

        /// <summary>
        /// Periodic task body that drains the deferred build queue.
        /// </summary>
        async Task<bool> Build()
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



        /// <summary>
        /// Register a transformer for a file extension (with or without leading dot, case insensitive) or a mime type.
        /// </summary>
        /// <param name="fileExtension">A file extension or a mime type (contains a '/').</param>
        /// <param name="transformHandler">The transformer to use.</param>
        /// <returns>True if added, false if a transformer is already registered for that key.</returns>
        /// <remarks>Must be called before the service is registered with the server (<see cref="GetTransformers"/> is only enumerated at registration).</remarks>
        protected bool Add(String fileExtension, ICachedTransformer transformHandler)
        {
            return MimeHandlers.TryAdd(fileExtension.FastTrimStartToLower('.'), transformHandler);
        }

        readonly SemiFrozenDictionary<String, ICachedTransformer> MimeHandlers = new SemiFrozenDictionary<string, ICachedTransformer>(StringComparer.Ordinal);

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, Func<HttpRequestTransformerState, Task<bool>>>> GetTransformers()
            => MimeHandlers.Select(x => new KeyValuePair<string, Func<HttpRequestTransformerState, Task<bool>>>(x.Key, Handle));


        /// <summary>
        /// Pick the first variant that the client accepts (Accept-Encoding for compressed files, Accept for webp/avif)
        /// and set it as the handler of the state.
        /// </summary>
        /// <returns>True if a variant was selected, false to serve the original (not built yet, original preferred or nothing acceptable).</returns>
        async Task<bool> Handle(HttpRequestTransformerState state)
        {
            var data = state.Request;
            var key = String.Join('\n', data.LocalUrl, state.ETag);
            var c = await Cache.GetOrUpdateAsync(key, GetFromCache, state).ConfigureAwait(false);
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

        /// <summary>
        /// Mime types that are only served if explicitly listed in the request Accept header.
        /// </summary>
        static readonly IReadOnlySet<String> AcceptMimeChecks = ReadOnlyData.Set<String>(
            "image/webp", "image/avif"
            );



        readonly ConcurrentDictionary<String, CachedTransformerEntry> ScheduledJobs = new (StringComparer.Ordinal);


        /// <summary>
        /// Register a new build for a key, or get the entry of a build already in progress.
        /// </summary>
        /// <returns>True if a new entry was created (the caller must build it).</returns>
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

        /// <summary>
        /// Memory cache miss: validate the disc cache, else start (or join) a build.
        /// </summary>
        async Task<CachedTransformerEntry> GetFromCache(String key, HttpRequestTransformerState state)
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
            ReadOnlyMemory<Byte> data;
            try
            {
                data = await state.ReadAllData().ConfigureAwait(false);
            }
            catch
            {
                //  Complete and unregister the entry, else other requests for the same key would wait for (or never start) a build that never happens
                e.Completed = true;
                ScheduledJobs.TryRemove(new KeyValuePair<String, CachedTransformerEntry>(key, e));
                throw;
            }
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


        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(CachedTransformer));



        int FolderIndex;

        sealed class Group
        {
            public DateTime LastAccess = DateTime.MinValue;
            public readonly List<String> Files = new List<string>(16);
        }

        long DeletedFiles;
        readonly ExceptionTracker PrunerErrors = new ExceptionTracker();

        /// <summary>
        /// Prune one data folder (round robin): delete stale temp files, and delete groups of files (same 26 char hash prefix)
        /// where no file has been accessed within the retention period.
        /// </summary>
        async Task<bool> Prune()
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


        /// <summary>
        /// Store the original size in a "[baseName].org" file (unless it already exists with content).
        /// </summary>
        /// <param name="baseName">The base name, see <see cref="CachedTransformerFile.BaseName"/>.</param>
        /// <param name="orgLength">The size of the original in bytes.</param>
        public static async Task SaveOrg(String baseName, long orgLength)
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


        /// <summary>
        /// Read the original size stored by <see cref="SaveOrg"/>.
        /// </summary>
        /// <param name="baseName">The base name, see <see cref="CachedTransformerFile.BaseName"/>.</param>
        /// <returns>The original size, or -1 if missing, invalid or not positive.</returns>
        /// <exception cref="IOException">The file exists but couldn't be read.</exception>
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
        /// All active cached data transformers.
        /// </summary>
        /// <param name="r">Table request parameters.</param>
        /// <returns>One row per registered extension or mime type.</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(29)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Http Server/{0}", "Cached transformers", null, "icons/world.svg")]
        public TableData CachedTransformersTable(TableDataRequest r)
            => TableDataTools.Get(r, 30000, MimeHandlers.Select(x => new MimeHandler(x)));

        /// <summary>
        /// Row type for <see cref="CachedTransformersTable"/>.
        /// </summary>
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
        /// All transformed files currently in the memory cache (accessed within the last hour).
        /// </summary>
        /// <param name="r">Table request parameters.</param>
        /// <returns>One row per memory cached file.</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(2)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Http Server/{0}", "Cached recent files", null, "icons/world.svg")]
        public TableData CachedRecentFilesTable(TableDataRequest r)
            => TableDataTools.Get(r, 2000, Cache.Select(x => new CachedFile(x)));


        /// <summary>
        /// Row type for <see cref="CachedRecentFilesTable"/>.
        /// </summary>
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
            /// The size of the original file.
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
