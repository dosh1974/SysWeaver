using System;

namespace SysWeaver.MicroService
{
    public sealed class SmServerInfo : SmServerStats
    {
        public int DriveCount;
        public int ProcessorCount;
        public long Memory;
        public String Machine;
        public String Os;
        public String OsBase;
        public String TzName;
    }



    public class SmServerStats
    {
        public int ProcessCount;
        public DateTime Utc;
        public String Time;
        public String TzDayName;

    }

}
