using System;
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
            var server = p.Server.TrimEnd('/') + "/FolderSync/";

            var maxThreads = p.MaxConcurrency;
            if (maxThreads <= 0)
                maxThreads = Math.Max(1, Environment.ProcessorCount + maxThreads);
            Comment = p.Comment;
            MaxThreads = maxThreads;
            Server = server;
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
        readonly String Server;
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
        /// Synchronize a local folder with a remote folder.
        /// </summary>
        /// <param name="sourceFolders">The local source folder. Multiple folders can be specified separated by a ';'</param>
        /// <param name="destName">The name of the remote folder</param>
        /// <param name="switchTo">If true, the newly synched folder will be used when updated</param>
        /// <param name="onEvent">An optional callback used to display what's going on</param>
        /// <returns>Sync results</returns>
        /// <exception cref="Exception"></exception>
        public async ValueTask<FolderSyncResult> SyncFolder(String sourceFolders, String destName, bool switchTo, Action<FolderSyncEvents, String> onEvent = null )
        {
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
            if (res.Files == null)
                return new FolderSyncResult
                {
                    SourceFiles = sourceFileCount,
                    SourceBytes = sourceBytes,
                };
            onEvent?.Invoke(FolderSyncEvents.Checked, sourceFolders);
            var destPrefix = String.Concat(Server, res.FolderCode, '/');
            var client = (Api as RemoteConnectionBase).Client;
            long fileCount = 0;
            long fileSize = 0;
            long payloadSize = 0;
            var uncompressible = Uncompressible;
            var exceptions = await res.Files.ConvertAsyncValue(async x =>
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
                        onEvent?.Invoke(FolderSyncEvents.Uploaded, x);
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
            return new FolderSyncResult
            {
                SourceFiles = sourceFileCount,
                SourceBytes = sourceBytes,
                Uploaded = fileCount,
                UploadedSourceBytes = fileSize,
                UploadedNetworkBytes = payloadSize,
            };
        }

        public void Dispose()
        {
            Api.Dispose();
        }


    }
}
