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
        public readonly FolderSyncFolder Syncher;
        public readonly IReadOnlyList<string> Auth;
        public readonly RequestOptions Options;
        public readonly IFileRepo Current;
        public readonly IFileRepo Master;

        public SmServiceInfo(ManagedService service, FolderSyncFolder syncher, ServerManagerParams p, IFileRepo current, IFileRepo master)
        {
            Service = service;
            Syncher = syncher;
            Auth = Authorization.GetRequiredTokens(service.SyncAuth ?? p.SyncAuth ?? Roles.Debug);
            Options = new RequestOptions(5, 4, 32, "br:Balanced,deflate:Balanced,gzip:Balanced", String.Join(',', Auth));
            Current = current;
            Master = master;
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
