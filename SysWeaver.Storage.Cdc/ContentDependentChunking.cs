using CommunityToolkit.HighPerformance;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver
{


    public static class ContentDependentChunking
    {
        public static readonly ReadOnlyMemory<Byte> FileHeader = new Byte[] { (Byte)'S', (Byte)'W', (Byte)'C', (Byte)'0' };


        public static readonly ReadOnlyMemory<Byte> MissingChunk = new Byte[] 
        { (Byte)' ', (Byte)'M', (Byte)'I', (Byte)'S', (Byte)'S', (Byte)'I', (Byte)'N', (Byte)'G', (Byte)' ', (Byte)'C', (Byte)'H', (Byte)'U', (Byte)'N', (Byte)'K', (Byte)' ' };


        public const String FileExt = "swcompact";
        public const String DotFileExt = "." + FileExt;


        static void WriteVar(Stream s, ulong i)
        {
            for (; ; )
            {
                if (i < 128)
                {
                    s.WriteByte((byte)i);
                    return;
                }
                s.WriteByte((byte)(0x80 | (i & 0x7f)));
                i >>= 7;
            }
        }

        public static bool TryReadVar(Stream s, out ulong res)
        {
            res = 0;
            int shift = 0;
            for (; ; )
            {
                int b = s.ReadByte();
                if (b < 0)
                {
                    if (shift == 0)
                        return false;
                    throw new Exception("Unexpected end of file!");
                }
                ulong d = (ulong)(b & 0x7f);
                d <<= shift;
                shift += 7;
                res |= d;
                if ((b & 0x80) == 0)
                    return true;
            }
        }

        static ulong ReadVar(Stream s)
        {
            ulong res = 0;
            int shift = 0;
            for (; ; )
            {
                int b = s.ReadByte();
                if (b < 0)
                    throw new Exception("Unexpected end of file!");
                ulong d = (ulong)(b & 0x7f);
                d <<= shift;
                shift += 7;
                res |= d;
                if ((b & 0x80) == 0)
                    return res;
            }
        }


        static async ValueTask WriteHeader(Stream s, long minTime, ReadOnlyMemory<Byte>? fileNames = null)
        {
            await s.WriteAsync(FileHeader).ConfigureAwait(false);
            WriteVar(s, (ulong)minTime);
            if (fileNames == null)
                WriteVar(s, 0);
            else
            {
                var d = fileNames ?? ReadOnlyMemory<Byte>.Empty;
                WriteVar(s, (ulong)d.Length);
                await s.WriteAsync(d).ConfigureAwait(false);
            }
        }

        static ValueTask WriteFile(Stream s, FileInfo file, long minTime, Byte[] block, int hashSize)
        {
            WriteVar(s, (ulong)(file.CreationTimeUtc.Ticks - minTime));
            WriteVar(s, (ulong)(file.LastWriteTimeUtc.Ticks - minTime));
            WriteVar(s, (ulong)(file.LastAccessTimeUtc.Ticks - minTime));
            WriteVar(s, (ulong)file.Attributes);
            WriteVar(s, (ulong)(block.LongLength / hashSize));
            return s.WriteAsync(block);
        }

        static async ValueTask<CdcFileHeader> ReadHeader(Stream s)
        {
            var temp = GC.AllocateUninitializedArray<Byte>(4);
            if (await s.ReadAsync(temp).ConfigureAwait(false) != 4)
                throw new Exception("Unexpected end of file! (expected header)");
            if (!IsHeader(temp))
                throw new Exception("Invalid file type!");
            var minTime = (long)ReadVar(s);
            var dirSize = (long)ReadVar(s);
            String[] files = null;
            if (dirSize > 0)
            {
                var dir = GC.AllocateUninitializedArray<Byte>((int)dirSize);
                if (await s.ReadAsync(dir).ConfigureAwait(false) != dirSize)
                    throw new Exception("Unexpected end of file! (expected directory info)");
                files = DecodeFileArray(dir);
            }
            return new CdcFileHeader(minTime, files);
        }

        static async ValueTask<CdcFileData> ReadFile(Stream s, int hashSize, bool allowFail = false)
        {
            ulong cc;
            if (allowFail)
            {
                if (!TryReadVar(s, out cc))
                    return null;
            }
            else
            {
                cc = ReadVar(s);
            }
            var c = (long)cc;
            var w = (long)ReadVar(s);
            var a = (long)ReadVar(s);
            var attr = (int)ReadVar(s);
            var bsize = (long)ReadVar(s);
            bsize *= hashSize;
            var d = GC.AllocateUninitializedArray<Byte>((int)bsize);
            if (await s.ReadAsync(d).ConfigureAwait(false) != bsize)
                throw new Exception("Unexpected end of file! (expected block info)");
            return new CdcFileData(c, w, a, attr, d);
        }



        static bool IsHeader(ReadOnlySpan<Byte> t)
        {
            var h = FileHeader.Span;
            if (h[0] != t[0])
                return false;
            if (h[1] != t[1])
                return false;
            if (h[2] != t[2])
                return false;
            if (h[3] != t[3])
                return false;
            return true;
        }


        /// <summary>
        /// Compact a file by storing the chunk lists
        /// </summary>
        /// <param name="fileName">Name of the file to compact</param>
        /// <param name="destName">Optional destination filename, default is to name it the same as the file with an added .swcompact extension</param>
        /// <param name="props">The props used</param>
        /// <returns></returns>
        public static async ValueTask CompactFile(String fileName, String destName = null, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            destName = destName ?? (fileName + DotFileExt);
            var file = new FileInfo(fileName);
            long minTime = long.MaxValue;
            minTime = Math.Min(minTime, file.LastWriteTimeUtc.Ticks);
            minTime = Math.Min(minTime, file.LastAccessTimeUtc.Ticks);
            minTime = Math.Min(minTime, file.CreationTimeUtc.Ticks);
            //  Get chunks
            Byte[] block;
            using (var i = file.OpenRead())
                block = await Cut(i, false, props).ConfigureAwait(false);
            //  Write file
            var tempName = destName + ".temp";
            try
            {
                using (var s = new FileStream(tempName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await WriteHeader(s, minTime).ConfigureAwait(false);
                    await WriteFile(s, file, minTime, block, props.HashSize).ConfigureAwait(false);
                }
                File.Move(tempName, destName, true);
            }
            finally
            {
                await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Compact a folder into a single file by storing the chunk lists
        /// </summary>
        /// <param name="folderName">Name of the folder to compact</param>
        /// <param name="destName">Optional destination filename, default is to name it the same as the folder with an added .swcompact extension</param>
        /// <param name="props">The props used</param>
        /// <returns></returns>
        public static async ValueTask CompactFolder(String folderName, String destName = null, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            destName = destName ?? (folderName + DotFileExt);
            //  Get files
            var fi = new DirectoryInfo(folderName);
            var nl = fi.FullName.Length + 1;
            var files = fi.GetFiles("*", SearchOption.AllDirectories);
            var cmp = StringComparer.Ordinal;
            Array.Sort(files, (a, b) => cmp.Compare(a.FullName, b.FullName));
            var fl = files.Length;
            long minTime = long.MaxValue;
            files.Process(file =>
            {
                minTime = Math.Min(minTime, file.LastWriteTimeUtc.Ticks);
                minTime = Math.Min(minTime, file.LastAccessTimeUtc.Ticks);
                minTime = Math.Min(minTime, file.CreationTimeUtc.Ticks);
            });
            var shortName = files.Convert(x => x.FullName.Substring(nl));
            var headerArray = EncodeFileArray(shortName);
            //  Get file chunks
            var l = CreateLock();
            async ValueTask<Byte[]> DoOne(FileInfo file)
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                using var i = file.OpenRead();
                return await Cut(i, false,props).ConfigureAwait(false);
            }
            var blocks = await files.ConvertAsyncValue(DoOne).ConfigureAwait(false);

            //  Write file
            var tempName = destName + ".temp";
            try
            {
                using (var s = new FileStream(tempName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await WriteHeader(s, minTime, headerArray).ConfigureAwait(false);
                    var hashSize = props.HashSize;
                    for (int i = 0; i < fl; ++ i)
                        await WriteFile(s, files[i], minTime, blocks[i], hashSize).ConfigureAwait(false);
                }
                File.Move(tempName, destName, true);
            }
            finally
            {
                await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Compact a file or folder into a single file by storing the chunk lists
        /// </summary>
        /// <param name="path">Path to the file system object (file or folder)</param>
        /// <param name="destName">Optional destination filename, default is to name it the same as the file or folder with an added .swcompact extension</param>
        /// <param name="props">The props used</param>
        /// <returns></returns>
        public static ValueTask Compact(String path, String destName = null, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            return File.Exists(path) ? CompactFile(path, destName, props) : CompactFolder(path, destName, props);
        }


        /// <summary>
        /// Write a bunch of uncompressed chunks (original data) to a stream
        /// </summary>
        /// <param name="dest">Target stream</param>
        /// <param name="chunkHashes">Array of binary chunk hashes </param>
        /// <param name="props"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async ValueTask WriteChunks(Stream dest, ReadOnlyMemory<Byte> chunkHashes, CdcProps props = null)
        {
            var sl = chunkHashes.Length;
            props = props ?? CdcProps.Default;
            int hashSize = props.HashSize;
            for (int i = 0; i < sl; i += hashSize)
            {
                var str = chunkHashes.Span.Slice(i, hashSize).ToHexString();
                var l = await TryDecompressChunk(dest, str, props).ConfigureAwait(false);
                if (l <= 0)
                    throw new Exception("Failed to write chunk");
            }
        }


        /// <summary>
        /// Expand a .swcompact file into a file or folder 
        /// </summary>
        /// <param name="fileName">The .swcompact filename</param>
        /// <param name="destName">Optional destination name (folder or file), default is the same as fileName excluding the .swcompact extension</param>
        /// <param name="props">The props used</param>
        /// <returns></returns>
        public static async ValueTask<CdcChunkStats> Expand(String fileName, String destName = null, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            destName = destName ?? PathExt.StripExtension(fileName);
            int hashSize = props.HashSize;
            //  Read header
            var fi = new FileInfo(fileName);
            using var s = fi.OpenRead();
            var header = await ReadHeader(s).ConfigureAwait(false);
            var minTime = header.MinTime;
            var files = header.Files;
        //  Processing of a single file
            long fileCount = 0;
            long chunkCount = 0;
            long chunkCompSize = 0;
            long chunkSize = 0;
            ConcurrentDictionary<String, int> localUnique = new(StringComparer.Ordinal);
            ConcurrentDictionary<String, CdcChunkFileStats> fileData = new(StringComparer.Ordinal);

            var l = CreateLock();
            async ValueTask DoOne(String file, CdcFileData data)
            {
                Interlocked.Increment(ref fileCount);
                using var _ = await l.Lock().ConfigureAwait(false);
                await PathExt.EnsureFolderExistAsync(Path.GetDirectoryName(file)).ConfigureAwait(false);
                var chunks = data.Chunks;
                try
                {
                    long fileChunkCount = 0;
                    long fileCompSize = 0;
                    long fileSize = 0;
                    using (var d = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var sl = chunks.Length;
                        for (int i = 0; i < sl; i += hashSize)
                        {
                            var str = chunks.Span.Slice(i, hashSize).ToHexString();
                            ++chunkCount;
                            ++fileChunkCount;
                            localUnique.TryAdd(str, 0);
                            var p = d.Position;
                            var l = await TryDecompressChunk(d, str, props).ConfigureAwait(false);
                            if (l <= 0)
                                throw new Exception("Failed to write chunk to \"" + file + "\"");
                            var dl = d.Position - p;
                            fileSize += dl;
                            fileCompSize += l;
                            Interlocked.Add(ref chunkSize, dl);
                            Interlocked.Add(ref chunkCompSize, l);
                        }
                    }
                    data.SetFileInfo(file, minTime);
                    fileData.TryAdd(file, new CdcChunkFileStats(file, fileChunkCount, fileCompSize, 0, fileSize));
                }
                catch
                {
                    await PathExt.TryDeleteFileAsync(file).ConfigureAwait(false);
                    throw;
                }
            }
            if (files != null)
            {
                //  Folder, process files in parallel
                var fl = files.Length;
                ValueTask[] tasks = new ValueTask[fl];
                for (int i = 0; i < fl; ++ i)
                    tasks[i] = DoOne(Path.Combine(destName, files[i]), await ReadFile(s, hashSize).ConfigureAwait(false));
                await TaskExt.WhenAll(tasks).ConfigureAwait(false);
            }
            else
            {
                //  File or empty Folder
                var d = await ReadFile(s, hashSize, true).ConfigureAwait(false);
                if (d != null)
                    await DoOne(destName, d).ConfigureAwait(false);
                else
                    await PathExt.EnsureFolderExistAsync(destName).ConfigureAwait(false);
            }
            return new CdcChunkStats(
                fi.Length,
                fileCount,
                chunkCount,
                0,
                chunkCompSize,
                chunkSize,
                null,
                null,
                localUnique.Keys.ToList(),
                fileData.Values.ToList()
                );
        }



        /// <summary>
        /// Expand a .swcompact file into a file or folder, replace missing chunks with "MISSING CHUNK".
        /// </summary>
        /// <param name="fileName">The .swcompact filename</param>
        /// <param name="destName">Optional destination name (folder or file), default is the same as fileName excluding the .swcompact extension</param>
        /// <param name="props">The props used</param>
        /// <returns>Stats</returns>
        public static async ValueTask<CdcChunkStats> Recover(String fileName, String destName = null, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            destName = destName ?? PathExt.StripExtension(fileName);
            int hashSize = props.HashSize;

            //  Read header
            var fi = new FileInfo(fileName);
            using var s = fi.OpenRead();
            var header = await ReadHeader(s).ConfigureAwait(false);
            var minTime = header.MinTime;
            var files = header.Files;


            //  Processing of a single file
            long fileCount = 0;
            long missingCount = 0;
            long chunkCount = 0;
            long chunkCompSize = 0;
            long chunkSize = 0;
            ConcurrentDictionary<String, int> localUnique = new(StringComparer.Ordinal);
            ConcurrentDictionary<String, int> missingChunks = new(StringComparer.Ordinal);
            ConcurrentDictionary<String, int> missingFiles = new(StringComparer.Ordinal);
            ConcurrentDictionary<String, CdcChunkFileStats> fileData = new(StringComparer.Ordinal);
            var l = CreateLock();
            var ms = MissingChunk;

            async ValueTask DoOne(String file, CdcFileData data)
            {
                Interlocked.Increment(ref fileCount);
                using var _ = await l.Lock().ConfigureAwait(false);
                await PathExt.EnsureFolderExistAsync(Path.GetDirectoryName(file)).ConfigureAwait(false);
                var chunks = data.Chunks;
                var sl = chunks.Length;
                try
                {
                    long fileChunkCount = 0;
                    long fileMissingCount = 0;
                    long fileCompSize = 0;
                    long fileSize = 0;
                    using (var d = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        for (int i = 0; i < sl; i += hashSize)
                        {
                            var str = chunks.Span.Slice(i, hashSize).ToHexString();
                            ++chunkCount;
                            ++fileChunkCount;
                            localUnique.TryAdd(str, 0);
                            var p = d.Position;
                            var l = await TryDecompressChunk(d, str, props).ConfigureAwait(false);
                            if (l <= 0)
                            {
                                Interlocked.Increment(ref missingCount);
                                missingChunks.TryAdd(str, 0);
                                missingFiles.TryAdd(file, 0);
                                var x = Encoding.UTF8.GetBytes(str);
                                d.WriteByte((Byte)'<');
                                await d.WriteAsync(ms).ConfigureAwait(false);
                                await d.WriteAsync(x).ConfigureAwait(false);
                                await d.WriteAsync(ms).ConfigureAwait(false);
                                d.WriteByte((Byte)'>');
                            }
                            else
                            {
                                var dl = d.Position - p;
                                fileSize += dl;
                                fileCompSize += l;
                                Interlocked.Add(ref chunkSize, dl);
                                Interlocked.Add(ref chunkCompSize, l);
                            }
                        }
                    }
                    data.SetFileInfo(file, minTime);
                    fileData.TryAdd(file, new CdcChunkFileStats(file, fileChunkCount, fileCompSize, fileMissingCount, fileSize));
                }
                catch
                {
                    await PathExt.TryDeleteFileAsync(file).ConfigureAwait(false);
                    throw;
                }
            }

            if (files != null)
            {
                //  Folder, process files in parallel
                var fl = files.Length;
                ValueTask[] tasks = new ValueTask[fl];
                for (int i = 0; i < fl; ++i)
                    tasks[i] = DoOne(Path.Combine(destName, files[i]), await ReadFile(s, hashSize).ConfigureAwait(false));
                await TaskExt.WhenAll(tasks).ConfigureAwait(false);
            }
            else
            {
                //  File or empty Folder
                var d = await ReadFile(s, hashSize, true).ConfigureAwait(false);
                if (d != null)
                    await DoOne(destName, d).ConfigureAwait(false);
                else
                    await PathExt.EnsureFolderExistAsync(destName).ConfigureAwait(false);
            }
            return new CdcChunkStats(
                fi.Length,
                fileCount,
                chunkCount,
                missingCount,
                chunkCompSize,
                chunkSize,
                missingChunks.Keys.ToList(),
                missingFiles.Keys.ToList(),
                localUnique.Keys.ToList(),
                fileData.Values.ToList()
                );
        }



        /// <summary>
        /// Find missing chunks in a .swcompact file 
        /// </summary>
        /// <param name="fileName">The .swcompact filename</param>
        /// <param name="props">The props used</param>
        /// <param name="touch">If true, mark exsiting chunks as used</param>
        /// <param name="getExpandedSize">If true, decompress the chunks to get the expanded size.
        /// WARNING! This is much slower!</param>
        /// <returns>Stats</returns>
        public static async ValueTask<CdcChunkStats> Verify(String fileName, CdcProps props = null, bool touch = false, bool getExpandedSize = false)
        {
            props = props ?? CdcProps.Default;
            var destName = PathExt.StripExtension(fileName);
            int hashSize = props.HashSize;

            //  Read header
            var fi = new FileInfo(fileName);
            using var s = fi.OpenRead();
            var header = await ReadHeader(s).ConfigureAwait(false);
            var minTime = header.MinTime;
            var files = header.Files;

            //  Processing of a single file
            long fileCount = 0;
            long missingCount = 0;
            long chunkCount = 0;
            long chunkCompSize = 0;
            long chunkSize = 0;
            HashSet<String> localUnique = new (StringComparer.Ordinal);
            HashSet<String> missingChunks = new (StringComparer.Ordinal);
            HashSet<String> missingFiles = new (StringComparer.Ordinal);
            Dictionary<String, CdcChunkFileStats> fileData = new (StringComparer.Ordinal);


            void DoOne(String file, CdcFileData data)
            {
                ++fileCount;
                var chunks = data.Chunks;
                var sl = chunks.Length;
                long fileChunkCount = 0;
                long fileMissingCount = 0;
                long fileCompSize = 0;
                long fileSize = 0;
                for (int i = 0; i < sl; i += hashSize)
                {
                    var str = chunks.Span.Slice(i, hashSize).ToHexString();
                    ++chunkCount;
                    ++fileChunkCount;
                    localUnique.Add(str);
                    var l = GetChunkSize(out var dl, str, props, touch, getExpandedSize);
                    if (l > 0)
                    {
                        fileSize += dl;
                        fileCompSize += l;
                        chunkSize += dl;
                        chunkCompSize += l;
                    }
                    else
                    {
                        ++fileMissingCount;
                        ++missingCount;
                        missingChunks.Add(str);
                        missingFiles.Add(file);
                    }
                }
                fileData[file] = new CdcChunkFileStats(file, fileChunkCount, fileCompSize, fileMissingCount, fileSize);
            }

            

            if (files != null)
            {
                //  Folder
                foreach (var f in files)
                    DoOne(Path.Combine(destName, f), await ReadFile(s, hashSize).ConfigureAwait(false));
            }
            else
            {
                //  File or empty Folder
                var d = await ReadFile(s, hashSize, true).ConfigureAwait(false);
                if (d != null)
                    DoOne(destName, d);
            }
            return new CdcChunkStats(
                fi.Length,
                fileCount, 
                chunkCount, 
                missingCount, 
                chunkCompSize, 
                chunkSize,
                missingChunks, 
                missingFiles, 
                localUnique, 
                fileData.Values.ToList());
        }


        static readonly ICompType HeaderComp = CompManager.GetFromHttp("br");
        
        
        static String[] DecodeFileArray(ReadOnlySpan<Byte> data)
        {
            using var dMem = HeaderComp.GetUnmanagedDecompressed(data);
            var d = dMem.Memory;
            var str = Encoding.UTF8.GetString(d.Span);
            var files = str.Split('\t');
            var l = files.Length;
            if (l > 1)
            {
                var prev = files[0];
                for (int i = 1; i < l; ++ i)
                {
                    var s = files[i];
                    var cl = s[0] - ' ';
                    prev = prev.Substring(0, cl) + s.Substring(1);
                    files[i] = prev;
                }
            }
            return files;
        }

        static ReadOnlyMemory<Byte> EncodeFileArray(String[] files)
        {
            //var org = HeaderComp.GetCompressed(Encoding.UTF8.GetBytes(String.Join('\t', files)), CompEncoderLevels.Best);  
            var l = files.Length;
            if (l > 1)
            {
                const int maxEqualCount = 127 - 32;
                var prev = files[0];
                for (int i = 1; i < l; ++i)
                {
                    var d = files[i];
                    var sl = prev.Length;
                    var nl = d.Length;
                    if (nl < sl)
                        sl = nl;
                    if (maxEqualCount < sl)
                        sl = maxEqualCount;
                    int e;
                    for (e = 0; e < sl; ++e)
                    {
                        if (d[e] != prev[e])
                            break;
                    }
                    prev = d;
                    files[i] = String.Concat((Char)(32 + e), d.Substring(e));
                }
            }
            var data = Encoding.UTF8.GetBytes(String.Join('\t', files));
            return HeaderComp.GetCompressed(data, CompEncoderLevels.Best);
        }


        /// <summary>
        /// Get the filename where a the compressed data of a chunk is stored
        /// </summary>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="props">The props used</param>
        /// <returns>A filename or null if the chunk isn't stored</returns>
        public static String TryGetChunkFile(String hashStr, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var fileName = GetFilename(hashStr, props);
            var fi = new FileInfo(fileName);
            return (fi.Exists && (fi.Length > 0)) ? fi.FullName : null;
        }

        /// <summary>
        /// Get the filename where a the compressed data of a chunk is stored
        /// </summary>
        /// <param name="hash">The hash of the chunk as binary data</param>
        /// <param name="props">The props used</param>
        /// <returns>A filename or null if the chunk isn't stored</returns>
        public static String TryGetChunkFile(ReadOnlySpan<Byte> hash, CdcProps props = null)
            => TryGetChunkFile(hash.ToHexString(), props);

        /// <summary>
        /// Open a stream to the compressed chunk data
        /// </summary>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="props">The props used</param>
        /// <returns>A stream to the data or null if the chunk isn't stored</returns>
        public static Stream TryOpenCompressedChunk(String hashStr, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var fileName = GetFilename(hashStr, props);
            var fi = new FileInfo(fileName);
            return (fi.Exists && (fi.Length > 0)) ? fi.OpenRead() : null;
        }

        /// <summary>
        /// Open a stream to the compressed chunk data
        /// </summary>
        /// <param name="hash">The hash of the chunk as binary data</param>
        /// <param name="props">The props used</param>
        /// <returns>A stream to the data or null if the chunk isn't stored</returns>
        public static Stream TryOpenCompressedChunk(ReadOnlySpan<Byte> hash, CdcProps props = null)
            => TryOpenCompressedChunk(hash.ToHexString(), props);



        /// <summary>
        /// Validate that a chunk exist
        /// </summary>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="props">The props used</param>
        /// <param name="touch">If true, mark exsiting chunk as used</param>
        /// <returns>True if the chunk exist</returns>
        public static bool ValidateChunk(String hashStr, CdcProps props = null, bool touch = false)
        {
            props = props ?? CdcProps.Default;
            var fileName = GetFilename(hashStr, props);
            var fi = new FileInfo(fileName);
            if (!fi.Exists)
                return false;
            if (fi.Length <= 0)
                return false;
            if (touch)
            {
                try
                {
                    fi.LastAccessTimeUtc = DateTime.UtcNow;
                }
                catch
                {
                }
            }
            return true;
        }

        /// <summary>
        /// Get the compressed size of a chunk
        /// </summary>
        /// <param name="expandedSize">The decompressed size of the chunk if getDecompressedSize = true</param>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="props">The props used</param>
        /// <param name="touch">If true, mark exsiting chunk as used</param>
        /// <param name="getExpandedSize">If true, decompress the chunks to get the expanded size.
        /// WARNING! This is much slower!</param>
        /// <returns>0 if the chunk doesn't exist, else the compressed length</returns>
        public static long GetChunkSize(out long expandedSize, String hashStr, CdcProps props = null, bool touch = false, bool getExpandedSize = false)
        {
            expandedSize = 0;
            props = props ?? CdcProps.Default;
            var fileName = GetFilename(hashStr, props);
            var fi = new FileInfo(fileName);
            if (!fi.Exists)
                return 0;
            var l = fi.Length;
            if (l <= 0)
                return 0;
            if (touch)
            {
                try
                {
                    fi.LastAccessTimeUtc = DateTime.UtcNow;
                }
                catch
                {
                }
            }
            if (getExpandedSize)
            {
                using var d = FileReadOnlyMemory.Read(fi.FullName);
                if (d != null)
                {
                    using var dMem = props.Comp.GetUnmanagedDecompressed(d.Memory.Span);
                    expandedSize = dMem.Memory.Length;
                }
            }
            return l;
        }


        /// <summary>
        /// Validate that a chunk exist
        /// </summary>
        /// <param name="hash">The hash of the chunk as binary data</param>
        /// <param name="props">The props used</param>
        /// <param name="touch">If true, mark exsiting chunk as used</param>
        /// <returns>True if the chunk exist</returns>
        public static bool ValidateChunk(ReadOnlySpan<Byte> hash, CdcProps props = null, bool touch = false)
            => ValidateChunk(hash.ToHexString(), props, touch);



        /// <summary>
        /// Try to copy a compressed chunk to a stream
        /// </summary>
        /// <param name="dest">The destination stream</param>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="props">The props used</param>
        /// <returns>True if the chunk was copied, false if the chunk didn't exist in the storage</returns>
        public static async ValueTask<bool> TryCopyCompressedChunk(Stream dest, String hashStr, CdcProps props = null)
        {
            using var s = TryOpenCompressedChunk(hashStr, props);
            if (s == null)
                return false;
            await dest.CopyToAsync(dest).ConfigureAwait(false);   
            return true;
        }

        /// <summary>
        /// Try to copy a compressed chunk to a stream
        /// </summary>
        /// <param name="dest">The destination stream</param>
        /// <param name="hash">The hash of the chunk as binary data</param>
        /// <param name="props">The props used</param>
        /// <returns>True if the chunk was copied, false if the chunk didn't exist in the storage</returns>
        public static ValueTask<bool> TryCopyCompressedChunk(Stream dest, ReadOnlySpan<Byte> hash, CdcProps props = null) =>
            TryCopyCompressedChunk(dest, hash.ToHexString(), props);


        /// <summary>
        /// Try to copy the decompressed data of a chunk to a stream
        /// </summary>
        /// <param name="dest">The destination stream</param>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="props">The props used</param>
        /// <returns>Zero if the chunk didn't exist in the storage, else the length of the compressed chunk</returns>
        public static async ValueTask<long> TryDecompressChunk(Stream dest, String hashStr, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            ReadOnlyMemory<Byte> mem;
            long l;
            using (var s = TryOpenCompressedChunk(hashStr, props))
            {
                if (s == null)
                    return 0;
                mem = await props.Comp.GetDecompressedAsync(s).ConfigureAwait(false);
                l = s.Position;
            }
            await dest.WriteAsync(mem).ConfigureAwait(false);
            return l;
        }

        /// <summary>
        /// Try to copy the decompressed data of a chunk to a stream
        /// </summary>
        /// <param name="dest">The destination stream</param>
        /// <param name="hash">The hash of the chunk as binary data</param>
        /// <param name="props">The props used</param>
        /// <returns>Zero if the chunk didn't exist in the storage, else the length of the compressed chunk</returns>
        public static ValueTask<long> TryDecompressChunk(Stream dest, ReadOnlySpan<Byte> hash, CdcProps props = null) =>
            TryDecompressChunk(dest, hash.ToHexString(), props);


        const long ClusterSize = 4096;
        const long ClusterRound = ClusterSize - 1;
        const long ClusterMask = ~(ClusterSize - 1);


        static AsyncLock CreateLock()
            => new AsyncLock(Math.Max(1, Environment.ProcessorCount - 1));


        static async ValueTask<CdcFolderStats> InternalFolderStats(String folder, AsyncLock l, bool getUncompressedStats, CdcProps props, DateTime oldIfUnusedSince)
        {
            var comp = props.Comp;
            var fileExt = ".bin." + props.CompFileExt;
            long count = 0;
            long compSize = 0;
            long discSize = 0;

            long unCompSize = 0;
            long otherSize = 0;
            long otherCount = 0;

            long oldCount = 0;
            long oldDiscSize = 0;

            String[] files;
            using (var _ = await l.Lock().ConfigureAwait(false))
                files = Directory.GetFiles(folder);
            await files.ProcessAsyncValue(async file =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                var fi = new FileInfo(file);
                var size = fi.Length;
                var estSize = (size + ClusterRound) & ClusterMask;
                Interlocked.Add(ref discSize, estSize);
                bool isChunk = file.FastEndsWith(fileExt) && (size > 0);
                if ((fi.LastAccessTimeUtc < oldIfUnusedSince) || (!isChunk))
                {
                    Interlocked.Increment(ref oldCount);
                    Interlocked.Add(ref oldDiscSize, estSize);
                }
                if (isChunk)
                {
                    Interlocked.Increment(ref count);
                    Interlocked.Add(ref compSize, size);
                    if (getUncompressedStats)
                    {
                        using var mem = await FileReadOnlyMemory.ReadAsync(file).ConfigureAwait(false);
                        using var dMem = comp.GetUnmanagedDecompressed(mem.Memory.Span);
                        long uncompSize = dMem.Memory.Length;
                        Interlocked.Add(ref unCompSize, uncompSize);
                    }
                }
                else
                {
                    Interlocked.Increment(ref otherCount);
                    Interlocked.Add(ref otherSize, size);
                }
            }).ConfigureAwait(false);
            return new CdcFolderStats(folder, discSize, count, compSize, unCompSize, otherCount, otherSize, oldIfUnusedSince, oldDiscSize, oldCount);
        }


        static async ValueTask<CdcPruneStats> InternalPrune(String folder, AsyncLock l, CdcProps props, DateTime oldIfUnusedSince)
        {
            var fileExt = ".bin." + props.CompFileExt;

            long count = 0;
            long discSize = 0;

            long oldErrors = 0;
            long oldCount = 0;
            long oldDiscSize = 0;

            String err = null;

            String[] files;
            using (var _ = await l.Lock().ConfigureAwait(false))
                files = Directory.GetFiles(folder);
            await files.ProcessAsyncValue(async file =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                var fi = new FileInfo(file);
                var size = fi.Length;
                var estSize = (size + ClusterRound) & ClusterMask;
                Interlocked.Increment(ref count);
                Interlocked.Add(ref discSize, estSize);
                bool isChunk = file.FastEndsWith(fileExt) && (size > 0);
                if (isChunk && (fi.LastAccessTimeUtc >= oldIfUnusedSince))
                    return;
                var ex = await PathExt.TryDeleteFileAsync(fi.FullName).ConfigureAwait(false);
                if (ex != null)
                {
                    Interlocked.Increment(ref oldErrors);
                    Interlocked.CompareExchange(ref err, ex.Message, null);
                    return;
                }
                Interlocked.Increment(ref oldCount);
                Interlocked.Add(ref oldDiscSize, estSize);
            }).ConfigureAwait(false);
            return new CdcPruneStats(folder, oldIfUnusedSince, discSize, count, oldDiscSize, oldCount, oldErrors, err);
        }


        /// <summary>
        /// Get stats about the chunked data storage folders
        /// </summary>
        /// <param name="getUncompressedStats">If true, all chunks are decompressed to get their raw size, this is slow</param>
        /// <param name="oldIfUnusedSince">If a chunk haven't been used since this UTC time, it's considered old</param>
        /// <param name="props">The props used</param>
        /// <returns>Array of folder statistics</returns>
        public static ValueTask<CdcFolderStats[]> GetFolderStats(bool getUncompressedStats = false, DateTime? oldIfUnusedSince = null, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var folders = props.ChunkFolders;
            var l = CreateLock();
            var o = oldIfUnusedSince == null ? DateTime.UtcNow.AddDays(-400).ToStartOfDay(12) : (oldIfUnusedSince ?? DateTime.UtcNow).ToStartOfMinute();
            return folders.ConvertAsyncValue(folder => InternalFolderStats(folder, l, getUncompressedStats, props, o));
        }

        /// <summary>
        /// Prune (delete) old chunks. WARNING! Chunks will be permanently removed!
        /// </summary>
        /// <param name="props">The props used</param>
        /// <param name="oldIfUnusedSince">If a chunk haven't been used since this UTC time, it's considered old and will be REMOVED permanently!</param>
        /// <returns>Array of folder statistics</returns>
        public static ValueTask<CdcPruneStats[]> Prune(DateTime? oldIfUnusedSince = null, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var folders = props.ChunkFolders;
            var l = CreateLock();
            var o = oldIfUnusedSince == null ? DateTime.UtcNow.AddDays(-400).ToStartOfDay(12) : (oldIfUnusedSince ?? DateTime.UtcNow).ToStartOfMinute();
            return folders.ConvertAsyncValue(folder => InternalPrune(folder, l, props, o));
        }

        /// <summary>
        /// Get the storage filename for a given hash
        /// </summary>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="props">The props used</param>
        /// <returns></returns>
        public static String GetFilename(String hashStr, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var folder = Folders.SelectFolder(props.ChunkFolders, hashStr);
            var filename = String.Concat(hashStr, ".bin.", props.CompFileExt);
            return Path.Combine(folder, filename);
        }

        /// <summary>
        /// Save some data as a compressed chunk
        /// </summary>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="data">The data</param>
        /// <param name="props">The props used</param>
        /// <returns>True if the chunk already exist in the storage or if it was saved successfully, false for any failure</returns>
        public static async ValueTask<bool> TrySaveDataAsChunk(String hashStr, ReadOnlyMemory<Byte> data, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var fileName = GetFilename(hashStr, props);
            var fi = new FileInfo(fileName);
            if (fi.Exists && (fi.Length > 0))
            {
                fi.LastAccessTimeUtc = DateTime.UtcNow;
                return true;
            }
            using var s = await SystemLock.GetAsync("ContentChunks" + hashStr).ConfigureAwait(false);
            fi = new FileInfo(fileName);
            if (fi.Exists && (fi.Length > 0))
            {
                fi.LastAccessTimeUtc = DateTime.UtcNow;
                return true;
            }
            var ex = await PathExt.EnsureCanWriteFileAsync(fileName).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            var comp = props.Comp.GetCompressed(data.Span, CompEncoderLevels.Balanced);
            var tempName = fileName + ".temp" + DateTime.UtcNow.Ticks;
            try
            {

                await comp.WriteToFileAsync(tempName).ConfigureAwait(false);
                File.Move(tempName, fileName, true);
            }
            catch
            {
                return false;               
            }
            finally
            {
                await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
            }
            return true;
        }



        /// <summary>
        /// Save a chunk to disc
        /// </summary>
        /// <param name="hashStr">The hexadecimal value of the hash for the chunk</param>
        /// <param name="data">The already compressed chunk data</param>
        /// <param name="props">The props used</param>
        /// <returns>True if the chunk already exist in the storage or if it was saved successfully, false for any failure</returns>
        static async ValueTask<bool> TrySaveChunk(String hashStr, ReadOnlyMemory<Byte> data, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var fileName = GetFilename(hashStr, props);
            var fi = new FileInfo(fileName);
            if (fi.Exists && (fi.Length > 0))
            {
                fi.LastAccessTimeUtc = DateTime.UtcNow;
                return true;
            }
            using var s = await SystemLock.GetAsync("ContentChunks" + hashStr).ConfigureAwait(false);
            fi = new FileInfo(fileName);
            if (fi.Exists && (fi.Length > 0))
            {
                fi.LastAccessTimeUtc = DateTime.UtcNow;
                return true;
            }
            var ex = await PathExt.EnsureCanWriteFileAsync(fileName).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            var tempName = fileName + ".temp" + DateTime.UtcNow.Ticks;
            try
            {
                await data.WriteToFileAsync(tempName).ConfigureAwait(false);
                File.Move(tempName, fileName, true);
            }
            catch
            {
                return false;
            }
            finally
            {
                await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
            }
            return true;
        }


        /// <summary>
        /// Save some data as a compressed chunk
        /// </summary>
        /// <param name="hash">The hash of the chunk as binary data</param>
        /// <param name="data">The data</param>
        /// <param name="props">The props used</param>
        /// <returns>True if the chunk already exist in the storage or if it was saved successfully, false for any failure</returns>
        public static ValueTask<bool> TrySaveChunk(ReadOnlySpan<Byte> hash, ReadOnlyMemory<Byte> data, CdcProps props = null)
            => TrySaveDataAsChunk(hash.ToHexString(), data, props);

        /// <summary>
        /// Compute the hash for a chunk of memory and save it to storage if it doesn't exist
        /// </summary>
        /// <param name="hash">Destination for the hash</param>
        /// <param name="data">The data</param>
        /// <param name="props">The props used</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async ValueTask CreateChunk(Memory<Byte> hash, ReadOnlyMemory<Byte> data, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            if (!props.Hash(data.Span, hash.Span, out var _hs))
                throw new Exception("Failed to hash!");
            if (!await TrySaveChunk(hash.Span, data, props).ConfigureAwait(false))
                throw new Exception("Failed to save chunk data!");
        }

        /// <summary>
        /// Compute the hash for a chunk of memory 
        /// </summary>
        /// <param name="hash">Destination for the hash</param>
        /// <param name="data">The data</param>
        /// <param name="props">The props used</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static void HashChunk(Memory<Byte> hash, ReadOnlyMemory<Byte> data, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            if (!props.Hash(data.Span, hash.Span, out var _hs))
                throw new Exception("Failed to hash!");
        }


        /// <summary>
        /// Cut a file into chunks, return the chunk hashes as binary data.
        /// This data is cached for 5 minutes.
        /// </summary>
        /// <param name="filename">The file</param>
        /// <param name="props">The properties, if null the default properties will be used (recommended)</param>
        /// <param name="cache">If true, data is cached</param>
        /// <returns></returns>
        public static async ValueTask<Byte[]> Cut(String filename, CdcProps props = null, bool cache = true)
        {
            props = props ?? CdcProps.Default;
            if (!cache)
            {
                using var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                return await Cut(s, false, props).ConfigureAwait(false);
            }
            var hash = await FileHash.GetHashAsync(filename).ConfigureAwait(false);
            if (hash == null)
                throw new Exception("File doesn't exist!");
            var key = hash + props.Key;
            return await CutCache.GetOrUpdateValueAsync(key, async _ =>
            {
                using var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                return await Cut(s, false, props).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        static readonly FastMemCache<String, Byte[]> CutCache = new(TimeSpan.FromMinutes(5), StringComparer.Ordinal);


        /// <summary>
        /// Cut a stream into chunks, return the chunk hashes as binary data
        /// </summary>
        /// <param name="s">The stream (will not be closed)</param>
        /// <param name="preview">If true, no chunk data is created</param>
        /// <param name="props">The properties, if null the default properties will be used (recommended)</param>
        /// <returns></returns>
        public static async ValueTask<Byte[]> Cut(Stream s, bool preview = false, CdcProps props = null)
        {
            //  Get parameters
            props = props ?? CdcProps.Default;
            var minSize = props.MinSize;
            var maxSize = props.MaxSize;
            var hashSize = props.HashSize;
            //  Read initial data and setup buffer "pointers"
            var bufSize = maxSize << 3; // Typically 1MB
            var data = ArrayPoolStream.Rent(bufSize);
            try
            {
                bufSize = data.Length;
                var dataSize = await s.ReadAsync(data).ConfigureAwait(false);
                if (dataSize <= 0)
                    return Array.Empty<Byte>();
                bool moreData = dataSize >= bufSize;
                int offset = 0;
                //  Setup hash data
                int hashGrow = (dataSize + dataSize) / props.AverageSize;
                if (hashGrow < 2)
                    hashGrow = 2;
                hashGrow *= hashSize;
                Byte[] hashes = GC.AllocateUninitializedArray<Byte>(hashGrow);
                var hashLen = hashGrow;
                int hashOffset = 0;
                //  Main loop
                while (dataSize > 0)
                {
                    //  Determine chunk size
                    var chunkSize = GetCutSize(data.AsSpan(offset, dataSize), props);
                    //  Make sure we can store the hash
                    if (hashOffset >= hashLen)
                    {
                        hashLen += hashGrow;
                        Array.Resize(ref hashes, hashLen);
                    }
                    if (preview)
                    {
                        //  Compute chunk hash 
                        HashChunk(hashes.AsMemory(hashOffset, hashSize), data.AsMemory().Slice(offset, chunkSize), props);
                    }
                    else
                    {
                        //  Compute chunk hash and save to storage (if required)
                        await CreateChunk(hashes.AsMemory(hashOffset, hashSize), data.AsMemory().Slice(offset, chunkSize), props).ConfigureAwait(false);
                    }
                    //  Step to next hash
                    hashOffset += hashSize;
                    //  Move to next chunk (if any left)
                    offset += chunkSize;
                    dataSize -= chunkSize;
                    //  Check if we need to read more data
                    if (moreData && (dataSize < maxSize))
                    {
                        //  Read more data
                        MoveToFront(data, offset, dataSize);
                        offset = 0;
                        dataSize += await s.ReadAsync(data.AsMemory(dataSize)).ConfigureAwait(false);
                        moreData = dataSize >= bufSize;
                    }
                }
                //  Finalise hash data
                Array.Resize(ref hashes, hashOffset);
                return hashes;
            }
            finally
            {
                ArrayPoolStream.Return(data);
            }
        }


        /// <summary>
        /// Add a file to the chunk store
        /// </summary>
        /// <param name="filename">The file</param>
        /// <param name="props">The properties, if null the default properties will be used (recommended)</param>
        /// <returns></returns>
        public static async ValueTask Add(String filename, CdcProps props = null)
        {
            using var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            await Cut(s, false, props).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a list chunks to a chunk list stream 
        /// </summary>
        /// <param name="destStream">The destination stream</param>
        /// <param name="chunks">Array of chunk hashes</param>
        /// <param name="props">The properties, if null the default properties will be used (recommended)</param>
        /// <returns>True if all the chunks was found and written to the stream, else false</returns>
        public static async ValueTask<bool> TryWriteChunkList(Stream destStream, ReadOnlyMemory<byte> chunks, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var hashSize = props.HashSize;
            var l = chunks.Length;
            for (int i = 0; i < l; i += hashSize)
            {
                var hashMem = chunks.Slice(i, hashSize);
                using var s = TryOpenCompressedChunk(hashMem.Span, props);
                if (s == null)
                    return false;
                var len = s.Length;
                WriteVar(destStream, (ulong)len);
                await destStream.WriteAsync(hashMem).ConfigureAwait(false);
                await s.CopyToAsync(destStream).ConfigureAwait(false);
            }
            return true;
        }


        public static async ValueTask<bool> TryWriteChunkList(Stream destStream, IEnumerable<ReadOnlyMemory<byte>> chunkEnum, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var hashSize = props.HashSize;
            foreach (var chunks in chunkEnum)
            {
                var l = chunks.Length;
                for (int i = 0; i < l; i += hashSize)
                {
                    var hashMem = chunks.Slice(i, hashSize);
                    using var s = TryOpenCompressedChunk(hashMem.Span, props);
                    if (s == null)
                        return false;
                    var len = s.Length;
                    WriteVar(destStream, (ulong)len);
                    await destStream.WriteAsync(hashMem).ConfigureAwait(false);
                    await s.CopyToAsync(destStream).ConfigureAwait(false);
                }
            }
            return true;
        }

        /// <summary>
        /// Get alist of all chunks that are missing
        /// </summary>
        /// <param name="chunks">The chunks to look for as a byte array of hashses</param>
        /// <param name="props"></param>
        /// <returns>A byte array of hashes with missing chunks</returns>
        public static ReadOnlyMemory<Byte> GetMissingChunks(ReadOnlyMemory<byte> chunks, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var hashSize = props.HashSize;
            var l = chunks.Length;
            var missing = GC.AllocateUninitializedArray<Byte>(l);
            var dest = missing.AsSpan();
            int o = 0;
            for (int i = 0; i < l; i += hashSize)
            {
                var src = chunks.Slice(i, hashSize).Span;
                if (ValidateChunk(src, props, true))
                    continue;
                src.CopyTo(dest);
                o += hashSize;
                dest = dest.Slice(hashSize);
            }
            return new ReadOnlyMemory<byte>(missing, 0, o);
        }



        /// <summary>
        /// Open a series of chunks as a single stream
        /// </summary>
        /// <param name="chunks">The chunks</param>
        /// <param name="props">The properties, if null the default properties will be used (recommended)</param>
        /// <returns>A stream with the chunk content (decompressed)</returns>
        /// <exception cref="Exception"></exception>
        public static Stream OpenChunkStream(ReadOnlyMemory<byte> chunks, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var hashSize = props.HashSize;
            return new CompressedChunkedStream(x =>
            {
                if (chunks.Length <= 0)
                    return null;
                var hash = chunks[..hashSize];
                chunks = chunks[hashSize..];
                return TryOpenCompressedChunk(hash.Span, props);
            }, props.Comp);
        }

        /// <summary>
        /// Try to add a list of chunks to the storage
        /// </summary>
        /// <param name="sourceStream"></param>
        /// <param name="props">The properties, if null the default properties will be used (recommended)</param>
        /// <returns>True if the chunks was added, elase false</returns>
        public static async ValueTask<bool> AddChunkList(Stream sourceStream, CdcProps props = null)
        {
            props = props ?? CdcProps.Default;
            var hashSize = props.HashSize;
            var hash = GC.AllocateUninitializedArray<Byte>(hashSize);
            while (ContentDependentChunking.TryReadVar(sourceStream, out var dataSize))
            {
                if (await sourceStream.ReadAsync(hash).ConfigureAwait(false) != hashSize)
                    return false;
                var data = GC.AllocateUninitializedArray<Byte>((int)dataSize);
                if (await sourceStream.ReadAsync(data).ConfigureAwait(false) != (long)dataSize)
                    return false;
                if (!await TrySaveChunk(hash.ToHex(), data, props).ConfigureAwait(false))
                    return false;
            }
            return true;
        }




        static int CenterSize(int average, int minimum, int sourceSize)
        {
            var offset = minimum + ((minimum + 1) >> 1);
            if (offset > average)
                offset = average;
            var size = average - offset;
            return size > sourceSize ? sourceSize : size;
        }

        /// <summary>
        /// Determine the chunk cut position for some memory
        /// </summary>
        /// <param name="source">The data source</param>
        /// <param name="props">The props used (may not be null)</param>
        /// <returns>The number of bytes from sourceOffset to use as a chunk</returns>
        static int GetCutSize(Span<Byte> source, CdcProps props)
        {
            var minSize = props.MinSize;
            var sourceSize = source.Length;
            if (sourceSize <= minSize)
                return sourceSize;
            var maxSize = props.MaxSize;
            if (sourceSize > maxSize)
                sourceSize = maxSize;
            var sourceLength1 = CenterSize(props.AverageSize, minSize, sourceSize);
            var sourceLength2 = sourceSize;

            var table = Table;

            uint hash = 0;
            var sourceOffset = minSize;
            var mask1 = props.Mask1;
            while (sourceOffset < sourceLength1)
            {
                hash = (hash >> 1) + table[source[sourceOffset++]];
                if ((hash & mask1) == 0)
                    return sourceOffset;
            }
            var mask2 = props.Mask2;
            while (sourceOffset < sourceLength2)
            {
                hash = (hash >> 1) + table[source[sourceOffset++]];
                if ((hash & mask2) == 0)
                    return sourceOffset;
            }
            return sourceSize;
        }

        /// <summary>
        /// Move some remaining memory to the front of a buffer
        /// </summary>
        /// <param name="d">The buffer</param>
        /// <param name="offset">The start offset to move memory from</param>
        /// <param name="size">The number of bytes to move</param>
        static void MoveToFront(Byte[] d, int offset, int size)
        {
            if (size <= 0)
                return;
            var ds = d.AsSpan();
            var src = ds.Slice(offset, size);
            src.CopyTo(d);
            //Buffer.BlockCopy(d, offset, d, 0, size);
        }

        static readonly uint[] Table = [
            1553318008, 574654857,  759734804,  310648967,  1393527547, 1195718329,
            694400241,  1154184075, 1319583805, 1298164590, 122602963,  989043992,
            1918895050, 933636724,  1369634190, 1963341198, 1565176104, 1296753019,
            1105746212, 1191982839, 1195494369, 29065008,   1635524067, 722221599,
            1355059059, 564669751,  1620421856, 1100048288, 1018120624, 1087284781,
            1723604070, 1415454125, 737834957,  1854265892, 1605418437, 1697446953,
            973791659,  674750707,  1669838606, 320299026,  1130545851, 1725494449,
            939321396,  748475270,  554975894,  1651665064, 1695413559, 671470969,
            992078781,  1935142196, 1062778243, 1901125066, 1935811166, 1644847216,
            744420649,  2068980838, 1988851904, 1263854878, 1979320293, 111370182,
            817303588,  478553825,  694867320,  685227566,  345022554,  2095989693,
            1770739427, 165413158,  1322704750, 46251975,   710520147,  700507188,
            2104251000, 1350123687, 1593227923, 1756802846, 1179873910, 1629210470,
            358373501,  807118919,  751426983,  172199468,  174707988,  1951167187,
            1328704411, 2129871494, 1242495143, 1793093310, 1721521010, 306195915,
            1609230749, 1992815783, 1790818204, 234528824,  551692332,  1930351755,
            110996527,  378457918,  638641695,  743517326,  368806918,  1583529078,
            1767199029, 182158924,  1114175764, 882553770,  552467890,  1366456705,
            934589400,  1574008098, 1798094820, 1548210079, 821697741,  601807702,
            332526858,  1693310695, 136360183,  1189114632, 506273277,  397438002,
            620771032,  676183860,  1747529440, 909035644,  142389739,  1991534368,
            272707803,  1905681287, 1210958911, 596176677,  1380009185, 1153270606,
            1150188963, 1067903737, 1020928348, 978324723,  962376754,  1368724127,
            1133797255, 1367747748, 1458212849, 537933020,  1295159285, 2104731913,
            1647629177, 1691336604, 922114202,  170715530,  1608833393, 62657989,
            1140989235, 381784875,  928003604,  449509021,  1057208185, 1239816707,
            525522922,  476962140,  102897870,  132620570,  419788154,  2095057491,
            1240747817, 1271689397, 973007445,  1380110056, 1021668229, 12064370,
            1186917580, 1017163094, 597085928,  2018803520, 1795688603, 1722115921,
            2015264326, 506263638,  1002517905, 1229603330, 1376031959, 763839898,
            1970623926, 1109937345, 524780807,  1976131071, 905940439,  1313298413,
            772929676,  1578848328, 1108240025, 577439381,  1293318580, 1512203375,
            371003697,  308046041,  320070446,  1252546340, 568098497,  1341794814,
            1922466690, 480833267,  1060838440, 969079660,  1836468543, 2049091118,
            2023431210, 383830867,  2112679659, 231203270,  1551220541, 1377927987,
            275637462,  2110145570, 1700335604, 738389040,  1688841319, 1506456297,
            1243730675, 258043479,  599084776,  41093802,   792486733,  1897397356,
            28077829,   1520357900, 361516586,  1119263216, 209458355,  45979201,
            363681532,  477245280,  2107748241, 601938891,  244572459,  1689418013,
            1141711990, 1485744349, 1181066840, 1950794776, 410494836,  1445347454,
            2137242950, 852679640,  1014566730, 1999335993, 1871390758, 1736439305,
            231222289,  603972436,  783045542,  370384393,  184356284,  709706295,
            1453549767, 591603172,  768512391,  854125182
        ];


    }




}
