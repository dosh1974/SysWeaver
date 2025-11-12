using System;
using System.Collections.Generic;

namespace SysWeaver.MicroService
{
    public class SmServiceDetail : SmServiceBrief
    {

        public long ProcId;
        public TimeSpan TotalProcessorTime;

        public SmServiceDetail()
        {
        }

        internal SmServiceDetail(SmServiceInfo info, IReadOnlyList<FolderSyncService.Data> data) : base(info, data)
        {
            var m = info.Metrics;
            ProcId = m.ProcessHandle;
            TotalProcessorTime = m.TotalProcessorTime;
        }

    }
}
