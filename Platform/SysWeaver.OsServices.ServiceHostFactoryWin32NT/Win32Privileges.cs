using System;
using System.Runtime.InteropServices;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Enables privileges in the token of this process (privileges that an admin has are disabled by default).
    /// </summary>
    static class Win32Privileges
    {
        //  LUID is two 32-bit values (4 byte aligned), so the 64-bit Luid must not be 8 byte aligned
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, int access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool LookupPrivilegeValueW(String system, String name, out long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, int len, IntPtr prev, IntPtr retLen);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        /// <summary>
        /// Enable a privilege, ex: "SeShutdownPrivilege".
        /// </summary>
        /// <param name="name">The privilege name</param>
        /// <returns>True if enabled, false if the process doesn't have the privilege</returns>
        public static bool Enable(String name)
        {
            const int TOKEN_ADJUST_PRIVILEGES = 0x20;
            const int TOKEN_QUERY = 0x8;
            const int SE_PRIVILEGE_ENABLED = 2;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
                return false;
            try
            {
                if (!LookupPrivilegeValueW(null, name, out var luid))
                    return false;
                var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
                //  Succeeds with ERROR_NOT_ALL_ASSIGNED (1300) if the privilege isn't held
                return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) && (Marshal.GetLastWin32Error() == 0);
            }
            finally
            {
                CloseHandle(token);
            }
        }
    }
}
