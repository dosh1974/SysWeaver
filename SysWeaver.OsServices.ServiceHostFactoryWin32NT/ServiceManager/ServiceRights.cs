using System;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Win32 service object access rights (SERVICE_*).
    /// </summary>
    [Flags]
    enum ServiceRights : uint
    {
        /// <summary>
        /// SERVICE_QUERY_CONFIG.
        /// </summary>
        QueryConfig = 0x1,
        /// <summary>
        /// SERVICE_CHANGE_CONFIG.
        /// </summary>
        ChangeConfig = 0x2,
        /// <summary>
        /// SERVICE_QUERY_STATUS.
        /// </summary>
        QueryStatus = 0x4,
        /// <summary>
        /// SERVICE_ENUMERATE_DEPENDENTS.
        /// </summary>
        EnumerateDependants = 0x8,
        /// <summary>
        /// SERVICE_START.
        /// </summary>
        Start = 0x10,
        /// <summary>
        /// SERVICE_STOP.
        /// </summary>
        Stop = 0x20,
        /// <summary>
        /// SERVICE_PAUSE_CONTINUE.
        /// </summary>
        PauseContinue = 0x40,
        /// <summary>
        /// SERVICE_INTERROGATE.
        /// </summary>
        Interrogate = 0x80,
        /// <summary>
        /// SERVICE_USER_DEFINED_CONTROL.
        /// </summary>
        UserDefinedControl = 0x100,
        /// <summary>
        /// DELETE, required to delete the service.
        /// </summary>
        Delete = 0x00010000,
        /// <summary>
        /// STANDARD_RIGHTS_REQUIRED.
        /// </summary>
        StandardRightsRequired = 0xF0000,
        /// <summary>
        /// SERVICE_ALL_ACCESS.
        /// </summary>
        AllAccess = StandardRightsRequired | QueryConfig | ChangeConfig | QueryStatus | EnumerateDependants | Start | Stop | PauseContinue | Interrogate | UserDefinedControl,

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
