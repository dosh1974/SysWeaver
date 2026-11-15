using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Static information about the server (and the current <see cref="SmServerStats"/>)
    /// </summary>
    public sealed class SmServerInfo : SmServerStats
    {
        /// <summary>
        /// The number of drives
        /// </summary>
        public int DriveCount;

        /// <summary>
        /// The number of (logical) processors
        /// </summary>
        public int ProcessorCount;

        /// <summary>
        /// The total amount of memory (RAM) in bytes
        /// </summary>
        public long Memory;

        /// <summary>
        /// The name of the machine
        /// </summary>
        public String Machine;

        /// <summary>
        /// The name of the operating system
        /// </summary>
        public String Os;

        /// <summary>
        /// The platform of the operating system (ex: Windows or Linux)
        /// </summary>
        public String OsBase;

        /// <summary>
        /// The standard name of the local time zone
        /// </summary>
        public String TzName;

        /// <summary>
        /// The token required by RebootComputer (valid for at least 15 minutes), so a crafted link or a plain GET request can't reboot the computer
        /// </summary>
        public String RebootToken;
    }



    /// <summary>
    /// Dynamic statistics about the server
    /// </summary>
    public class SmServerStats
    {
        /// <summary>
        /// The number of processes
        /// </summary>
        public int ProcessCount;

        /// <summary>
        /// The current time (UTC)
        /// </summary>
        public DateTime Utc;

        /// <summary>
        /// The current local time of the server, formatted as "yyyy-MM-dd HH:mm:ss.fff"
        /// </summary>
        public String Time;

        /// <summary>
        /// The name of the local time zone at the current time (the daylight saving name if daylight saving time is in effect)
        /// </summary>
        public String TzDayName;

    }

}
