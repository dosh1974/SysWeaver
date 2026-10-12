using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
                        h.Add(f.Trim().FastTrimStartToLower('.'));
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
                    PathExt.CreateDataFolder(x);
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
            => NullTaskFileUploadStatus;
        static readonly Task<FileUploadStatus> NullTaskFileUploadStatus = Task.FromResult(FileUploadStatus.None);

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

        public async Task<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
        {
            Cache();
            var orr = await OnRequest(r, false).ConfigureAwait(false);
            var resS = orr.Item1;
            var l = info.Length;
            if (resS != FileUploadStatus.None)
                return ArrayExt.Create(l, new FileUploadResult(resS));
            var s = GC.AllocateUninitializedArray<FileUploadResult>(l);
            var context = orr.Item2;
            using var __x = context as IDisposable;
            var e = ValidExtensions;
            var ml = MaxLength;
            for (int i = 0; i < l; ++ i)
            {
                var f = info[i];
                if (ml > 0)
                {
                    if (f.Length > ml)
                    {
                        s[i] = FileRepoTools.RefuseSize;
                        continue;
                    }
                }
                if (e != null)
                {
                    var extP = f.Name.LastIndexOf('.');
                    var ext = extP < 0 ? "" : f.Name.FastToLower(extP + 1);
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
                        var comp = Directory.EnumerateFiles(dir, Path.GetFileName(fullName) + ".*", SearchOption.TopDirectoryOnly).FirstOrDefault(x => CompManager.GetFromExt(Path.GetExtension(x)) != null);
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
                var chunks = f.Chunks;
                if (chunks != null)
                {
                    //  Check what chunks exist
                    var props = CdcProps.Default;
                    var hashSize = props.HashSize;
                    var len = chunks.Length;
                    var missing = GC.AllocateUninitializedArray<Byte>(len);
                    int wo = 0;
                    for (int co = 0; co < len; co += hashSize)
                    {
                        if (ContentDependentChunking.ValidateChunk(chunks.AsSpan(co, hashSize), props))
                            continue;
                        Array.Copy(chunks, co, missing, wo, hashSize);
                        wo += hashSize;
                    }
                    Array.Resize(ref missing, wo);
                    s[i] = new FileUploadResult(FileUploadStatus.Upload)
                    {
                        Chunks = missing
                    };
                    ChunkCache.GetOrUpdate(f.ChunkCacheKey, _ => missing);
                }
            }
            return s;
        }

        readonly FastMemCache<String, Byte[]> ChunkCache = new FastMemCache<string, byte[]>(TimeSpan.FromMinutes(30), StringComparer.Ordinal);

        public async Task<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
        {
            Cache();
            var orr = await OnRequest(r, true).ConfigureAwait(false);
            var resS = orr.Item1;
            if (resS != FileUploadStatus.None)
                return new FileUploadResult(resS);
            var context = orr.Item2;
            using var __x = context as IDisposable;
            //  Never trust that CanFileBeUploaded was called (or that the client obeyed it)
            var length = file.Length;
            var ml = MaxLength;
            if ((ml > 0) && (length > ml))
                return FileRepoTools.RefuseSize;
            var e = ValidExtensions;
            if (e != null)
            {
                var n = file.Name;
                var extP = n.LastIndexOf('.');
                var ext = extP < 0 ? "" : n.FastToLower(extP + 1);
                if (!e.Contains(ext))
                    return FileRepoTools.RefuseExtension;
            }
            var fileName = GetFullFilename(out resS, out var uri, file, r, context);
            if (resS != FileUploadStatus.None)
                return new FileUploadResult(resS);
            //  Ensure that only one instance of the same file can be uploaded at once
            if (!SystemLock.TryGet("SysWeaver.MediaUpload." + fileName.ToLower(), out var ldisp))
                return FileRepoTools.OperationInProgress;
            using var __y = ldisp;
            ICompDecoder decompress = null;
            if (ChunkCache.TryGet(file.ChunkCacheKey, out var chunks))
            {
                if (await ContentDependentChunking.AddChunkList(s).ConfigureAwait(false) < 0)
                    return new FileUploadResult(FileUploadStatus.InvalidFile);
                await s.DisposeAsync().ConfigureAwait(false);
                s = ContentDependentChunking.OpenChunkStream(chunks);
            }
            else
            {
                if (decoder != null)
                {
                    if (SavePreCompressed)
                    {
                        fileName = String.Join('.', fileName, decoder.FileExtensions.First());
                    }
                    else
                    {
                        //  Decompress while writing, so that the size limit applies to the decompressed data
                        decompress = decoder;
                    }
                }
            }
            var prev = new FileInfo(fileName);
            var prevLength = prev.Exists ? prev.Length : 0;
            bool tooLarge = false;
            using (s)
            {
                EnsureFolder(Path.GetDirectoryName(fileName));
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    //  Never write more than the announced (and accepted) length (pre compressed data is smaller, the client only compresses if it's smaller)
                    var ls = new MaxLengthWriteStream(fs, length);
                    try
                    {
                        if (decompress != null)
                            await decompress.DecompressAsync(s, ls).ConfigureAwait(false);
                        else
                            await s.CopyToAsync(ls).ConfigureAwait(false);
                    }
                    catch when (ls.Exceeded)
                    {
                        tooLarge = true;
                    }
                }
            }
            if (tooLarge)
            {
                await PathExt.TryDeleteFileAsync(fileName).ConfigureAwait(false);
                return FileRepoTools.RefuseSize;
            }
            var fi = new FileInfo(fileName);
            if (!fi.Exists)
                return FileRepoTools.UploadFailed;
            var t = DateTime.UnixEpoch.AddMilliseconds(file.LastModified);
            //  The creation time first: on Linux it can't be set, .NET sets the last write time instead
            fi.CreationTimeUtc = DateTime.UtcNow;
            fi.LastWriteTimeUtc = t;
            resS = await OnUploaded(fi, prevLength, file, r, context).ConfigureAwait(false);
            if (resS != FileUploadStatus.None)
                return new FileUploadResult(resS);
            String url = null;
            if (ServeFiles)
                url = String.Join('/', WebFolder, uri);
            return new FileUploadResult(resS, url);
        }

        /// <summary>
        /// A write only stream that forwards writes to another stream, throwing an IOException (and setting Exceeded) if more than a given number of bytes are written.
        /// </summary>
        sealed class MaxLengthWriteStream : Stream
        {
            public MaxLengthWriteStream(Stream inner, long max)
            {
                Inner = inner;
                Max = max;
            }
            readonly Stream Inner;
            readonly long Max;
            long Written;

            /// <summary>
            /// True if a write was refused because the max length was exceeded
            /// </summary>
            public bool Exceeded;

            void Add(int count)
            {
                var w = Written + count;
                if (w > Max)
                {
                    Exceeded = true;
                    throw new IOException("The data is larger than the max allowed size of " + Max + " bytes!");
                }
                Written = w;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => Written;
            public override long Position { get => Written; set => throw new NotSupportedException(); }
            public override void Flush() => Inner.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => Inner.FlushAsync(cancellationToken);
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                Add(count);
                Inner.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                Add(buffer.Length);
                Inner.Write(buffer);
            }

            public override void WriteByte(byte value)
            {
                Add(1);
                Inner.WriteByte(value);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Add(count);
                return Inner.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Add(buffer.Length);
                return Inner.WriteAsync(buffer, cancellationToken);
            }
        }


    }
}
