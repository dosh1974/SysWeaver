using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.FolderSync;
using SysWeaver.Net;
using SysWeaver.Serialization;

namespace SysWeaver.MicroService
{


    /// <summary>
    /// Upload url "FolderSync/{JobId}/{LocalFile}".
    /// </summary>
    [WebApiUrl("../FolderSync")]
    [IsMicroService]
    public partial class FolderSyncService : IHttpServerModule, IHttpRequestHandler, IDisposable, IHaveStats, IPerfMonitored
    {

        #region IHttpRequestHandler

        public HttpServerRequest Redirected { get; set; }

        public int ClientCacheDuration => 0;

        public int RequestCacheDuration => 0;

        public HttpCompressionPriority Compression => null;

        public ICompDecoder Decoder => null;

        public IReadOnlyList<string> Auth => null;

        public ValueTask<string> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = true;
            request.SetResMime(HttpServerTools.JsonMime);
            return null;
        }

        public HttpRequestData Get(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            if (request.HttpMethod != HttpServerMethods.POST)
            {
                request.SetResMime(MimeTypeMap.Json);
                return FalseValue;
            }
            var x = request.LocalUrl.Split('/');
            var len = x.Length;
            if (len < 2)
            {
                request.SetResMime(MimeTypeMap.Json);
                return FalseValue;
            }
            if (x[1].FastEquals(nameof(GetChunks)))
            {
                if (len != 2)
                {
                    request.SetResMime(MimeTypeMap.Json);
                    return FalseValue;
                }
                request.SetResMime(MimeTypeMap.Data);
                return new HttpRequestData(await GetChunks(request).ConfigureAwait(false));
            }
            if (len < 3)
            {
                request.SetResMime(MimeTypeMap.Json);
                return FalseValue;
            }
            var jobId = x[1];
            var filename = String.Join('/', x, 2, len - 2);
            var u = x[0];
            if (u.FastEquals("FolderSync"))
            {
                var res = await UploadFile(jobId, filename, request).ConfigureAwait(false);
                request.SetResMime(MimeTypeMap.Json);
                return res ? TrueValue : FalseValue;
            }
            if (u.FastEquals("FolderSyncCdc"))
            {
                request.SetResMime(MimeTypeMap.Data);
                return new HttpRequestData(await UploadCdcFile(jobId, filename, request).ConfigureAwait(false));
            }

            var res2 = await UploadCdcChunks(jobId, request).ConfigureAwait(false);
            request.SetResMime(MimeTypeMap.Json);
            return res2 ? TrueValue : FalseValue;

        }

        static readonly HttpRequestData TrueValue = new (Encoding.UTF8.GetBytes("true"));
        static readonly HttpRequestData FalseValue = new (Encoding.UTF8.GetBytes("false"));

        #endregion//IHttpRequestHandler

        #region IHttpServerModule

