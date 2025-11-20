using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    public partial class FolderSyncService : IHttpServerModule, IHttpRequestHandler, IDisposable
    {

        #region IHttpRequestHandler

        public HttpServerRequest Redirected { get; set; }

        public int ClientCacheDuration => 0;

        public int RequestCacheDuration => 0;

        public bool UseStream => false;

        public HttpCompressionPriority Compression => null;

        public ICompDecoder Decoder => null;

        public IReadOnlyList<string> Auth => null;

        public ValueTask<string> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }


        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = true;
            request.SetResMime(HttpServerTools.JsonMime);
            return null;
        }

        public Stream GetStream(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest context)
        {
            if (!context.Method.FastEquals("POST"))
                return FalseValue;
            var x = context.LocalUrl.Split('/');
            var len = x.Length;
            if (len < 3)
            {
                context.SetResMime(MimeTypeMap.Json);
                return FalseValue;
            }
            var jobId = x[1];
            var filename = String.Join('/', x, 2, len - 2);
            var u = x[0];
            if (u.FastEquals("FolderSync"))
            {
                var res = await UploadFile(jobId, filename, context).ConfigureAwait(false);
                context.SetResMime(MimeTypeMap.Json);
                return res ? TrueValue : FalseValue;
            }
            if (u.FastEquals("FolderSyncCdc"))
            {
                context.SetResMime(MimeTypeMap.Data);
                return await UploadCdcFile(jobId, filename, context).ConfigureAwait(false);
            }

            var res2 = await UploadCdcChunks(jobId, context).ConfigureAwait(false);
            context.SetResMime(MimeTypeMap.Json);
            return res2 ? TrueValue : FalseValue;

        }

        static readonly ReadOnlyMemory<byte> TrueValue = Encoding.UTF8.GetBytes("true");
        static readonly ReadOnlyMemory<byte> FalseValue = Encoding.UTF8.GetBytes("false");

        #endregion//IHttpRequestHandler

        #region IHttpServerModule

        public String[] OnlyForPrefixes { get; init; } = 
        [
            "FolderSync/",
            "FolderSyncCdc/",
            "FolderSyncCdcChunks/",
        ];


        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            context.LocalUrl.SplitFirst('/', out var r);
            var f = r.SplitFirst('/');
            return Apis.Contains(f) ? null : this;
        }

        static readonly IReadOnlySet<String> Apis = ReadOnlyData.Set(StringComparer.Ordinal,
            "explore",
            "Folders",
            nameof(SyncFolder),
            nameof(SynchedFoldersTable),
            nameof(Activate),
            nameof(Remove),
            nameof(GetSynchedFolderManifest),
            ""
            );

        #endregion//IHttpServerModule

        public FolderSyncService(ServiceManager manager, FolderSyncParams p)
        {
            p = p ?? new();
            Manager = manager;
            FileMod = manager.TryGet<FileHttpServerModule>();
            foreach (var x in p.Folders.Nullable())
                AddFolder(x);
            
            TempRemove = TimeSpan.Zero;
            Prune().RunAsync();
            TempRemove = TimeSpan.FromHours(12);
            PruneTask = new PeriodicTask(Prune, 5 * 60 * 1000, true, true, true);
        }




        public String AddFolder(FolderSyncFolder x)
        {
            var folders = Folders;
            var path = Path.GetFullPath(PathTemplate.Resolve(x.DiscFolder));
            var name = x.Name;
            PathExt.EnsureFolderExist(path);
            PathExt.AllowAllAccess(path);
            path = new DirectoryInfo(path).FullName;
            if (String.IsNullOrEmpty(name))
                name = Path.GetFileName(path);
            var auth = x.Auth ?? Roles.Debug;
            var folder = new Folder(
                name,
                path,
                auth,
                TimeSpan.FromDays(Math.Max(0, x.RemoveBackupsDays)),
                x
                );
            folders.TryAdd(name.FastToLower(), folder);
            var fm = FileMod;
            if (fm != null)
                fm.AddFolder(folder.ModFolder);
            return path;
        }

        readonly FileHttpServerModule FileMod;
        readonly ServiceManager Manager;

        PeriodicTask PruneTask;

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
            foreach (var f in Folders)
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
            foreach (var f in GetFolderData())
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
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            var fm = FileMod;
            if (fm != null)
            {
                foreach (var x in Folders.Values)
                    fm.RemoveFolder(x.ModFolder);
            }
        }


        readonly ConcurrentDictionary<String, Folder> Folders = new ConcurrentDictionary<String, Folder>(StringComparer.Ordinal);



        readonly ConcurrentDictionary<String, Sync> SyncJobs = new ConcurrentDictionary<string, Sync>(StringComparer.Ordinal);



        const String TimeFmt = "yyyy-MM-dd_HH.mm.ss_ffff";

        String GetBakName(String folder)
        {
            var d = new DirectoryInfo(folder);
            var dir = String.Concat(d.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), '_', d.LastWriteTimeUtc.ToString(TimeFmt));
            return dir;
        }

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

        async ValueTask<Exception> InternalActivate(Folder folder, String target, String from, HttpServerRequest context)
        {
            if (!SystemLock.TryGet("ActLock" + from, out var lck))
                return new Exception("Folder \"" + from + "\" is in use, try later!");
            using var _ = lck;
            if (folder.Compress)
            {
            //  De-compress stored
                var compact = from + ContentDependentChunking.DotFileExt;
                if (File.Exists(compact))
                {
                    Manager.AddMessage(LogPrefix + "Expanding \"" + compact + "\"");
                    try
                    {
                        await ContentDependentChunking.Expand(compact).ConfigureAwait(false);
                        var ex2 = await PathExt.TryDeleteFileAsync(compact).ConfigureAwait(false);
                        if (ex2 != null)
                            return ex2;
                    }
                    catch (Exception ex3)
                    {
                        return ex3;
                    }
                }
            }
            var destFolder = folder.DestPath;
            var cmd = folder.OnDeactivate;
            if (cmd != null)
                await RunCommands(cmd).ConfigureAwait(false);
            var a = folder.OnDeactivateAsync;
            if (a != null)
            {
                try
                {
                    var ex3 = await a(folder.Name, destFolder, RunCommand).ConfigureAwait(false);
                    if (ex3 != null)
                        return ex3;
                }
                catch (Exception ex2)
                {
                    return ex2;
                }
            }
            var bakName = GetBakName(target);
            var ex = await PathExt.TryFolderSwapAsync(target, bakName, from).ConfigureAwait(false);
            if (ex == null)
                new DirectoryInfo(target).LastAccessTimeUtc = DateTime.UtcNow;
            cmd = folder.OnActivate;
            if (cmd != null)
                await RunCommands(cmd).ConfigureAwait(false);
            a = folder.OnActivateAsync;
            if (a != null)
            {
                try
                {
                    var ex3 = await a(folder.Name, destFolder, RunCommand).ConfigureAwait(false);
                    if (ex3 != null)
                        return ex3;
                }
                catch (Exception ex2)
                {
                    return ex2;
                }
            }
            context.Server.InvalidateCache();

            if (folder.Compress)
            {
                //  Compress stored
                var ex2 = await TryCompressFolderLog(bakName).ConfigureAwait(false);
                if (ex2 != null)
                    return ex2;
            }

            return ex;
        }

        static Object AuditInputFilter_SyncFolder(long id, HttpServerRequest request, Object obj)
        {
            var i = obj as FolderSyncRequest;
            if (i == null)
                return null;
            return new FolderSyncRequestAudit
            {
                FileCount = i.Files?.Length ?? 0,
                Folder = i.Folder,
                UseFolder = i.UseFolder,
                Machine = i.Machine,
                Comment = i.Comment,
            };
        }

        static Object AuditOutputFilter_SyncFolder(long id, HttpServerRequest request, Object obj)
        {
            var i = obj as FolderSyncResponse;
            if (i == null)
                return null;
            return new FolderSyncResponseAudit
            {
                FileCount = i.Files?.Length ?? 0,
                FolderCode = i.FolderCode,
                Cdc = i.Cdc,
            };
        }

        readonly ExceptionTracker Exs = new ExceptionTracker();

        /// <summary>
        /// Upload a file
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="filename"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<bool> UploadFile(String jobId, String filename, HttpServerRequest context)
        {
            if (!SyncJobs.TryGetValue(jobId, out Sync sync))
                throw new Exception("Invalid sync job!");
            sync.Touch();
            var target = sync.Target;
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            var fileKey = filename.FastToLower();
            if (!sync.Files.TryGetValue(fileKey, out var file))
                throw new Exception("Invalid filename!");
            if (Interlocked.CompareExchange(ref file.InProgress, 1, 0) != 0)
                throw new Exception("File is already being uploaded!");

            ICompType cmp = null;
            var compression = context.GetReqHeader("Content-Encoding");
            if (!String.IsNullOrEmpty(compression))
            {
                cmp = CompManager.GetFromHttp(compression);
                if (cmp == null)
                    throw new Exception("Unsupported compression method");
            }
            var data = context.InputStream;
            var dest = Path.Combine(sync.DestPath, file.Name);
            Interlocked.Increment(ref sync.FileInProgess);
            try
            {

                var ex = await PathExt.EnsureCanWriteFileAsync(dest).ConfigureAwait(false);
                if (ex != null)
                    throw ex;

                using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (cmp == null)
                    {
                        await data.CopyToAsync(destStream).ConfigureAwait(false);
                    }
                    else
                    {
                        await cmp.DecompressAsync(data, destStream).ConfigureAwait(false);
                    }
                    Interlocked.Add(ref sync.UploadSize, destStream.Position);
                }
                Interlocked.Add(ref sync.NetworkSize, context.ReqContentLength);
                Interlocked.Increment(ref sync.UploadCount);
                sync.Touch();
                new FileInfo(dest).LastWriteTimeUtc = file.LastModified;
                sync.Files.TryRemove(fileKey, out var __);
                if (sync.Files.Count > 0)
                    return true;
                if (Interlocked.CompareExchange(ref sync.DoExit, 1, 0) != 0)
                    return true;
                return await Finalize(jobId, sync, context).ConfigureAwait(false);
            }
            catch (Exception exx)
            {
                await PathExt.TryDeleteFileAsync(dest).ConfigureAwait(false);
                Interlocked.Exchange(ref file.InProgress, 0);
                Exs.OnException(exx);
                Manager.AddMessage(String.Concat(LogPrefix, "File upload failed, fo folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref sync.FileInProgess);
            }
        }

        /// <summary>
        /// Upload a file using Cdc chunks
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="filename"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<ReadOnlyMemory<Byte>> UploadCdcFile(String jobId, String filename, HttpServerRequest context)
        {
            if (!SyncJobs.TryGetValue(jobId, out Sync sync))
                throw new Exception("Invalid sync job!");
            sync.Touch();
            var target = sync.Target;
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            var fileKey = filename.FastToLower();
            if (!sync.Files.TryGetValue(fileKey, out var file))
                throw new Exception("Invalid filename!");
            if (Interlocked.CompareExchange(ref file.InProgress, 1, 0) != 0)
                throw new Exception("File is already being uploaded!");
            var dest = Path.Combine(sync.DestPath, file.Name);
            Interlocked.Increment(ref sync.FileInProgess);
            try
            {
                var hashData = await context.InputStream.ReadAllMemoryAsync().ConfigureAwait(false);
                var hashDataLen = hashData.Length;
                var hashSize = CdcProps.Default.HashSize;
                var hashCount = hashDataLen / hashSize;
                var missingMap = new Byte[(hashCount + 7) >> 3];
                bool anyMissing = false;
                int chunkIndex = 0;
                for (int i = 0; i < hashDataLen; i += hashSize, ++chunkIndex)
                {
                    if (!ContentDependentChunking.ValidateChunk(hashData.Slice(i, hashSize).Span))
                    {
                        missingMap[chunkIndex >> 3] |= (Byte)(1 << (chunkIndex & 7));
                        anyMissing = true;
                        Interlocked.Increment(ref sync.NewChunkCount);
                    }
                }
                Interlocked.Add(ref sync.ChunkCount, hashCount);
                if (anyMissing)
                {
                    file.CdcChunks = hashData;
                    return missingMap;
                }
                var ex = await PathExt.EnsureCanWriteFileAsync(dest).ConfigureAwait(false);
                if (ex != null)
                    throw ex;
                using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    for (int i = 0; i < hashDataLen; i += hashSize)
                    {
                        if (!await ContentDependentChunking.TryDecompressChunk(destStream, hashData.Slice(i, hashSize).Span).ConfigureAwait(false))
                            throw new Exception("Failed to decompress chunk!");
                    }
                    Interlocked.Add(ref sync.UploadSize, destStream.Position);
                }
                Interlocked.Add(ref sync.NetworkSize, context.ReqContentLength);
                Interlocked.Increment(ref sync.UploadCount);
                sync.Touch();
                new FileInfo(dest).LastWriteTimeUtc = file.LastModified;
                sync.Files.TryRemove(fileKey, out var __);
                if (sync.Files.Count > 0)
                    return ReadOnlyMemory<Byte>.Empty;
                if (Interlocked.CompareExchange(ref sync.DoExit, 1, 0) != 0)
                    return ReadOnlyMemory<Byte>.Empty;
                await Finalize(jobId, sync, context).ConfigureAwait(false);
                return ReadOnlyMemory<Byte>.Empty;
            }
            catch (Exception exx)
            {
                await PathExt.TryDeleteFileAsync(dest).ConfigureAwait(false);
                Interlocked.Exchange(ref file.InProgress, 0);
                Exs.OnException(exx);
                Manager.AddMessage(String.Concat(LogPrefix, "File upload failed, fo folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref sync.FileInProgess);
            }


        }


        /// <summary>
        /// Upload the Cdc chunks required to create all files
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<bool> UploadCdcChunks(String jobId, HttpServerRequest context)
        {
            if (!SyncJobs.TryGetValue(jobId, out Sync sync))
                throw new Exception("Invalid sync job!");
            sync.Touch();
            var target = sync.Target;
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            Interlocked.Increment(ref sync.FileInProgess);
            try
            {
                await ContentDependentChunking.AddChunkList(context.InputStream).ConfigureAwait(false);
                var rl = context.ReqContentLength;
                Interlocked.Add(ref sync.NetworkSize, rl);
                Interlocked.Add(ref sync.NewChunkSize, rl);
                var hashSize = CdcProps.Default.HashSize;
                var exceptions = await sync.Files.ToList().ConvertAsyncValue(async fileX =>
                {
                    var fileKey = fileX.Key;
                    var file = fileX.Value;
                    var hashData = file.CdcChunks;
                    if (hashData.IsEmpty)
                        return null;
                    var dest = Path.Combine(sync.DestPath, file.Name);
                    try
                    {
                        var ex = await PathExt.EnsureCanWriteFileAsync(dest).ConfigureAwait(false);
                        if (ex != null)
                            throw ex;
                        var hashDataLen = hashData.Length;
                        using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            for (int i = 0; i < hashDataLen; i += hashSize)
                            {
                                if (!await ContentDependentChunking.TryDecompressChunk(destStream, hashData.Slice(i, hashSize).Span).ConfigureAwait(false))
                                    return new Exception("Failed to decompress chunk!");
                            }
                            Interlocked.Add(ref sync.UploadSize, destStream.Position);
                        }
                        new FileInfo(dest).LastWriteTimeUtc = file.LastModified;
                    }
                    catch (Exception exx)
                    {
                        await PathExt.TryDeleteFileAsync(dest).ConfigureAwait(false);
                        Interlocked.Exchange(ref file.InProgress, 0);
                        Exs.OnException(exx);
                        Manager.AddMessage(String.Concat(LogPrefix, "File upload failed, Cdc to folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                        return exx;
                    }
                    Interlocked.Increment(ref sync.UploadCount);
                    sync.Files.TryRemove(fileKey, out var __);
                    return null;
                }).ConfigureAwait(false);
                sync.Touch();
                foreach (var e in exceptions)
                    if (e != null)
                        throw e;
                if (sync.Files.Count > 0)
                    return true;
                if (Interlocked.CompareExchange(ref sync.DoExit, 1, 0) != 0)
                    return true;
                await Finalize(jobId, sync, context).ConfigureAwait(false);
                return true;
            }
            catch (Exception exx)
            {
                Exs.OnException(exx);
                Manager.AddMessage(String.Concat(LogPrefix, "File upload failed, to folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref sync.FileInProgess);
            }
        }

        async ValueTask<bool> Finalize(String jobId, Sync sync, HttpServerRequest context)
        {
            var dest = sync.DestPath;
            var target = sync.Target;
            try
            {
                await WriteManifest(target, sync.R, dest, sync.CopyCount, sync.CopySize, sync.UploadCount, sync.UploadSize, sync.NetworkSize, sync.User, sync.Start).ConfigureAwait(false);
                if (sync.UseFolder)
                {
                    var exx = await InternalActivate(target, target.DestPath, dest, context).ConfigureAwait(false);
                    if (exx == null)
                    {
                        Manager.AddMessage(String.Concat(LogPrefix, "Activated folder \"", target.Name, "\""));
                    }
                    else
                    {
                        Exs.OnException(exx);
                        Manager.AddMessage(String.Concat(LogPrefix, "Sync failed, activating folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                        return false;
                    }
                }
                else
                {
                    var bakFolder = String.Concat(dest.TrimEnd(Path.DirectorySeparatorChar), "_", jobId);
                    var exx = await PathExt.TryMoveFolderAsync(dest, bakFolder).ConfigureAwait(false);
                    if (exx == null)
                    {
                        new DirectoryInfo(bakFolder).LastAccessTimeUtc = DateTime.UtcNow;
                        context.Server.InvalidateCache();
                        Manager.AddMessage(String.Concat(LogPrefix, "Synced folder \"", target.Name, "\""));
                    }
                    else
                    {
                        Exs.OnException(exx);
                        Manager.AddMessage(String.Concat(LogPrefix, "Sync failed, creating folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exx)
            {
                Exs.OnException(exx);
                Manager.AddMessage(String.Concat(LogPrefix, "Sync failed for folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                throw;
            }
            finally
            {
                SyncJobs.TryRemove(jobId, out var _);
                sync.D.Dispose();
            }

        }

        /// <summary>
        /// Activate a folder
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth("")]
        [WebApiAudit("FolderSync")]
        public async Task<bool> Activate(FolderSyncOperation r, HttpServerRequest context)
        {
            var discFolder = r.DiscFolder;
            if (!PathExt.IsValidFilename(discFolder))
                throw new Exception("Invalid disc folder!");
            var folderName = r.Folder.FastToLower();
            if (!Folders.TryGetValue(folderName, out var folder))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(folder.Auth))
                throw new Exception("Not authorized!");
            if (!SystemLock.TryGet(folder.LockName, out var lck))
                throw new Exception("A folder sync is in progress!");
            using var _x = lck;
            //  Validate 
            var path = folder.DestPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(path);
            var parent = Path.GetDirectoryName(path);
            var temp = name + "_Temp";
            var di = new DirectoryInfo(Path.Combine(parent, discFolder));
            if (!di.Exists)
                throw new Exception("Can't find disc folder!");
            discFolder = di.Name;
            if (discFolder.FastEquals(name))
                throw new Exception("Can't activate an active folder!");
            if (discFolder.FastStartsWith(temp))
                throw new Exception("Can't activate a temporary folder!");
            var ex = await InternalActivate(folder, path, di.FullName, context).ConfigureAwait(false);
            if (ex != null)
            {
                Exs.OnException(ex);
                Manager.AddMessage(String.Concat(LogPrefix, "Failed to activate folder \"", folder.Name, "\""), ex, MessageLevels.Warning);
                throw ex;
            }
            Manager.AddMessage(String.Concat(LogPrefix, "Activated folder \"", folder.Name, "\""));
            return true;
        }

        /// <summary>
        /// Remove a folder
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth("")]
        [WebApiAudit("FolderSync")]
        public async Task<bool> Remove(FolderSyncOperation r, HttpServerRequest context)
        {
            var discFolder = r.DiscFolder;
            if (!PathExt.IsValidFilename(discFolder))
                throw new Exception("Invalid disc folder!");
            var folderName = r.Folder.FastToLower();
            if (!Folders.TryGetValue(folderName, out var folder))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(folder.Auth))
                throw new Exception("Not authorized!");
            if (!SystemLock.TryGet(folder.LockName, out var lck))
                throw new Exception("A folder sync is in progress!");
            using var _x = lck;
            //  Validate 
            var path = folder.DestPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(path);
            var parent = Path.GetDirectoryName(path);
            var temp = name + "_Temp";
            var di = new DirectoryInfo(Path.Combine(parent, discFolder));
            if (!di.Exists)
                throw new Exception("Can't find disc folder!");
            discFolder = di.Name;
            if (discFolder.FastEquals(name))
                throw new Exception("Can't remove an active folder!");
            if (discFolder.FastStartsWith(temp))
                throw new Exception("Can't remove a temporary folder!");
            var ex = await PathExt.TryDeleteDirectoryAsync(di.FullName, false).ConfigureAwait(false);
            context.Server.InvalidateCache();
            if (ex != null)
                throw ex;
            return true;
        }

        const String ManifestName = "_FolderSync.txt";

        static String V(long value) => value.ToString("### ### ### ### ### ### ### ##0").TrimStart();
        
        void BuildManifest(Folder ff, String manifestName)
        {
            var start = DateTime.UtcNow;
            long totCount = 0;
            long totBytes = 0;
            String folder = Path.GetDirectoryName(manifestName);
            var di = new DirectoryInfo(folder);
            foreach (var f in di.GetFiles("*", SearchOption.AllDirectories))
            {
                ++totCount;
                totBytes += f.Length;
            }
            var end = DateTime.UtcNow;
            var duration = end - start;
            StringBuilder b = new StringBuilder();
            int tab = 16;
            b.Append("Start :".PadRight(tab)).AppendLine(start.ToString("O"));
            b.Append("End :".PadRight(tab)).AppendLine(end.ToString("O"));
            b.Append("Duration :".PadRight(tab)).AppendLine(duration.ToString());
            b.Append("Files :".PadRight(tab)).AppendLine(V(totCount));
            b.Append("Size :".PadRight(tab)).Append(V(totBytes)).AppendLine(" bytes");
            b.AppendLine("Comment :").AppendLine("Re-constructed from exsiting or manually copied data");
            File.WriteAllText(manifestName, b.ToString());
        }

        async ValueTask WriteManifest(Folder ff, FolderSyncRequest r, String folder, long copyCount, long copySize, long uploadCount, long uploadSize, long networkSize, String user, DateTime start)
        {
            var end = DateTime.UtcNow;
            var duration = end - start;
            var totCount = copyCount + uploadCount;
            var totBytes = copySize + uploadSize;
            StringBuilder b = new StringBuilder();
            int tab = 16;
            b.Append("Start :".PadRight(tab)).AppendLine(start.ToString("O"));
            b.Append("End :".PadRight(tab)).AppendLine(end.ToString("O"));
            b.Append("Duration :".PadRight(tab)).AppendLine(duration.ToString());
            b.Append("User :".PadRight(tab)).AppendLine(user ?? "-");
            b.Append("Machine :".PadRight(tab)).AppendLine(r.Machine ?? "-");
            b.Append("Files :".PadRight(tab)).AppendLine(V(totCount));
            b.Append("Size :".PadRight(tab)).Append(V(totBytes)).AppendLine(" bytes");
            b.Append("Copied :".PadRight(tab)).Append(V(copyCount)).Append(" [ ").Append((100M * (Decimal)copyCount / (Decimal)Math.Max(1, totCount)).ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" % ]");
            b.Append("Copied size :".PadRight(tab)).Append(V(copySize)).Append(" bytes [ ").Append((100M * (Decimal)copySize / (Decimal)Math.Max(1, totBytes)).ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" % ]");
            b.Append("Uploaded :".PadRight(tab)).Append(V(uploadCount)).Append(" [ ").Append((100M * (Decimal)uploadCount / (Decimal)Math.Max(1, totCount)).ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" % ]");
            b.Append("Uploaded size :".PadRight(tab)).Append(V(uploadSize)).Append(" bytes [ ").Append((100M * (Decimal)uploadSize / (Decimal)Math.Max(1, totBytes)).ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" % ]");
            b.Append("Network size :".PadRight(tab)).Append(V(networkSize)).Append(" bytes [ ").Append((100M * (Decimal)networkSize / (Decimal)Math.Max(1, uploadSize)).ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" % ]");
            var c = r.Comment;
            if (!String.IsNullOrEmpty(c))
                b.AppendLine("Comment :").AppendLine(c);
            await File.WriteAllTextAsync(Path.Combine(folder, ManifestName), b.ToString()).ConfigureAwait(false);

            var cmd = ff.OnNewFolder;
            if (cmd != null)
            {
                var x = new Dictionary<String, String>(StringComparer.Ordinal);
                x.Add("name", ff.Name);
                x.Add("target", folder);
                x.Add("targetname", Path.GetFileName(folder));
                x.Add("targetdir", Path.GetDirectoryName(folder));
                var rcmd = cmd.Convert(c => PathTemplate.Resolve(c, x));
                await RunCommands(rcmd).ConfigureAwait(false);
            }
            var a = ff.OnNewFolderAsync;
            if (a != null)
            {
                try
                {
                    var ex3 = await a(ff.Name, folder, RunCommand).ConfigureAwait(false);
                    if (ex3 != null)
                        Exs.OnException(ex3);
                }
                catch (Exception ex2)
                {
                    Exs.OnException(ex2);
                }
            }
        }


        /// <summary>
        /// Begin sync of a folder
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth("")]
        [WebApiAudit("FolderSync")]
        [WebApiAuditFilterParams(nameof(AuditInputFilter_SyncFolder))]
        [WebApiAuditFilterReturn(nameof(AuditOutputFilter_SyncFolder))]
        public async Task<FolderSyncResponse> SyncFolder(FolderSyncRequest r, HttpServerRequest context)
        {
            DateTime start = DateTime.UtcNow;
            var folderName = r.Folder.FastToLower();
            if (!Folders.TryGetValue(folderName, out var target))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            String newFolderName = null;
            if (!SystemLock.TryGet(target.LockName, out var lck))
                throw new Exception("A folder sync is already in progress!");
            try
            { 
                var dest = target.DestPath;
                ConcurrentDictionary<String, FileSync> upload = new (StringComparer.Ordinal);
                ConcurrentDictionary<String, int> copy = new (StringComparer.Ordinal);
                ConcurrentDictionary<String, int> all = new(StringComparer.Ordinal);
                await r.Files.ProcessAsyncValue(async x =>
                {
                    var name = x.Name;
                    var fullPath = Path.GetFullPath(Path.Combine(dest, name));
                    if (!fullPath.FastStartsWith(dest))
                        throw new Exception("Invalid file name!");
                    all.TryAdd(name, 0);
                    var hash = await FileHash.GetHashAsync(fullPath).ConfigureAwait(false);
                    if ((hash == null) || (!hash.FastEquals(x.Hash)))
                    {
                        upload.TryAdd(name, new FileSync(name, x.LastModified));
                        return;
                    }
                    copy.TryAdd(name, 0);
                }).ConfigureAwait(false);
                all.TryAdd(ManifestName, 0);
                var destL = dest.Length;
                if (upload.Count <= 0)
                {
                    //  Check for any removed files
                    bool foundExtra = false;
                    foreach (var x in Directory.GetFiles(dest, "*", SearchOption.AllDirectories))
                    {
                        var local = x.Substring(destL);
                        foundExtra = !all.ContainsKey(local);
                        if (foundExtra)
                            break;
                    }
                //  Noting to do!
                    if (!foundExtra)
                        return new FolderSyncResponse();
                }

                String jobId;
                DateTime now;
                for (int ret = 0; ;++ ret)
                {
                    now = DateTime.UtcNow;
                    jobId = now.ToString(TimeFmt);
                    newFolderName = String.Concat(dest.TrimEnd(Path.DirectorySeparatorChar), "_Temp", jobId);
                    if (!Directory.Exists(newFolderName))
                    {
                        if (!SyncJobs.ContainsKey(jobId))
                        {
                            var ex = await PathExt.EnsureFolderExistAsync(newFolderName).ConfigureAwait(false);
                            if (ex == null)
                                break;
                            if (ret > 10)
                                throw ex;
                        }
                        PathExt.AllowAllAccess(newFolderName);
                    }
                    await Task.Delay(1).ConfigureAwait(false);
                }
                var f = new DirectoryInfo(newFolderName);
                f.LastWriteTimeUtc = now;
                long copySize = 0;
                if (copy.Count > 0)
                {
                    foreach (var name in copy.Keys)
                    {
                        var destFile = Path.Combine(newFolderName, name);
                        PathExt.EnsureFolderExist(Path.GetDirectoryName(destFile));
                        PathExt.AllowAllAccess(newFolderName);
                        var sourceFile = Path.Combine(dest, name);
                        File.Copy(sourceFile, destFile);
                        copySize += new FileInfo(sourceFile).Length;
                    }
                }
                if (upload.Count <= 0)
                {
                    await WriteManifest(target, r, newFolderName, copy.Count, copySize, 0, 0, 0, context.Session.Auth?.Username, start).ConfigureAwait(false);
                    if (r.UseFolder)
                    {
                        var exx = await InternalActivate(target, dest, newFolderName, context).ConfigureAwait(false);
                        if (exx == null)
                        {
                            Manager.AddMessage(String.Concat(LogPrefix, "Activated folder \"", target.Name, "\""));
                        }
                        else
                        {
                            Exs.OnException(exx);
                            Manager.AddMessage(String.Concat(LogPrefix, "Sync failed, activating folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                            throw exx;
                        }
                    }
                    else
                    {
                        var exx = await PathExt.TryMoveFolderAsync(newFolderName, String.Concat(dest.TrimEnd(Path.DirectorySeparatorChar), "_", jobId)).ConfigureAwait(false);
                        if (exx == null)
                        { 
                            new DirectoryInfo(newFolderName).LastAccessTimeUtc = DateTime.UtcNow;
                            context.Server.InvalidateCache();
                            Manager.AddMessage(String.Concat(LogPrefix, "Synced folder \"", target.Name, "\""));
                        }
                        else
                        {
                            Exs.OnException(exx);
                            Manager.AddMessage(String.Concat(LogPrefix, "Sync failed, creating folder \"", target.Name, "\""), exx, MessageLevels.Warning);
                            throw exx;
                        }
                    }
                    return new FolderSyncResponse();
                }
                SyncJobs.TryAdd(jobId, new Sync(r, upload.Values, newFolderName, target, r.UseFolder, lck, copy.Count, copySize, context.Session.Auth?.Username, start));
                lck = null;
                return new FolderSyncResponse
                {
                    FolderCode = jobId,
                    Files = upload.Values.Select(x => x.Name).ToArray(),
                    Cdc = target.Compress ? CdcProps.Default.Key : null,
                };
            }
            catch
            {
                if (newFolderName != null)
                    await PathExt.TryDeleteDirectoryAsync(newFolderName, false).ConfigureAwait(false);
                throw;
            }
            finally
            {
                lck?.Dispose();
            }
        }


        IEnumerable<Data> GetFolderData(Folder folder)
        {
            var uploadName = folder.Name;
            var path = folder.DestPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(path);
            var parent = Path.GetDirectoryName(path);
            var temp = Path.Combine(parent, name + "_Temp");
            var mp = ManifestParsers;
            return Directory.GetDirectories(parent, name + "*", SearchOption.TopDirectoryOnly)
            .Where(x => !x.FastStartsWith(temp))
            .Select(dir =>
            {
                var fn = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var di = new DirectoryInfo(dir);
                var lastTime = di.LastWriteTimeUtc;
                var acc = di.LastAccessTimeUtc;
                if (acc > lastTime)
                    lastTime = acc;
                String actions = null;
                var isActive = name.FastEquals(fn);
                if (!isActive)
                {
                    actions = JsonSer.ToString(new FolderSyncOperation
                    {
                        Folder = uploadName,
                        DiscFolder = fn,
                    });
                    actions = Uri.EscapeDataString(actions);
                }
                var a = folder.Auth;
                var data = new Data
                {
                    Name = uploadName,
                    DiscFolder = fn,
                    IsActive = isActive,
                    Uploaded = di.CreationTimeUtc,
                    LastUsed = lastTime,
                    Actions = actions,
                    FullPath = di.FullName,
                    Comp = File.Exists(di.FullName + ContentDependentChunking.DotFileExt),
                    Auth = a == null ? null : String.Join(',', a),
                    Folder = folder,
                };
                try
                {
                    var mn = Path.Combine(di.FullName, ManifestName);
                    if (!File.Exists(mn))
                    {
                        try
                        {
                            BuildManifest(folder, mn);
                            if (folder.Compress)
                            {

                            }
                        }
                        catch
                        {
                        }
                    }
                    if (File.Exists(mn))
                    {
                        var t = File.ReadAllLines(mn);
                        int lineIndex = 0;
                        foreach (var x in t)
                        {
                            var line = x.Trim();
                            var key = line.SplitFirst(':', out var value).TrimEnd().FastToLower();
                            if (mp.TryGetValue(key, out var fnx))
                            {
                                try
                                {
                                    fnx(data, value.TrimStart(), t, lineIndex);
                                }
                                catch
                                {
                                }
                            }
                            ++lineIndex;
                        }
                    }
                }
                catch
                {
                }
                return data;
            });

        }

        static readonly IEnumerable<Data> Empty = Array.Empty<Data>();

        public IEnumerable<Data> GetFolderData(String syncName)
            => Folders.TryGetValue(syncName.FastToLower(), out var f) ? GetFolderData(f) : Empty;

        public IEnumerable<Data> GetFolderData()
            => Folders.Values.SelectMany(GetFolderData);


        /// <summary>
        /// All synched folders as a table
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "Debug/SynchedFolders", "Synched folders", "Details about folders that can be synched remotely", "IconSync", -6)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData SynchedFoldersTable(TableDataRequest r)
        {
            if (r == null)
                r = new TableDataRequest();
            if ((r.Order == null) || (r.Order.Length <= 0))
                r.Order = [
                    nameof(Data.Name),
                    "-" + nameof(Data.Uploaded),
                    ];
            return TableDataTools.Get(r, 5000, GetFolderData());
        }



        /// <summary>
        /// Display the manifest of a folder
        /// </summary>
        /// <param name="folder">name/version, ex: "web/web_asas"</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRawText]
        public async Task<ReadOnlyMemory<Byte>> GetSynchedFolderManifest(String folder)
        {
            var f = folder.Split('/');
            var folderName = f[0].FastToLower();
            var ff = f[1];
            if (!Folders.TryGetValue(folderName, out var data))
                throw new Exception("Unknown folder id");
            var target = data.DestPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fname = Path.GetFileName(target);
            var parent = Path.GetDirectoryName(target);
            if (!ff.StartsWith(fname))
                throw new Exception("Invalid folder version name");
            var name = Path.Combine(parent, ff, ManifestName);
            return await File.ReadAllBytesAsync(name).ConfigureAwait(false);
        }

        static readonly IReadOnlyDictionary<String, Action<Data, String, String[], int>> ManifestParsers = new Dictionary<String, Action<Data, String, String[], int>>(StringComparer.Ordinal)
        {
            { "end", (data, value, l, i) => data.Uploaded = DateTime.Parse(value).ToUniversalTime() },
            { "files", (data, value, l, i) => data.Count = long.Parse(value.Replace(" ", "")) },
            { "size", (data, value, l, i) => data.Size = long.Parse(value.SplitFirst('b').Replace(" ", "")) },
            { "user", (data, value, l, i) => data.User = value },
            { "machine", (data, value, l, i) => data.Machine = value },
            { "comment", (data, value, l, i) => data.Comment = String.Join('\n', l, i + 1, l.Length - i - 1).Trim() },

        }.Freeze();

        static readonly ITextSerializer JsonSer = SerManager.GetText("json");


    }

}
