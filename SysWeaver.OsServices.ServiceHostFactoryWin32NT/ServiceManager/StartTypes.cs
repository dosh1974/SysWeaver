#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    enum StartTypes : uint
    {
        /// <summary>
        /// A device driver started by the system loader. This value is valid only for driver services.
        /// </summary>
        SERVICE_BOOT_START = 0x00000000,
        /// <summary>
        /// A device driver started by the IoInitSystem function. This value is valid only for driver services.
        /// </summary>
        SERVICE_SYSTEM_START = 0x00000001,
        /// <summary>
        /// A service started automatically by the service control manager during system startup.
        /// </summary>
        SERVICE_AUTO_START = 0x00000002,
        /// <summary>
        /// A service started by the service control manager when a process calls the StartService function.
        /// </summary>
        SERVICE_DEMAND_START = 0x00000003,
        /// <summary>
        /// A service that cannot be started. Attempts to start the service result in the error code ERROR_SERVICE_DISABLED.
        /// </summary>
        SERVICE_DISABLED = 0x00000004,

        /// <summary>
        /// Used when chaning config to signal that this should not change
        /// </summary>
        SERVICE_NO_CHANGE = 0xffffffffu,

    }
}



