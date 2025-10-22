using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    sealed class UserStorageFileRepo : IFileRepo
    {
        public UserStorageFileRepo(
            String key, 
            UserStorageUpload p, 
            Func<HttpServerRequest, String, ValueTuple<String, String>> getPaths, 
            Func<String, ValueTask<long>> getMaxSize,
            Func<String, ValueTuple<FileInfo, ICompType>> getDiscFile
            )
        {
            Key = key;
            var a = p.Auth ?? "";
            GetPaths = getPaths;
            GetMaxSize = getMaxSize;
            GetDiscFile = getDiscFile;

            AllowPreCompressed = p.AllowPreCompressed;
            UploadAuth = a.FastEquals("-") ? "" : a;
            var l = p.Whitelist?.Trim();
            if (String.IsNullOrEmpty(l))
            {
                l = p.Blacklist?.Trim();
                if (!String.IsNullOrEmpty(l))
                {
                    Blacklist = new HashSet<String>(l.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.TrimStart('.').FastToLower()), StringComparer.Ordinal).Freeze();
                    IsValidExt = f => !Blacklist.Contains(f.GetExtension().TrimStart('.').FastToLower());
                }
            }
            else
            {
                Whitelist = new HashSet<String>(l.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.TrimStart('.').FastToLower()), StringComparer.Ordinal).Freeze();
                IsValidExt = f => Whitelist.Contains(f.GetExtension().TrimStart('.').FastToLower());
            }
            MaxSize = p.MaxFileSize;
            AllowMultiple = p.AllowMultiple;
        }

        readonly Func<HttpServerRequest, String, ValueTuple<String, String>> GetPaths;
        readonly Func<String, ValueTask<long>> GetMaxSize;
        readonly Func<String, ValueTuple<FileInfo, ICompType>> GetDiscFile;

        readonly long MaxSize;
        readonly IReadOnlySet<String> Whitelist;
        readonly IReadOnlySet<String> Blacklist;
        readonly Func<FileUploadInfo, bool> IsValidExt;
        readonly bool AllowMultiple;
        readonly bool AllowPreCompressed;


        public string Key { get; init; }

        public IReadOnlyList<FileHttpServerModuleFolder> ExposeFolders => null;

        public string UploadAuth { get; init; }

        public async ValueTask<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
        {
            var il = info.Length;
            if ((il > 1) && (!AllowMultiple))
                return ArrayExt.Create(il, FileRepoTools.MultipleFilesNotAllowed);
            long size = 0;
            var storage = await GetMaxSize(r.Session.Auth.Guid).ConfigureAwait(false);
            var maxSize = MaxSize;
            if ((maxSize <= 0) || (maxSize > storage))
                maxSize = storage;

            var testExt = IsValidExt;
            var gp = GetPaths;
            var gdf = GetDiscFile;
            var res = info.Convert(f =>
            {
                if (testExt != null)
                    if (!testExt(f))
                        return FileRepoTools.RefuseExtension;
                var s = f.Length;
                if (s > maxSize)
                    return FileRepoTools.RefuseSize;
                size += s;
                if (size > maxSize)
                    return FileRepoTools.RefuseSize;
                return FileRepoTools.Upload;
            });
            await res.ProcessAsyncValue(async (s, i) =>
            {
                if (s.Result != FileUploadStatus.Upload)
                    return;
                var f = info[i];
                var name = gp(r, f.Name);
                var d = gdf(name.Item1);
                if (d.Item1 == null)
                    return;
                String hash;
                if (d.Item2 == null)
                    hash = await FileHash.GetHashAsync(d.Item1.FullName).ConfigureAwait(false);
                else
                    hash = await DecompressedFileHash.GetHashAsync(d.Item1.FullName).ConfigureAwait(false);
                var wh = FileRepoTools.FormatAsFileHash(f.Hash);
                if (hash.FastEquals(wh))
                    res[i] = new FileUploadResult(FileUploadStatus.AlreadyUploaded, name.Item2);
            }).ConfigureAwait(false);
            return res;
        }


        static async ValueTask WriteToFile(Stream s, String dest)
        {
            using var ds = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            await s.CopyToAsync(ds).ConfigureAwait(false);
        }

        public async ValueTask<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
        {
            var storage = await GetMaxSize(r.Session.Auth.Guid).ConfigureAwait(false);
            var maxSize = MaxSize;
            if ((maxSize <= 0) || (maxSize > storage))
                maxSize = storage;
            if (file.Length > maxSize)
                return FileRepoTools.RefuseSize;
            var paths = GetPaths(r, file.Name);
            var filename = paths.Item1;
            var url = paths.Item2;
            if (decoder != null)
            {
                if (AllowPreCompressed)
                    await WriteToFile(s, String.Join('.', filename, decoder.FileExtensions.FirstOrDefault())).ConfigureAwait(false);
                else
                {
                    using var ds = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
                    await decoder.DecompressAsync(s, ds).ConfigureAwait(false);
                }
            }
            else
                await WriteToFile(s, filename).ConfigureAwait(false);
            return new FileUploadResult(FileUploadStatus.None, url);
        }
    }

}
