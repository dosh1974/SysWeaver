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


        static void AddIFrame(String file, String fn, RemoteFolder f, String version, TextTemplate redirectTemplate, StaticDataHttpServerModule sm)
        {
            var x = f.Folder;
            var srcName = String.Concat(fn, '/', file);
            var toRootCount = srcName.Count(x => x == '/');
            var toRoot = StringTools.Create("../", toRootCount);
            Dictionary<String, String> vars = new(StringComparer.Ordinal);
            vars["ToRoot"] = toRoot;
            vars["Url"] = String.Concat("../", fn.Substring(fn.LastIndexOf('/') + 1), '_', version, '/', file);
            if (x.VersionCheck)
            {
                vars["GetVersion"] = String.Concat(toRoot, "FolderSync/GetFolderVersion?\'", x.Name, "\'");
                vars["AutoReload"] = x.AutoReload <= 0 ? "0" : ("" + (x.AutoReload * 1000));
            }else
            {
                vars["GetVersion"] = "";
                vars["AutoReload"] = "0";
            }
            var html = redirectTemplate.Get(vars);
            sm.AddText(srcName,
                "Redirect page from " + nameof(FolderSyncService),
                html,
                MimeTypeMap.Html,
                null,
                0,
                null,
                false,
                DateTime.UtcNow,
                null,
                null,
                true);
        }

        static void RemoveIFrame(String file, String fn, StaticDataHttpServerModule sm)
        {
            var srcName = String.Concat(fn, '/', file);
            sm.Remove(srcName);
        }

        public async Task<String> AddRemoteFolder(FsRemoteFolder x)
        {

            var folder = GetRemoteFolderDest(x);
            var folders = RemoteFolders;
            var name = EnvInfo.ResolveText(x.Name);
            var f = new RemoteFolder(name, x, folder, this);
            if (!folders.TryAdd(name.FastToLower(), f))
                throw new Exception(String.Concat("A folder named \"", name, "\" already added!"));
            await PathExt.EnsureFolderExistAsync(folder).ConfigureAwait(false);
            if (x.SyncOnStart)
            {
                using var syncer = new FolderSyncer(f.SyncParams);
                await f.TrySyncFolder(syncer, true).ConfigureAwait(false);
            }
            else
                f.Version = await FolderSyncer.GetPullFolderVersion(folder).ConfigureAwait(false);
            var wf = x.WebFolder;
            var fn = f.WebFolder;
            if (fn != null)
            {
                var fm = FileMod;
                var d = new FileHttpServerModuleFolder
                {
                    DiscFolder = folder,
                };
                wf.CopyTo(d);
                var swapMethod = f.SwapMethod;
                if (swapMethod == WebFolderSwapMethods.IFramePage)
                {
                    var files = x.HtmlShims.ArrayOrNullIfEmpty() ?? ["index.html"];
                    var rd = RedirectTemplate;
                    var sm = StaticMod;
                    var version = f.Version;
                    foreach (var file in files)
                        AddIFrame(file, fn, f, version, rd, sm);
                }
                if (swapMethod != WebFolderSwapMethods.None)
                {
                    d.WebFolder = String.Concat(fn, '_', f.Version);
                    fm.AddFolder(d);
                    foreach (var versionFolder in Directory.GetDirectories(Path.GetDirectoryName(folder), Path.GetFileName(folder) + "_*", SearchOption.TopDirectoryOnly))
                    {
                        var version = versionFolder.Substring(versionFolder.LastIndexOf('_') + 1);
                        if (version.Length != 32)
                            continue;

                        d = new FileHttpServerModuleFolder
                        {
                            DiscFolder = versionFolder,
                        };
                        wf.CopyTo(d);
                        d.WebFolder = String.Concat(fn, '_', version);
                        fm.AddFolder(d);
                    }
                }else
                {
                    fm.AddFolder(d);
                }
            }
            f.StartUpdater();
            return folder;
        }

        public bool RemoveRemoteFolder(FsRemoteFolder x)
        {
            var folders = RemoteFolders;
            var name = EnvInfo.ResolveText(x.Name);
            if (!folders.TryRemove(name.FastToLower(), out var f))
                return false;
            var folder = f.DestPath;
            f.Dispose();
            var wf = x.WebFolder;
            var fn = f.WebFolder;
            if (fn != null)
            {
                var fm = FileMod;
                var d = new FileHttpServerModuleFolder
                {
                    DiscFolder = folder,
                };
                wf.CopyTo(d);
                var swapMethod = f.SwapMethod;
                if (swapMethod == WebFolderSwapMethods.IFramePage)
                {
                    var files = x.HtmlShims.ArrayOrNullIfEmpty() ?? ["index.html"];
                    var sm = StaticMod;
                    foreach (var file in files)
                        RemoveIFrame(file, fn, sm);
                }
                if (swapMethod != WebFolderSwapMethods.None)
                {
                    d.WebFolder = String.Concat(fn, '_', f.Version);
                    fm.AddFolder(d);
                    foreach (var versionFolder in Directory.GetDirectories(Path.GetDirectoryName(folder), Path.GetFileName(folder) + "_*", SearchOption.TopDirectoryOnly))
                    {
                        var version = versionFolder.Substring(versionFolder.LastIndexOf('_') + 1);
                        if (version.Length != 32)
                            continue;

                        d = new FileHttpServerModuleFolder
                        {
                            DiscFolder = versionFolder,
                        };
                        wf.CopyTo(d);
                        d.WebFolder = String.Concat(fn, '_', version);
                        fm.RemoveFolder(d);
                    }
                }
                else
                {
                    fm.RemoveFolder(d);
                }
            }


            return true;
        }


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

        void RemoteOnHttpServerAdd(HttpServerBase server, RemoteFolder f, bool replace = false)
        {
            var fn = f.WebFolder;
            server.AddFolderRedirect(fn + '/', String.Concat(fn, '_', f.Version, '/'), 307, replace);
        }


        void RemoteOnHttpServerAdd(HttpServerBase server)
        {
            foreach (var f in RemoteFolders.Values)
            {
                if (f.SwapMethod != WebFolderSwapMethods.HttpRedirect)
                    continue;
                RemoteOnHttpServerAdd(server, f);
            }
        }

        void RemoteOnHttpServerRemove(HttpServerBase server)
        {
            foreach (var f in RemoteFolders.Values)
            {
                var x = f.Folder;
                if (f.SwapMethod != WebFolderSwapMethods.HttpRedirect)
                    continue;
                var fn = f.WebFolder;
                server.RemoveFolderRedirect(fn + '/');
            }
        }

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
                DeleteAfterHours = Math.Max(f.DeleteAfterHours, 1),
                SwapMethod = folder.SwapMethod.ToString().RemoveCamelCase(),
                VersionCheck = f.VersionCheck,
                AutoReload = Math.Max(0, f.AutoReload),
            };
            var wn = folder.WebFolder;
            if (wn != null)
            {
                var w = f.WebFolder;
                data.IsServed = true;
                data.WebFolder = wn;
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


        readonly ExceptionTracker PruneErrors = new ExceptionTracker(); 

        async Task PruneRemoteFolders()
        {
            using var _ = PerfMon.Track(nameof(PruneRemoteFolders));
            foreach (var x in RemoteFolders.Values)
            {

                var target = x.DestPath;
                var folder = Path.GetDirectoryName(target);
                var name = Path.GetFileName(target);
                var deleteAt = DateTime.UtcNow - x.DeleteAfter;
                foreach (var versionFolder in Directory.GetDirectories(folder, name + "_*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var lastChanged = new DirectoryInfo(versionFolder).LastWriteTimeUtc;
                        if (lastChanged >= deleteAt)
                            continue;
                        foreach (var f in Directory.GetFiles(versionFolder, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var lv = new FileInfo(f).LastWriteTimeUtc;
                                if (lv <= lastChanged)
                                    continue;
                                lastChanged = lv;
                                if (lastChanged >= deleteAt)
                                    break;
                            }
                            catch (Exception ex2)
                            {
                                PruneErrors.OnException(ex2);
                            }
                        }
                        if (lastChanged >= deleteAt)
                            continue;
                        if (x.SwapMethod != WebFolderSwapMethods.None)
                        {
                            var version = versionFolder.Substring(versionFolder.LastIndexOf('_') + 1);
                            if (version.Length == 32)
                            {
                                var d = new FileHttpServerModuleFolder
                                {
                                    DiscFolder = versionFolder,
                                };
                                x.Folder.WebFolder.CopyTo(d);
                                d.WebFolder = String.Concat(x.WebFolder, '_', version);
                                FileMod.RemoveFolder(d);
                                await Task.Delay(100).ConfigureAwait(false);
                            }
                        }
                        var ex = await PathExt.TryDeleteDirectoryAsync(versionFolder, false).ConfigureAwait(false);
                        if (ex == null)
                            Manager.AddMessage(String.Concat("Removed old remote folder \"", versionFolder, '"'));
                        else
                            PruneErrors.OnException(ex);
                    }
                    catch (Exception ex)
                    {
                        PruneErrors.OnException(ex);
                    }
                }

            }

        }


    }

}
