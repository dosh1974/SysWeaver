using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.FolderSync;
using SysWeaver.Net;
using SysWeaver.Serialization;


namespace SysWeaver.MicroService
{
    public partial class FolderSyncService
    {

        #region Shared folder

        async ValueTask<bool> ScanSharedFolders()
        {
            foreach (var x in SharedFolders.Values)
            {
                if (!SystemLock.TryGet(x.LockName, out var l))
                    continue;
                using (l)
                {
                    try
                    {
                        if (await x.UpdateFiles().ConfigureAwait(false))
                            Manager.AddMessage(String.Concat(LogPrefix, "Change detected in shared folder \"", x.DestPath, "\", new version = ", x.Files.Item1), MessageLevels.Debug);
                    }
                    catch
                    {
                    }
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
            return true;
        }



        /// <summary>
        /// Check if a new version of a shared folder is available
        /// </summary>
        /// <param name="r">Folder name and version (aka hash)</param>
        /// <param name="context"></param>
        /// <returns>True if a new version is available</returns>
        [WebApi]
        [WebApiAuth("")]
        public bool SharedFolderHasChanged(SharedFolderSyncRequest r, HttpServerRequest context)
        {
            var folderName = r.Folder.FastToLower();
            if (!SharedFolders.TryGetValue(folderName, out var target))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            if (SystemLock.IsLocked(target.LockName))
                return false;
            var filesD = target.Files;
            if (filesD == null)
                throw new Exception("Target folder doesn't exist!");
            var version = filesD.Item1;
            return !version.FastEquals(r.Version);
        }


        /// <summary>
        /// Wait until a a new version of a shared folder is available (or the request time's out)
        /// </summary>
        /// <param name="r">Folder name and version (aka hash)</param>
        /// <param name="context"></param>
        /// <returns>True if a new version is available</returns>
        [WebApi]
        [WebApiAuth("")]
        public async Task<bool> WaitUntilSharedFolderHasChanged(SharedFolderSyncRequest r, HttpServerRequest context)
        {
            var folderName = r.Folder.FastToLower();
            if (!SharedFolders.TryGetValue(folderName, out var target))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            var version = r.Version;
            try
            {
                var newVersion = await target.Changer.WaitForChange(version, (5 * 60 - 10) * 1000).ConfigureAwait(false);
                return !newVersion.FastEquals(version);
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Check for updates against a shared folder
        /// </summary>
        /// <param name="r">Folder and local files</param>
        /// <param name="context"></param>
        /// <returns>Changes required to sync the local folder</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth("")]
        public SharedFolderDiff CheckSharedFolder(LocalFolderInfo r, HttpServerRequest context)
        {
            DateTime start = DateTime.UtcNow;
            var folderName = r.Folder.FastToLower();
            if (!SharedFolders.TryGetValue(folderName, out var target))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            if (SystemLock.IsLocked(target.LockName))
                throw new Exception("The folder is being updated!");
            var filesD = target.Files;
            if (filesD == null)
                throw new Exception("Target folder doesn't exist!");
            var version = filesD.Item1;
            var files = filesD.Item2;
            Dictionary<String, FolderSyncFile> localFiles = new(StringComparer.Ordinal);
            foreach (var x in r.Files.Nullable())
                localFiles.TryAdd(x.Name, x);
            var dest = target.DestPath;
            var fileCount = files.Count;
            List<FolderSyncFile> download = new(fileCount);
            List<String> keep = new(fileCount);
            foreach (var file in files.Values)
            {
                var fi = file.File;
                var fn = fi.Name;
                if (localFiles.TryRemove(fn, out var f))
                {
                    if (fi.Hash.FastEquals(f.Hash))
                    {
                        keep.Add(fn);
                        continue;
                    }
                }
                download.Add(fi);
            }
            return new SharedFolderDiff
            {
                Version = version,
                Download = download.Count > 0 ? download.ToArray() : null,
                Keep = keep.Count > 0 ? keep.ToArray() : null,
                Cdc = CdcProps.Default.Key.FastEquals(r.Cdc) ? CdcProps.Default.Key : null,
            };
        }


        /// <summary>
        /// Get the list of chunk hashes for a file
        /// </summary>
        /// <param name="r">Folder and file to get hash chunks for</param>
        /// <param name="context"></param>
        /// <returns>Hashes for the chunks that make up the file</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth("")]
        [WebApiRaw(MimeTypeMap.Data)]
        public ReadOnlyMemory<Byte> GetSharedFileChunks(SharedFileChunksRequest r, HttpServerRequest context)
        {
            DateTime start = DateTime.UtcNow;
            var folderName = r.Folder.FastToLower();
            if (!SharedFolders.TryGetValue(folderName, out var target))
                throw new Exception("Unknown folder id");
            if (!context.Session.IsValid(target.Auth))
                throw new Exception("Not authorized!");
            if (SystemLock.IsLocked(target.LockName))
                throw new Exception("The folder is being updated!");
            var filesD = target.Files;
            if (filesD == null)
                throw new Exception("Target folder doesn't exist!");
            var version = filesD.Item1;
            if (!version.FastEquals(r.Version))
                throw new Exception("A newer version is available!");
            var files = filesD.Item2;
            if (!files.TryGetValue(r.File, out var fi))
                throw new Exception("Unknown file!");
            return fi.ChunkHashes;
        }

        /// <summary>
        /// Request contains a list of chunk hashes as binary data
        /// </summary>
        /// <param name="context"></param>
        /// <returns>A stream of chunks as binary data</returns>
        async ValueTask<ReadOnlyMemory<Byte>> GetChunks(HttpServerRequest context)
        {
            using var _ = PerfMon.Track(nameof(GetChunks));
            var dataMem = await context.InputStream.ReadAllUnmanagedMemoryAsync(false).ConfigureAwait(false);
            var data = dataMem.Memory;
            var props = CdcProps.Default;
            async ValueTask<ReadOnlyMemory<Byte>> Read(String x = null)
            {
                using var __ = PerfMon.Track(nameof(GetChunks) + '.' + nameof(Read));
                using var ms = new MemoryStream();
                if (!await ContentDependentChunking.TryWriteChunkList(ms, data).ConfigureAwait(false))
                    throw new Exception("Got missing chunks!");
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Position);
            }
            if (data.Length > 2048)
            {
                using var __ = PerfMon.Track(nameof(GetChunks) + ".Big");
                return await Read().ConfigureAwait(false);
            }
            Span<Byte> hashData = stackalloc Byte[SHA256.HashSizeInBytes];
            SHA256.HashData(data.Span, hashData);
            var hash = hashData.ToHexString();
            return await ChunkDataListCache.GetOrUpdateValueAsync(hash, Read).ConfigureAwait(false);
        }

        readonly FastMemCache<String, ReadOnlyMemory<Byte>> ChunkDataListCache = new(TimeSpan.FromMinutes(5), StringComparer.Ordinal);

        SharedFolderData GetPullFolderData(SharedFolder folder)
        {

            var uploadName = folder.Name;
            var path = folder.DestPath;
            var a = folder.Auth;
            var data = new SharedFolderData
            {
                Name = uploadName,
                Version = folder.Files?.Item1,
                DiscFolder = path,
                Auth = a == null ? null : String.Join(',', a),
                Folder = folder,
            };
            return data;
        }


        public IEnumerable<SharedFolderData> GetSharedFolderData()
            => SharedFolders.Values.Select(GetPullFolderData);


        /// <summary>
        /// All shared folders as a table
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, HttpServerBase.MenuPath, "Shared folders", "Details about folders that can be synched (downloaded)", "IconSync", 51)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData SharedFoldersTable(TableDataRequest r)
        {
            if (r == null)
                r = new TableDataRequest();
            return TableDataTools.Get(r, 5000, GetSharedFolderData());
        }


        #endregion//Shared folder

    }

}
