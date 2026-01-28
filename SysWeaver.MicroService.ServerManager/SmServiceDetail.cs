using System;
using System.Collections.Generic;

namespace SysWeaver.MicroService
{

    public sealed class SmServiceDetail : SmServiceBrief
    {
        /// <summary>
        /// The process id 
        /// </summary>
        public long ProcId;
        
        /// <summary>
        /// The total CPU time spent bny the service since it started
        /// </summary>
        public TimeSpan TotalProcessorTime;

        /// <summary>
        /// Name of the executable
        /// </summary>
        public String ExeName;

        /// <summary>
        /// Information about the log (if log is at default location)
        /// </summary>
        public SmFileInfo Log;

        /// <summary>
        /// Configuration files that are active
        /// </summary>
        public SmFileInfo[] Configs;

        /// <summary>
        /// Master configuration files
        /// </summary>
        public SmFileInfo[] MasterConfigs;

        /// <summary>
        /// Information about available versions
        /// </summary>
        public SmVersionBrief[] Versions;

        /// <summary>
        /// The folder that contains the actual files
        /// </summary>
        public String CurrentFolder;

        /// <summary>
        /// If true, this the service manager service
        /// </summary>
        public bool IsSm;

        public SmServiceDetail()
        {
        }

        internal SmServiceDetail(SmServiceInfo info, IReadOnlyList<FolderSyncService.PushData> data, String exeName, SmFileInfo log, SmFileInfo[] configs, SmFileInfo[] masterConfigs) : base(info, data)
        {
            var p = info.Process;
            var m = p?.Metrics;
            var pid = p?.Id ?? 0;
            ProcId = pid;
            IsSm = pid == EnvInfo.ProcessId;
            TotalProcessorTime = m?.TotalCpuTime ?? TimeSpan.Zero;
            ExeName = exeName;
            Log = log;
            Configs = configs;
            MasterConfigs = masterConfigs;
            Versions = data.Convert(x => new SmVersionBrief(x));
            CurrentFolder = info.Syncher.DiscFolder;
            Array.Sort(Versions);

        }

    }
}
