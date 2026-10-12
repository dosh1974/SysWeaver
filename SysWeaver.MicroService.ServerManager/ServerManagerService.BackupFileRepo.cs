using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public sealed partial class ServerManagerService
    {
        sealed class BackupFileRepo : IFileRepo
        {
            readonly ServerManagerService Manager;

            public BackupFileRepo(String key, String discFolder, ServerManagerService manager, bool isKey = false)
            {
                Manager = manager;
                IsKey = isKey;
                Key = key;
                DiscFolder = discFolder;
                UploadAuth = isKey ? Roles.Admin : "";
                ValidExt = isKey ? ValidKeyExt : ValidConfigExt;
                InvalidSuffixes = isKey ? ReadOnlySet<String>.Empty : InvalidConfigSuffixes;
            }
            readonly bool IsKey;
            readonly String DiscFolder;

            public string Key { get; init; }

            public IReadOnlyList<FileHttpServerModuleFolder> ExposeFolders => null;

            readonly IReadOnlySet<String> InvalidSuffixes;
            readonly IReadOnlySet<String> ValidExt;

            public string UploadAuth { get; init; }

            /// <summary>
            /// Get the full name of an uploaded file, null if the name isn't a plain file name in the disc folder (a folder part, "..", an absolute path etc)
            /// </summary>
            String GetDest(String name)
            {
                if (!IsPlainFileName(name))
                    return null;
                //  Also verify the normalized path (ex: trailing dots and spaces are removed on Windows)
                var folder = Path.GetFullPath(DiscFolder);
                var dest = Path.GetFullPath(Path.Combine(folder, name));
                var dir = Path.GetDirectoryName(dest);
                if ((dir == null) || !dir.TrimEnd(Path.DirectorySeparatorChar).Equals(folder.TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    return null;
                return dest;
            }

            static readonly FileUploadResult InvalidFileName = new FileUploadResult(FileUploadStatus.InvalidFileName);

            async Task<FileUploadResult> CheckFile(FileUploadInfo file)
            {
                //  Never use the name before it's verified (optpass-67: "..\\x.json" or an absolute name wrote outside the folder)
                var dest = GetDest(file.Name);
                if (dest == null)
                    return InvalidFileName;
                if (File.Exists(dest))
                {
                    var h = await FileHash.GetHashAsync(dest).ConfigureAwait(false);
                    h = HashTools.ToHexHash(h);
                    if (h.FastEquals(file.Hash))
                        return FileUploadResult.AlreadyUploaded;
                }
                if (!ValidExt.Contains(file.GetExtension().FastToLower()))
                    return FileUploadResult.RefuseExtension;
                var lfile = file.Name.FastToLower();
                foreach (var x in InvalidSuffixes)
                    if (lfile.FastEndsWith(x))
                        return FileUploadResult.Refuse;
                if (file.Length > (64 << 10))
                    return FileUploadResult.RefuseSize;
                return FileUploadResult.Upload;
            }

            public Task<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
            {
                if (!IsKey)
                {
                    try
                    {
                        Key.SplitFirst('_', out var serviceName);
                        Manager.Validate(serviceName, r);
                    }
                    catch
                    {
                        return Task.FromResult(ArrayExt.Create(info.Length, FileUploadResult.NotAuthorized));
                    }
                }
                return info.ConvertAsync(CheckFile);
            }

            public async Task<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
            {
                if (!IsKey)
                {
                    try
                    {
                        Key.SplitFirst('_', out var serviceName);
                        Manager.Validate(serviceName, r);
                    }
                    catch
                    {
                        return FileUploadResult.NotAuthorized;
                    }
                }
                var res = await CheckFile(file).ConfigureAwait(false);
                if (res.Result != FileUploadStatus.Upload)
                    return res;
                var dest = GetDest(file.Name);

                var a = Manager.Audit;
                HttpApiAudit ad = null;
                long id = 0;
                if (a != null)
                {
                    id = ApiAudit.GetId();
                    ad = new HttpApiAudit(String.Concat("Upload ", Key, '/', file.Name), AuditGroup);
                    a.OnApiBegin(id, r, ad, file.Hash);
                }
                try
                {
                    //  Never read more than the announced (and accepted) length, decompressed if the data is compressed
                    var length = (int)Math.Max(file.Length, 0);
                    String text;
                    var buffer = ArrayPool<Byte>.Shared.Rent(length + 1);
                    try
                    {
                        var used = await ReadLimited(s, decoder, buffer, length).ConfigureAwait(false);
                        if (used < 0)
                        {
                            if (a != null)
                                a.OnApiEnd(id, r, ad, "Refused, the data is larger than the announced length");
                            return FileUploadResult.RefuseSize;
                        }
                        text = Encoding.UTF8.GetString(buffer, 0, used);
                    }
                    finally
                    {
                        ArrayPool<Byte>.Shared.Return(buffer);
                    }
                    await Manager.SaveWithBackup(dest, text, true).ConfigureAwait(false);
                    if (a != null)
                        a.OnApiEnd(id, r, ad, IsKey ? "** PROTECTED **" : text.LimitLength(2048));
                    Manager.Syncer.GetManagedFolders(Key);
                    r.Session.InvalidateCache();
                    r.Server.InvalidateCache();
                    return FileUploadResult.None;
                }
                catch (Exception ex)
                {
                    if (a != null)
                        a.OnApiException(id, r, ad, ex);
                    throw;
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
            /// A write only stream that writes to a byte array, throwing an IOException (and setting Exceeded) if more than a given number of bytes are written (a copy of the one in CustomUserImageService).
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
        }

    }

}
