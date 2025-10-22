using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    public class StaticFileRepo : IFileRepo
    {
        /// <summary>
        /// Upload key (used by clients to choose this repo, this value must be set!)
        /// </summary>
        public string Key {  get; set; }

        /// <summary>
        /// The required auth for uploading files (null = no auth required, "" = no special auth token is required, but user must be authenticated)
        /// </summary>
        public String UploadAuth { get; set; }

        /// <summary>
        /// Maximum allowed file size allowed
        /// </summary>
        public long MaxLength = 10L << 20;

        /// <summary>
        /// What extensions to allow (null or empty to allow all)
        /// </summary>
        public String[] Extensions;

        /// <summary>
        /// Where to store the uploaded files on disc (null to use a temp folder), {0} = Key
        /// </summary>
        public String DestPath;

        /// <summary>
        /// If true, a subfolder per user is created (avoids collision of data)
        /// </summary>
        public bool AddUserFolder;

        /// <summary>
        /// If greater than zero, remove files automatically if they haven't been accessed for this many day.
        /// Only works if serving files.
        /// </summary>
        public int RemoveIfUnusedDays;

        /// <summary>
        /// If true (and DestPath is null) then a folder shared between all apps is used
        /// </summary>
        public bool SharedFolder;


        #region Serving files

        /// <summary>
        /// Set to true to serve these files (as in being able to download them)
        /// </summary>
        public bool ServeFiles = true;

        /// <summary>
        /// If true, compressed files that are uploaded will stay compressed on disc, files will be served assuming pre compressed, i.e "Test.txt.gzip" may be served in place of "Test.txt" if Test.txt is older or non existent.
        /// </summary>
        public bool SavePreCompressed = true;

        /// <summary>
        /// The web folder to serve files in (null or empty uses the Key)
        /// </summary>
        public String WebFolder;

        /// <summary>
        /// Number of seconds to cache the file on a client
        /// </summary>
        public int ClientCacheDuration = 5;
        
        /// <summary>
        /// Number of seconds to cache any intermediate results (i.e small files that are compressed on the fly)
        /// </summary>
        public int RequestCacheDuration = 30;
        
        /// <summary>
        /// The maximum size of a file that can be cached
        /// </summary>
        public long MaxCacheSize = 32768;
        
        /// <summary>
        /// The preferred on the fly compression schemes
        /// </summary>
        public String Compression = "br: Balanced, deflate: Balanced, gzip: Balanced";
        
        /// <summary>
        /// The required auth for these files (null = no auth required, "" = no special auth token is required, but user must be authenticated)
        /// </summary>
        public String ServeAuth;

        #endregion//Serving files



        /// <summary>
        /// The information about the folders to expose (if serving files are enabled)
        /// </summary>
        public IReadOnlyList<FileHttpServerModuleFolder> ExposeFolders
        {
            get
            {
                Cache();
                return ServeFolders;
            }
        }

        /// <summary>
        /// True if the repo owner is expected to periodically remove old files by calling the RemoveOldFilesNow method.
        /// </summary>
        public bool RemoveOld
        {
            get
            {
                Cache();
                return DoRemoveOld > 0;
            }
        }

        
        bool IsCached;
        bool DoAddUserFolder;
        int DoRemoveOld;
        HashSet<String> ValidExtensions;
        String[] SaveFolders;
        FileHttpServerModuleFolder[] ServeFolders; 

        protected void Cache()
        {
            if (IsCached)
                return;
            lock (this)
            {
                if (IsCached)
                    return;
                DoAddUserFolder = AddUserFolder && (UploadAuth != null);
                var e = Extensions;
                if ((e != null) && (e.Length > 0))
                {
                    var h = new HashSet<string>();
                    ValidExtensions = h;
                    foreach (var f in e)
                        h.Add(f.Trim().TrimStart('.').FastToLower());
                }
                var p = DestPath;
                String[] folders;
                if (p != null)
                {
                    p = String.Format(p, Key);
                    p = PathExt.RootExecutable(p);
                    p = PathExt.GetFullDirectoryName(p);
                    folders = [p];
                }else
                {
                    var t = "Upload_" + Key;
                    folders = (SharedFolder ? Folders.AllSharedFolders : Folders.AllAppFolders).Select(x => Path.Combine(x, t)).ToArray();
                }
                foreach (var x in folders)
                {
                    var ex = PathExt.EnsureFolderExist(x);
                    PathExt.AllowAllAccess(x);
                }
                SaveFolders = folders;
                if (ServeFiles)
                {
                    var w = WebFolder;
                    w = (String.IsNullOrEmpty(w) ? Key : w).Trim('/');
                    WebFolder = w;
                    var rem = RemoveIfUnusedDays;
                    DoRemoveOld = rem > 0 ? rem : 0;
                    ServeFolders = folders.Select(x => new FileHttpServerModuleFolder
                    {
                        AssumePreCompressed = SavePreCompressed,
                        UpdateAccessTime = rem > 0,
                        DiscFolder = x,
                        WebFolder = w,
                        ClientCacheDuration = ClientCacheDuration,
                        RequestCacheDuration = RequestCacheDuration,
                        MaxCacheSize = MaxCacheSize,
                        Compression = Compression,
                        Auth = ServeAuth,
                    }).ToArray();
                }
                IsCached = true;
            }
        }


        public async Task<int> RemoveOldFilesNow(ExceptionTracker tracker)
        {
            if (!IsCached)
                return 0;
            var rem = DoRemoveOld;
            if (rem <= 0)
                return 0;
            int delCount = 0;
            try
            {
                var lastAccess = DateTime.UtcNow.AddDays(-rem);
                foreach (var folder in SaveFolders)
                {
                    try
                    {

                        if (DoAddUserFolder)
                        {
                            foreach (var d in Directory.GetDirectories(folder))
                            {
                                try
                                {
                                    bool didDelete = false;
                                    foreach (var f in Directory.GetFiles(d))
                                    {
                                        try
                                        {
                                            if (new FileInfo(f).LastAccessTimeUtc < lastAccess)
                                            {
                                              var ex = await PathExt.TryDeleteFileAsync(f).ConfigureAwait(false);
                                                if (ex == null)
                                                {
                                                    ++delCount;
                                                    didDelete = true;
                                                }else
                                                {
                                                    tracker?.OnException(ex);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tracker?.OnException(ex);
                                        }
                                    }
                                    if (didDelete)
                                    {
                                        var ex = await PathExt.TryRemoveEmptyFoldersAsync(d).ConfigureAwait(false);
                                        if (ex == null)
                                        {
                                            ++delCount;
                                        }
                                        else
                                        {
                                            tracker?.OnException(ex);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tracker?.OnException(ex);
                                }
                            }
                        }
                        else
                        {
                            foreach (var f in Directory.GetFiles(folder))
                            {
                                try
                                {
                                    if (new FileInfo(f).LastAccessTimeUtc < lastAccess)
                                    {
                                        var ex = await PathExt.TryDeleteFileAsync(f).ConfigureAwait(false);
                                        if (ex == null)
                                        {
                                            ++delCount;
                                        }
                                        else
                                        {
                                            tracker?.OnException(ex);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tracker?.OnException(ex);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        tracker?.OnException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                tracker?.OnException(ex);
            }
            return delCount;
        }





        /// <summary>
        /// Called once per request to check credentials etc, may return an object used in other calls
        /// </summary>
        /// <param name="r"></param>
        /// <param name="isUpload">True if it's the actual upload request</param>
        /// <returns></returns>
        protected virtual Task<Tuple<FileUploadStatus, Object>> OnRequest(HttpServerRequest r, bool isUpload) => DefaultOnRequest;
    
        
        protected static readonly Task<Tuple<FileUploadStatus, Object>> DefaultOnRequest = Task.FromResult(Tuple.Create(FileUploadStatus.None, (Object)null));


        /// <summary>
        /// Get filename and status (None to procceed with the upload elese return this state).
        /// It's VERY important to verify that the filename doesn't escape the intended path (using .. for instance).
        /// </summary>
        /// <param name="status">Return None to proceed as usual</param>
        /// <param name="uri">The url to return when sharing (minus the base share) </param>
        /// <param name="info"></param>
        /// <param name="r"></param>
        /// <param name="context">As returned by the OnRequest method</param>
        /// <returns></returns>
        protected virtual String GetFullFilename(out FileUploadStatus status, out String uri, FileUploadInfo info, HttpServerRequest r, Object context)
        {
            var name = info.Name;
            var folders = SaveFolders;
            var folder = folders[(String.GetHashCode(name) & 0x7fffffff) % folders.Length];
            uri = null;
            if (DoAddUserFolder)
            {
                uri = r.Session.Auth.Guid.ToHex();
                folder = Path.Combine(folder, uri);
            }
            var fi = new FileInfo(Path.Combine(folder, name));
            var fullName = fi.FullName;
            if (!fullName.FastStartsWith(folder + Path.DirectorySeparatorChar))
            {
                status = FileUploadStatus.InvalidFileName;
                return null;
            }
            uri = uri == null ? fi.Name : String.Concat(uri, '/', fi.Name);
            status = FileUploadStatus.None;
            return fullName;
        }

        protected virtual Task<FileUploadStatus> OnUploaded(FileInfo file, long replacedSize, FileUploadInfo info, HttpServerRequest r, Object context)
        {
            return Task.FromResult(FileUploadStatus.None);
        }

        protected static void EnsureFolder(String path)
        {
            if (Directory.Exists(path))
                return;
            try
            {
                Directory.CreateDirectory(path);
            }
            catch
            {
            }
        }

        public async ValueTask<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
        {
            Cache();
            var orr = await OnRequest(r, false).ConfigureAwait(false);
            var resS = orr.Item1;
            var l = info.Length;
            var s = GC.AllocateUninitializedArray<FileUploadResult>(l);
            if (resS != FileUploadStatus.None)
                return ArrayExt.Create(l, new FileUploadResult(resS));
            var context = orr.Item2;
            using var __x = context as IDisposable;
            var e = ValidExtensions;
            var ml = MaxLength;
            for (int i = 0; i < l; ++ i)
            {
                var f = info[i];
                if (ml > 0)
                {
                    if (info.Length > ml)
                    {
                        s[i] = FileRepoTools.RefuseSize;
                        continue;
                    }
                }
                if (e != null)
                {
                    var extP = f.Name.LastIndexOf('.');
                    var ext = extP < 0 ? "" : f.Name.Substring(extP + 1).FastToLower();
                    if (!e.Contains(ext))
                    {
                        s[i] = FileRepoTools.RefuseExtension;
                        continue;
                    }
                }
                var fn = f.Name.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                f.Name = fn;
                if (!PathExt.IsValidSubPath(fn))
                {
                    s[i] = FileRepoTools.InvalidFileName;
                    continue;
                }
                var fullName = GetFullFilename(out resS, out var uri, f, r, context);
                if (resS != FileUploadStatus.None)
                {
                    s[i] = new FileUploadResult(resS);
                    continue;
                }
                var fi = new FileInfo(fullName);
                if (fi.Exists)
                {
                    if (fi.Length == f.Length)
                    {
                        var hash = FileRepoTools.FormatAsFileHash(f.Hash);
                        if (hash == null)
                        {
                            s[i] = FileRepoTools.Refuse;
                            continue;
                        }
                        try
                        {
                            if (FileHash.GetHash(fullName).FastEquals(hash))
                            {
                                fi.LastAccessTimeUtc = DateTime.UtcNow;
                                String url = null;
                                if (ServeFiles)
                                    url = String.Join('/', WebFolder, uri);
                                s[i] = new FileUploadResult(FileUploadStatus.AlreadyUploaded, url);
                                continue;
                            }
                        }
                        catch
                        {
                        }
                    }
                }else
                {
                    if (SavePreCompressed)
                    {
                        var dir = Path.GetDirectoryName(fullName);
                        EnsureFolder(dir);
                        var comp = Directory.GetFiles(dir, Path.GetFileName(fullName) + ".*", SearchOption.TopDirectoryOnly).FirstOrDefault(x => CompManager.GetFromExt(Path.GetExtension(x)) != null);
                        if (comp != null)
                        {
                            var hash = FileRepoTools.FormatAsFileHash(f.Hash);
                            if (hash == null)
                            {
                                s[i] = FileRepoTools.Refuse;
                                continue;
                            }
                            try
                            {
                                if (DecompressedFileHash.GetHash(comp) == hash)
                                {
                                    new FileInfo(comp).LastAccessTimeUtc = DateTime.UtcNow;
                                    String url = null;
                                    if (ServeFiles)
                                        url = String.Join('/', WebFolder, uri);
                                    s[i] = new FileUploadResult(FileUploadStatus.AlreadyUploaded, url);
                                    continue;
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                s[i] = FileRepoTools.Upload;
            }
            return s;
        }

        public async ValueTask<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
        {
            Cache();
            var orr = await OnRequest(r, true).ConfigureAwait(false);
            var resS = orr.Item1;
            if (resS != FileUploadStatus.None)
                return new FileUploadResult(resS);
            var context = orr.Item2;
            using var __x = context as IDisposable;
            var fileName = GetFullFilename(out resS, out var uri, file, r, context);
            if (resS != FileUploadStatus.None)
                return new FileUploadResult(resS);
            //  Ensure that only one instance of the same file can be uploaded at once
            if (!SystemLock.TryGet("SysWeaver.MediaUpload." + fileName.ToLower(), out var ldisp))
                return FileRepoTools.OperationInProgress;
            using var __y = ldisp;
            if (decoder != null)
            {
                if (SavePreCompressed)
                {
                    fileName = String.Join('.', fileName, decoder.FileExtensions.First());
                }else
                {
                    var raw = await decoder.GetDecompressedAsync(s).ConfigureAwait(false);
                    await s.DisposeAsync().ConfigureAwait(false);
                    s = raw.AsStream();
                }
            }
            var prev = new FileInfo(fileName);
            var prevLength = prev.Exists ? prev.Length : 0;
            using (s)
            {
                EnsureFolder(Path.GetDirectoryName(fileName));
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    await s.CopyToAsync(fs).ConfigureAwait(false);
            }
            var fi = new FileInfo(fileName);
            if (!fi.Exists)
                return FileRepoTools.UploadFailed;
            var t = DateTime.UnixEpoch.AddMilliseconds(file.LastModified);
            fi.LastWriteTimeUtc = t;
            fi.CreationTimeUtc = DateTime.UtcNow;
            resS = await OnUploaded(fi, prevLength, file, r, context).ConfigureAwait(false);
            if (resS != FileUploadStatus.None)
                return new FileUploadResult(resS);
            String url = null;
            if (ServeFiles)
                url = String.Join('/', WebFolder, uri);
            return new FileUploadResult(resS, url);
        }


    }
}
