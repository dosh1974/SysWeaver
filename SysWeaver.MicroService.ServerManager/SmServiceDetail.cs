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

        public SmServiceDetail()
        {
        }

        internal SmServiceDetail(SmServiceInfo info, IReadOnlyList<FolderSyncService.Data> data, String exeName, SmFileInfo log, SmFileInfo[] configs, SmFileInfo[] masterConfigs) : base(info, data)
        {
            var m = info.Metrics;
            ProcId = m.ProcessHandle;
            TotalProcessorTime = m.TotalProcessorTime;
            ExeName = exeName;
            Log = log;
            Configs = configs;
            MasterConfigs = masterConfigs;
            Versions = data.Convert(x => new SmVersionBrief(x));
            Array.Sort(Versions);

        }

    }
}
