using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{


    [RequiredDep<FolderSyncService>()]
    public sealed class ServerManagerService
    {
        public ServerManagerService(ServiceManager manager, ServerManagerParams p)
        {
            p = p ?? new ServerManagerParams();
            var s = manager.Get<FolderSyncService>();
            Manager = manager;
            Syncer = s;
            foreach (var f in p.Folders.Nullable())
            {
                f.Auth = f.Auth ?? p.SyncAuth;
                s.AddFolder(f);
            }

            var destFolders = PathTemplate.Resolve(String.IsNullOrEmpty(p.ServiceFolder) ? @"$(CommonApplicationData)\SysWeaver\ManagedServices" : p.ServiceFolder).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var f in destFolders)
            {
                PathExt.EnsureFolderExist(f);
                PathExt.AllowAllAccess(f);
            }
            var ss = Services;
            foreach (var f in p.Services.Nullable())
            {
                if (!ss.TryAdd(f.Name.FastToLower(), f))
                    throw new Exception("Must have a unique name!");
                var df = f.DiscFolder;
                if (String.IsNullOrEmpty(df))
                {
                    df = Path.GetFullPath(Folders.SelectFolder(destFolders, f.Name));
                    df = Path.Combine(df, f.Name, "bin");
                    PathExt.EnsureFolderExist(df);
                    PathExt.AllowAllAccess(df);
                    PathExt.AllowAllAccess(Path.Combine(df, f.Name));
                }
                else
                {
                    df = PathTemplate.Resolve(df);
                }
                var v = new FolderSyncFolder
                {
                    Name = f.Name,
                    DiscFolder = df,
                    Compress = p.CompressServices,
                    Auth = f.SyncAuth ?? p.SyncAuth,
                    RemoveBackupsDays = p.RemoveServiceBackupsDays,
                    OnNewFolderAsync = OnNewFolder,
                    OnActivateAsync = OnServiceActivate,
                    OnDeactivateAsync = OnServiceDeactivate,
                };
                s.AddFolder(v);
            }
        }

        readonly ConcurrentDictionary<String, ManagedService> Services = new ConcurrentDictionary<string, ManagedService>(StringComparer.Ordinal);
        readonly ServiceManager Manager;

        static String FindServiceExe(String path)
        {
            foreach (var x in Directory.GetFiles(path, "*.exe"))
            {
                var fn = Path.GetFileName(x).FastToLower();
                if (!fn.FastEquals("createdump.exe"))
                    return x.ToQuoted();
            }
            return null;
        }

        static HashSet<String> GetConfigs(String path, String name)
        {
            var h = new HashSet<String>(StringComparer.Ordinal);
            foreach (var x in Directory.GetFiles(path, name + ".*.json"))
            {
                var fn = Path.GetFileName(x);
                var l = fn.FastToLower();
                if (l.IndexOf(".lastgood.") >= 0)
                    continue;
                if (l.IndexOf(".deps.") >= 0)
                    continue;
                if (l.IndexOf(".replace.") >= 0)
                    continue;
                var bits = l.Split('.');
                var bl = bits.Length;
                if (bits.Length > 2)
                {
                    var date = bits[bl - 2];
                    var p = date.Replace('-', '_').Split('_');
                    if (p.Length == 6)
                    {
                        var ds = String.Concat(
                            p[0], '-',
                            p[1], '-',
                            p[2], ' ',
                            p[3], ':',
                            p[4], ':',
                            p[5]);
                        if (DateTime.TryParse(ds, out var res))
                            continue;
                    }
                }
                h.Add(fn);
            }
            foreach (var x in Directory.GetFiles(path, name + ".*.config"))
            {
                var fn = Path.GetFileName(x);
                var l = fn.FastToLower();
                h.Add(fn);
            }
            return h;
        }

        const String LogPrefix = "[ServerManager] ";

        async ValueTask<Exception> OnNewFolder(String name, String path, Func<String, ValueTask<int>> commandRunner)
        {
            if (!Services.TryGetValue(name.FastToLower(), out var ss))
                return null;
            if (!ss.MasterConfig)
                return null;
            var exe = FindServiceExe(path);
            if (exe == null)
                return null;
            var ename = Path.GetFileNameWithoutExtension(exe);
            var parent  = Path.GetDirectoryName(path);
            var existing = GetConfigs(parent, ename);
            var m = Manager;
            Exception ex = null;
            foreach (var config in GetConfigs(path, ename).OrderBy(x => x).ToList())
            {
                var master = Path.Combine(parent, config);
                var version = Path.Combine(path, config);


                if (existing.Remove(config))
                {
                    m.AddMessage(String.Concat(LogPrefix, "Replacing config \"", version, "\" with master config"));
                    ex = ex ?? await PathExt.TryCopyFileAsync(master, version).ConfigureAwait(false);
                }else
                {
                    m.AddMessage(String.Concat(LogPrefix, "Creating new master config from \"", version, '"'));
                    ex = ex ?? await PathExt.TryCopyFileAsync(version, master).ConfigureAwait(false);
                }
            }
            foreach (var config in existing.OrderBy(x => x).ToList())
            {
                var master = Path.Combine(parent, config);
                var version = Path.Combine(path, config);
                m.AddMessage(String.Concat(LogPrefix, "Creating new config from master \"", version, '"'));
                ex = ex ?? await PathExt.TryCopyFileAsync(master, version).ConfigureAwait(false);
            }
            return ex;
        }

        async ValueTask<Exception> OnServiceActivate(String name, String path, Func<String, ValueTask<int>> commandRunner)
        {
            var exe = FindServiceExe(path);
            if (exe == null)
                return null;
            var res = await commandRunner(exe + " start").ConfigureAwait(false);
            if (res < 0)
                return new Exception("Failed to start service \"" + name + "\", error: " + res);
            return null;
        }


        async ValueTask<Exception> OnServiceDeactivate(String name, String path, Func<String, ValueTask<int>> commandRunner)
        {
            var exe = FindServiceExe(path);
            if (exe == null)
                return null;
            var res = await commandRunner(exe + " uninstall").ConfigureAwait(false);
            if (res < 0)
                return new Exception("Failed to uninstall service \"" + name + "\", error: " + res);
            return null;
        }

        readonly FolderSyncService Syncer;

    }
}
