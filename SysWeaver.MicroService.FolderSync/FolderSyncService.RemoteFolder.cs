using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.FolderSync;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        internal sealed class RemoteFolder : IDisposable
        {
            public readonly String Name;
            public readonly String DestPath;
            public readonly FsRemoteFolder Folder;
            public readonly IReadOnlyList<String> Auth;
            public readonly String RemoteAddress;
            public readonly String RemoteName;
            public readonly FolderSyncService S;

            public readonly String WebFolder;
            public readonly WebFolderSwapMethods SwapMethod;

            public readonly TimeSpan DeleteAfter;

            public String Version;

            readonly AsyncLock Lock = new AsyncLock();
            readonly ServiceManager Manager;
            public readonly FolderSyncerParams SyncParams;

            public RemoteFolder(String name, FsRemoteFolder folder, String destPath, FolderSyncService s)
            {
                DeleteAfter = TimeSpan.FromHours(Math.Max(folder.DeleteAfterHours, 1));
                Name = name;
                Folder = folder;
                DestPath = destPath;
                Auth = AuthTools.GetList(folder.WebFolder?.Auth);
                RemoteAddress = EnvInfo.ResolveText(folder.RemoteAddress);
                RemoteName = String.IsNullOrEmpty(folder.RemoteName) ? folder.Name : EnvInfo.ResolveText(folder.RemoteName);
                S = s;
                Manager = s.Manager;
                var wf = folder.WebFolder?.WebFolder;
                if (String.IsNullOrEmpty(wf))
                    wf = null;
                if (s.FileMod == null)
                    wf = null;
                wf = wf?.TrimEnd('/');
                WebFolder = wf;
                var sm = folder.SwapMethod;
                if (sm == WebFolderSwapMethods.IFramePage)
                    if (s.StaticMod == null)
                        sm = WebFolderSwapMethods.None;
                if (wf == null)
                    sm = WebFolderSwapMethods.None;
                SwapMethod = sm;
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

            public async ValueTask<Exception> TrySyncFolder(FolderSyncer syncer, bool first = false)
            {
                using var _ = await Lock.Lock().ConfigureAwait(false);
                var m = Manager;
                m.AddMessage(String.Concat(Prefix, "Synching folder \"", DestPath, "\" from \"", RemoteName, "\" on \"", RemoteAddress, '"'));
                var folder = Folder;
                var fn = WebFolder;
                var swapMethod = SwapMethod;
                try
                {
                    using var __ = Manager.Tab();
                    var res = await syncer.PullFolder(RemoteName, DestPath, true, true, (e, s) =>
                    {
                        m.AddMessage(String.Concat(Prefix, e, ": ", s), MessageLevels.Debug);
                    },
                    async (bak, oldVersion) =>
                    {
                        if (first)
                            return null;
                        if (swapMethod == WebFolderSwapMethods.None)
                            return null;
                        //  Use the new backup folder as the "main" folder
                        if (!S.FileMod.ChangeDiscFolder(String.Concat(WebFolder, '_', oldVersion), DestPath, bak))
                            return new Exception("Failed to change folder!");
                        //  Wait a small amount so that ongoing request may finish
                        await Task.Delay(100).ConfigureAwait(false);
                        return null;
                    }
                    ).ConfigureAwait(false);
                    if (res == null)
                    {
                        m.AddMessage(Prefix + "Failed to pull folder", MessageLevels.Warning);
                        return null;
                    }
                    var newVersion = res.Version;
                    var exx = res.Errors?.FirstOrDefault();
                    Exceptions.OnException(exx);
                    if (exx != null)
                    {
                        m.AddMessage(Prefix + "Failed to pull folder", exx, MessageLevels.Warning);
                    }
                    else
                    {
                        //  Add the new folder
                        if ((!first) && (swapMethod != WebFolderSwapMethods.None))
                        {
                            var fm = S.FileMod;
                            var d = new FileHttpServerModuleFolder
                            {
                                DiscFolder = DestPath,
                            };
                            folder.WebFolder.CopyTo(d);
                            d.WebFolder = String.Concat(fn, '_', newVersion);
                            fm.AddFolder(d);
                            if (swapMethod == WebFolderSwapMethods.IFramePage)
                            {
                                var files = folder.HtmlShims.ArrayOrNullIfEmpty() ?? ["index.html"];
                                var rd = S.RedirectTemplate;
                                var sm = S.StaticMod;
                                foreach (var file in files)
                                    AddIFrame(file, fn, this, newVersion, rd, sm);
                            }
                            else
                            {
                                var httpServer = S.HttpServer;
                                if (httpServer != null)
                                    S.RemoteOnHttpServerAdd(httpServer, this, true);
                            }
                        }
                        Version = newVersion;
                        m.AddMessage(String.Concat(Prefix, "Folder updated to version ", newVersion));
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

        }
    }

}
