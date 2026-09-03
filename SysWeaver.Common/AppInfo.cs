using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// https://github.com/SimpleStack/simplestack.orm



namespace SysWeaver
{
    /// <summary>
    /// Service used to change the application information such as display name etc
    /// </summary>
    public sealed class AppInfo
    {

        const String LogPrefix = "[AppInfo] ";

        public AppInfo(IMessageHost m, AppInfoParams p = null)
        {
            if (p == null)
            {
                m?.AddMessage(LogPrefix  + "No app info parameters supplied!", MessageLevels.Warning);
                return;
            }
            var name = PathTemplate.Resolve(p.AppName);
            var dispName = PathTemplate.Resolve(p.AppDisplayName);
            var desc = PathTemplate.Resolve(p.AppDescription);
            var lang = PathTemplate.Resolve(p.AppLanguage);
            if (name != null)
            {
                if (name != EnvInfo.AppName)
                {
                    m?.AddMessage(String.Concat(LogPrefix, nameof(EnvInfo), '.', nameof(EnvInfo.AppName), " changed to ", name.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppName = name;
                    dispName = dispName ?? StringTools.RemoveCamelCase(name, ' ', true);
                }
            }
            if (dispName != null)
            {
                if (dispName != EnvInfo.AppDisplayName)
                {
                    m?.AddMessage(String.Concat(LogPrefix, nameof(EnvInfo), '.', nameof(EnvInfo.AppDisplayName), " changed to ", dispName.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppDisplayName = dispName;
                }
            }
            if (desc != null)
            {
                if (desc != EnvInfo.AppDescription)
                {
                    m?.AddMessage(String.Concat(LogPrefix, nameof(EnvInfo), '.', nameof(EnvInfo.AppDescription), " changed to ", desc.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppDescription = desc;
                }
            }
            if (lang != null)
            {
                if (lang != EnvInfo.AppLanguage)
                {
                    m?.AddMessage(String.Concat(LogPrefix, nameof(EnvInfo), '.', nameof(EnvInfo.AppLanguage), " changed to ", lang.ToQuoted()), MessageLevels.Debug);
                    EnvInfo.AppLanguage = lang;
                }
            }
            EnvInfo.AppSeed = p.AppSeed;
            if (m != null)
            {
                m.AddMessage(String.Concat(LogPrefix, "OS name:         ", EnvInfo.OsName));
                m.AddMessage(String.Concat(LogPrefix, "OS platform:     ", EnvInfo.OsPlatform));
                m.AddMessage(String.Concat(LogPrefix, "OS version:      ", EnvInfo.OsVersion));
                m.AddMessage(String.Concat(LogPrefix, "OS architecture: ", RuntimeInformation.OSArchitecture.ToString()));
                m.AddMessage(String.Concat(LogPrefix, "Key folder:      ", Folders.KeyFolder.ToQuoted()));
                void w(String title, IReadOnlyList<String> folders)
                {
                    m.AddMessage(String.Concat(LogPrefix, title, folders.Count != 1 ? "s:" : ":"));
                    using (var t = m.Tab())
                    {
                        foreach (var f in folders)
                            m.AddMessage(String.Concat(LogPrefix, '"', f, '"'));
                    }
                }
                w("Current user application folder", Folders.UserAppFolders);
                w("Current user shared folder", Folders.UserSharedFolders);
                w("All users application folder", Folders.AllAppFolders);
                w("All users shared folder", Folders.AllSharedFolders);
            }

            ThreadPool.GetMinThreads(out int workerThreads, out int ioThreads);
            DefWorkerThreads = workerThreads;
            DefIoThreads = ioThreads;
            var s = p.ThreadPoolWorkerThreads;
            if (s == 0)
            {
                m.AddMessage(LogPrefix + "Using default minimum number of ThreadPool worker threads: " + workerThreads);
            }
            else
            {
                workerThreads = Math.Max(workerThreads, s > 0 ? s : (Environment.ProcessorCount * -s + 50) / 100);
                m.AddMessage(LogPrefix + "Setting the minimum number of ThreadPool worker threads to " + workerThreads + " (default: " + DefWorkerThreads + ")");
            }

            s = p.ThreadPoolIoThreads;
            if (s == 0)
            {
                m.AddMessage(LogPrefix + "Using default minimum number of ThreadPool IO threads: " + ioThreads);
            }
            else
            {
                ioThreads = Math.Max(ioThreads, s > 0 ? s : (Environment.ProcessorCount * -s + 50) / 100);
                m.AddMessage(LogPrefix + "Setting the minimum number of ThreadPool IO threads to " + ioThreads + " (default: " + DefIoThreads + ")");
            }
            if (workerThreads != DefWorkerThreads || ioThreads != DefIoThreads)
                ThreadPool.SetMinThreads(workerThreads, ioThreads);

        }


        readonly int DefWorkerThreads;
        readonly int DefIoThreads;

        readonly int UseWorkerThreads;
        readonly int UseIoThreads;


        public void Dispose()
        {
            var workerThreads = DefWorkerThreads;
            var ioThreads = DefIoThreads;
            if (workerThreads != UseWorkerThreads || ioThreads != UseIoThreads)
                ThreadPool.SetMinThreads(workerThreads, ioThreads);
        }

        public override string ToString() => String.Concat(
            "Name: ", EnvInfo.AppName
            , ", Display name: ", EnvInfo.AppDisplayName
            , ", Seed: ", EnvInfo.AppSeed
            , ", Description: ", EnvInfo.AppDescription
            );
    }


}
