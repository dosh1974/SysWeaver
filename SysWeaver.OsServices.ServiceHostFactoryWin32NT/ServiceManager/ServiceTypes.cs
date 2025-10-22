#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    enum ServiceTypes : uint
    {
        /// <summary>
        /// File system driver service.
        /// </summary>
        SERVICE_FILE_SYSTEM_DRIVER = 0x00000002,
        /// <summary>
        /// Driver service.
        /// </summary>
        SERVICE_KERNEL_DRIVER = 0x00000001,
        /// <summary>
        /// Service that runs in its own process.
        /// </summary>
        SERVICE_WIN32_OWN_PROCESS = 0x00000010,
        /// <summary>
        /// Service that shares a process with other services.
        /// </summary>
        SERVICE_WIN32_SHARE_PROCESS = 0x00000020,


        /// <summary>
        /// Used when chaning config to signal that this should not change
        /// </summary>
        SERVICE_NO_CHANGE = 0xffffffffu,
    }

}

