using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Managed version of the Win32 SERVICE_FAILURE_ACTIONS structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    sealed class ServiceFailureActions
    {
        /// <summary>
        /// Time without failures, in SECONDS, after which the failure count is reset (INFINITE = never).
        /// </summary>
        public int dwResetPeriod;
        /// <summary>
        /// Optional broadcast message before a reboot action, null for no change.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpRebootMsg;
        /// <summary>
        /// Optional command line for the run command action, null for no change.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpCommand;
        /// <summary>
        /// Number of <see cref="ScAction"/> elements in <see cref="lpsaActions"/>.
        /// </summary>
        public int cActions;
        /// <summary>
        /// Pointer to an array of <see cref="ScAction"/> (must be pinned while used), the last action is used for all subsequent failures.
        /// </summary>
        public nint lpsaActions;
    }

}
