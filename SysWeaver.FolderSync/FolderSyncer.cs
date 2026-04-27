using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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

        public const String TimeFmt = "yyyy-MM-dd_HH.mm.ss_ffff";

        public static String GetBakName(String folder)
        {
            var d = new DirectoryInfo(folder);
            var dir = String.Concat(d.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), '_', d.LastWriteTimeUtc.ToString(TimeFmt));
            return dir;
        }


        public static String ComputeCombinedHash(IReadOnlyList<String> fileNames, IReadOnlyList<String> hashStrings)
        {
            var count = hashStrings.Count;
            if (count <= 0)
                return "Empty";
            int maxL = 0;
            for (int i = 0; i < count; ++ i)
            {
                var ll = hashStrings[i].Length;
                if (ll > maxL)
                    maxL = ll;
                ll = fileNames[i].Length;
                if (ll > maxL)
                    maxL = ll;
            }
            var mem = maxL << 2;
            Byte[] data = new Byte[mem];
            var e = Encoding.ASCII;
            --count;
            var h = MD5.Create();
            int l;
            for (int i = 0; i < count; ++ i)
            {
                if (!e.TryGetBytes(fileNames[i], data, out l))
                    throw new Exception("Internal error!");
                h.TransformBlock(data, 0, l, data, 0);
                if (!e.TryGetBytes(hashStrings[i], data, out l))
                    throw new Exception("Internal error!");
                h.TransformBlock(data, 0, l, data, 0);
            }
            if (!e.TryGetBytes(fileNames[count], data, out l))
                throw new Exception("Internal error!");
            h.TransformBlock(data, 0, l, data, 0);
            if (!e.TryGetBytes(hashStrings[count], data, out l))
                throw new Exception("Internal error!");
            h.TransformFinalBlock(data, 0, l);
            return h.Hash.ToHexString();
        }


        readonly String Comment;
        readonly int MaxThreads;
        readonly String UploadBase;
        readonly String UploadCdcBase;
        readonly String UploadChunkBase;
        readonly IFolderSyncApi Api;

        /// <summary>
        /// The http client (with credentials) used to make requests
        /// </summary>
        public HttpClient Client => (Api as RemoteConnectionBase).Client;

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
            var res = await Api.CheckManagedFolder(new ManagedFolderSyncRequest
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
                            onEvent?.Invoke(FolderSyncEvents.Completed, x);
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
                var chunksToSend = uniqueChunks.Keys.ToList();
                var ucc = chunksToSend.Count;
                if (ucc > 0)
                {
                    Interlocked.Add(ref newChunkCount, ucc);
                    async Task<FolderSyncResult> SendSome(int offset, int count)
                    {
                        using var _ = await throttler.Lock().ConfigureAwait(false);
                        IUnmanagedReadOnlyMemory<Byte> mem = null;
                        using (var ms = new ArrayPoolStream(count * CdcProps.Default.AverageSize + 4096))
                        {
                            if (!await ContentDependentChunking.TryWriteChunkList(ms, chunksToSend.Skip(offset).Take(count), props).ConfigureAwait(false))
                                throw new Exception("Failed to write chunks!");
                            mem = ms.GetMemory();
                        }
                        using var mm = mem;
                        var destFile = String.Concat(UploadChunkBase, res.FolderCode, "/Data");
                        String data;
                        String ct;
                        using (var content = new ReadOnlyMemoryContent(mem.Memory))
                        {
                            var res2 = await client.PostAsync(destFile, content).ConfigureAwait(false);
                            data = await res2.Content.ReadAsStringAsync().ConfigureAwait(false);
                            ct = res2.Content.Headers.ContentType.MediaType;
                        }
                        var ml = mem.Memory.Length;
                        if (ct.FastStartsWith("application/json"))
                        {
                            Interlocked.Add(ref newChunkSize, ml);
                            if (!data.FastEquals("true"))
                                return new FolderSyncResult
                                {
                                    SourceFiles = sourceFileCount,
                                    SourceBytes = sourceBytes,
                                    Errors = [new Exception("Failed to upload missing chunks!")],
                                };
                            Interlocked.Add(ref payloadSize, ml);
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
                        return null;
                    }
                    const int maxChunksPerBatch = 64;
                    var pchunks = (ucc + maxChunksPerBatch - 1) / maxChunksPerBatch;
                    var r = new Task<FolderSyncResult>[pchunks];
                    for (int i = 0, offset = 0; i < pchunks; ++ i, offset += maxChunksPerBatch)
                    {
                        var count = ucc - offset;
                        if (count > maxChunksPerBatch)
                            count = maxChunksPerBatch;
                        r[i] = SendSome(offset, count);
                    }
                    await Task.WhenAll(r).ConfigureAwait(false);


                    foreach (var rr in r)
                    {
                        var xres = rr.Result;
                        if (xres != null)
                            return xres;
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
                            onEvent?.Invoke(FolderSyncEvents.Completed, x);
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
        public async ValueTask<FolderSyncResult> PullFolder(String srcName, String destFolder, bool switchTo = false, bool useCdc = true, Action<FolderSyncEvents, String> onEvent = null)
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
            var t = files.Values.OrderBy(x => x.Name).ToList();
            var version = FolderSyncer.ComputeCombinedHash(t.Select(x => x.Name).ToList(), t.Select(x => x.Hash).ToList());
            if (files.Count > 16)
            {
                if (!await api.SharedFolderHasChanged(new SharedFolderSyncRequest
                {
                    Folder = srcName,
                    Version = version,
                }).ConfigureAwait(false))
                {
                    return new FolderSyncResult
                    {
                        SourceBytes = sourceBytes,
                        SourceFiles = sourceFileCount,
                    };
                }
            }
            var res = await api.CheckSharedFolder(new LocalFolderInfo
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
            if (version.FastEquals(res.Version))
            {
                return new FolderSyncResult
                {
                    SourceBytes = sourceBytes,
                    SourceFiles = sourceFileCount,
                };
            }
            version = res.Version;
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
                    var ex = await PathExt.EnsureCanWriteFileAsync(dst).ConfigureAwait(false);
                    if (ex != null)
                        throw ex;
                    ex = await PathExt.TryCopyFileAsync(Path.Combine(destFolder, fn), dst).ConfigureAwait(false);
                    onEvent?.Invoke(FolderSyncEvents.Completed, dst);
                    if (ex != null)
                        throw ex;
                    fi = new FileInfo(dst);
                }
                files.TryRemove(x, out var _);
                Interlocked.Increment(ref sourceFileCount);
                Interlocked.Add(ref sourceBytes, fi.Length);
            }).ConfigureAwait(false);
            long transferred = 0;
            long transferredBytes = 0;
            long discBytes = 0;
            long chunkTotalCount = 0;
            long missingChunkCount = 0;
            long missingChunkBytes = 0;
            //  Download files
            var dls = res.Download;
            if (dls != null)
            {
                var remote = api as RemoteConnectionBase;
                var client = remote.Client;
                var baseUrl = remote.UrlBase;
                var baseDownloadUrl = String.Concat(baseUrl, "ManagedFolders/", srcName, '/');

                async ValueTask<ReadOnlyMemory<Byte>> CacheCdc(String fn)
                {
                    var dst = Path.Combine(downloadFolder, fn);
                    if (!File.Exists(dst))
                        dst = Path.Combine(destFolder, fn);
                    if (File.Exists(dst))
                        await ContentDependentChunking.Add(dst, props).ConfigureAwait(false);
                    return ReadOnlyMemory<Byte>.Empty;
                }
                useCdc = res.Cdc != null;
                await dls.ProcessAsyncValue(async dl =>
                {
                    var x = dl.Name;
                    using var _ = await throttler.Lock().ConfigureAwait(false);
                    var fn = x.Replace('/', Path.DirectorySeparatorChar);
                    var targetFile = Path.Combine(downloadFolder, fn);
                    var ex = await PathExt.EnsureCanWriteFileAsync(targetFile).ConfigureAwait(false);
                    if (ex != null)
                        throw ex;
                    long? transferBytes = null;
                    if (useCdc)
                    {
                        var syncTask = client.PostJsonRequestRaw(baseUrl + nameof(IFolderSyncEndPoints.GetSharedFileChunks), new SharedFileChunksRequest
                        {
                            File = x,
                            Folder = srcName,
                            Version = version,
                        });
                        await TaskExt.WhenAll([syncTask, CacheCdc(fn)]).ConfigureAwait(false);
                        var chunkHashes = syncTask.Result;
                        var cl = chunkHashes.Length;
                        var hs = props.HashSize;
                        transferBytes = cl;
                        chunkTotalCount += (cl / hs);
                        var missing = ContentDependentChunking.GetMissingChunks(chunkHashes);
                        var ml = missing.Length;
                        if (ml > 0)
                        {
                            missingChunkCount += (ml / hs);
                            var mem = await client.PostRawRequestRaw(baseUrl + nameof(IFolderSyncEndPoints.GetChunks), missing);
                            using (var ms = mem.AsStream())
                                if (await ContentDependentChunking.AddChunkList(ms, props).ConfigureAwait(false) < 0)
                                    throw new Exception("Failed to add missing chunks!");
                            cl = mem.Length;
                            transferBytes += cl;
                            missingChunkBytes += cl;
                        }
                        using var dest = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                        await ContentDependentChunking.WriteChunks(dest, chunkHashes, props).ConfigureAwait(false);
                    }
                    else
                    {
                        using var get = await client.GetAsync(baseDownloadUrl + x).ConfigureAwait(false);
                        var cc = get.Content;
                        transferBytes = cc.Headers.ContentLength;
                        using var dest = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                        await cc.CopyToAsync(dest).ConfigureAwait(false);
                    }
                    if (!(await FileHash.GetHashAsync(targetFile).ConfigureAwait(false)).FastEquals(dl.Hash))
                        throw new Exception("Downloaded file is corrupted!");
                    var fi = new FileInfo(targetFile);
                    fi.LastWriteTimeUtc = dl.LastModified;
                    var destBytes = fi.Length;
                    files.TryRemove(x, out var orgI);
                    Interlocked.Add(ref discBytes, destBytes);
                    Interlocked.Add(ref transferredBytes, transferBytes ?? destBytes);
                    Interlocked.Increment(ref transferred);
                    Interlocked.Increment(ref sourceFileCount);
                    Interlocked.Add(ref sourceBytes, destBytes);
                    onEvent?.Invoke(FolderSyncEvents.Completed, targetFile);
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
                onEvent?.Invoke(FolderSyncEvents.Completed, targetFile);
            }).ConfigureAwait(false);
            await PathExt.TryRemoveEmptyFoldersAsync(downloadFolder).ConfigureAwait(false);
            if (switchTo)
            {
                var bak = GetBakName(destFolder);
                var ex = await PathExt.TryFolderSwapAsync(destFolder, bak, downloadFolder).ConfigureAwait(false);
                if (ex != null)
                    throw ex;
            }
            return new FolderSyncResult
            {
                SourceFiles = sourceFileCount,
                SourceBytes = sourceBytes,
                TransferredCount = transferred,
                TransferredNetworkSize = transferredBytes,
                TransferredSourceBytes = discBytes,
                ChunkCount = chunkTotalCount,
                NewChunkCount = missingChunkCount,
                NewChunkSize = missingChunkBytes
            };
        }


        public void Dispose()
        {
            Api.Dispose();
        }


    }
}
