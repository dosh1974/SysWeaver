using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver;
using SysWeaver.Compression;
using SysWeaver.Remote;

namespace SysWeaver.FolderSync
{

    public sealed class FolderSyncer : IDisposable
    {
        public FolderSyncer(FolderSyncerParams p)
        {
            var serverBase = p.Server.TrimEnd('/');
            var maxThreads = p.MaxConcurrency;
            if (maxThreads <= 0)
                maxThreads = Math.Max(1, Environment.ProcessorCount + maxThreads);
            Comment = p.Comment;
            MaxThreads = maxThreads;

            var server = serverBase + "/FolderSync/";
            UploadBase = server;
            UploadCdcBase = serverBase + "/FolderSyncCdc/";
            UploadChunkBase = serverBase + "/FolderSyncCdcChunks/";
            var rrc = new RemoteConnection
            {
                User = p.User,
                Password = p.Password,
                CredFile = p.CredFile,
                BaseUrl = server,
                IgnoreCertErrors = p.IgnoreCertErrors,
                AuthMethod = RemoteAuthMethod.SysWeaverLogin,
                SysWeaverBaseSuffix = "../",
                TimeoutInMilliSeconds = (60 * 60 * 1000),
                Compression = "br",
                CompLevel = CompEncoderLevels.Best,
            };
            Api = rrc.Create<IFolderSyncApi>();
        }

        readonly String Comment;
        readonly int MaxThreads;
        readonly String UploadBase;
        readonly String UploadCdcBase;
        readonly String UploadChunkBase;
        readonly IFolderSyncApi Api;

        static readonly ICompType Comp = CompManager.GetFromHttp("br");

        static readonly IReadOnlySet<String> Uncompressible = ReadOnlyData.Set(StringComparer.Ordinal,
                "png",
                "avif",
                "webp",

                "aac",
                "wma",
                "flac",
                "ogg",
                "mp3",

                "webm",
                "mp4",
                "mpeg",
                "wmv",
                "avi",
                "mov",
                "mkv",
                "flv",
                "mts",
                "m2ts",

                "pdf",

                "docx",
                "docm",
                "xlsx",
                "xlsm",
                "pptx",
                "pptm",
                "vsdx",
                "vsdm",

                "br",
                "deflate",
                "gz",
                "gzip",
                "zip",
                "7z",
                "rar",
                "bz2"
            );



