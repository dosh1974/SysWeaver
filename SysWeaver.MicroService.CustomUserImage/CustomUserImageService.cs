using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                s = new HashSet<int>(s.Where(x => x > 0)).OrderBy(x => x).ToArray();
                if (s.Length <= 0)
                    s = null;
            }
            Sizes = s ?? [64, 512];
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

        public async ValueTask<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
        {
            var len = info.Length;
            if (len != 1)
                return ArrayExt.Create(len, FileRepoTools.MultipleFilesNotAllowed);
            var fi = info[0];
            if (fi.Length > (10 << 20))
                return [FileRepoTools.RefuseSize];
            if (!SupportedImageFiles.Contains(fi.GetExtension().FastToLower()))
                return [FileRepoTools.RefuseExtension];
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
                    var hash = await File.ReadAllTextAsync(hashFile).ConfigureAwait(false);
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



        public async ValueTask<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
        {
            var uid = r.Session.Auth.Guid;
            var name = uid.ToHex();
            if (!SystemLock.TryGet("SysWeaver.CustomUserImages." + name, out var ldisp))
                return FileRepoTools.OperationInProgress;
            using var x = ldisp;
            var data = await s.ReadAllMemoryAsync().ConfigureAwait(false);
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
                img.Alpha(AlphaOption.Remove);
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
            AuthManager?.InvalidateUserImageCache(uid);
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
            var uid = r.Session.Auth.Guid;
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
            AuthManager?.InvalidateUserImageCache(uid);
            r.Session.InvalidateCache();
            //r.Server.PushMessageUser(uid, HttpServerBase.MessageRefresh);
            return true;
        }


        #region IUserImageHandler

        public int[] Sizes { get; init; }

        public async ValueTask<bool> Delete(string userGuid)
        {
            var name = userGuid.ToHex();
            var folders = DataFolders;
            var fl = folders.Count;
            var shard = GetShard(name, fl);
            var pathPrefix = Path.Combine(folders[shard], Key, name);
            var saveExt = SaveExt;
            var ex = await PathExt.TryDeleteFileAsync(pathPrefix + ".txt").ConfigureAwait(false);
            foreach (var size in Sizes)
                ex = ex ?? (await PathExt.TryDeleteFileAsync(String.Concat(pathPrefix, '_', size, saveExt)).ConfigureAwait(false));
            return ex == null;
        }

        static readonly RequestOptions ReqOp = new RequestOptions(3, 2, 100000000, null, "");

        public ValueTask<IHttpRequestHandler> Get(string userGuid, int size)
        {
            var name = userGuid.ToHex();
            var folders = DataFolders;
            var fl = folders.Count;
            var shard = GetShard(name, fl);
            var pathPrefix = Path.Combine(folders[shard], Key, name);
            var fn = String.Concat(pathPrefix, '_', size, SaveExt);
            var fi = new FileInfo(fn);
            return ValueTask.FromResult<IHttpRequestHandler>(fi.Exists ? new FileHttpRequestHandler(SaveMime, fi, ReqOp, true, null) : null);
        }

        #endregion//IUserImageHandler


        static int GetShard(String s, int shardCount)
            => (String.GetHashCode(s) & 0x7fffffff) % shardCount;

    }

}
