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
        public string lpDependencies;
        public string lpServiceStartName;
        public string lpDisplayName;
    };

}
