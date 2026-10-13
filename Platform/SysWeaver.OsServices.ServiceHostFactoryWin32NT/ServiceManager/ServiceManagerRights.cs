using System;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Win32 Service Control Manager access rights (SC_MANAGER_*).
    /// </summary>
    [Flags]
    enum ServiceManagerRights : uint
    {
        /// <summary>
        /// SC_MANAGER_CONNECT, required to connect.
        /// </summary>
        Connect = 0x0001,
        /// <summary>
        /// SC_MANAGER_CREATE_SERVICE, create services.
        /// </summary>
        CreateService = 0x0002,
        /// <summary>
        /// SC_MANAGER_ENUMERATE_SERVICE, enumerate services.
        /// </summary>
        EnumerateService = 0x0004,
        /// <summary>
        /// SC_MANAGER_LOCK, lock the database.
        /// </summary>
        Lock = 0x0008,
        /// <summary>
        /// SC_MANAGER_QUERY_LOCK_STATUS, query the lock status.
        /// </summary>
        QueryLockStatus = 0x0010,
        /// <summary>
        /// SC_MANAGER_MODIFY_BOOT_CONFIG, modify the boot configuration.
        /// </summary>
        ModifyBootConfig = 0x0020,
        /// <summary>
        /// STANDARD_RIGHTS_REQUIRED.
        /// </summary>
        StandardRightsRequired = 0xF0000,
        /// <summary>
        /// SC_MANAGER_ALL_ACCESS.
        /// </summary>
        AllAccess = StandardRightsRequired | Connect | CreateService | EnumerateService | Lock | QueryLockStatus | ModifyBootConfig,

        /// <summary>
        /// Generic access right (GENERIC_READ).
        /// </summary>
        GENERIC_READ = AccessMask.GENERIC_READ,
        /// <summary>
        /// Generic access right (GENERIC_WRITE).
        /// </summary>
        GENERIC_WRITE = AccessMask.GENERIC_WRITE,
        /// <summary>
        /// Generic access right (GENERIC_EXECUTE).
        /// </summary>
        GENERIC_EXECUTE = AccessMask.GENERIC_EXECUTE,
        /// <summary>
        /// Generic access right (GENERIC_ALL).
        /// </summary>
        GENERIC_ALL = AccessMask.GENERIC_ALL,

    }

}