        /// <summary>
        /// Update a remote repository from local folders
        /// </summary>
        /// <param name="sourceFolders">The local source folder. Multiple folders can be specified separated by a ';'</param>
        /// <param name="destName">The name of the remote repository to update</param>
        /// <param name="switchTo">If true, the newly synched folder will be used when updated</param>
        /// <param name="useCdc">If true, try to use Content Dependent Chunking</param>
        /// <param name="ignore">An optional callback used to ignore some files</param>
        /// <param name="onEvent">An optional callback used to display what's going on</param>
        /// <returns>Sync results</returns>
        /// <exception cref="Exception"></exception>
        public async ValueTask<FolderSyncResult> PushFolders(String sourceFolders, String destName, bool switchTo = false, bool useCdc = true, Func<String, bool> ignore = null, Action<FolderSyncEvents, String> onEvent = null)
        {
            //var props = useCdc ? new CdcProps(folders: [@"D:\Temp\CdcSyncTest"]) : null;
            var props = useCdc ? CdcProps.Default : null;
            //var throttler = new AsyncLock(1);
            var throttler = new AsyncLock(MaxThreads);
            Dictionary<String, Tuple<String, FolderSyncFile>> files = new (StringComparer.Ordinal);
            long sourceBytes = 0;
            long sourceFileCount = 0;
            foreach (var x in sourceFolders.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var di = new DirectoryInfo(x);
                if (!di.Exists)
                    throw new Exception("The source folder doesn't exist!");
                var sourceFolder = di.FullName;
                var sfl = sourceFolder.Length + 1;
                var srcFiles = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
                var l = srcFiles.Length;
                if (ignore != null)
                {
                    int o = 0;
                    for (int i = 0; i < l; i++)
                    {
                        var n = srcFiles[i];
                        if (ignore(n))
                            continue;
                        srcFiles[o] = n;
                        ++o;
                    }
                    if (o != l)
                    {
                        l = o;
                        Array.Resize(ref srcFiles, l);
                    }
                }
                sourceFileCount += srcFiles.Length;
                foreach (var f in await srcFiles.ConvertAsyncValue(async x =>
                {
                    using var _ = await throttler.Lock().ConfigureAwait(false);
                    var hash = await FileHash.GetHashAsync(x).ConfigureAwait(false);
                    var localFile = x.Substring(sfl);
                    var fi = new FileInfo(x);
                    Interlocked.Add(ref sourceBytes, fi.Length);
                    onEvent?.Invoke(FolderSyncEvents.Hashed, localFile);
                    return Tuple.Create(x, new FolderSyncFile
                    {
                        Name = localFile,
                        Hash = hash,
                        LastModified = fi.LastWriteTimeUtc,
                    });
                }).ConfigureAwait(false))
                    files[f.Item2.Name.FastToLower()] = f;
            }
            onEvent?.Invoke(FolderSyncEvents.Scanned, sourceFolders);
            var res = await Api.SyncFolder(new FolderSyncRequest
            {
                Folder = destName,
                Files = files.Values.OrderBy(x => x.Item2.Name).Select(x => x.Item2).ToArray(),
                UseFolder = switchTo,
                Cdc = useCdc ? props.Key : null,
                Comment = Comment,
                Machine = Environment.MachineName,
            }).ConfigureAwait(false);
            //  Some error
            if (res == null)
                return new FolderSyncResult
                {
                    SourceFiles = sourceFileCount,
                    SourceBytes = sourceBytes,
                    Errors = [new Exception("Folder sync request failed")]
                };
            //  Already synced
            var uploadFiles = res.Files;
            if (uploadFiles == null)
                return new FolderSyncResult
                {
                    SourceFiles = sourceFileCount,
                    SourceBytes = sourceBytes,
                };
            onEvent?.Invoke(FolderSyncEvents.Checked, sourceFolders);
            var client = (Api as RemoteConnectionBase).Client;
            long fileCount = 0;
            long fileSize = 0;
            long payloadSize = 0;
            useCdc = res.Cdc.FastEquals(props.Key);
            if (useCdc)
            {
                long chunkCount = 0;
                long newChunkCount = 0;
                long newChunkSize = 0;
                var destPrefix = String.Concat(UploadCdcBase, res.FolderCode, '/');
                //  Send chunk information and gather unknown chunks
                var xfileCount = uploadFiles.Length;
                ConcurrentDictionary<ReadOnlyMemory<Byte>, int> uniqueChunks = new(ReadOnlyMemoryComparer.GetEqualityComparer<Byte>());
                var hashSize = props.HashSize;
                var exceptions = await uploadFiles.ConvertAsyncValue(async x =>
                {
                    try
                    {

                        using var _ = await throttler.Lock().ConfigureAwait(false);
                        var srcFile = files[x.FastToLower()].Item1;
                        var fi = new FileInfo(srcFile);
                        var destFile = destPrefix + x.Replace('\\', '/');
                        Byte[] chunks;
                        using (var fs = fi.OpenRead())
                            chunks = await ContentDependentChunking.Cut(fs, false, props).ConfigureAwait(false);
                        using var content = new ByteArrayContent(chunks);
                        var res = await client.PostAsync(destFile, content).ConfigureAwait(false);
                        var data = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        var ct = res.Content.Headers.ContentType.MediaType;
                        if (ct.FastStartsWith(MimeTypeMap.Data))
                        {
                            var chunkMem = chunks.AsMemory();
                            var l = data.Length;
                            for (int i = 0; i < l; ++ i)
                            {
                                int mask = data[i];
                                int bc = i << 3;
                                for (int j = 0; mask != 0; ++ j, mask = mask >> 1)
                                {
                                    if ((mask & 1) == 0)
                                        continue;
                                    int chunk = bc + j;
                                    uniqueChunks.TryAdd(chunkMem.Slice(chunk * hashSize, hashSize), 0);
                                }
                            }
                            Interlocked.Increment(ref fileCount);
                            Interlocked.Add(ref fileSize, fi.Length);
                            Interlocked.Add(ref payloadSize, chunks.Length);
                            Interlocked.Add(ref chunkCount, chunks.Length / hashSize);
                            onEvent?.Invoke(FolderSyncEvents.Comnpleted, x);
                            return null;
                        }
                        return new Exception(Encoding.UTF8.GetString(data));
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                }).ConfigureAwait(false);
                var t = exceptions.Where(x => x != null).ToArray();
                if (t.Length > 0)
                    return new FolderSyncResult
                    {
                        SourceFiles = sourceFileCount,
                        SourceBytes = sourceBytes,
                        Errors = t
                    };
                //  Send missing chunks
                var ucc = uniqueChunks.Count;
                if (ucc > 0)
                {
                    Interlocked.Add(ref newChunkCount, ucc);
                    ReadOnlyMemory<Byte> mem;
                    using (var ms = new MemoryStream(ucc * CdcProps.Default.AverageSize + 4096))
                    {
                        if (!await ContentDependentChunking.TryWriteChunkList(ms, uniqueChunks.Keys, props).ConfigureAwait(false))
                            throw new Exception("Failed to write chunks!");
                        mem = ms.GetBuffer().AsMemory().Slice(0, (int)ms.Position);
                    }
                    var destFile = String.Concat(UploadChunkBase, res.FolderCode, "/Data");
                    String data;
                    String ct;
                    using (var content = new ReadOnlyMemoryContent(mem))
                    {
                        var res2 = await client.PostAsync(destFile, content).ConfigureAwait(false);
                        data = await res2.Content.ReadAsStringAsync().ConfigureAwait(false);
                        ct = res2.Content.Headers.ContentType.MediaType;
                    }
                    if (ct.FastStartsWith("application/json"))
                    {
                        Interlocked.Add(ref newChunkSize, mem.Length);
                        if (!data.FastEquals("true"))
                            return new FolderSyncResult
                            {
                                SourceFiles = sourceFileCount,
                                SourceBytes = sourceBytes,
                                Errors = [new Exception("Failed to upload missing chunks!")],
                            };
                        Interlocked.Add(ref payloadSize, mem.Length);
                    }
                    else
                    {
                        return new FolderSyncResult
                        {
                            SourceFiles = sourceFileCount,
                            SourceBytes = sourceBytes,
                            Errors = [new Exception(data)],
                        };
                    }
                }
                return new FolderSyncResult
                {
                    SourceFiles = sourceFileCount,
                    SourceBytes = sourceBytes,
                    TransferredCount = fileCount,
                    TransferredSourceBytes = fileSize,
                    TransferredNetworkSize = payloadSize,
                    ChunkCount = chunkCount,
                    NewChunkCount = newChunkCount,
                    NewChunkSize = newChunkSize,
                };

            }
            else
            {
                var destPrefix = String.Concat(UploadBase, res.FolderCode, '/');
                var uncompressible = Uncompressible;
                var exceptions = await uploadFiles.ConvertAsyncValue(async x =>
                {
                    //  Upload each file in paralell
                    try
                    {
                        using var _ = await throttler.Lock().ConfigureAwait(false);
                        var srcFile = files[x.FastToLower()].Item1;
                        var destFile = destPrefix + x.Replace('\\', '/');



                        var fi = new FileInfo(srcFile);
                        bool compress = !uncompressible.Contains(new FileInfo(srcFile).Extension.FastToLower());
                        using var s = compress ? (await CompressedFile.OpenAsync(srcFile, Comp, CompEncoderLevels.Best).ConfigureAwait(false)) : new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var content = new StreamContent(s);
                        if (compress)
                            content.Headers.ContentEncoding.Add(Comp.HttpCode);
                        var res = await client.PostAsync(destFile, content).ConfigureAwait(false);
                        var data = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var ct = res.Content.Headers.ContentType.MediaType;
                        if (ct.FastStartsWith("application/json"))
                        {
                            if (!data.FastEquals("true"))
                                return new Exception("Failed to upload \"" + x + "\"");
                            Interlocked.Increment(ref fileCount);
                            Interlocked.Add(ref fileSize, fi.Length);
                            Interlocked.Add(ref payloadSize, s.Position);
                            onEvent?.Invoke(FolderSyncEvents.Comnpleted, x);
                        }
                        else
                        {
                            return new Exception(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                    return null;
                }).ConfigureAwait(false);
                var t = exceptions.Where(x => x != null).ToArray();
                if (t.Length > 0)
                    return new FolderSyncResult
                    {
                        SourceFiles = sourceFileCount,
                        SourceBytes = sourceBytes,
                        Errors = t
                    };
            }
            return new FolderSyncResult
            {
                SourceFiles = sourceFileCount,
                SourceBytes = sourceBytes,
                TransferredCount = fileCount,
                TransferredSourceBytes = fileSize,
                TransferredNetworkSize = payloadSize,
            };
        }


        /// <summary>
        /// Update a local folder from a remote repository
        /// </summary>
        /// <param name="srcName">The name of the remote repository</param>
        /// <param name="destFolder">The local destination folder to update</param>
        /// <param name="switchTo">If true, the newly synched folder will be used when updated</param>
        /// <param name="useCdc">If true, try to use Content Dependent Chunking</param>
        /// <param name="onEvent">An optional callback used to display what's going on</param>
        /// <returns>Sync results</returns>
        /// <exception cref="Exception"></exception>
        public async ValueTask<FolderSyncResult> PullFolders(String srcName, String destFolder, bool switchTo = false, bool useCdc = true, Action<FolderSyncEvents, String> onEvent = null)
        {
            //var props = useCdc ? new CdcProps(folders: [@"D:\Temp\CdcSyncTest"]) : null;
            var props = useCdc ? CdcProps.Default : null;
            //var throttler = new AsyncLock(1);
            var throttler = new AsyncLock(MaxThreads);
            ConcurrentDictionary<String, FolderSyncFile> files = new(StringComparer.Ordinal);
            long sourceBytes = 0;
            long sourceFileCount = 0;
            var di = new DirectoryInfo(destFolder);
            destFolder = di.FullName;
            var downloadFolder = destFolder + "_UploadTemp";
            var api = Api;
            foreach (var sourceFolder in new String[] { downloadFolder, destFolder })
            {
                if (!Directory.Exists(sourceFolder))
                    continue;
                var sfl = sourceFolder.Length + 1;
                var srcFiles = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
                var l = srcFiles.Length;
                /*                if (ignore != null)
                                {
                                    int o = 0;
                                    for (int i = 0; i < l; i++)
                                    {
                                        var n = srcFiles[i];
                                        if (ignore(n))
                                            continue;
                                        srcFiles[o] = n;
                                        ++o;
                                    }
                                    if (o != l)
                                    {
                                        l = o;
                                        Array.Resize(ref srcFiles, l);
                                    }
                                }
                */
                await srcFiles.ProcessAsyncValue(async x =>
                {
                    using var _ = await throttler.Lock().ConfigureAwait(false);
                    var localFile = x.Substring(sfl).Replace(Path.DirectorySeparatorChar, '/');
                    var hash = await FileHash.GetHashAsync(x).ConfigureAwait(false);
                    var fi = new FileInfo(x);
                    onEvent?.Invoke(FolderSyncEvents.Hashed, localFile);
                    if (files.TryAdd(localFile, new FolderSyncFile
                    {
                        Name = localFile,
                        Hash = hash,
                        LastModified = fi.LastWriteTimeUtc,
                    }))
                    {
                        Interlocked.Increment(ref sourceFileCount);
                        Interlocked.Add(ref sourceBytes, fi.Length);

                    }
                }).ConfigureAwait(false);
            }
            onEvent?.Invoke(FolderSyncEvents.Scanned, destFolder);
            var res = await api.SyncPullFolder(new FolderPullRequest
            {
                Folder = srcName,
                Files = files.Values.OrderBy(x => x.Name).ToArray(),
                Cdc = useCdc ? props.Key : null,
            }).ConfigureAwait(false);
            //  Some error
            if (res == null)
                return new FolderSyncResult
                {
                    SourceFiles = sourceFileCount,
                    SourceBytes = sourceBytes,
                    Errors = [new Exception("Folder sync request failed")]
                };
            onEvent?.Invoke(FolderSyncEvents.Checked, destFolder);
            //  Copy files
            sourceBytes = 0;
            sourceFileCount = 0;
            await res.Keep.Nullable().ProcessAsyncValue(async x =>
            {
                using var _ = await throttler.Lock().ConfigureAwait(false);
                var fn = x.Replace('/', Path.DirectorySeparatorChar);
                var dst = Path.Combine(downloadFolder, fn);
                var fi = new FileInfo(dst);
                if (!fi.Exists)
                {
                    var ex = await PathExt.TryCopyFileAsync(Path.Combine(destFolder, fn), dst).ConfigureAwait(false);
                    if (ex != null)
                        throw ex;
                }
                files.TryRemove(x, out var _);
                Interlocked.Increment(ref sourceFileCount);
                Interlocked.Add(ref sourceBytes, fi.Length);
            }).ConfigureAwait(false);
            long transferred = 0;
            long transferredBytes = 0;
            long discBytes = 0;
            //  Download files
            var dls = res.Download;
            if (dls != null)
            {
                var remote = api as RemoteConnectionBase;
                var client = remote.Client;
                var baseUrl = String.Concat(remote.UrlBase, "PullFolders/", srcName, '/');
                await dls.ProcessAsyncValue(async x =>
                {
                    using var _ = await throttler.Lock().ConfigureAwait(false);
                    var fn = x.Replace('/', Path.DirectorySeparatorChar);
                    var targetFile = Path.Combine(downloadFolder, fn);
                    var ex = await PathExt.EnsureCanWriteFileAsync(targetFile).ConfigureAwait(false);
                    if (ex != null)
                        throw ex;
                    DateTime? lm;
                    long? c;
                    long ds;
                    {
                        using var get = await client.GetAsync(baseUrl + x).ConfigureAwait(false);
                        var cc = get.Content;
                        lm = cc.Headers.LastModified?.DateTime;
                        c = cc.Headers.ContentLength;
                        using var dest = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                        await cc.CopyToAsync(dest).ConfigureAwait(false);
                        ds = dest.Position;
                    }
                    var fi = new FileInfo(targetFile);
                    if (lm != null)
                        fi.LastWriteTimeUtc = lm ?? DateTime.UtcNow;
                    Interlocked.Add(ref discBytes, ds);
                    Interlocked.Add(ref transferredBytes, c ?? ds);
                    Interlocked.Increment(ref transferred);
                    files.TryRemove(x, out var _);
                    Interlocked.Increment(ref sourceFileCount);
                    Interlocked.Add(ref sourceBytes, fi.Length);
                }).ConfigureAwait(false);
            }
            //  Delete files
            await files.Keys.ProcessAsyncValue(async x =>
            {
                using var _ = await throttler.Lock().ConfigureAwait(false);
                var fn = x.Replace('/', Path.DirectorySeparatorChar);
                var targetFile = Path.Combine(downloadFolder, fn);
                var ex = await PathExt.TryDeleteFileAsync(targetFile).ConfigureAwait(false);
                if (ex != null)
                    throw ex;
            }).ConfigureAwait(false);
            await PathExt.TryRemoveEmptyFoldersAsync(downloadFolder).ConfigureAwait(false);
            return new FolderSyncResult
            {
                SourceFiles = sourceFileCount,
                SourceBytes = sourceBytes,
                TransferredCount = transferred,
                TransferredNetworkSize = transferredBytes,
                TransferredSourceBytes = discBytes,
            };
/*
            

            //  Already synced
            var uploadFiles = res.Files;
            if (uploadFiles == null)
                return new FolderSyncResult
                {
                    SourceFiles = sourceFileCount,
                    SourceBytes = sourceBytes,
                };
            onEvent?.Invoke(FolderSyncEvents.Checked, destFolder);
            var client = (Api as RemoteConnectionBase).Client;
            long fileCount = 0;
            long fileSize = 0;
            long payloadSize = 0;
            useCdc = res.Cdc.FastEquals(props.Key);
            if (useCdc)
            {
                long chunkCount = 0;
                long newChunkCount = 0;
                long newChunkSize = 0;
                var destPrefix = String.Concat(UploadCdcBase, res.FolderCode, '/');
                //  Send chunk information and gather unknown chunks
                var xfileCount = uploadFiles.Length;
                ConcurrentDictionary<ReadOnlyMemory<Byte>, int> uniqueChunks = new(ReadOnlyMemoryComparer.GetEqualityComparer<Byte>());
                var hashSize = props.HashSize;
                var exceptions = await uploadFiles.ConvertAsyncValue(async x =>
                {
                    try
                    {

                        using var _ = await throttler.Lock().ConfigureAwait(false);
                        var srcFile = files[x.FastToLower()].Item1;
                        var fi = new FileInfo(srcFile);
                        var destFile = destPrefix + x.Replace('\\', '/');
                        Byte[] chunks;
                        using (var fs = fi.OpenRead())
                            chunks = await ContentDependentChunking.Cut(fs, false, props).ConfigureAwait(false);
                        using var content = new ByteArrayContent(chunks);
                        var res = await client.PostAsync(destFile, content).ConfigureAwait(false);
                        var data = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        var ct = res.Content.Headers.ContentType.MediaType;
                        if (ct.FastStartsWith(MimeTypeMap.Data))
                        {
                            var chunkMem = chunks.AsMemory();
                            var l = data.Length;
                            for (int i = 0; i < l; ++i)
                            {
                                int mask = data[i];
                                int bc = i << 3;
                                for (int j = 0; mask != 0; ++j, mask = mask >> 1)
                                {
                                    if ((mask & 1) == 0)
                                        continue;
                                    int chunk = bc + j;
                                    uniqueChunks.TryAdd(chunkMem.Slice(chunk * hashSize, hashSize), 0);
                                }
                            }
                            Interlocked.Increment(ref fileCount);
                            Interlocked.Add(ref fileSize, fi.Length);
                            Interlocked.Add(ref payloadSize, chunks.Length);
                            Interlocked.Add(ref chunkCount, chunks.Length / hashSize);
                            onEvent?.Invoke(FolderSyncEvents.Comnpleted, x);
                            return null;
                        }
                        return new Exception(Encoding.UTF8.GetString(data));
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                }).ConfigureAwait(false);
                var t = exceptions.Where(x => x != null).ToArray();
                if (t.Length > 0)
                    return new FolderSyncResult
                    {
                        SourceFiles = sourceFileCount,
                        SourceBytes = sourceBytes,
                        Errors = t
                    };
                //  Send missing chunks
                var ucc = uniqueChunks.Count;
                if (ucc > 0)
                {
                    Interlocked.Add(ref newChunkCount, ucc);
                    ReadOnlyMemory<Byte> mem;
                    using (var ms = new MemoryStream(ucc * CdcProps.Default.AverageSize + 4096))
                    {
                        if (!await ContentDependentChunking.TryWriteChunkList(ms, uniqueChunks.Keys, props).ConfigureAwait(false))
                            throw new Exception("Failed to write chunks!");
                        mem = ms.GetBuffer().AsMemory().Slice(0, (int)ms.Position);
                    }
                    var destFile = String.Concat(UploadChunkBase, res.FolderCode, "/Data");
                    String data;
                    String ct;
                    using (var content = new ReadOnlyMemoryContent(mem))
                    {
                        var res2 = await client.PostAsync(destFile, content).ConfigureAwait(false);
                        data = await res2.Content.ReadAsStringAsync().ConfigureAwait(false);
                        ct = res2.Content.Headers.ContentType.MediaType;
                    }
                    if (ct.FastStartsWith("application/json"))
                    {
                        Interlocked.Add(ref newChunkSize, mem.Length);
                        if (!data.FastEquals("true"))
                            return new FolderSyncResult
                            {
                                SourceFiles = sourceFileCount,
                                SourceBytes = sourceBytes,
                                Errors = [new Exception("Failed to upload missing chunks!")],
                            };
                        Interlocked.Add(ref payloadSize, mem.Length);
                    }
                    else
                    {
                        return new FolderSyncResult
                        {
                            SourceFiles = sourceFileCount,
                            SourceBytes = sourceBytes,
                            Errors = [new Exception(data)],
                        };
                    }
                }
                return new FolderSyncResult
                {
                    SourceFiles = sourceFileCount,
                    SourceBytes = sourceBytes,
                    Uploaded = fileCount,
                    UploadedSourceBytes = fileSize,
                    UploadedNetworkBytes = payloadSize,
                    ChunkCount = chunkCount,
                    NewChunkCount = newChunkCount,
                    NewChunkSize = newChunkSize,
                };

            }
            else
            {
                var destPrefix = String.Concat(UploadBase, res.FolderCode, '/');
                var uncompressible = Uncompressible;
                var exceptions = await uploadFiles.ConvertAsyncValue(async x =>
                {
                    //  Upload each file in paralell
                    try
                    {
                        using var _ = await throttler.Lock().ConfigureAwait(false);
                        var srcFile = files[x.FastToLower()].Item1;
                        var destFile = destPrefix + x.Replace('\\', '/');



                        var fi = new FileInfo(srcFile);
                        bool compress = !uncompressible.Contains(new FileInfo(srcFile).Extension.FastToLower());
                        using var s = compress ? (await CompressedFile.OpenAsync(srcFile, Comp, CompEncoderLevels.Best).ConfigureAwait(false)) : new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var content = new StreamContent(s);
                        if (compress)
                            content.Headers.ContentEncoding.Add(Comp.HttpCode);
                        var res = await client.PostAsync(destFile, content).ConfigureAwait(false);
                        var data = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var ct = res.Content.Headers.ContentType.MediaType;
                        if (ct.FastStartsWith("application/json"))
                        {
                            if (!data.FastEquals("true"))
                                return new Exception("Failed to upload \"" + x + "\"");
                            Interlocked.Increment(ref fileCount);
                            Interlocked.Add(ref fileSize, fi.Length);
                            Interlocked.Add(ref payloadSize, s.Position);
                            onEvent?.Invoke(FolderSyncEvents.Comnpleted, x);
                        }
                        else
                        {
                            return new Exception(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                    return null;
                }).ConfigureAwait(false);
                var t = exceptions.Where(x => x != null).ToArray();
                if (t.Length > 0)
                    return new FolderSyncResult
                    {
                        SourceFiles = sourceFileCount,
                        SourceBytes = sourceBytes,
                        Errors = t
                    };
            }
            return new FolderSyncResult
            {
                SourceFiles = sourceFileCount,
                SourceBytes = sourceBytes,
                Uploaded = fileCount,
                UploadedSourceBytes = fileSize,
                UploadedNetworkBytes = payloadSize,
            };
*/
        }


        public void Dispose()
        {
            Api.Dispose();
        }


    }
}
