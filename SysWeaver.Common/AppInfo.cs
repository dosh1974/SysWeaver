using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// https://github.com/SimpleStack/simplestack.orm



namespace SysWeaver
{
    /// <summary>
    /// Service used to change the application information such as display name etc.
    /// Constructing an instance applies the supplied <see cref="AppInfoParams"/> to the process wide <see cref="EnvInfo"/> properties,
    /// logs some environment information and adjusts the minimum number of thread pool threads.
    /// </summary>
    /// <remarks>
    /// The changes are global (static state), so only one instance should be created (typically by the service manager at startup).
    /// <see cref="Dispose"/> restores the thread pool minimums but not the <see cref="EnvInfo"/> values.
    /// </remarks>
    public sealed class AppInfo
    {

        const String LogPrefix = "[AppInfo] ";

        /// <summary>
        /// Apply the application information.
        /// </summary>
        /// <param name="m">Optional message host used for logging (may be null).</param>
        /// <param name="p">The parameters to apply, if null a warning is logged and nothing is changed.
        /// Name, display name, description and language support <see cref="PathTemplate"/> variables.
        /// If the name changes and no display name is given, the display name is derived from the name (camel case split).</param>
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
                m?.AddMessage(LogPrefix + "Using default minimum number of ThreadPool worker threads: " + workerThreads);
            }
            else
            {
                workerThreads = Math.Max(workerThreads, s > 0 ? s : (Environment.ProcessorCount * -s + 50) / 100);
                m?.AddMessage(LogPrefix + "Setting the minimum number of ThreadPool worker threads to " + workerThreads + " (default: " + DefWorkerThreads + ")");
            }

            s = p.ThreadPoolIoThreads;
            if (s == 0)
            {
                m?.AddMessage(LogPrefix + "Using default minimum number of ThreadPool IO threads: " + ioThreads);
            }
            else
            {
                ioThreads = Math.Max(ioThreads, s > 0 ? s : (Environment.ProcessorCount * -s + 50) / 100);
                m?.AddMessage(LogPrefix + "Setting the minimum number of ThreadPool IO threads to " + ioThreads + " (default: " + DefIoThreads + ")");
            }
            if (workerThreads != DefWorkerThreads || ioThreads != DefIoThreads)
                ThreadPool.SetMinThreads(workerThreads, ioThreads);
            UseWorkerThreads = workerThreads;
            UseIoThreads = ioThreads;
        }


        readonly int DefWorkerThreads;
        readonly int DefIoThreads;

        readonly int UseWorkerThreads;
        readonly int UseIoThreads;


        /// <summary>
        /// Restore the minimum number of thread pool threads to the values that were in effect before this instance was created.
        /// </summary>
        /// <remarks>Note that this class doesn't implement <see cref="IDisposable"/>, so this must be called explicitly.</remarks>
        public void Dispose()
        {
            var workerThreads = DefWorkerThreads;
            var ioThreads = DefIoThreads;
            if (workerThreads != UseWorkerThreads || ioThreads != UseIoThreads)
                ThreadPool.SetMinThreads(workerThreads, ioThreads);
        }

        /// <summary>
        /// Returns the current (global) application name, display name, seed and description.
        /// </summary>
        /// <returns>A display string.</returns>
        public override string ToString() => String.Concat(
            "Name: ", EnvInfo.AppName
            , ", Display name: ", EnvInfo.AppDisplayName
            , ", Seed: ", EnvInfo.AppSeed
            , ", Description: ", EnvInfo.AppDescription
            );
    }


}
