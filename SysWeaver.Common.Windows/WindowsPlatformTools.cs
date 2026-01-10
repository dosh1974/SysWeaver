using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Management;

namespace SysWeaver
{
    public sealed class WindowsPlatformTools : IPlatformTools
    {
        public string Name => "Windows";

        public bool FlushToDisc(SafeHandle h)
        {
            return FlushFileBuffers(h.DangerousGetHandle());
        }

        static readonly object _winMemoryLock = new();
        static readonly MEMORYSTATUSEX _memStatus = new();

        public bool GetMemorySize(out ulong availableBytes, out ulong totalBytes)
        {
            try
            {
                lock (_winMemoryLock) // lock because of reusing the static class _memStatus
                {
                    GlobalMemoryStatusEx(_memStatus);
                    availableBytes = _memStatus.ullAvailPhys;
                    totalBytes = _memStatus.ullTotalPhys;
                }
                return true;
            }
            catch
            {
                availableBytes = 0;
                totalBytes = 0;
                return false;
            }
        }

        #pragma warning disable CA1416

        static readonly ManagementObjectSearcher CpuUseSsearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name=\"_Total\"");

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
            catch
            {
            }
            cpuUsage = 0;
            return false;
        }

        #pragma warning restore CA1416

        #region Imports

        [DllImport("kernel32", SetLastError = true)]
        static extern bool FlushFileBuffers(IntPtr handle);


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX lpBuffer);


        #endregion//Imports




    }


}
