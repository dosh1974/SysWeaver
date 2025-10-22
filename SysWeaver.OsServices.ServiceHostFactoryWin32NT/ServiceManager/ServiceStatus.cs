using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    [StructLayout(LayoutKind.Sequential)]
    sealed class ServiceStatus
    {
        public int dwServiceType = 0;
        public ServiceState dwCurrentState = 0;
        public int dwControlsAccepted = 0;
        public int dwWin32ExitCode = 0;
        public int dwServiceSpecificExitCode = 0;
        public int dwCheckPoint = 0;
        public int dwWaitHint = 0;
    }

}
