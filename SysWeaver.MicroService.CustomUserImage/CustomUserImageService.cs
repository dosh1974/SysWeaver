using ImageMagick;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Media;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public sealed class CustomUserImageService : IFileRepo, IUserImageHandler, IDisposable
    {
        public CustomUserImageService(ServiceManager manager, CustomUserImageParams p)
        {
            Manager = manager;
            manager.OnServiceAdded += Manager_OnServiceAdded;
            AuthManager = manager.TryGet<AuthManagerService>();

            p = p ?? new CustomUserImageParams();
            Key = String.IsNullOrEmpty(p.Key) ? "UserImage" : p.Key;
            var s = p.Sizes;
            if (s != null)
            {
                s = new HashSet<int>(s.Where(x => x != 0)).OrderBy(x => x < 0 ? -x : x).ToArray();
                if (s.Length <= 0)
                    s = null;
            }
            s = s ?? [-64, 512];

            var sl = s.Length;
            var priv = new RequestOptions(3, 2, 100000000, null, p.PrivateAuth);
            var pub = new RequestOptions(3, 2, 100000000, null, p.PublicAuth);
            Dictionary<int, RequestOptions> sizeOptions = new Dictionary<int, RequestOptions>();
            for (int i = 0; i < sl; ++ i)
            {
                var val = s[i];
                var vp = priv;
                if (val < 0)
                {
                    val = -val;
                    vp = pub;
                    s[i] = val;
                }
                sizeOptions.Add(val, vp);
            }
            SizeOptions = sizeOptions.Freeze();
            Sizes = s;
            DataFolders = p.SystemWide ? Folders.AllSharedFolders : Folders.AllAppFolders;
            AllowTransparent = p.AllowTransparent;

            if (p.Format == "jpeg")
            {
                SaveFormat = MagickFormat.Jpeg;
                SaveExt = ".jpg";
                SaveMime = MimeTypeMap.GetMimeType(SaveExt);
                AllowTransparent = false;
            }

            MagickColor col = new MagickColor("#000");
            try
            {
                col = new MagickColor(p.BackgroundColor ?? "#000");
            }
            catch
            {
            }
            col.A = 0xffff;
            Background = col;
        }

        void Manager_OnServiceAdded(object arg1, ServiceInfo arg2)
        {
            var a = arg1 as AuthManagerService;
            if (a != null)
                AuthManager = a;
        }

        public void Dispose()
        {
            Manager.OnServiceAdded -= Manager_OnServiceAdded;
        }

        readonly ServiceManager Manager;
        AuthManagerService AuthManager;

        readonly IReadOnlyList<String> DataFolders;

        #region IFileRepo

        readonly MagickFormat SaveFormat = MagickFormat.Png;
        readonly String SaveExt = ".png";
        readonly Tuple<String, bool> SaveMime = MimeTypeMap.GetMimeType(".png");
        readonly bool AllowTransparent;
        readonly MagickColor Background;

        public string Key { get; init; }

        public IReadOnlyList<FileHttpServerModuleFolder> ExposeFolders { get; init; }


        public string UploadAuth { get; } = "";

        static readonly IReadOnlySet<String> SupportedImageFiles = ReadOnlyData.Set(StringComparer.Ordinal,
            ".png",
            ".tif",
            ".tiff",
            ".jpg",
            ".jpeg",
            ".webp",
            ".avif"
        );

        public async Task<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
        {
            var len = info.Length;
            if (len != 1)
                return ArrayExt.Create(len, FileRepoTools.MultipleFilesNotAllowed);
            var fi = info[0];
            var check = CheckFile(fi);
            if (check != null)
                return [check];
            var name = r.Session.Auth.Guid.ToHex();
            var folders = DataFolders;
            var fl = folders.Count;
            var shard = GetShard(name, fl);
            var pathPrefix = Path.Combine(folders[shard], Key, name);
            var hashFile = pathPrefix + ".txt";
            if (File.Exists(hashFile))
            {
                try
                {
                    var hash = await FileExt.ReadTextAsync(hashFile).ConfigureAwait(false);
                    if (hash.FastEquals(fi.Hash))
                    {
                        foreach (var x in Sizes)
                        {
                            var f = new FileInfo(String.Concat(pathPrefix, '_', x, SaveExt));
                            if (!f.Exists)
                                return [FileRepoTools.Upload];
                            if (f.Length <= 0)
                                return [FileRepoTools.Upload];
                        }
                        return [new FileUploadResult(FileUploadStatus.AlreadyUploaded, String.Concat("auth/UserImages/", name, "/large"))];
                    }
                }
                catch
                {
                }
            }
            return [FileRepoTools.Upload];
        }



        /// <summary>
        /// The max size of an image file in bytes
        /// </summary>
        const int MaxFileLength = 10 << 20;

        /// <summary>
        /// Check the announced size and the extension of a file
        /// </summary>
        /// <param name="file">The file</param>
        /// <returns>null if the file can be uploaded, else the refusal</returns>
        static FileUploadResult CheckFile(FileUploadInfo file)
        {
            if (file.Length > MaxFileLength)
                return FileRepoTools.RefuseSize;
            if (!SupportedImageFiles.Contains(file.GetExtension().FastToLower()))
                return FileRepoTools.RefuseExtension;
            return null;
        }

        public async Task<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
        {
            //  Never trust that CanFileBeUploaded was called (or that the client obeyed it)
            var check = CheckFile(file);
            if (check != null)
                return check;
            var uid = r.Session.Auth.Guid;
            var name = uid.ToHex();
            if (!SystemLock.TryGet("SysWeaver.CustomUserImages." + name, out var ldisp))
                return FileRepoTools.OperationInProgress;
            using var x = ldisp;
            //  Never read more than the announced (and accepted) length, decompressed if the data is compressed
            var length = (int)Math.Max(file.Length, 0);
            var buffer = ArrayPool<Byte>.Shared.Rent(length + 1);
            try
            {
                var used = await ReadLimited(s, decoder, buffer, length).ConfigureAwait(false);
                if (used < 0)
                    return FileRepoTools.RefuseSize;
                return await Upload(new ReadOnlyMemory<Byte>(buffer, 0, used), file, r, uid, name).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Read (and decompress) all data of a stream into a buffer, the stream is disposed
        /// </summary>
        /// <param name="s">The stream to read from</param>
        /// <param name="decoder">The decoder of compressed data, null if the data isn't compressed</param>
        /// <param name="buffer">The buffer to read into, at least max + 1 bytes</param>
        /// <param name="max">The max number of bytes to read</param>
        /// <returns>The number of bytes read, -1 if there are more than max bytes</returns>
        static async ValueTask<int> ReadLimited(Stream s, ICompDecoder decoder, Byte[] buffer, int max)
        {
            using (s)
            {
                if (decoder != null)
                {
                    var ls = new LimitedMemoryWriteStream(buffer, max);
                    try
                    {
                        await decoder.DecompressAsync(s, ls).ConfigureAwait(false);
                    }
                    catch when (ls.Exceeded)
                    {
                        return -1;
                    }
                    return ls.Written;
                }
                int used = 0;
                for (; ; )
                {
                    //  Read one byte more than the max, to detect a stream that is too long
                    var r = await s.ReadAsync(buffer.AsMemory(used, max + 1 - used)).ConfigureAwait(false);
                    if (r <= 0)
                        return used;
                    used += r;
                    if (used > max)
                        return -1;
                }
            }
        }

        /// <summary>
        /// A write only stream that writes to a byte array, throwing an IOException (and setting Exceeded) if more than a given number of bytes are written.
        /// </summary>
        sealed class LimitedMemoryWriteStream : Stream
        {
            public LimitedMemoryWriteStream(Byte[] buffer, int max)
            {
                Buffer = buffer;
                Max = max;
            }
            readonly Byte[] Buffer;
            readonly int Max;

            /// <summary>
            /// The number of bytes written
            /// </summary>
            public int Written;

            /// <summary>
            /// True if a write was refused because the max length was exceeded
            /// </summary>
            public bool Exceeded;

            int Add(int count)
            {
                var w = Written;
                if (count > (Max - w))
                {
                    Exceeded = true;
                    throw new IOException("The data is larger than the max allowed size of " + Max + " bytes!");
                }
                Written = w + count;
                return w;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => Written;
            public override long Position { get => Written; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<Byte>(buffer, offset, count));

            public override void Write(ReadOnlySpan<byte> buffer) => buffer.CopyTo(Buffer.AsSpan(Add(buffer.Length)));

            public override void WriteByte(byte value) => Buffer[Add(1)] = value;

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Write(new ReadOnlySpan<Byte>(buffer, offset, count));
                return Task.CompletedTask;
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Write(buffer.Span);
                return ValueTask.CompletedTask;
            }
        }

        async Task<FileUploadResult> Upload(ReadOnlyMemory<Byte> data, FileUploadInfo file, HttpServerRequest r, String uid, String name)
        {
            var info = new MagickImageInfo(data.Span);
            switch (info.Format)
            {
                case MagickFormat.Png:
                case MagickFormat.Jpeg:
                case MagickFormat.Tif:
                case MagickFormat.WebP:
                case MagickFormat.Avif:
                    break;
                default:
                    return FileRepoTools.InvalidFile;
            }
            var sizes = Sizes;
            var ss = sizes.Length;
            ReadOnlyMemory<Byte>[] imgs = new ReadOnlyMemory<byte>[ss];

            using var img = new MagickImage(data.Span);
            if ((!AllowTransparent) && (img.HasAlpha))
            {
                img.BackgroundColor = Background;
                img.Alpha(AlphaAction.Remove);
            }
            for (int i = 0; i < ss; ++ i)
            {
                var size = sizes[i];
                using var resized = img.Clone() as MagickImage;
                if (AllowTransparent)
                    ImageTools.FitInto(resized, size, size, true, MagickColors.Transparent);
                else
                    ImageTools.FillInto(resized, size, size, true);
                resized.Format = SaveFormat;
                imgs[i] = ImageTools.ToData(resized);
            }
            var folders = DataFolders;
            var fl = folders.Count;
            var shard = GetShard(name, fl);
            var folder = Path.Combine(folders[shard], Key);
            var pathPrefix = Path.Combine(folder, name);
            var saveExt = SaveExt;
            var hashFile = pathPrefix + ".txt";
            var ex = await PathExt.TryDeleteFileAsync(hashFile).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            await PathExt.EnsureFolderExistAsync(folder).ConfigureAwait(false);
            for (int i = 0; i < ss; ++ i)
            {
                var size = sizes[i];
                await FileExt.WriteMemoryAsync(String.Concat(pathPrefix, '_', size, saveExt), imgs[i], true).ConfigureAwait(false);
            }
            await File.WriteAllTextAsync(hashFile, file.Hash).ConfigureAwait(false);
            AuthManager?.InvalidateUserImageCache(uid, r);
            r.Session.InvalidateCache();
            //r.Server.PushMessageUser(uid, HttpServerBase.MessageRefresh);
            return new FileUploadResult(FileUploadStatus.None, String.Concat("auth/UserImages/", name, "/large"));
        }

        #endregion//IFileRepo

        /// <summary>
        /// Remove a custom user image
        /// </summary>
        /// <param name="r"></param>
        /// <returns>True if successful</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<bool> RemoveUserImage(HttpServerRequest r)
        {
            var session = r.Session;
            var uid = session.Auth.Guid;
            var name = uid.ToHex();
            if (!SystemLock.TryGet("SysWeaver.CustomUserImages." + name, out var ldisp))
                return false;
            using var x = ldisp;
            var folders = DataFolders;
            var fl = folders.Count;
            var shard = GetShard(name, fl);
            var folder = Path.Combine(folders[shard], Key);
            var pathPrefix = Path.Combine(folder, name);
            var saveExt = SaveExt;
            var hashFile = pathPrefix + ".txt";
            var ex = await PathExt.TryDeleteFileAsync(hashFile).ConfigureAwait(false);
            var exs = await Sizes.ConvertAsync(size => PathExt.TryDeleteFileAsync(String.Concat(pathPrefix, '_', size, saveExt))).ConfigureAwait(false);
            AuthManager?.InvalidateUserImageCache(uid, r);
            return true;
        }


        #region IUserImageHandler

        public int[] Sizes { get; init; }

        public async Task<bool> Delete(string userGuid)
        {
            var name = userGuid.ToHex();
            var folders = DataFolders;
            var fl = folders.Count;
            var shard = GetShard(name, fl);
            var pathPrefix = Path.Combine(folders[shard], Key, name);
            var saveExt = SaveExt;
            var ex = await PathExt.TryDeleteFileAsync(pathPrefix + ".txt").ConfigureAwait(false);
            foreach (var size in Sizes)
            {
                //  Always try to delete all files, return the first error
                var e = await PathExt.TryDeleteFileAsync(String.Concat(pathPrefix, '_', size, saveExt)).ConfigureAwait(false);
                ex ??= e;
            }
            return ex == null;
        }

        readonly IReadOnlyDictionary<int, RequestOptions> SizeOptions;

        static readonly Task<IHttpRequestHandler> NoImage = Task.FromResult<IHttpRequestHandler>(null);

        public Task<IHttpRequestHandler> Get(string userGuid, int size)
        {
            if (!SizeOptions.TryGetValue(size, out var opt))
                return NoImage;
            var name = userGuid.ToHex();
            var folders = DataFolders;
            var fl = folders.Count;
            var shard = GetShard(name, fl);
            var pathPrefix = Path.Combine(folders[shard], Key, name);
            var fn = String.Concat(pathPrefix, '_', size, SaveExt);
            var fi = new FileInfo(fn);
            if (!fi.Exists)
                return NoImage;
            return Task.FromResult<IHttpRequestHandler>(new FileHttpRequestHandler(SaveMime, fi, opt, true, null));
        }

        #endregion//IUserImageHandler


        static int GetShard(String s, int shardCount)
            => (String.GetHashCode(s) & 0x7fffffff) % shardCount;

    }

}
