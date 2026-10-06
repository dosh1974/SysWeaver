#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Win32 service start types as passed to CreateService (same values as <see cref="StartTypes"/>).
    /// </summary>
    enum ServiceBootFlag : uint
    {
        /// <summary>
        /// SERVICE_BOOT_START, a driver started by the system loader.
        /// </summary>
        Start = 0x00000000,
        /// <summary>
        /// SERVICE_SYSTEM_START, a driver started by IoInitSystem.
        /// </summary>
        SystemStart = 0x00000001,
        /// <summary>
        /// SERVICE_AUTO_START, started automatically during system startup.
        /// </summary>
        AutoStart = 0x00000002,
        /// <summary>
        /// SERVICE_DEMAND_START, started manually.
        /// </summary>
        DemandStart = 0x00000003,
        /// <summary>
        /// SERVICE_DISABLED, can't be started.
        /// </summary>
        Disabled = 0x00000004
    }

}
