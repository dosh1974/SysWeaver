using CommunityToolkit.HighPerformance;
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

        static String GetRemoteFolderDest(FsRemoteFolder x)
        {
            var folder = x.DiscFolder;
            if (String.IsNullOrEmpty(folder))
            {
                var hash = MD5.HashData(String.Join('\n', x.RemoteAddress, x.Name).AsSpan().Cast<char, byte>());
                folder = hash.ToHexString();
                folder = Path.Combine(Folders.SelectFolder(Folders.AllAppFolders, folder), "RemoteFolders", String.Join('_', PathExt.SafeFilename(x.Name), folder), "Data");
            }
            else
            {
                folder = Path.GetFullPath(PathTemplate.Resolve(folder));
            }
            return folder;
        }


        const String Prefix = "[" + nameof(FolderSyncService) + "] ";




        /// <summary>
        /// Get the current version of a remote or shared folder
        /// </summary>
        /// <param name="folderName">The name of the remote or shared folder as declared in the config</param>
        /// <param name="context"></param>
        /// <returns>The version or null if not allowed or non-existent</returns>
        [WebApi]
        public String GetFolderVersion(String folderName, HttpServerRequest context)
        {
            folderName = folderName.FastToLower();
            if (!RemoteFolders.TryGetValue(folderName, out var fi))
            {
                if (!SharedFolders.TryGetValue(folderName, out var si))
                    return null;
                var ws = si.ModFolder;
                if (ws?.WebFolder == null)
                    return null;
                if (!context.Session.IsValid(si.Auth))
                    return null;
                return si.Files?.Item1;
            }
            var wf = fi.Folder.WebFolder;
            if (wf?.WebFolder == null)
                return null;
            if (!context.Session.IsValid(fi.Auth))
                return null;
            return fi.Version;
        }

        /// <summary>
        /// Sync a remote folder
        /// </summary>
        /// <param name="folderName">The name of the remote folder as declared in the config</param>
        [WebApiAuth(Roles.AdminOps)]
        [WebApi]
        public async Task SyncRemoteFolderNow(String folderName)
        {
            folderName = folderName.FastToLower();
            if (!RemoteFolders.TryGetValue(folderName, out var fi))
                throw new Exception("Unknown remote folder name!");
            var ex = await fi.TrySyncFolder().ConfigureAwait(false);
            if (ex != null)
                throw ex;
        }

        RemoteFolderData GetRemoteFolderData(RemoteFolder folder)
        {
            var f = folder.Folder;
            var ex = folder.Exceptions;
            var c = ex.LastTime;
            var data = new RemoteFolderData
            {
                Name = folder.Name,
                Version = folder.Version,
                RemoteAddress = folder.RemoteAddress,
                RemoteName = folder.RemoteName,
                DiscFolder = folder.DestPath,
                ExCount = ex.Count,
                ExLastTime = c == 0 ? DateTime.MinValue : new DateTime(c, DateTimeKind.Utc),
                LastException = ex.LastException?.ToString(),
            };
            var w = f.WebFolder;
            if (w != null)
            {
                data.WebFolder = w.WebFolder;
                data.ClientCacheDuration = w.ClientCacheDuration;
                data.RequestCacheDuration = w.RequestCacheDuration;
                data.MaxCacheSize = w.MaxCacheSize;
                data.Compression = w.Compression;
                data.AssumePreCompressed = w.AssumePreCompressed;
                data.Auth = w.Auth;
                data.UpdateAccessTime = w.UpdateAccessTime;
                data.IsDynamic = w.IsDynamic;
            }
            return data;
        }


        public IEnumerable<RemoteFolderData> GetRemoteFolderData()
            => RemoteFolders.Values.Select(GetRemoteFolderData);



        /// <summary>
        /// All shared folders as a table
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, HttpServerBase.MenuPath, "Remote folders", "Details about folders that are automatically synched from a remote service", "IconSync", 52)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData RemoteFoldersTable(TableDataRequest r)
        {
            if (r == null)
                r = new TableDataRequest();
            return TableDataTools.Get(r, 5000, GetRemoteFolderData());
        }


    }

}
