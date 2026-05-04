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

        static String GetRemoteFolderDest(RemoteCachedFolder x)
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


        readonly ExceptionTracker RemoteSyncExceptions = new ();

        const String Prefix = "[" + nameof(FolderSyncService) + "] ";

        async ValueTask TrySyncFolder(RemoteFolder folder)
        {
            var f = folder.Folder;
            Manager.AddMessage(String.Concat(Prefix, "Synching folder \"", folder.DestPath, "\" from \"", f.Name, "\" on \"", f.RemoteAddress, '"'));
            using var __ = Manager.Tab();
            try
            {
                using var syncer = new FolderSyncer(new FolderSyncerParams
                {
                    CredFile = f.CredFile,
                    IgnoreCertErrors = false,
                    MaxConcurrency = -1,
                    Password = f.Password,
                    User = "service",
                    Server = f.RemoteAddress,
                });
                var res = await syncer.PullFolder(f.Name, folder.DestPath, true, true, (e, s) =>
                {
                    Manager.AddMessage(String.Concat(Prefix, e, ": ", s), MessageLevels.Debug);
                }).ConfigureAwait(false);
                if (res == null)
                    return;
                folder.Version = res.Version;
                RemoteSyncExceptions.OnException(res.Errors?.FirstOrDefault());
            }
            catch (Exception ex)
            {
                RemoteSyncExceptions.OnException(ex);
            }
        }


    }

}
