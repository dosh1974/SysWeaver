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
    }



    public class SmServerStats
    {
        public int ProcessCount;

    }

}
