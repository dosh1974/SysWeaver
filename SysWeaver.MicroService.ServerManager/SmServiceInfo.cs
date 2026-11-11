using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Net;
using SysWeaver.OsServices;

namespace SysWeaver.MicroService
{
    sealed class SmServiceInfo
    {
        public readonly ManagedService Service;
        public readonly FsManagedFolder Syncher;
        public readonly IReadOnlyList<string> Auth;
        public readonly RequestOptions Options;
        public readonly IFileRepo Current;
        public readonly IFileRepo Master;

#pragma warning disable CS0649
        public readonly FileHttpServerModuleFolder Bak;

#pragma warning restore CS0649

        public SmServiceInfo(ManagedService service, FsManagedFolder syncher, ServerManagerParams p, IFileRepo current, IFileRepo master, FileHttpServerModuleFolder bak)
        {
            Service = service;
            Syncher = syncher;
            Auth = Authorization.GetRequiredTokens(service.SyncAuth ?? p.SyncAuth ?? Roles.Debug);
            Options = new RequestOptions(5, 4, 32, "br:Balanced,deflate:Balanced,gzip:Balanced", String.Join(',', Auth));
            Current = current;
            Master = master;
        }

        //public long LastCpu;
        //        public TimeSpan LastTotCpu;
        public volatile ServiceStatus Status;

        public volatile SmProcess Process;

        /// <summary>
        /// The service executable that is used (see ServerManagerService.GetServiceExe), null until detected.
        /// </summary>
        public volatile SmExeState Exe;

        /// <summary>
        /// The cached list of executables in the active folder.
        /// </summary>
        public volatile SmExeCandidates Candidates;

        /// <summary>
        /// Serializes the detection / selection of the executable.
        /// </summary>
        public readonly Object ExeLock = new Object();

        /// <summary>
        /// When a fallback to another executable was last tried (UTC ticks, rate limited).
        /// </summary>
        public long LastExeFallback;
        //public readonly BucketValueHistory<SmServiceData> History = new (TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), SmServiceData.Add);
        //public readonly BucketValueHistory<SmServiceData> HistoryShort = new(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(3), SmServiceData.Add);
    }

    /// <summary>
    /// The executable that is used for a service.
    /// </summary>
    sealed class SmExeState
    {
        /// <summary>
        /// The full path of the executable, null if none was found.
        /// </summary>
        public String FullName;

        /// <summary>
        /// True if selected by a user.
        /// </summary>
        public bool Manual;

        /// <summary>
        /// When it was resolved (a missing executable is re-detected after a while, not on every status poll).
        /// </summary>
        public DateTime Resolved;
    }

    /// <summary>
    /// The executables found in a folder (cached, the folder is re-scanned when it's last write time changes or after a while).
    /// </summary>
    sealed class SmExeCandidates
    {
        public String Folder;
        public DateTime FolderTime;
        public DateTime Scanned;
        public IReadOnlyList<ExecutableFile> Files;
    }

    sealed class SmServiceData
    {

        public static SmServiceData Add(SmServiceData a, SmServiceData b)
        {
            return new SmServiceData
            {
                MemUsage = a.MemUsage + b.MemUsage,
                CpuUsage = a.CpuUsage + b.CpuUsage,
            };
        }

        public long MemUsage;
        public double CpuUsage;
    }
/*
    sealed class SmServiceMetrics
    {
        public readonly DateTime Time = DateTime.UtcNow;
        public ServiceStatus Status;
        public long ProcessHandle;
        public long MemUsage;
        public TimeSpan TotalProcessorTime;
        public double CpuUsage;


    }
*/
}
