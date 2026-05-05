using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.FolderSync;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        internal sealed class ManagedFolder
        {


            /// <summary>
            /// Optional commands to execute before deactivating (before old folder is renamed to back-up name)
            /// </summary>
            public readonly String[] OnDeactivate;

            /// <summary>
            /// Optional commands to execute to activate (after the folder have been replaced with old content)
            /// </summary>
            public readonly String[] OnActivate;

            /// <summary>
            /// Optional commands to execute when a new folder is uploaded
            /// </summary>
            public readonly String[] OnNewFolder;

            public FsManagedFolder.ActivationHandler OnActivateAsync;
            public FsManagedFolder.ActivationHandler OnDeactivateAsync;
            public FsManagedFolder.ActivationHandler OnNewFolderAsync;

            public readonly String LockName;
            public readonly String Name;
            public readonly String DestPath;
            public readonly IReadOnlyList<String> Auth;
            public TimeSpan RemoveAfter;
            public readonly FileHttpServerModuleFolder ModFolder;

            /// <summary>
            /// If true, folder versions are compressed.
            /// Activating (swapping) is slower but disc usage is reduced a lot (especially for many versions).
            /// </summary>
            public readonly bool Compress;

            public readonly FsSharedFolder PullFolder;

            static String[] ParseCommands(String s, IReadOnlyDictionary<String, String> extra)
            {
                if (String.IsNullOrEmpty(s))
                    return null;
                var r = s.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var l = r.Length;
                if (l <= 0)
                    return null;
                for (int i = 0; i < l; ++i)
                    r[i] = PathTemplate.Resolve(r[i], extra);
                return r;
            }

            public ManagedFolder(string name, string path, string auth, TimeSpan removeAfter, FsManagedFolder fs, FsSharedFolder pullFolder)
            {
                PullFolder = pullFolder;
                Name = name;
                var tp = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                DestPath = tp + Path.DirectorySeparatorChar;
                Auth = Authorization.GetRequiredTokens(auth);
                RemoveAfter = removeAfter;
                LockName = "FolderSync_" + Encoding.UTF8.GetBytes(name.FastToLower()).ToHex();
                ModFolder = new FileHttpServerModuleFolder
                {
                    AssumePreCompressed = true,
                    Auth = Roles.AdminOps,
                    ClientCacheDuration = 5,
                    RequestCacheDuration = 4,
                    WebFolder = String.Concat("FolderSync/", nameof(FolderSyncParams.ManagedFolders), '/', name),
                    DiscFolder = tp,
                };
                var x = new Dictionary<String, String>(StringComparer.Ordinal)
                {
                    { "name", name },
                    { "target", tp },
                    { "targetname", Path.GetFileName(tp) },
                    { "targetdir", Path.GetDirectoryName(tp) }
                };
                OnActivate = ParseCommands(fs.OnActivate, x);
                OnDeactivate = ParseCommands(fs.OnDeactivate, x);
                OnNewFolder = fs.OnNewFolder?.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                OnActivateAsync = fs.OnActivateAsync;
                OnDeactivateAsync = fs.OnDeactivateAsync;
                OnNewFolderAsync = fs.OnNewFolderAsync;
                Compress = fs.Compress;
            }

        }



        internal sealed class SharedFile
        {
            public override string ToString() => File.Name;
            public readonly FolderSyncFile File;
            public readonly ReadOnlyMemory<Byte> ChunkHashes;

            public SharedFile(FolderSyncFile file, ReadOnlyMemory<byte> chunkHashes)
            {
                File = file;
                ChunkHashes = chunkHashes;
            }
        }

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

            public async ValueTask<bool> UpdateFiles()
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





        internal sealed class RemoteFolder : IDisposable
        {
            public readonly String Name;
            public readonly String DestPath;
            public readonly FsRemoteFolder Folder;
            public readonly IReadOnlyList<String> Auth;
            public readonly String RemoteAddress;
            public readonly String RemoteName;


            public String Version;

            readonly AsyncLock Lock = new AsyncLock();
            readonly ServiceManager Manager;
            readonly FolderSyncerParams SyncParams;

            public RemoteFolder(String name, FsRemoteFolder folder, String destPath, ServiceManager manager)
            {
                Name = name;
                Folder = folder;
                DestPath = destPath;
                Auth = AuthTools.GetList(folder.WebFolder?.Auth);
                RemoteAddress = EnvInfo.ResolveText(folder.RemoteAddress);
                RemoteName = String.IsNullOrEmpty(folder.RemoteName) ? folder.Name : EnvInfo.ResolveText(folder.RemoteName);
                Manager = manager;
                SyncParams = new FolderSyncerParams
                {
                    CredFile = folder.CredFile,
                    IgnoreCertErrors = false,
                    MaxConcurrency = -1,
                    Password = folder.Password,
                    User = folder.User,
                    Server = RemoteAddress,
                };
            }


            public readonly ExceptionTracker Exceptions = new();


            async ValueTask<Exception> TrySyncFolder(FolderSyncer syncer)
            {
                using var _ = await Lock.Lock().ConfigureAwait(false);
                var m = Manager;
                m.AddMessage(String.Concat(Prefix, "Synching folder \"", DestPath, "\" from \"", RemoteName, "\" on \"", RemoteAddress, '"'));
                try
                {
                    using var __ = Manager.Tab();
                    var f = Folder;
                    var res = await syncer.PullFolder(RemoteName, DestPath, true, true, (e, s) =>
                    {
                        m.AddMessage(String.Concat(Prefix, e, ": ", s), MessageLevels.Debug);
                    }).ConfigureAwait(false);
                    if (res == null)
                    {
                        m.AddMessage(Prefix + "Failed to pull folder", MessageLevels.Warning);
                        return null;
                    }
                    Version = res.Version;
                    var exx = res.Errors?.FirstOrDefault();
                    Exceptions.OnException(exx);
                    if (exx != null)
                    {
                        m.AddMessage(Prefix + "Failed to pull folder", exx, MessageLevels.Warning);
                    }
                    else
                    {
                        m.AddMessage(String.Concat(Prefix, "Folder updated to version ", res.Version));

                    }
                    return exx;
                }
                catch (Exception ex)
                {
                    Exceptions.OnException(ex);
                    m.AddMessage(Prefix + "Failed to pull folder", ex, MessageLevels.Warning);
                    return ex;
                }
            }

            public async ValueTask<Exception> TrySyncFolder()
            {
                using var syncer = new FolderSyncer(SyncParams);
                return await TrySyncFolder(syncer).ConfigureAwait(false);
            }



            PeriodicTask Updater;

            public void StartUpdater()
            {
                Updater = new PeriodicTask(UpdateChecker, 1000, true, true, true);
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref Updater, null)?.Dispose();
            }


            async ValueTask<bool> UpdateChecker(CancellationToken cs)
            {
                try
                {
                    Exception exx = null;
                    using (var syncer = new FolderSyncer(SyncParams))
                    {
                        var u = Updater;
                        Action stop = () => syncer.RemoteConnection.Cancel();
                        u.OnStopping += stop;
                        try
                        {
                            if (await syncer.WaitPullFolder(Name, Version).ConfigureAwait(false))
                                exx = await TrySyncFolder(syncer).ConfigureAwait(false);
                        }
                        finally
                        {
                            u.OnStopping -= stop;
                        }
                    }
                    if (exx != null)
                    {
                        Exceptions.OnException(exx);
                        await Task.Delay(10000, cs).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Exceptions.OnException(ex);
                    await Task.Delay(10000, cs).ConfigureAwait(false);
                }
                return true;
            }

            private void U_OnStopping()
            {
                throw new NotImplementedException();
            }
        }
    }

}
