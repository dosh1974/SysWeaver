using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Managed version of the Win32 SERVICE_STATUS_PROCESS structure (currently unused).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    sealed class ServiceStatusProcess
    {
        /// <summary>
        /// The service type.
        /// </summary>
        public int serviceType;
        /// <summary>
        /// The current state.
        /// </summary>
        public ServiceStates currentState;
        /// <summary>
        /// The control codes the service accepts.
        /// </summary>
        public int controlsAccepted;
        /// <summary>
        /// Win32 error code.
        /// </summary>
        public int win32ExitCode;
        /// <summary>
        /// Service specific error code.
        /// </summary>
        public int serviceSpecificExitCode;
        /// <summary>
        /// Progress counter.
        /// </summary>
        public int checkPoint;
        /// <summary>
        /// Estimated time in milliseconds for a pending operation.
        /// </summary>
        public int waitHint;
        /// <summary>
        /// The process id of the service.
        /// </summary>
        public int processID;
        /// <summary>
        /// SERVICE_RUNS_IN_SYSTEM_PROCESS or 0.
        /// </summary>
        public int serviceFlags;
    }

}
