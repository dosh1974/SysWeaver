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

        [TableDataUrl("📊", "*../ServerManager/server_metrics.html?q1=GetProcessCpuChart?{0}&q2=GetProcessCpuHistoryShortChart?{0}&q3=GetProcessCpuHistoryChart?{0}&title=Process {0} Cpu use", "Click to show Cpu use details")]
        public long C;

        /// <summary>
        /// Current physical memory usage
        /// </summary>
        [TableDataByteSize]
        public long MemUsage;

        [TableDataUrl("📊", "*../ServerManager/server_metrics.html?q1=GetProcessMemChart?{0}&q2=GetProcessMemHistoryShortChart?{0}&q3=GetProcessMemHistoryChart?{0}&title=Process {0} memory use", "Click to show memory use details")]
        public long M;

        /// <summary>
        /// The peak physical memory usage
        /// </summary>
        [TableDataByteSize]
        public long PeakMemUsage;

        /// <summary>
        /// The maximum allowed physical memory
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
        /// Actions to perform on the process
        /// </summary>
        [TableDataActions(
    "Kill",
    "Click to kill the process",
    "../ServerManager/KillProcess?{0}",
    "../icons/skull.svg"
    )]
        [TableDataOrder(100)]
        public long Actions => Id; 
    }

}
