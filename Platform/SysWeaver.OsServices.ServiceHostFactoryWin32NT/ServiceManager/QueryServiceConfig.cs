using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Managed version of the Win32 QUERY_SERVICE_CONFIG structure (returned by QueryServiceConfig).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    sealed class QueryServiceConfig
    {
        public ServiceTypes dwServiceType;
        public StartTypes dwStartType;
        public uint dwErrorControl;
        public string lpBinaryPathName;
        public string lpLoadOrderGroup;
        public uint dwTagID;
        /// <summary>
        /// Pointer to a double null terminated list of the names of the services and groups (prefixed with '+') this service depends on (a string would only hold the first name).
        /// Only valid while the native buffer it was read from is alive, read it with <see cref="Win32ServiceManager.ReadMultiString(nint)"/> (<see cref="Win32ServiceManager.GetDependencies(string, bool)"/> does).
        /// </summary>
        public nint lpDependencies;
        public string lpServiceStartName;
        public string lpDisplayName;
    };

}
