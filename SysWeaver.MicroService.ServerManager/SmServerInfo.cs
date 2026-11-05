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
        /// <summary>
        /// The token required by RebootComputer (valid for at least 15 minutes), so a crafted link or a plain GET request can't reboot the computer
        /// </summary>
        public String RebootToken;
    }



    public class SmServerStats
    {
        public int ProcessCount;
        public DateTime Utc;
        public String Time;
        public String TzDayName;

    }

}
