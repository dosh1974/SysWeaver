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
        public readonly FolderPushFolder Syncher;
        public readonly IReadOnlyList<string> Auth;
        public readonly RequestOptions Options;
        public readonly IFileRepo Current;
        public readonly IFileRepo Master;
        public readonly FileHttpServerModuleFolder Bak;

        public SmServiceInfo(ManagedService service, FolderPushFolder syncher, ServerManagerParams p, IFileRepo current, IFileRepo master, FileHttpServerModuleFolder bak)
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
        //public readonly BucketValueHistory<SmServiceData> History = new (TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), SmServiceData.Add);
        //public readonly BucketValueHistory<SmServiceData> HistoryShort = new(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(3), SmServiceData.Add);
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
