using System;
using System.Collections.Generic;

namespace SysWeaver.MicroService
{


    public class SmFileInfo
    {
        public String Name;
        public long Size;
        public DateTime LastModified;
    }

    public class SmServiceDetail : SmServiceBrief
    {

        public long ProcId;
        public TimeSpan TotalProcessorTime;
        public String ExeName;
        public SmFileInfo Log;
        public SmFileInfo[] Configs;
        public SmFileInfo[] MasterConfigs;

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
        }

    }
}
