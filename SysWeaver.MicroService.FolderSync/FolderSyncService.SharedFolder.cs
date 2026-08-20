using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.FolderSync;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        internal sealed class SharedFolder
        {
            public readonly String LockName;
            public readonly String Name;
            public readonly String DestPath;
            public readonly IReadOnlyList<String> Auth;
            public readonly FileHttpServerModuleFolder ModFolder;
            public readonly BlockUntilStringValueChange Changer = new BlockUntilStringValueChange();

            public SharedFolder(string name, string path, string auth)
            {
                Name = name;
                var tp = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                DestPath = tp + Path.DirectorySeparatorChar;
                Auth = Authorization.GetRequiredTokens(auth);
                LockName = "FolderSync_" + Encoding.UTF8.GetBytes(name.FastToLower()).ToHex();
                ModFolder = new FileHttpServerModuleFolder
                {
                    AssumePreCompressed = true,
                    Auth = auth == null ? null : String.Join(',', auth),
                    ClientCacheDuration = 5,
                    RequestCacheDuration = 4,
                    WebFolder = String.Concat("FolderSync/", nameof(FolderSyncParams.SharedFolders), '/', name),
                    DiscFolder = tp,
                };
            }

            public async Task<bool> UpdateFiles()
            {
                var files = new ConcurrentDictionary<String, SharedFile>(StringComparer.Ordinal);
                try
                {
                    var dp = DestPath;
                    var dl = dp.Length;
                    var src = Directory.GetFiles(dp, "*", SearchOption.AllDirectories);
                    var minAge = DateTime.UtcNow.AddSeconds(-3);
                    bool error = false;
                    await src.ProcessAsyncValue(async fn =>
                    {
                        var fi = new FileInfo(fn);
                        if (fi.LastWriteTimeUtc > minAge)
                            error = true;
                        if (fi.CreationTimeUtc > minAge)
                            error = true;
                        var lname = fn.Substring(dl).Replace(Path.DirectorySeparatorChar, '/');
                        var hash = await FileHash.GetHashAsync(fn).ConfigureAwait(false);
                        var data = await ContentDependentChunking.Cut(fn, CdcProps.Default).ConfigureAwait(false);
                        files.TryAdd(lname, new SharedFile(new FolderSyncFile
                        {
                            Name = lname,
                            Hash = hash,
                            LastModified = fi.LastWriteTimeUtc,
                        }, data));
                    }).ConfigureAwait(false);
                    if (error)
                        return false;
                }
                catch
                {
                }
                var t = files.Values.Select(x => x.File).OrderBy(x => x.Name).ToList();
                var version = FolderSyncer.ComputeCombinedHash(t.Select(x => x.Name).ToList(), t.Select(x => x.Hash).ToList());
                if (!(Files?.Item1?.FastEquals(version) ?? false))
                {
                    Files = Tuple.Create(version, files.Freeze());
                    Changer.Change(version);
                    return true;
                }
                return false;
            }

            public volatile Tuple<String, IReadOnlyDictionary<String, SharedFile>> Files;


        }
    }

}
