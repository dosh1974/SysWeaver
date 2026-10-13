using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Managed version of the Win32 SERVICE_STATUS structure (blittable, filled in place by QueryServiceStatus / ControlService).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    sealed class ServiceStatus
    {
        /// <summary>
        /// The service type (SERVICE_WIN32_OWN_PROCESS etc).
        /// </summary>
        public int dwServiceType = 0;
        /// <summary>
        /// The current state.
        /// </summary>
        public ServiceState dwCurrentState = 0;
        /// <summary>
        /// The control codes the service accepts.
        /// </summary>
        public int dwControlsAccepted = 0;
        /// <summary>
        /// Win32 error code for start/stop failures.
        /// </summary>
        public int dwWin32ExitCode = 0;
        /// <summary>
        /// Service specific error code.
        /// </summary>
        public int dwServiceSpecificExitCode = 0;
        /// <summary>
        /// Progress counter during lengthy operations.
        /// </summary>
        public int dwCheckPoint = 0;
        /// <summary>
        /// Estimated time in milliseconds for a pending operation.
        /// </summary>
        public int dwWaitHint = 0;
    }

}
