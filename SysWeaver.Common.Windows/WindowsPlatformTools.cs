using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
//using System.Management;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Management;

namespace SysWeaver
{
    public sealed class WindowsPlatformTools : IPlatformTools
    {
        public string Name => "Windows";

        public String DefaultKeyDir => @"C:\Keys";

        public string OsFriendlyName { get; } = GetOsFriendlyName();


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

        static readonly PerformanceCounter CpuCounter;

        static WindowsPlatformTools()
        {
            var c = new PerformanceCounter("Processor Information", "% Processor Time", "_Total");
            CpuCounter = c;
            c.NextValue();
            Thread.Sleep(10);
            c.NextValue();
        }

        public bool GetCpuUsage(out double cpuUsage)
        {
            try
            {
                var c = CpuCounter;
                lock (c)
                    cpuUsage = c.NextValue();
                return true;
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
            }
            cpuUsage = 0;
            return false;
        }

        public bool Reboot()
            => ExitWindows(ExitWindowsFlags.Reboot, ShutdownReason.FlagPlanned, true);


        static readonly SecurityIdentifier Everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);


        static readonly FileSystemAccessRule FullAll = new FileSystemAccessRule(
            Everyone,
            FileSystemRights.FullControl,
            InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
            PropagationFlags.InheritOnly,
            AccessControlType.Allow);


