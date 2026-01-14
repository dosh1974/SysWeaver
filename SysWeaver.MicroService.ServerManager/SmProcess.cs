using System;

namespace SysWeaver.MicroService
{
    internal sealed class SmProcess
    {
        public SmProcess(long id, String name, DateTime start, String mainFilename)
        {
            Id = id;
            Name = name;
            StartTime = start;
            MainFilename = mainFilename;
        }
        public readonly long Id;
        public readonly String Name;
        public readonly DateTime StartTime;
        public readonly String MainFilename;
        public readonly BucketValueHistory<SmServiceData> History = new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), SmServiceData.Add);
        public readonly BucketValueHistory<SmServiceData> HistoryShort = new(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(3), SmServiceData.Add);

        public volatile SmProcessMetrics Metrics;


        internal long LastCpu;
        internal TimeSpan LastTotCpu;


    }

}
