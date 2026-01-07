using System;
using System.Runtime.InteropServices;

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
            lock (_winMemoryLock) // lock because of reusing the static class _memStatus
            {
                GlobalMemoryStatusEx(_memStatus);

                availableBytes = _memStatus.ullAvailPhys;
                totalBytes = _memStatus.ullTotalPhys;
            }
            return true;
        }


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
