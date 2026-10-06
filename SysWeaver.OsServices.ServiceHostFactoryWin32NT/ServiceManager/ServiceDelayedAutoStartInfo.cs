using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Managed version of the Win32 SERVICE_DELAYED_AUTO_START_INFO structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    sealed class ServiceDelayedAutoStartInfo
    {
        /// <summary>
        /// True if an auto start service is started after other auto start services (marshalled as a 4 byte BOOL).
        /// </summary>
        public bool fDelayedAutostart;
    };

}
