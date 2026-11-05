using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    [TableDataPrimaryKey(nameof(Id), nameof(Name))]
    internal sealed class SmProcessMetrics
    {
        public SmProcessMetrics(SmProcess p)
        {
            var i = p.Id;
            Id = i;
            C = i;
            M = i;
            Name = p.Name;
            var s = p.StartTime;
            StartTime = s;
            Duration = s;
            MainFilename = p.MainFilename;
            var ck = SmProcessKillToken.CanKill(i, p.Name);
            CanKill = ck;
            KillToken = ck ? SmProcessKillToken.Create(i, s) : null;
        }

        /// <summary>
        /// Unique process identifier
        /// </summary>
        public long Id;
        
        /// <summary>
        /// Friendly name
        /// </summary>
        public String Name;

        /// <summary>
        /// The time when the process was started
        /// </summary>
        public DateTime StartTime;

        /// <summary>
        /// Duration that the process have been running
        /// </summary>
        [TableDataUptime]
        public DateTime Duration;

        /// <summary>
        /// Current cpu usage as a percentage 
        /// </summary>
        [TableDataNumber(2, "{0} %")]
        public double CpuUsage;

        /// <summary>
        /// Charts of the CPU use of the process
        /// </summary>
        //  An icon only column: the header shows no title text (TableDataNoTitle)
        [TableDataUrl("📊", "*../ServerManager/server_metrics.html?q1=GetProcessCpuChart?{0}&q2=GetProcessCpuHistoryShortChart?{0}&q3=GetProcessCpuHistoryChart?{0}&title=Process {0} CPU use", "Show CPU use details")]
        [TableDataTitle("CPU chart")]
        [TableDataNoTitle]
        public long C;

        /// <summary>
        /// Current physical memory usage (working set), 0 if the process can't be read
        /// </summary>
        [TableDataByteSize]
        public long MemUsage;

        /// <summary>
        /// Charts of the memory use of the process
        /// </summary>
        //  An icon only column: the header shows no title text (TableDataNoTitle)
        [TableDataUrl("📊", "*../ServerManager/server_metrics.html?q1=GetProcessMemChart?{0}&q2=GetProcessMemHistoryShortChart?{0}&q3=GetProcessMemHistoryChart?{0}&title=Process {0} memory use", "Show memory use details")]
        [TableDataTitle("Memory chart")]
        [TableDataNoTitle]
        public long M;

        /// <summary>
        /// The peak physical memory usage (peak working set), 0 if the process can't be read
        /// </summary>
        [TableDataByteSize]
        public long PeakMemUsage;

        /// <summary>
        /// The maximum working set size that the OS tries to keep for the process (a soft quota, not a usage: the same default, ex: 1380 KB on Windows, for most processes), 0 if the process can't be read.
        /// Shown as "Max mem usage", as before; see <see cref="PeakMemUsage"/> for the highest memory use.
        /// </summary>
        [TableDataByteSize]
        public long MaxMemUsage;

        /// <summary>
        /// The number of handles opened
        /// </summary>
        public int HandleCount;

        /// <summary>
        /// Overall priority category
        /// </summary>
        public String PriorityClass;
        
        /// <summary>
        /// Base priority
        /// </summary>
        public int BasePriority;

        /// <summary>
        /// Indicating whether the associated process priority should be temporarily boosted by the operating system when the main window has focus
        /// </summary>
        public bool PriorityBoost;

        //  The memory values below are 0 if the process can't be read (as before)

        [TableDataByteSize]
        public long NonpagedSystemMemory;

        [TableDataByteSize]
        public long PagedSystemMemory;

        [TableDataByteSize]
        public long PagedMemory;

        [TableDataByteSize]
        public long PeakPagedMemory;

        [TableDataByteSize]
        public long VirtualMemory;

        [TableDataByteSize]
        public long PeakVirtualMemory;

        [TableDataByteSize]
        public long PrivateMemory;

        /// <summary>
        /// Amount of time the process has spent utilizing the CPU
        /// </summary>
        public TimeSpan TotalCpuTime;

        /// <summary>
        /// Amount of time the process has spent running code inside the operating system core
        /// </summary>
        public TimeSpan TotalSystemCpuTime;

        /// <summary>
        /// Amount of time the process has spent running code inside the application portion of the process (not the operating system core)
        /// </summary>
        public TimeSpan TotalUserCpuTime;

        /// <summary>
        /// Filename of the main module
        /// </summary>
        public String MainFilename;


        /// <summary>
        /// True if the process may be killed (not the idle or system process, nor the server manager itself)
        /// </summary>
        [TableDataIgnore]
        public bool CanKill;

        /// <summary>
        /// The token required to kill the process (see <see cref="SmProcessKillToken"/>), null if it can't be killed
        /// </summary>
        [TableDataIgnore]
        public String KillToken;

        /// <summary>
        /// Actions to perform on the process
        /// </summary>
        //  The value is "pid,canKill,killToken" ({0}, {2} and {3} in the action formats): the Kill action is only shown if the process can be killed,
        //  and the kill request must include the kill token (so a plain link or GET request can't kill a process).
        //  Kill is final (G47, declared, never guessed from the words): the web table keeps the row muted without actions until the user refreshes.
        [TableDataActionsFull(new[] { TableDataActionFlags.Final },
    "Kill",
    "Kill the process",
    "../ServerManager/KillProcess?\"{3}\"",
    "kill",
    "Kill the process?",
    "{2}",
    ""
    )]
        [TableDataOrder(100)]
        public String Actions => String.Join(',', Id, CanKill ? "1" : "0", KillToken ?? "");
    }

}
