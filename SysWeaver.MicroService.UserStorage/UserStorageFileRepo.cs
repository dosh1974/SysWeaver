using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
            Func<String, Task<long>> getMaxSize,
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
                    Blacklist = new HashSet<String>(l.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.FastTrimStartToLower('.')), StringComparer.Ordinal).Freeze();
                    IsValidExt = f => !Blacklist.Contains(f.GetExtension().FastTrimStartToLower('.'));
                }
            }
            else
            {
                Whitelist = new HashSet<String>(l.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.FastTrimStartToLower('.')), StringComparer.Ordinal).Freeze();
                IsValidExt = f => Whitelist.Contains(f.GetExtension().FastTrimStartToLower('.'));
            }
            MaxSize = p.MaxFileSize;
            AllowMultiple = p.AllowMultiple;
        }

        readonly Func<HttpServerRequest, String, ValueTuple<String, String>> GetPaths;
        readonly Func<String, Task<long>> GetMaxSize;
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

        public async Task<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
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


        /// <summary>
        /// Write (or decompress) a stream to a file, never more than maxLength bytes
        /// </summary>
        /// <param name="s">The stream to read from</param>
        /// <param name="dest">The file to write</param>
        /// <param name="decompress">The decoder to decompress the data with, null to write the data as is</param>
        /// <param name="maxLength">The max number of bytes to write</param>
        /// <returns>False if the data is larger than maxLength (the file is deleted)</returns>
        static async Task<bool> WriteToFile(Stream s, String dest, ICompDecoder decompress, long maxLength)
        {
            await PathExt.EnsureCanWriteFileAsync(dest).ConfigureAwait(false);
            bool tooLarge = false;
            using (var ds = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var ls = new MaxLengthWriteStream(ds, maxLength);
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
            if (!tooLarge)
                return true;
            await PathExt.TryDeleteFileAsync(dest).ConfigureAwait(false);
            return false;
        }

        public async Task<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
        {
            var storage = await GetMaxSize(r.Session.Auth.Guid).ConfigureAwait(false);
            var maxSize = MaxSize;
            if ((maxSize <= 0) || (maxSize > storage))
                maxSize = storage;
            var length = file.Length;
            if (length > maxSize)
                return FileRepoTools.RefuseSize;
            var paths = GetPaths(r, file.Name);
            var filename = paths.Item1;
            var url = paths.Item2;
            //  Never write more than the announced (and accepted) length (pre compressed data is smaller, the client only compresses if it's smaller)
            bool ok;
            var written = filename;
            if (decoder != null)
            {
                if (AllowPreCompressed)
                {
                    written = String.Join('.', filename, decoder.FileExtensions.FirstOrDefault());
                    ok = await WriteToFile(s, written, null, length).ConfigureAwait(false);
                }
                else
                    ok = await WriteToFile(s, filename, decoder, length).ConfigureAwait(false);
            }
            else
                ok = await WriteToFile(s, filename, null, length).ConfigureAwait(false);
            if (!ok)
                return FileRepoTools.RefuseSize;
            //  Remove the file of the same name in the other encodings (the plain file is found first, a compressed one when there is no plain file)
            await DeleteOtherEncodings(filename, written).ConfigureAwait(false);
            return new FileUploadResult(FileUploadStatus.None, url);
        }

        /// <summary>
        /// The file extensions of the compressed files (".br", ".gz" etc), the same as UserStorageService.CompTypes
        /// </summary>
        readonly String[] CompExtensions = CompManager.ExtensionHandlers.Select(x => "." + x.Key.TrimStart('.')).Distinct(StringComparer.Ordinal).ToArray();

        /// <summary>
        /// Delete the plain and compressed versions of a file, except the one that was written
        /// </summary>
        /// <param name="filename">The name of the plain file</param>
        /// <param name="written">The name of the file that was written (the plain or a compressed file)</param>
        async Task DeleteOtherEncodings(String filename, String written)
        {
            if (!written.FastEquals(filename))
                await DeleteFile(filename).ConfigureAwait(false);
            foreach (var x in CompExtensions)
            {
                var f = String.Concat(filename, x);
                if (!written.FastEquals(f))
                    await DeleteFile(f).ConfigureAwait(false);
            }
        }

        static async Task DeleteFile(String filename)
        {
            var ex = await PathExt.TryDeleteFileAsync(filename).ConfigureAwait(false);
            if (ex != null)
                throw ex;
        }

        /// <summary>
        /// A write only stream that forwards writes to another stream, throwing an IOException (and setting Exceeded) if more than a given number of bytes are written (a copy of StaticFileRepo.MaxLengthWriteStream).
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
