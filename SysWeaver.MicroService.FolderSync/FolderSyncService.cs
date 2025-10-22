using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
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
    public class FolderSyncService : IHttpServerModule, IHttpRequestHandler, IDisposable
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
                return FalseValue;
            var jobId = x[1];
            var filename = String.Join('/', x, 2, len - 2);
            var res = await UploadFile(jobId, filename, context).ConfigureAwait(false);
            return res ? TrueValue : FalseValue;
        }

        static readonly ReadOnlyMemory<byte> TrueValue = Encoding.UTF8.GetBytes("true");
        static readonly ReadOnlyMemory<byte> FalseValue = Encoding.UTF8.GetBytes("false");

        #endregion//IHttpRequestHandler


        #region IHttpServerModule

        public String[] OnlyForPrefixes { get; init; } = ["FolderSync/"];


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

        readonly FileHttpServerModule FileMod;

        public FolderSyncService(ServiceManager manager, FolderSyncParams p)
        {
            Dictionary<String, Folder> folders = new (StringComparer.Ordinal);
            var fm = manager.TryGet<FileHttpServerModule>();
            fm = manager.TryGet<FileHttpServerModule>();
            foreach (var x in p.Folders)
            {
                var path = Path.GetFullPath(x.DiscFolder);
                var name = x.Name;
                PathExt.EnsureFolderExist(path);
                path = new DirectoryInfo(path).FullName;
                if (String.IsNullOrEmpty(name))
                    name = Path.GetFileName(path);
                var auth = x.Auth ?? Roles.Debug;
                var folder = new Folder(name, path, auth, TimeSpan.FromDays(Math.Max(0, x.RemoveBackupsDays)));
                folders.Add(name.FastToLower(), folder);
                if (fm != null)
                    fm.AddFolder(folder.ModFolder);
            }
            Folders = folders.Freeze();
            TempRemove = TimeSpan.Zero;
            Prune().RunAsync();
            TempRemove = TimeSpan.FromHours(12);
            PruneTask = new PeriodicTask(Prune, 5 * 60 * 1000, true, true, true);
        }

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
                            await Task.Delay(100).ConfigureAwait(false);
                        }
                    }
                }
                catch
                {
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
            return true;
        }

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

        sealed class Folder
        {
            public readonly String LockName;
            public readonly String Name;
            public readonly String DestPath;
            public readonly IReadOnlyList<String> Auth;
            public TimeSpan RemoveAfter;
            public readonly FileHttpServerModuleFolder ModFolder;
            public Folder(string name, string path, string auth, TimeSpan removeAfter)
            {
                Name = name;
                DestPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Auth = Authorization.GetRequiredTokens(auth);
                RemoveAfter = removeAfter;
                LockName = "FolderSync_" + Encoding.UTF8.GetBytes(name.FastToLower()).ToHex();
                ModFolder = new FileHttpServerModuleFolder
                {
                    AssumePreCompressed = true,
                    Auth = Roles.AdminOps,
                    ClientCacheDuration = 5,
                    RequestCacheDuration = 4,
                    WebFolder = "FolderSync/Folders/" + name,
                    DiscFolder = DestPath,
                };
            }
        }

        sealed class FileSync
        {
            public override string ToString() => Name;

            public readonly String Name;

            public readonly DateTime LastModified;

            public int InProgress;

            public FileSync(string name, DateTime lastModified)
            {
                Name = name;
                LastModified = lastModified;
            }
        }


        sealed class Sync
        {
            public readonly ConcurrentDictionary<String, FileSync> Files = new ConcurrentDictionary<string, FileSync>();
            public readonly Folder Target;
            public readonly String DestPath;
            public readonly bool UseFolder;
            public readonly IDisposable D;

            public long FileInProgess;



            public void Touch()
            {
                Interlocked.Exchange(ref LastUsed, DateTime.UtcNow.Ticks);
            }

            static readonly long ExpirationTime = TimeSpan.FromMinutes(5).Ticks;

            public bool IsOld
            {
                get
                {
                    return (DateTime.UtcNow.Ticks - Interlocked.Read(ref LastUsed)) > ExpirationTime;
                }
            }

            long LastUsed;

            public int DoExit;

            public readonly long CopyCount;
            public readonly long CopySize;
            public readonly String User;
            public readonly DateTime Start;
            public readonly FolderSyncRequest R;

            public long UploadCount;
            public long UploadSize;
            public long NetworkSize;

            public Sync(FolderSyncRequest r, IEnumerable<FileSync> files, string destPath, Folder target, bool activate, IDisposable d, long copyCount, long copySize, String user, DateTime start)
            {
                R = r;
                var fs = Files;
                foreach (var f in files)
                    fs.TryAdd(f.Name.FastToLower().Replace('\\', '/'), f);
                Target = target;
                DestPath = destPath;
                UseFolder = activate;
                D = d;
                CopyCount = copyCount;
                CopySize = copySize;
                User = user;
                Start = start;
                LastUsed = DateTime.UtcNow.Ticks;

            }
        }


        readonly IReadOnlyDictionary<String, Folder> Folders;



        readonly ConcurrentDictionary<String, Sync> SyncJobs = new ConcurrentDictionary<string, Sync>();



        const String TimeFmt = "yyyy-MM-dd_HH.mm.ss_ffff";

        String GetBakName(String folder)
        {
            var d = new DirectoryInfo(folder);
            var dir = String.Concat(d.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), '_', d.LastWriteTimeUtc.ToString(TimeFmt));
            return dir;
        }
        async ValueTask<Exception> InternalActivate(String target, String from, HttpServerRequest context)
        {
            var ex = await PathExt.TryFolderSwapAsync(target, GetBakName(target), from).ConfigureAwait(false);
            if (ex == null)
                new DirectoryInfo(target).LastAccessTimeUtc = DateTime.UtcNow;
            context.Server.InvalidateCache();
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
            };
        }

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
            ICompType cmp = null;
            var compression = context.GetReqHeader("Content-Encoding");
            if (!String.IsNullOrEmpty(compression))
            {
                cmp = CompManager.GetFromHttp(compression);
                if (cmp == null)
                    throw new Exception("Unsupported compression method");
            }
            if (!SyncJobs.TryGetValue(jobId, out Sync sync))
                throw new Exception("Invalid sync job!");
            var target = sync.Target;
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            var fileKey = filename.FastToLower();
            if (!sync.Files.TryGetValue(fileKey, out var file))
                throw new Exception("Invalid filename!");
            if (Interlocked.CompareExchange(ref file.InProgress, 1, 0) != 0)
                throw new Exception("File is already being uploaded!");
            sync.Touch();
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
                dest = sync.DestPath;
                try
                {
                    await WriteManifest(sync.R, dest, sync.CopyCount, sync.CopySize, sync.UploadCount, sync.UploadSize, sync.NetworkSize, sync.User, sync.Start).ConfigureAwait(false);
                    if (sync.UseFolder)
                    {
                        var exx = await InternalActivate(target.DestPath, dest, context).ConfigureAwait(false);
                        if (exx != null)
                            return false;
                    }
                    else
                    {
                        var exx = await PathExt.TryMoveFolderAsync(dest, String.Concat(dest.TrimEnd(Path.DirectorySeparatorChar), "_", jobId)).ConfigureAwait(false);
                        if (exx == null)
                            new DirectoryInfo(dest).LastAccessTimeUtc = DateTime.UtcNow;
                        context.Server.InvalidateCache();
                        if (exx != null)
                            return false;
                    }
                    return true;
                }
                finally
                {
                    SyncJobs.TryRemove(jobId, out var _);
                    sync.D.Dispose();
                }
            }
            catch
            {
                await PathExt.TryDeleteFileAsync(dest).ConfigureAwait(false);
                Interlocked.Exchange(ref file.InProgress, 0);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref sync.FileInProgess);
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
            var ex = await InternalActivate(path, di.FullName, context).ConfigureAwait(false);
            if (ex != null)
                throw ex;
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


        Task WriteManifest(FolderSyncRequest r, String folder, long copyCount, long copySize, long uploadCount, long uploadSize, long networkSize, String user, DateTime start)
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
            return File.WriteAllTextAsync(Path.Combine(folder, ManifestName), b.ToString());
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
            if (!Folders.TryGetValue(folderName, out var folder))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(folder.Auth))
                throw new Exception("Not authorized!");
            if (!SystemLock.TryGet(folder.LockName, out var lck))
                throw new Exception("A folder sync is already in progress!");
            String newFolderName = null;
            try
            { 
                var dest = folder.DestPath;
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
                        if (!SyncJobs.ContainsKey(jobId))
                        {
                            var ex = await PathExt.EnsureFolderExistAsync(newFolderName).ConfigureAwait(false);
                            if (ex == null)
                                break;
                            if (ret > 10)
                                throw ex;
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
                        var sourceFile = Path.Combine(dest, name);
                        File.Copy(sourceFile, destFile);
                        copySize += new FileInfo(sourceFile).Length;
                    }
                }
                if (upload.Count <= 0)
                {
                    await WriteManifest(r, newFolderName, copy.Count, copySize, 0, 0, 0, context.Session.Auth?.Username, start).ConfigureAwait(false);
                    if (r.UseFolder)
                    {
                        var exx = await InternalActivate(dest, newFolderName, context).ConfigureAwait(false);
                        if (exx != null)
                            throw exx;
                    }
                    else
                    {
                        var exx = await PathExt.TryMoveFolderAsync(newFolderName, String.Concat(dest.TrimEnd(Path.DirectorySeparatorChar), "_", jobId)).ConfigureAwait(false);
                        if (exx == null)
                            new DirectoryInfo(newFolderName).LastAccessTimeUtc = DateTime.UtcNow;
                        context.Server.InvalidateCache();
                        if (exx != null)
                            throw exx;
                    }
                    return new FolderSyncResponse();
                }
                SyncJobs.TryAdd(jobId, new Sync(r, upload.Values, newFolderName, folder, r.UseFolder, lck, copy.Count, copySize, context.Session.Auth?.Username, start));
                lck = null;
                return new FolderSyncResponse
                {
                    FolderCode = jobId,
                    Files = upload.Values.Select(x => x.Name).ToArray(),
                   
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






        /// <summary>
        /// All synched folders as a table
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "Debug/SynchedFolders", "Synched folders", "Details about folders that can be synched remotely", "IconSync", -6)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData SynchedFoldersTable(TableDataRequest r, HttpServerRequest context)
        {
            if (r == null)
                r = new TableDataRequest();
            if ((r.Order == null) || (r.Order.Length <= 0))
                r.Order = [
                    nameof(Data.Name),
                    "-" + nameof(Data.Uploaded),
                    ];
            return TableDataTools.Get(r, 5000, Folders.Values.SelectMany(folder =>
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
                    var data = new Data
                    {
                        Name = uploadName,
                        DiscFolder = fn,
                        IsActive = isActive,
                        Uploaded = di.CreationTimeUtc,
                        LastUsed = lastTime,
                        Actions = actions,
                        FullPath = di.FullName,
                    };
                    try
                    {
                        var mn = Path.Combine(di.FullName, ManifestName);
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
            }));
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
            { "end", (data, value, l, i) => data.Uploaded = DateTime.Parse(value) },
            { "files", (data, value, l, i) => data.Count = long.Parse(value.Replace(" ", "")) },
            { "size", (data, value, l, i) => data.Size = long.Parse(value.SplitFirst('b').Replace(" ", "")) },
            { "user", (data, value, l, i) => data.User = value },
            { "machine", (data, value, l, i) => data.Machine = value },
            { "comment", (data, value, l, i) => data.Comment = String.Join('\n', l, i + 1, l.Length - i - 1).Trim() },

        }.Freeze();

        static readonly ITextSerializer JsonSer = SerManager.GetText("json");

        sealed class Data
        {
 

            /// <summary>
            /// True if active
            /// </summary>
            public bool IsActive;

            /// <summary>
            /// Folder name on disc
            /// </summary>
            [TableDataUrl("{0}", "../FolderSync/GetSynchedFolderManifest?\"{1}/{0}\"", "Click to show the manifest file.")]
            public String DiscFolder;

            /// <summary>
            /// Name of the repo, use this when synchronizing a local folder.
            /// </summary>
            [TableDataUrl("{0}", "*../FolderSync/Folders/{0}/explore", "Click to explore \"{3}\".")]
            public String Name;


            /// <summary>
            /// Number of files in the folder
            /// </summary>
            public long Count;

            /// <summary>
            /// The number of bytes (sum of all file sizes)
            /// </summary>
            [TableDataByteSize]
            public long Size;

            /// <summary>
            /// Folder creation time
            /// </summary>
            [TableDataSortDesc]
            public DateTime Uploaded;

            /// <summary>
            /// The service user that uploaded this
            /// </summary>
            public String User;

            /// <summary>
            /// The name of the source machine (this can be anything)
            /// </summary>
            public String Machine;

            /// <summary>
            /// Optional comment supplied when uploading this folder
            /// </summary>
            [TableDataText]
            public String Comment;

            /// <summary>
            /// Actions that can be performed
            /// </summary>
            [TableDataActions(
                "Activate", 
                "Click to activate this folder (rename to base name)",
                "../FolderSync/" + nameof(Activate) + "?{0}",
                "IconOk",

                "Remove",
                "Click to remove this folder",
                "../FolderSync/" + nameof(Remove) + "?{0}",
                "IconCancel"
                )]
            public String Actions;

            /// <summary>
            /// When folder was last used (as active)
            /// </summary>
            [TableDataSortDesc]
            public DateTime LastUsed;

            /// <summary>
            /// Full path
            /// </summary>
            [TableDataText]
            public String FullPath;


        }


    }

}
