using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.OsServices;

namespace SysWeaver.MicroService
{
    sealed class SmServiceInfo
    {
        public readonly ManagedService Service;
        public readonly FolderSyncFolder Syncher;
        public readonly IReadOnlyList<string> Auth;


        public SmServiceInfo(ManagedService service, FolderSyncFolder syncher, ServerManagerParams p)
        {
            Service = service;
            Syncher = syncher;
            Auth = Authorization.GetRequiredTokens(service.SyncAuth ?? p.SyncAuth ?? Roles.Debug);
        }

        public long LastCpu;
        public TimeSpan LastTotCpu;
        public volatile SmServiceMetrics Metrics = new SmServiceMetrics();
        public readonly ConcurrentQueue<SmServiceMetrics> History = new ConcurrentQueue<SmServiceMetrics>();

    }

    sealed class SmServiceMetrics
    {
        public readonly DateTime Time = DateTime.UtcNow;
        public ServiceStatus Status;
        public long ProcessHandle;
        public long MemUsage;
        public TimeSpan TotalProcessorTime;
        public double CpuUsage;


    }

}
