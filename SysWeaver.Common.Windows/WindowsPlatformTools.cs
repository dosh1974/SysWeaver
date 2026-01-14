using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Management;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver
{
    public sealed class WindowsPlatformTools : IPlatformTools
    {
        public string Name => "Windows";

        public bool FlushToDisc(SafeHandle h)
        {
            try
            {
                return FlushFileBuffers(h.DangerousGetHandle());
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
                return false;
            }
        }


        public bool GetMemorySize(out ulong availableBytes, out ulong totalBytes)
        {
            try
            {
                var ms = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(ms))
                {
                    availableBytes = ms.ullAvailPhys;
                    totalBytes = ms.ullTotalPhys;
                }else
                {
                    throw new Exception(String.Concat(nameof(GlobalMemoryStatusEx), " failed with error code: ", GetLastError()));
                }
                return true;
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
                availableBytes = 0;
                totalBytes = 0;
                return false;
            }
        }

        #pragma warning disable CA1416

        readonly ManagementObjectSearcher CpuUseSsearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name=\"_Total\"");

        public bool GetCpuUsage(out double cpuUsage)
        {
            try
            {
                using var cpuTimes = CpuUseSsearcher.Get();
                using var e = cpuTimes.GetEnumerator();
                if (e.MoveNext())
                {
                    var mo = e.Current as ManagementObject;
                    var o = mo["PercentIdleTime"];
                    long u = 100L - (long)(ulong)o;
                    if (u < 0)
                        u = 0;
                    if (u > 100)
                        u = 100;
                    cpuUsage = u;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
            }
            cpuUsage = 0;
            return false;
        }

        #pragma warning restore CA1416

        #region Imports

        [DllImport("kernel32", SetLastError = true)]
        static extern bool FlushFileBuffers(IntPtr handle);


        [StructLayout(LayoutKind.Sequential)]
        sealed class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32")]
        static extern int GetLastError();


        #endregion//Imports

        #region IHaveStats

        readonly ExceptionTracker Exs = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
            => Exs.GetStats("Windows.Platform", "Exception.");

        #endregion//IHaveStats

    }


}
