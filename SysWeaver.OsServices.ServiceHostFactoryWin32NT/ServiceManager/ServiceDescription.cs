using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    [StructLayout(LayoutKind.Sequential)]
    sealed class ServiceDescription
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpDescription;
    }

}
