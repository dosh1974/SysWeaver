using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Managed version of the Win32 SERVICE_DESCRIPTION structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    sealed class ServiceDescription
    {
        /// <summary>
        /// The description text.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpDescription;
    }

}