        public Exception MakeDirectoryAccessableToEveryOne(String directoryName)
        {
            try
            {
                var t = new DirectoryInfo(directoryName);
                var ac = t.GetAccessControl();
                bool exist = false;
                foreach (FileSystemAccessRule x in ac.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (!x.IdentityReference.Value.FastEquals(Everyone.Value))
                        continue;
                    exist = (x.FileSystemRights == FullAll.FileSystemRights) && (x.InheritanceFlags == FullAll.InheritanceFlags) && (x.PropagationFlags != PropagationFlags.NoPropagateInherit);
                    break;
                }
                if (!exist)
                {
                    ac.AddAccessRule(FullAll);
                    t.SetAccessControl(ac);
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        static int GetARCHFriendlyBits(Architecture architecture)
        {
            return architecture switch
            {
                Architecture.X64 => 64,
                Architecture.X86 => 32,
                Architecture.Arm64 => 64,
                Architecture.Arm => 32,
                Architecture.Wasm => -1,
                Architecture.S390x => -1,
                _ => -1,
            };
        }

        static string GetOsFriendlyName()
        {
            ManagementObjectSearcher searcher = new("SELECT Caption FROM Win32_OperatingSystem");
            ManagementObject os = searcher.Get().Cast<ManagementObject>().First();
            if (os["Caption"].ToString() is string osResult)
                return $"{osResult} build {Environment.OSVersion.Version.Build} ({GetARCHFriendlyBits(RuntimeInformation.OSArchitecture)} bits)";
            return $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
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

        [DllImport("user32.dll", SetLastError = true)]
        static extern int ExitWindowsEx(ExitWindowsFlags uFlags, ShutdownReason dwReason);

        static bool ExitWindows(ExitWindowsFlags exitWindows, ShutdownReason reason, bool ajustToken)
        {
            if (ajustToken && !TokenAdjuster.EnablePrivilege("SeShutdownPrivilege", true))
            {
                return false;
            }
            return ExitWindowsEx(exitWindows, reason) != 0;
        }

        [Flags]
        enum ExitWindowsFlags : uint
        {
            // ONE of the following:
            LogOff = 0x00,
            ShutDown = 0x01,
            Reboot = 0x02,
            PowerOff = 0x08,
            RestartApps = 0x40,
            // plus AT MOST ONE of the following two:
            Force = 0x04,
            ForceIfHung = 0x10,
        }

        [Flags]
        enum ShutdownReason : uint
        {
            None = 0,

            MajorApplication = 0x00040000,
            MajorHardware = 0x00010000,
            MajorLegacyApi = 0x00070000,
            MajorOperatingSystem = 0x00020000,
            MajorOther = 0x00000000,
            MajorPower = 0x00060000,
            MajorSoftware = 0x00030000,
            MajorSystem = 0x00050000,

            MinorBlueScreen = 0x0000000F,
            MinorCordUnplugged = 0x0000000b,
            MinorDisk = 0x00000007,
            MinorEnvironment = 0x0000000c,
            MinorHardwareDriver = 0x0000000d,
            MinorHotfix = 0x00000011,
            MinorHung = 0x00000005,
            MinorInstallation = 0x00000002,
            MinorMaintenance = 0x00000001,
            MinorMMC = 0x00000019,
            MinorNetworkConnectivity = 0x00000014,
            MinorNetworkCard = 0x00000009,
            MinorOther = 0x00000000,
            MinorOtherDriver = 0x0000000e,
            MinorPowerSupply = 0x0000000a,
            MinorProcessor = 0x00000008,
            MinorReconfig = 0x00000004,
            MinorSecurity = 0x00000013,
            MinorSecurityFix = 0x00000012,
            MinorSecurityFixUninstall = 0x00000018,
            MinorServicePack = 0x00000010,
            MinorServicePackUninstall = 0x00000016,
            MinorTermSrv = 0x00000020,
            MinorUnstable = 0x00000006,
            MinorUpgrade = 0x00000003,
            MinorWMI = 0x00000015,

            FlagUserDefined = 0x40000000,
            FlagPlanned = 0x80000000
        }

        sealed class TokenAdjuster
        {
            // PInvoke stuff required to set/enable security privileges
            const int SE_PRIVILEGE_ENABLED = 0x00000002;
            const int TOKEN_ADJUST_PRIVILEGES = 0X00000020;
            const int TOKEN_QUERY = 0X00000008;
            const int TOKEN_ALL_ACCESS = 0X001f01ff;
            const int PROCESS_QUERY_INFORMATION = 0X00000400;

            [DllImport("advapi32", SetLastError = true)]
            static extern int OpenProcessToken(
                IntPtr ProcessHandle, // handle to process
                int DesiredAccess, // desired access to process
                ref IntPtr TokenHandle // handle to open access token
                );

            [DllImport("kernel32", SetLastError = true)]
            static extern bool CloseHandle(IntPtr handle);

            [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            static extern int AdjustTokenPrivileges(
                IntPtr TokenHandle,
                int DisableAllPrivileges,
                IntPtr NewState,
                int BufferLength,
                IntPtr PreviousState,
                ref int ReturnLength);

            [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            static extern bool LookupPrivilegeValue(
                string lpSystemName,
                string lpName,
                ref LUID lpLuid);

            public static bool EnablePrivilege(string lpszPrivilege, bool bEnablePrivilege)
            {
                bool retval = false;
                int ltkpOld = 0;
                IntPtr hToken = IntPtr.Zero;
                TOKEN_PRIVILEGES tkp = new TOKEN_PRIVILEGES();
                tkp.Privileges = new int[3];
                TOKEN_PRIVILEGES tkpOld = new TOKEN_PRIVILEGES();
                tkpOld.Privileges = new int[3];
                LUID tLUID = new LUID();
                tkp.PrivilegeCount = 1;
                if (bEnablePrivilege)
                    tkp.Privileges[2] = SE_PRIVILEGE_ENABLED;
                else
                    tkp.Privileges[2] = 0;
                if (LookupPrivilegeValue(null, lpszPrivilege, ref tLUID))
                {
                    Process proc = Process.GetCurrentProcess();
                    if (proc.Handle != IntPtr.Zero)
                    {
                        if (OpenProcessToken(proc.Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
                            ref hToken) != 0)
                        {
                            tkp.PrivilegeCount = 1;
                            tkp.Privileges[2] = SE_PRIVILEGE_ENABLED;
                            tkp.Privileges[1] = tLUID.HighPart;
                            tkp.Privileges[0] = tLUID.LowPart;
                            const int bufLength = 256;
                            IntPtr tu = Marshal.AllocHGlobal(bufLength);
                            Marshal.StructureToPtr(tkp, tu, true);
                            if (AdjustTokenPrivileges(hToken, 0, tu, bufLength, IntPtr.Zero, ref ltkpOld) != 0)
                            {
                                // successful AdjustTokenPrivileges doesn't mean privilege could be changed
                                if (Marshal.GetLastWin32Error() == 0)
                                {
                                    retval = true; // Token changed
                                }
                            }
                            TOKEN_PRIVILEGES tokp = (TOKEN_PRIVILEGES)Marshal.PtrToStructure(tu, typeof(TOKEN_PRIVILEGES));
                            Marshal.FreeHGlobal(tu);
                        }
                    }
                }
                if (hToken != IntPtr.Zero)
                {
                    CloseHandle(hToken);
                }
                return retval;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct LUID
            {
                internal int LowPart;
                internal int HighPart;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct LUID_AND_ATTRIBUTES
            {
                LUID Luid;
                int Attributes;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct TOKEN_PRIVILEGES
            {
                internal int PrivilegeCount;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
                internal int[] Privileges;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct _PRIVILEGE_SET
            {
                int PrivilegeCount;
                int Control;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)] // ANYSIZE_ARRAY = 1
                LUID_AND_ATTRIBUTES[] Privileges;
            }
        }


        #endregion//Imports

        #region IHaveStats

        readonly ExceptionTracker Exs = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
            => Exs.GetStats("Windows.Platform", "Exception.");

        #endregion//IHaveStats





    }


}