        public String[] OnlyForPrefixes { get; init; } = 
        [
            "FolderSync/",
            "FolderSyncCdc/",
            "FolderSyncCdcChunks/",
            "FolderSyncCdcPullChunks/",
        ];

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(FolderSyncService));

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            context.LocalUrl.SplitFirst('/', out var r);
            var f = r.SplitFirst('?', out var rest);
            var l = r.SplitLast('/');
            f = f.SplitFirst('/');
            return Apis.Contains(f) || l.FastEquals("explore") ? null : this;
        }

        static readonly IReadOnlySet<String> Apis = ReadOnlyData.Set(StringComparer.Ordinal,
            nameof(FolderSyncParams.ManagedFolders),
            nameof(FolderSyncParams.SharedFolders),
            nameof(CheckManagedFolder),
            nameof(ManagedFoldersTable),
            nameof(Activate),
            nameof(Remove),
            nameof(GetManagedFolderManifest),
            nameof(CheckSharedFolder),
            nameof(SharedFolderHasChanged),
            nameof(GetSharedFileChunks),
            nameof(SharedFoldersTable),
            ""
            );

        #endregion//IHttpServerModule

        public FolderSyncService(ServiceManager manager, FolderSyncParams p)
        {
            p = p ?? new();
            Manager = manager;
            FileMod = manager.TryGet<FileHttpServerModule>();
            foreach (var x in p.ManagedFolders.Nullable())
                AddManagedFolder(x).RunAsync();
            foreach (var x in p.SharedFolders.Nullable())
                AddSharedFolder(x).RunAsync();
            TempRemove = TimeSpan.Zero;
            Prune().RunAsync();
            TempRemove = TimeSpan.FromHours(12);
            PruneTask = new PeriodicTask(Prune, 5 * 60 * 1000, true, true, true);
            ScanTask = new PeriodicTask(ScanSharedFolders, 3000, true, true, true);
        }

        public async ValueTask<String> AddManagedFolder(FsManagedFolder x)
        {
            var folders = PushFolders;
            var path = Path.GetFullPath(PathTemplate.Resolve(x.DiscFolder));
            var name = x.Name;
            path = PathExt.CreateDataFolder(path);
            if (String.IsNullOrEmpty(name))
                name = Path.GetFileName(path);
            var auth = x.Auth ?? Roles.Debug;
            var pullF = x.AllowPull ? new FolderPullFolder
            {
                Name = name,
                DiscFolder = path,
                Auth = x.PullAuth ?? auth,
            } : null;
            var folder = new ManagedFolder(
                name,
                path,
                auth,
                TimeSpan.FromDays(Math.Max(0, x.RemoveBackupsDays)),
                x,
                pullF
                );
            folders.TryAdd(name.FastToLower(), folder);
            var fm = FileMod;
            if (fm != null)
                fm.AddFolder(folder.ModFolder);
            if (pullF != null)
                await AddSharedFolder(pullF).ConfigureAwait(false);
            return path;
        }

        public bool RemoveManagedFolder(FsManagedFolder x)
        {
            var path = Path.GetFullPath(PathTemplate.Resolve(x.DiscFolder));
            var name = x.Name;
            path = new DirectoryInfo(path).FullName;
            if (String.IsNullOrEmpty(name))
                name = Path.GetFileName(path);
            if (!PushFolders.TryRemove(name.FastToLower(), out var folder))
                return false;
            var pf = folder.PullFolder;
            if (pf != null)
                RemoveSharedFolder(pf);
            var fm = FileMod;
            if (fm != null)
                fm.RemoveFolder(folder.ModFolder);
            return true;
        }

        public async ValueTask<String> AddSharedFolder(FolderPullFolder x)
        {
            var folders = PullFolders;
            var path = Path.GetFullPath(PathTemplate.Resolve(x.DiscFolder));
            var name = x.Name;
            path = PathExt.CreateDataFolder(path);
            if (String.IsNullOrEmpty(name))
                name = Path.GetFileName(path);
            var auth = x.Auth ?? Roles.Debug;
            var folder = new SharedFolder(
                name,
                path,
                auth);
            using (var l = await SystemLock.GetAsync(folder.LockName).ConfigureAwait(false))
                await folder.UpdateFiles().ConfigureAwait(false);
            folders.TryAdd(name.FastToLower(), folder);
            var fm = FileMod;
            if (fm != null)
                fm.AddFolder(folder.ModFolder);
            return path;
        }

        public bool RemoveSharedFolder(FolderPullFolder x)
        {
            var path = Path.GetFullPath(PathTemplate.Resolve(x.DiscFolder));
            var name = x.Name;
            path = new DirectoryInfo(path).FullName;
            if (String.IsNullOrEmpty(name))
                name = Path.GetFileName(path);
            if (!PullFolders.TryRemove(name.FastToLower(), out var folder))
                return false;
            var fm = FileMod;
            if (fm != null)
                fm.RemoveFolder(folder.ModFolder);
            return true;
        }

        readonly FileHttpServerModule FileMod;
        readonly ServiceManager Manager;

        PeriodicTask PruneTask;
        PeriodicTask ScanTask;

        readonly TimeSpan TempRemove;


        async ValueTask<bool> Prune()
        {
            List<String> toDelete = new List<string>();
            var syncJobs = SyncJobs;
            foreach (var x in syncJobs)
            {
                var s = x.Value;
                if (Interlocked.Read(ref s.FileInProgess) <= 0)
                    if (s.IsOld)
                        toDelete.Add(x.Key);
            }
            foreach (var x in toDelete)
            {
                if (!syncJobs.TryRemove(x, out var job))
                    continue;
                try
                {
                    job.D.Dispose();
                }
                catch
                {
                }
            }

            var tempRemove = TempRemove;
            foreach (var f in PushFolders)
            {
                try
                {
                    var d = f.Value;
                    var targetDir = d.DestPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var parentDir = Path.GetDirectoryName(targetDir);
                    var dirName = Path.GetFileName(targetDir);
                    var exp = d.RemoveAfter;
                    var tempStart = dirName + "_Temp";
                    foreach (var dir in Directory.GetDirectories(parentDir, dirName + "_*", SearchOption.TopDirectoryOnly))
                    {
                        var di = new DirectoryInfo(dir);
                        var lastTime = di.LastWriteTimeUtc;
                        var acc = di.LastAccessTimeUtc;
                        if (acc > lastTime)
                            lastTime = acc;
                        var age = DateTime.UtcNow - lastTime;
                        var isTemp = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).FastStartsWith(tempStart);
                        if (age > (isTemp ? tempRemove : exp))
                        {
                            await PathExt.TryDeleteDirectoryAsync(dir, false).ConfigureAwait(false);
                            await PathExt.TryDeleteFileAsync(dir + ContentDependentChunking.DotFileExt).ConfigureAwait(false);
                            await Task.Delay(100).ConfigureAwait(false);
                        }
                    }
                }
                catch
                {
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
        //  Perform and schedule compression
            var scheduled = Scheduled;
            while (scheduled.TryDequeue(out var f))
            {
                if (!SystemLock.TryGet("ActLock" + f, out var lck))
                    break;
                using var _ = lck;
                await TryCompressFolderLog(f).ConfigureAwait(false);
            }
            foreach (var f in GetManagedFolders())
            {
                if (f.IsActive)
                    continue;
                if (f.Comp)
                    continue;
                if (f.Folder.Compress)
                    scheduled.Enqueue(f.FullPath);
            }
            return true;
        }

        async ValueTask<Exception> TryCompressFolderLog(String folder)
        {
            var m = Manager;
            m.AddMessage(String.Concat(LogPrefix, "Compressing: \"", folder, "\""));
            using var _ = m.Tab();
            var ex = await TryCompressFolder(folder).ConfigureAwait(false);
            if (ex == null)
                m.AddMessage(String.Concat(LogPrefix, "Compression done!"));
            else
                m.AddMessage(String.Concat(LogPrefix, "Compression of: \"", folder, "\" failed!"), ex, MessageLevels.Warning);
            return ex;
        }

        async ValueTask<ValueTuple<Exception, CdcChunkStats>> TryExpandFolderLog(String compact)
        {
            Manager.AddMessage(LogPrefix + "Expanding \"" + compact + "\"");
            try
            {
                var stats = await ContentDependentChunking.Expand(compact).ConfigureAwait(false);
                var ex = await PathExt.TryDeleteFileAsync(compact).ConfigureAwait(false);
                return ValueTuple.Create(ex, stats);
            }
            catch (Exception ex3)
            {
                return ValueTuple.Create(ex3, (CdcChunkStats)null);
            }
        }

        async ValueTask<Exception> TryCompressFolder(String folder)
        { 
            try
            {
                if (!Directory.Exists(folder))
                    return null;
                var comp = folder + ContentDependentChunking.DotFileExt;
                if (File.Exists(comp))
                    return null;
                await ContentDependentChunking.Compact(folder).ConfigureAwait(false);
                var noDel = Path.Combine(folder, "_FolderSync.txt");
                return await PathExt.TryCleanDirectoryAsync(folder, (fn, isFolder) => !fn.FastEquals(isFolder ? folder : noDel)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        readonly ConcurrentDictionary<String, int> IsCompressing = new ConcurrentDictionary<string, int>();
        readonly ConcurrentQueue<String> Scheduled = new ConcurrentQueue<string>();


        public void Dispose()
        {
            Interlocked.Exchange(ref ScanTask, null)?.Dispose();
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            var fm = FileMod;
            if (fm != null)
            {
                foreach (var x in PushFolders.Values)
                    fm.RemoveFolder(x.ModFolder);
            }
        }

        readonly ConcurrentDictionary<String, ManagedFolder> PushFolders = new ConcurrentDictionary<String, ManagedFolder>(StringComparer.Ordinal);

        readonly ConcurrentDictionary<String, SharedFolder> PullFolders = new ConcurrentDictionary<String, SharedFolder>(StringComparer.Ordinal);

        readonly ConcurrentDictionary<String, Sync> SyncJobs = new ConcurrentDictionary<string, Sync>(StringComparer.Ordinal);



        const String LogPrefix = "[FolderSync] ";

        async ValueTask<int> RunCommand(String cmd)
        {
            var m = Manager;
            m.AddMessage(String.Concat(LogPrefix, "Running command: \"", cmd, "\":"));
            using var _ = m.Tab();
            try
            {
                var exe = SystemHelper.GetCommandAndArgs(out var args, cmd);
                return await ExternalProcess.RunAsync(exe, args, (text, err) =>
                {
                    m.AddMessage(LogPrefix + text, err ? MessageLevels.Warning : MessageLevels.Info);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                m.AddMessage(LogPrefix + "Failed to run!", ex, MessageLevels.Warning);
            }
            return -42;
        }

        async ValueTask RunCommands(String[] commands)
        {
            foreach (var cmd in commands)
                await RunCommand(cmd).ConfigureAwait(false);
        }


        public IEnumerable<Stats> GetStats()
        {
            const String n = nameof(FolderSyncService);
            foreach (var x in ChunkDataListCache.GetStats(n, "ChunkPullCache."))
                yield return x;
        }

    }

}
